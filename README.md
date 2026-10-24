# taskdeck-overlay

A lightweight, always-on-top Windows overlay that shows system metrics and the status of tasks run by Claude Code, in one small window you can keep over other apps.

▶️ [Watch the demo (MP4)](docs/overlay.mp4)

> **Status: v0.1.0, first release.** See [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) for the design and roadmap.

## Features

- System metrics: CPU (usage and temperature), GPU (usage, VRAM, temperature), RAM, disk and network.
- Task list for Claude Code sessions with name, status, duration and step progress. See [docs/hooks-setup.md](docs/hooks-setup.md).
- Visual cue (colored border) when a task finishes, fails or waits for you, with an optional sound.
- Small, borderless, draggable window with adjustable transparency. Position and preferences are remembered.
- Tray icon to hide and restore the overlay; while hidden, task events show as a tray notification.
- Low resource usage: about 0.1% CPU at idle. See [Performance](#performance).

## Install

Requirements: Windows 10 or 11, 64-bit.

1. Download a zip from [Releases](https://github.com/SantiSi5/taskdeck-overlay/releases):
   - `taskdeck-overlay-<version>-win-x64.zip` (about 1 MB) needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
   - `taskdeck-overlay-<version>-win-x64-self-contained.zip` (about 70 MB) runs without installing anything else.
2. Extract it to a folder and run `TaskdeckOverlay.exe`.
3. To see Claude Code tasks, follow [docs/hooks-setup.md](docs/hooks-setup.md).

Right-click the overlay for settings (opacity, sensors, sound). The tray icon hides and restores it.

## How task data is collected

The overlay only receives events that the user configures themselves through [Claude Code hooks](https://docs.claude.com). Hooks send metadata only (task name, status, time, project), never prompt or response content. Communication is local (`127.0.0.1`), and nothing leaves your machine. There is no telemetry and no scraping of claude.ai.

## Stack

C# / .NET (WPF), [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) for sensors. Reading temperatures requires administrator rights, so on admin accounts the app asks for elevation (UAC) at startup. On standard accounts it runs without elevation and shows "N/A (needs admin)".

## Performance

Measured with [scripts/measure.ps1](scripts/measure.ps1) over 20 s at idle, running as administrator with all sensors on (Ryzen 7 5800XT, NVIDIA GPU, Windows 11).

| Scenario | CPU avg | CPU max | Private memory | Working set |
|---|---|---|---|---|
| Idle, sensors on, admin | 0.09% | 0.29% | 103 MB | 266 MB |

CPU use stays close to zero because metrics are sampled once per second, hardware sensors every two seconds, and sampling pauses while the overlay is hidden.

Memory is higher than the original target ("tens of MB"). Measured contributions to private memory:

| Part | Approx. MB |
|---|---|
| WPF window, tray icon and app | 55 |
| GPU sensors (NVIDIA, via LibreHardwareMonitor) | 25 |
| Low-level driver for temperatures (admin only) | 14 |
| Disk performance counters | 8 |

Turning off "GPU and temperatures" in the right-click menu releases the sensor part. The working set is larger because it also counts shared Windows DLLs.

## Build from source

```powershell
dotnet build src\TaskdeckOverlay
Start-Process src\TaskdeckOverlay\bin\Debug\net8.0-windows\TaskdeckOverlay.exe
.\scripts\publish.ps1   # release zips in dist\
```

## Roadmap

1. ✅ Overlay with CPU and RAM.
2. ✅ GPU, disk, network and temperatures.
3. ✅ Local event server and task list.
4. ✅ Task progress, notifications and settings.
5. ✅ Performance measurements, packaging and first release.

## Disclaimer

This is an independent, unofficial project. It is not affiliated with, endorsed by, or sponsored by Anthropic. "Claude" is a trademark of Anthropic.

## License

[MIT](LICENSE). Third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
