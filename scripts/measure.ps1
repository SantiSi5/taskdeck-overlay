<#
.SYNOPSIS
    Measures CPU and memory use of a running taskdeck-overlay process.

.DESCRIPTION
    Samples the process once per second and prints average and peak values, plus a
    Markdown table row ready to paste into the README.

    - CPU %      : share of the whole machine (all logical cores), like Task Manager.
    - Private MB : memory owned by the process (closest to Task Manager's "Memory" column).
    - Working MB : physical memory currently mapped, including shared DLLs.

.EXAMPLE
    .\scripts\measure.ps1 -Seconds 60 -Label "idle, sensors on"
#>
param(
    [int]$Seconds = 60,
    [string]$ProcessName = "TaskdeckOverlay",
    [string]$Label = "idle"
)

$ErrorActionPreference = "Stop"

# Decimal point regardless of the regional settings, so the README row is consistent.
[System.Threading.Thread]::CurrentThread.CurrentCulture = [cultureinfo]::InvariantCulture

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Write-Error "Process '$ProcessName' is not running. Start the overlay first."
}

$cores = [Environment]::ProcessorCount
$cpu = @(); $private = @(); $working = @()

Write-Host "Measuring PID $($proc.Id) for $Seconds s ($cores logical cores)..."

$proc.Refresh()
$lastCpu = $proc.TotalProcessorTime
$lastTime = [DateTime]::UtcNow

for ($i = 0; $i -lt $Seconds; $i++) {
    Start-Sleep -Seconds 1
    $proc.Refresh()
    if ($proc.HasExited) { Write-Error "The process exited during the measurement." }

    $now = [DateTime]::UtcNow
    $usedMs = ($proc.TotalProcessorTime - $lastCpu).TotalMilliseconds
    $elapsedMs = ($now - $lastTime).TotalMilliseconds
    $cpu += 100.0 * $usedMs / ($elapsedMs * $cores)
    $private += $proc.PrivateMemorySize64 / 1MB
    $working += $proc.WorkingSet64 / 1MB

    $lastCpu = $proc.TotalProcessorTime
    $lastTime = $now
    Write-Progress -Activity "Measuring" -PercentComplete (100 * ($i + 1) / $Seconds)
}
Write-Progress -Activity "Measuring" -Completed

function Stat($values) {
    $m = $values | Measure-Object -Average -Maximum
    [pscustomobject]@{ Avg = [math]::Round($m.Average, 2); Max = [math]::Round($m.Maximum, 2) }
}

$c = Stat $cpu; $p = Stat $private; $w = Stat $working

Write-Host ""
Write-Host ("CPU %       avg {0,8}   max {1,8}" -f $c.Avg, $c.Max)
Write-Host ("Private MB  avg {0,8}   max {1,8}" -f $p.Avg, $p.Max)
Write-Host ("Working MB  avg {0,8}   max {1,8}" -f $w.Avg, $w.Max)
Write-Host ""
Write-Host "README row:"
Write-Host ("| {0} | {1}% | {2}% | {3} MB | {4} MB |" -f $Label, $c.Avg, $c.Max, [math]::Round($p.Avg), [math]::Round($w.Avg))
