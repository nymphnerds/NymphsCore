# Llama Server Monitor

A lightweight .NET 8 Windows Forms application that monitors a llama-server running inside WSL2, displaying real-time metrics such as model name, context length, GPU VRAM usage, GPU temperature, and tokens-per-second throughput.

## Features

- **Real-time metrics** — auto-refreshes every 2 seconds
- **Model info** — displays loaded model name and GPU tensor split count
- **Context length** — shows active context window size
- **GPU VRAM** — current VRAM usage / total VRAM
- **GPU temperature** — live GPU core temperature
- **Tokens/sec** — live inference throughput
- **Zero configuration** — auto-detects log file, auto-deploys helper script to WSL

## Requirements

| Requirement | Detail |
|---|---|
| OS | Windows 10 or Windows 11 |
| WSL | WSL2 with a Linux distro installed (default: `Ubuntu`) |
| Runtime | .NET 8 Windows Desktop Runtime (for running published app) |
| SDK | .NET 8 SDK (for building from source) |
| llama-server | Must be running inside WSL and writing a log to `/tmp/llama_*_server.log` |

## How It Works

1. The app calls `wsl.exe` to execute a bundled Bash script (`monitor_query.sh`) inside WSL.
2. The script locates the latest llama-server log under `/tmp/`, tails the last ~200 lines, and extracts key metrics using `grep`/`awk`.
3. Metrics are returned line-by-line on stdout, which the C# app captures and parses.
4. Parsed values are applied to WinForms labels on the UI thread.

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

On first launch, the app checks if `monitor_query.sh` exists at `~/Nymphs-Brain/scripts/monitor_query.sh` inside WSL. If it doesn't, it auto-deploys the bundled copy from the application directory. No manual setup required.

## Configuration

The WSL distro name can be changed by modifying the `DistroName` constant in `Form1.cs` (default: `"Ubuntu"`).

### Project Files

| File | Purpose |
|---|---|
| `Form1.cs` | Main UI, WSL invocation, metric parsing, timer loop |
| `Program.cs` | Application entry point |
| `monitor_query.sh` | Bash helper script that queries llama-server log inside WSL |
| `LlamaServerMonitor.csproj` | .NET 8 project file |
| `.gitignore` | Excludes `bin/` and `obj/` |

## License

Part of the NymphsCore project.