# Llama Server Monitor

A lightweight .NET 8 Windows Forms application that monitors a llama-server running inside WSL2, displaying real-time metrics such as model name, context length, GPU VRAM usage, GPU temperature, and tokens-per-second throughput.

## Features

- **Real-time metrics** — auto-refreshes every 5 seconds
- **Dark theme UI** — black background with color-coded status indicators
- **Model info** — displays loaded model name (cyan)
- **Context length** — shows active context window size (light green)
- **GPU VRAM** — current VRAM usage / total VRAM (orange)
- **GPU temperature** — live GPU core temperature (red accent)
- **Tokens/sec** — live inference throughput (yellow)
- **System tray integration** — minimize to tray, double-click to restore, context menu with Show/Refresh/Exit
- **Custom refresh icon button** — blue circular arrow icon with hover effect
- **Server detection** — process-based PID check for online/offline status
- **Zero configuration** — auto-detects log file, auto-deploys helper script to WSL

## Requirements

| Requirement | Detail |
|---|---|
| OS | Windows 10 or Windows 11 |
| WSL | WSL2 with the `NymphsCore` distro installed |
| Runtime | .NET 8 Windows Desktop Runtime (for running published app) |
| SDK | .NET 8 SDK (for building from source) |
| llama-server | Must be running inside WSL and writing a log to `/tmp/llama_*_server.log` |

## How It Works

1. The app calls `wsl.exe -d NymphsCore` to execute a bundled Bash script (`monitor_query.sh`) inside WSL.
2. On startup, the app auto-deploys `monitor_query.sh` to `~/Nymphs-Brain/scripts/monitor_query.sh` in the WSL distro if not already present.
3. The script locates the latest llama-server log under `/tmp/`, tails the last ~200 lines, and extracts key metrics using `grep`/`awk` based on the query type passed as an argument.
4. Query types: `model`, `context`, `gpu-vram`, `gpu-temp`, `tps`, `pid`
5. Metrics are returned on stdout, which the C# app captures and parses individually.
6. Parsed values are applied to WinForms labels on the UI thread with color-coded indicators.
7. Server online/offline status is determined by checking the llama-server process PID.

## Quick Start — Build from Source

```bash
cd Monitor
dotnet restore
dotnet build -c Release
```

The built executable will be at `bin/Release/net8.0-windows/LlamaServerMonitor.exe`.

## Publish — Single-file executable

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

The single-file `LlamaServerMonitor.exe` will be in the `publish/` folder, ready to distribute alongside `monitor_query.sh` (bundled automatically).

## First Run

On first launch, the app checks if `monitor_query.sh` exists at `~/Nymphs-Brain/scripts/monitor_query.sh` inside the `NymphsCore` WSL distro. If it doesn't, it auto-deploys the bundled copy from the application directory. No manual setup required.

## Configuration

| Setting | Location | Default |
|---|---|---|
| WSL Distro | `WslDistro` constant in `Form1.cs` | `"NymphsCore"` |
| Script Path | `WslScriptPath` constant in `Form1.cs` | `"~/Nymphs-Brain/scripts/monitor_query.sh"` |
| Refresh Interval | `_statusTimer.Interval` in `StartMonitoring()` | `5000` ms |

### Project Files

| File | Purpose |
|---|---|
| `Form1.cs` | Main UI, WSL invocation, metric parsing, timer loop, custom refresh icon button |
| `Program.cs` | Application entry point |
| `monitor_query.sh` | Bash helper script that queries llama-server log inside WSL |
| `LlamaServerMonitor.csproj` | .NET 8 project file |
| `.gitignore` | Excludes `bin/` and `obj/` |

## UI Colors

| Metric | Color | Hex |
|---|---|---|
| Status Running | Green | — |
| Status Offline | Red | — |
| Model | Cyan | `#00BCD2` |
| Context | Light Green | `#8BC34A` |
| GPU VRAM | Orange | `#FF9800` |
| GPU Temp | Red Accent | `#FF5252` |
| Tokens/sec | Yellow | `#FFEB3B` |
| Refresh Icon | Blue | `#2196F3` |
| Background | Black | `#000000` |

## License

Part of the NymphsCore project.