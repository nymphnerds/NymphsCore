# AI Handoff — Llama Server Monitor

## Purpose

This document provides a complete technical handoff for AI agents working on or extending the **Llama Server Monitor** project. It covers architecture, the Windows↔WSL bridge, the helper script lifecycle, parsing logic, and common modification patterns.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     Windows (C# WinForms)                   │
│                                                             │
│  MainForm.cs                                                │
│  ├─ BuildUI()          → creates labels, timer, tray icon  │
│  ├─ StartMonitoring()  → deploys script, starts timer      │
│  ├─ CheckServer()      → async polling loop (every 5s)     │
│  │  ├─ GetLlamaServerPid() → wsl query "pid"               │
│  │  ├─ WslQuery("model")                                  │
│  │  ├─ WslQuery("context")                                 │
│  │  ├─ WslQuery("gpu-vram")                                │
│  │  ├─ WslQuery("gpu-temp")                                │
│  │  └─ WslQuery("tps")                                     │
│  └─ EnsureScriptDeployed() → copies .sh to WSL on 1st run  │
│                                                             │
│  wsl.exe ←─────────────────────────────────────────────────┐│
└─────────────────────────────────────────────────────────────┘│
                                                                ▼
┌─────────────────────────────────────────────────────────────┐
│                  WSL ("NymphsCore" distro)                   │
│                                                             │
│  ~/Nymphs-Brain/scripts/monitor_query.sh  ← helper script   │
│  ├─ Finds /tmp/llama_*_server.log via `find`                │
│  ├─ Tails last 200 lines                                    │
│  └─ Greps for requested metric, prints value to stdout      │
└─────────────────────────────────────────────────────────────┘
```

**Data flow:** Timer fires → `CheckServer()` spawns `wsl.exe` → script reads log + greps → stdout captured → C# parses → UI labels updated via `Invoke()`.

---

## Key Constants (in `Form1.cs`)

| Constant | Value | Purpose |
|---|---|---|
| `WslDistro` | `"NymphsCore"` | WSL distro name to target |
| `WslScriptPath` | `"~/Nymphs-Brain/scripts/monitor_query.sh"` | Remote script path inside WSL |
| Timer Interval | `5000` ms | Auto-refresh polling interval |
| WSL Timeout | `8000` ms | Max wait for `wsl.exe` to return |
| Deploy Check Timeout | `5000` ms | Max wait for script existence check |

---

## Windows ↔ WSL Bridge

### How `wsl.exe` is Invoked

Every metric query uses `ProcessStartInfo` with:
- **FileName:** `"wsl.exe"` (resolved from PATH, no hardcoded path needed)
- **Arguments:** `"-d NymphsCore ~/Nymphs-Brain/scripts/monitor_query.sh {query}"`
- **UseShellExecute:** `false` (required to redirect stdout)
- **RedirectStandardOutput:** `true` (capture script output)
- **RedirectStandardError:** `true` (capture errors silently)
- **CreateNoWindow:** `true` (no console popup)
- **StandardOutputEncoding:** `UTF8`

Example invocation for TPS:
```
wsl.exe -d NymphsCore ~/Nymphs-Brain/scripts/monitor_query.sh tps
```

### Why This Approach

- **No named pipes, no HTTP, no SSH** — just `wsl.exe` as a subprocess bridge
- Each query is a fresh process (lightweight, ~10-20ms overhead)
- Scripts are idempotent and stateless
- Falls back gracefully: if WSL is unavailable or script fails, returns `""`

---

## Helper Script Lifecycle

### 1. Bundling (Build Time)

The `.csproj` includes:
```xml
<None Update="monitor_query.sh">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```
This copies `monitor_query.sh` to the output directory (`bin/` or `publish/`) alongside the `.exe` on every build.

### 2. Deployment (First Launch)

`EnsureScriptDeployed()` runs once at startup:
1. Checks if local script exists next to `.exe`
2. Runs `wsl.exe -d NymphsCore test -f ~/Nymphs-Brain/scripts/monitor_query.sh`
3. If exit code is 0 → script already deployed, skip
4. If not deployed:
   - Converts Windows app path to WSL `/mnt/c/...` path
   - Runs `mkdir -p ~/Nymphs-Brain/scripts`
   - Runs `cp "/mnt/c/.../monitor_query.sh" ~/Nymphs-Brain/scripts/monitor_query.sh`

### 3. Runtime

Every metric query calls the deployed script in WSL via `WslQuery()`. The script:
- Finds the log file
- Greps for the requested metric
- Prints the value to stdout
- Returns immediately

---

## Helper Script Internals (`monitor_query.sh`)

The script accepts one argument: the query type. Supported queries:

| Query | What It Does |
|---|---|
| `pid` | Finds `llama-server` process PID via `pgrep` |
| `model` | Greps log for model name line (e.g., `model_name:` or loading line) |
| `context` | Greps log for context/seq-len value |
| `gpu-vram` | Greps log for VRAM line (e.g., `VRAM: 28 GB / 24 GB`) |
| `gpu-temp` | Greps log for temperature line |
| `tps` | Greps log for `eval time` line, extracts tokens/sec value |

### Script Structure (simplified)

```bash
#!/usr/bin/env bash
QUERY="$1"
LOG=$(ls -t /tmp/llama_*_server.log 2>/dev/null | head -1)
TAIL=$(tail -n 200 "$LOG" 2>/dev/null)

case "$QUERY" in
  pid)     pgrep -f llama-server | head -1 ;;
  model)   echo "$TAIL" | grep -i 'model' | head -1 | awk '{print $2}' ;;
  context) echo "$TAIL" | grep -i 'context\|n_ctx' | head -1 | awk -F':' '{print $2}' | tr -d ' ' ;;
  gpu-vram) echo "$TAIL" | grep -i 'vram' | head -1 | sed 's/.*VRAM://' | xargs ;;
  gpu-temp) echo "$TAIL" | grep -i 'temp' | head -1 | awk -F':' '{print $2}' | tr -d ' ' | sed 's/°C//' | xargs ;;
  tps)     echo "$TAIL" | grep 'eval time' | head -1 | awk '{for(i=1;i<=NF;i++) if($i=="tokens") print $(i-1)/1}' ;;
esac
```

**Output contract:** Each query prints exactly one line of plain text to stdout. No extra output, no logging to stdout (all debug goes to stderr).

---

## C# Parsing Logic

### `WslQuery(string query)` — `Form1.cs`

- Spawns `wsl.exe` with the script and query argument
- Waits up to 8 seconds for exit
- Reads stdout, trims whitespace, returns the string
- Returns `""` on any failure (WSL not found, timeout, script error)

### `CheckServer()` — metric update flow

1. `GetLlamaServerPid()` → if no PID, set all labels to `"—"`, status to `"⏹ OFFLINE"`
2. If running, fire 5 sequential `WslQuery()` calls (model, context, vram, temp, tps)
3. Each result is checked: empty → `"—"`, otherwise use raw string
4. UI updated via `Invoke()` for thread safety (queries run on background thread)

### UI Thread Safety

`CheckServer()` runs inside `Task.Run()`, so all label updates are wrapped in `Invoke()`:
```csharp
Invoke(() => {
    _modelLabel!.Text = string.IsNullOrEmpty(model) ? "—" : model;
    // ...
});
```

---

## UI Layout

| Control | Type | Position | Color |
|---|---|---|---|
| Status | Label, 12pt Bold | (20, 18) | Green/Red/Gray |
| Refresh | Button, Flat | (320, 14) | Dark gray |
| "Model:" | Label | (20, 58) | White |
| Model value | Label, Bold | (135, 58) | Cyan |
| "Context:" | Label | (20, 88) | White |
| Context value | Label, Bold | (135, 88) | LightGreen |
| "GPU VRAM:" | Label | (20, 118) | White |
| VRAM value | Label, Bold | (135, 118) | Orange |
| "GPU Temp:" | Label | (20, 148) | White |
| Temp value | Label, Bold | (135, 148) | RedAccent |
| "Tokens/sec:" | Label | (20, 178) | White |
| TPS value | Label, Bold | (135, 178) | Yellow |

Window: 420×260, fixed border, no maximize. Dark theme (black background).

---

## System Tray

- `NotifyIcon` always visible, tooltip shows status
- Double-click → show form
- Right-click menu: Show / Refresh / ─ / Exit
- Form close → minimize to tray (form closing hides, doesn't exit)

---

## Common Modifications

### Change WSL Distro
Edit `WslDistro` constant in `Form1.cs`:
```csharp
private static readonly string WslDistro = "Ubuntu"; // or any distro name
```

### Change Polling Interval
Edit timer interval in `StartMonitoring()`:
```csharp
_statusTimer = new System.Windows.Forms.Timer { Interval = 3000 }; // 3 seconds
```

### Add a New Metric
1. Add a new label field and UI element in `BuildUI()`
2. Add a new query call in `CheckServer()`: `var foo = await WslQuery("foo");`
3. Add a `foo)` case to `monitor_query.sh`
4. Update label in the `Invoke()` block

### Change Script Deployment Path
Edit `WslScriptPath` constant in `Form1.cs`. Also update `EnsureScriptDeployed()` if the directory structure changes.

---

## Project File Summary

| File | Role |
|---|---|
| `Form1.cs` | Entire application logic (UI, WSL bridge, polling, parsing) |
| `Program.cs` | Entry point, creates `MainForm`, calls `Application.Run()` |
| `monitor_query.sh` | Bash helper deployed to WSL, extracts metrics from log |
| `LlamaServerMonitor.csproj` | .NET 8 WinForms project, bundles script to output |
| `.gitignore` | Excludes `bin/` and `obj/` |
| `README.md` | User-facing documentation |
| `AI_HANDOFF.md` | This file |

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| All metrics show "—" | llama-server not running or log not in `/tmp/` | Verify server is running, check log path |
| Status stays "Checking..." | `wsl.exe` not in PATH | Ensure WSL is installed on Windows |
| Script not found in WSL | Deployment failed | Manually copy `monitor_query.sh` to `~/Nymphs-Brain/scripts/` |
| Wrong distro | `WslDistro` constant mismatch | Edit constant to match installed distro name |
| GPU metrics show "—" | Log doesn't contain GPU lines | Server may be running CPU-only; check log manually |

---

## Building & Running

```bash
# Build
cd Monitor
dotnet build -c Release

# Run (debug)
dotnet run

# Publish single-file
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

The published `LlamaServerMonitor.exe` requires .NET 8 Desktop Runtime on the target machine. `monitor_query.sh` will be copied to the publish folder automatically.