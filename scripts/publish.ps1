<#
.SYNOPSIS
    Builds the release packages into dist\.

.DESCRIPTION
    Produces two zip files for win-x64:
    - taskdeck-overlay-<version>-win-x64.zip               (needs the .NET 8 Desktop Runtime)
    - taskdeck-overlay-<version>-win-x64-self-contained.zip (no runtime needed, larger)

    Both are folder builds, not single-file: LibreHardwareMonitor extracts its sensor
    driver next to its own assembly, which has no path inside a single-file bundle.

.EXAMPLE
    .\scripts\publish.ps1
#>
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\TaskdeckOverlay\TaskdeckOverlay.csproj"
$dist = Join-Path $root "dist"
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory $dist | Out-Null

$variants = @(
    @{ Suffix = "";                 SelfContained = "false" },
    @{ Suffix = "-self-contained";  SelfContained = "true" }
)

foreach ($v in $variants) {
    $name = "taskdeck-overlay-$version-win-x64$($v.Suffix)"
    $out = Join-Path $dist $name

    Write-Host "Publishing $name..."
    dotnet publish $project -c Release -r win-x64 --self-contained $v.SelfContained -o $out -p:DebugType=none
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $name" }

    foreach ($doc in "LICENSE", "THIRD-PARTY-NOTICES.md", "README.md") {
        Copy-Item (Join-Path $root $doc) $out
    }

    Compress-Archive -Path "$out\*" -DestinationPath "$out.zip"
}

Write-Host ""
Get-ChildItem $dist -Filter *.zip | ForEach-Object {
    "{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB)
}
