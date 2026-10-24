# Connecting Claude Code to taskdeck-overlay

The overlay receives events from Claude Code through its `http` hooks. Everything stays on your machine (`127.0.0.1`).

## 1. Create a token (once)

In PowerShell:

```powershell
$b = New-Object byte[] 24
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
setx TASKDECK_TOKEN ([BitConverter]::ToString($b) -replace '-','')
```

Close and reopen your terminals so the variable is visible, both for the overlay and for Claude Code.

## 2. Add the hooks

Merge the `hooks` section of [claude-code-hooks.json](claude-code-hooks.json) into your `~/.claude/settings.json` (all projects) or a project's `.claude/settings.json`. If you already have a `hooks` section, add these events to it instead of replacing it.

The hooks are non-blocking: if the overlay is closed, Claude Code keeps working normally.

## 3. Run the overlay

```powershell
dotnet run --project src\TaskdeckOverlay
```

The default port is `47600`; set `TASKDECK_PORT` to change it (and update the URL in the hooks).

## What is read

Each event is a JSON POST. The overlay reads only the event name, the session id and the working directory (used to show the project name). Prompt text is never stored or logged.
