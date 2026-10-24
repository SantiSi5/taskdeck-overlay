# taskdeck-overlay

A lightweight, always-on-top Windows overlay that shows system metrics and the status of tasks run by Claude Code, in one small window you can keep over other apps.

> **Status: in development (phase 4 of 5).** Not released yet. See [PROJECT_CONTEXT.md](PROJECT_CONTEXT.md) for the design and roadmap.

## Features

- System metrics: CPU (usage and temperature), GPU (usage, VRAM, temperature), RAM, disk and network.
- Task list for Claude Code sessions with name, status, duration and step progress. See [docs/hooks-setup.md](docs/hooks-setup.md).
- Visual cue (colored border) when a task finishes, fails or waits for you, with an optional sound.
- Small, borderless, draggable window with adjustable transparency. Position and preferences are remembered.
- Very low resource usage (targets to be measured and published).

## How task data is collected

The overlay only receives events that the user configures themselves through [Claude Code hooks](https://docs.claude.com). Hooks send metadata only (task name, status, time, project), never prompt or response content. Communication is local (`127.0.0.1`), and nothing leaves your machine. There is no telemetry and no scraping of claude.ai.

## Stack

C# / .NET (WPF), [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) for sensors. Reading temperatures requires running as administrator; without it, the app shows "N/A (needs admin)".

## Roadmap

1. ✅ Overlay with CPU and RAM.
2. ✅ GPU, disk, network and temperatures.
3. ✅ Local event server and task list.
4. 🚧 Task progress, notifications and settings.
5. Performance measurements, packaging and first release.

## Disclaimer

This is an independent, unofficial project. It is not affiliated with, endorsed by, or sponsored by Anthropic. "Claude" is a trademark of Anthropic.

## License

[MIT](LICENSE)
