# Monitor Project — Technical Handoff

> **Location:** `NymphsCore/Monitor/`  
> **Author:** Rauty  
> **Purpose:** Complete technical handoff documenting how the Llama Server Monitor works, how it integrates into NymphsCore, and how the Windows Monitor app interacts with WSL.

---

## 1. Project Overview

The **Llama Server Monitor** is a lightweight .NET 8 Windows Forms application that provides real-time visibility into a `llama-server` instance running inside the `NymphsCore` WSL2 distro. It displays:

| Metric | Source |
|---|---|
| Server Status (ONLINE / OFFLINE) | Process PID check via `ps aux` |
| Model Name | Parsed from `~/Nymphs-Brain/logs/lms.log` |
| Context Length | Parsed from running process arguments (`-c` flag) |
| GPU VRAM Usage | `nvidia-smi` query |
| GPU Temperature | `nvidia-smi` query |
| Tokens/Second (TPS) | Parsed from `lms.log` eval time lines |

The application runs entirely on the Windows side, communicates with WSL via `wsl.exe` subprocess invocation, and requires zero manual configuration on first launch.

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                          Windows (Host)                             │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              LlamaServerMonitor.exe (WinForms)               │   │
│  │                                                              │   │
│  │  Program.cs                                                  │   │
│  │  └─ Mutex-based single-instance guard                       │   │
│  │     └─ Application.Run(new MainForm())                      │   │
│  │                                                              │   │
│  │  MainForm.cs (Form1.cs)                                     │   │
│  │  ├─ BuildUI()                                                │   │
│  │  │  ├─ Status label (top-left)                              │   │
│  │  │  ├─ Refresh icon button (top-right, custom-drawn)        │   │
│  │  │  ├─ 5 metric rows (label + colored value)                │   │
│  │  │  ├─ System tray icon with context menu                   │   │
│  │  │  └─ Main menu (Refresh / Exit)                           │   │
│  │  │                                                          │   │
│  │  ├─ StartMonitoring()                                        │   │
│  │  │  ├─ EnsureScriptDeployed()  ──┐                          │   │
│  │  │  └─ Timer (5000ms) ──┬── CheckServer()                   │   │
│  │  │                      │    │                               │   │
│  │  │                      │    ├─ GetLlamaServerPid()          │   │
│  │  │                      │    ├─ WslQuery("model")            │   │
│  │  │                      │    ├─ WslQuery("context")          │   │
│  │  │                      │    ├─ WslQuery("gpu-vram")         │   │
│  │  │                      │    ├─ WslQuery("gpu-temp")         │   │
│  │  │                      │    └─ WslQuery("tps")              │   │
│  │  │                      │       │                            │   │
│  │  │                      │       ▼                            │   │
│  │  │                      │  wsl.exe -d NymphsCore ...         │   │
│  │  └──────────────────────┴────────────────────────────────────┤   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              │                                     │
│                              ▼                                     │
└─────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────────┐
│                     WSL ("NymphsCore" distro)                       │
│                                                                     │
│  ~/Nymphs-Brain/scripts/monitor_query.sh  ← Helper Script          │
│  ├─ Receives query type as $1 argument                             │   │
│  ├─ Reads data from:                                               │   │
│  │  ├─ ~/Nymphs-Brain/logs/lms.log  (model, context, tps)          │   │
│  │  ├─ ps aux  (pid, context via -c flag)                          │   │
│  │  └─ nvidia-smi  (gpu-vram, gpu-temp)                            │   │
│  └─ Prints single value to stdout                                  │
│                                                                     │
│  Data Files:                                                        │
│  ├─ ~/Nymphs-Brain/logs/lms.log       ← llama-server log           │   │
│  └─ ~/Nymphs-Brain/bin/lms-start      ← server start script ref    │   │
└─────────────────────────────────────────────────────────────────────┘
```

**Data Flow Summary:** Timer fires every 5s → `CheckServer()` runs on background thread → spawns `wsl.exe` processes → bash script reads logs/queries GPU → returns plain text on stdout → C# captures and parses → UI updated via `Invoke()` for thread safety.

---

## 3. Project Files

| File | Role |
|---|---|
| `Program.cs` | Entry point. Uses a named mutex (`Global\LlamaServerMonitor_Mutex`) to ensure only one instance runs. Launches `MainForm`. |
| `Form1.cs` | Entire application logic: UI construction, WSL bridge, metric polling, parsing, system tray, custom refresh icon button. |
| `monitor_query.sh` | Bash helper script. Bundled at build time, auto-deployed to WSL on first run. Extracts metrics from logs and system queries. |
| `LlamaServerMonitor.csproj` | .NET 8 WinForms project. Configures single-file publish, bundles `monitor_query.sh` via `CopyToOutputDirectory`. |
| `.gitignore` | Excludes `bin/` and `obj/` build artifacts. |
| `README.md` | User-facing documentation with features, requirements, and quick start guide. |
| `AI_HANDOFF.md` | Detailed AI agent handoff with internals, modification patterns, and troubleshooting. |

---

## 4. How the Windows App Works

### 4.1 Single-Instance Guard

```csharp
using var mutex = new System.Threading.Mutex(
    true, "Global\\LlamaServerMonitor_Mutex", out var createdNew);
if (!createdNew) return;  // Another instance is already running
```

A global named mutex prevents duplicate instances. If a second launch is attempted, it silently exits.

### 4.2 UI Construction (`BuildUI()`)

The UI is built entirely in code (no designer file):

- **Form:** 420×260 pixels, fixed border, no maximize, centered, black background
- **Status Label:** Top-left, 12pt bold, color-coded (Green = RUNNING, Red = OFFLINE, Gray = Checking)
- **Refresh Button:** Top-right, custom `Panel` subclass that draws a blue circular arrow icon with hover effects
- **5 Metric Rows:** Each has a white label (e.g., "Model:") and a colored bold value label
- **Color Scheme:**
  - Model → Cyan (`#00BCD2`)
  - Context → Light Green (`#8BC34A`)
  - GPU VRAM → Orange (`#FF9800`)
  - GPU Temp → Red Accent (`#FF5252`)
  - Tokens/sec → Yellow (`#FFEB3B`)

### 4.3 System Tray Integration

- `NotifyIcon` is always visible with tooltip showing current status
- **Double-click** → restores/shows the form
- **Right-click menu:** Show / Refresh / ─ / Exit
- Closing the form minimizes to tray (does not exit the application)

### 4.4 Polling Loop

```csharp
_statusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
_statusTimer.Tick += (s, e) => CheckServer();
```

Every 5 seconds, `CheckServer()` is invoked. It runs inside `Task.Run()` for non-blocking execution, then marshals UI updates back to the UI thread via `Invoke()`.

### 4.5 Custom Refresh Icon Button

The refresh button is a `Panel` subclass (`RefreshIconButton`) that:
- Overrides `OnPaint` to draw a rounded rectangle background and a blue circular arrow icon
- Handles `OnMouseEnter` / `OnMouseLeave` for hover background color changes
- Uses `DoubleBuffered = true` to prevent flicker
- Exposes a `Click` event wired to `CheckServer()`

---

## 5. Windows ↔ WSL Integration

### 5.1 The `wsl.exe` Subprocess Bridge

The entire Windows↔WSL communication happens through `wsl.exe` subprocess invocation. There are no named pipes, HTTP servers, or SSH connections — just direct process spawning.

### 5.2 `WslQuery(string query)` Method

```csharp
private static async Task<string> WslQuery(string query)
{
    var psi = new ProcessStartInfo
    {
        FileName = "wsl.exe",
        Arguments = $"-d {WslDistro} {WslScriptPath} {query}",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8
    };

    using var proc = Process.Start(psi);
    proc.WaitForExit(8000);  // 8-second timeout
    return (await proc.StandardOutput.ReadToEndAsync()).Trim();
}
```

**Key details:**
- **`-d NymphsCore`** targets the specific WSL distro
- **`UseShellExecute = false`** is required to redirect stdout/stderr
- **`CreateNoWindow = true`** prevents console window flicker
- **8-second timeout** prevents hanging if WSL is unresponsive
- Returns empty string `""` on any failure (catch-all)

### 5.3 Example Invocation

A TPS query results in the following Windows command:
```
wsl.exe -d NymphsCore ~/Nymphs-Brain/scripts/monitor_query.sh tps
```

Each metric query spawns a fresh `wsl.exe` process. The overhead is minimal (~10-20ms per query), and queries are sequential (not parallel) to avoid WSL process contention.

### 5.4 Script Deployment (`EnsureScriptDeployed()`)

On first launch, the app auto-deploys `monitor_query.sh` to the WSL distro:

1. **Check local script:** Looks for `monitor_query.sh` next to the `.exe` in the app directory
2. **Check WSL:** Runs `wsl.exe -d NymphsCore test -f ~/Nymphs-Brain/scripts/monitor_query.sh`
3. **If already exists (exit code 0):** Skip deployment
4. **If missing:** 
   - Converts Windows app path (e.g., `C:\Apps\Monitor\`) to WSL path (`/mnt/c/Apps/Monitor/`)
   - Runs `mkdir -p ~/Nymphs-Brain/scripts`
   - Runs `cp "/mnt/c/Apps/Monitor/monitor_query.sh" ~/Nymphs-Brain/scripts/monitor_query.sh`

This is a fire-and-forget operation — failures are silently caught, and the app continues running (queries will just return empty strings).

---

## 6. Helper Script (`monitor_query.sh`)

### 6.1 Script Location

- **Source (Windows):** `NymphsCore/Monitor/monitor_query.sh`
- **Deployed (WSL):** `~/Nymphs-Brain/scripts/monitor_query.sh`
- **Bundled:** Copied to output directory via `.csproj` `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`

### 6.2 Usage

```bash
monitor_query.sh <query>
```

### 6.3 Query Types

| Query | Implementation | Data Source |
|---|---|---|
| `pid` | `ps aux \| grep '[l]lama-server' \| awk '{print $2}'` | Process table |
| `model` | `grep 'general.name str' "$LOG_FILE" \| tail -1 \| sed 's/.*= //'` | `~/Nymphs-Brain/logs/lms.log` |
| `context` | `ps aux \| grep '[l]lama-server' \| grep -oP -- '-c\s+\K[0-9]+'` (formatted with commas) | Process arguments |
| `gpu-vram` | `nvidia-smi --query-gpu=memory.used,memory.total --format=csv,noheader,nounits` → formatted as "X GB/Y GB" | NVIDIA driver |
| `gpu-temp` | `nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader` → appended with "C" | NVIDIA driver |
| `tps` | `grep 'eval time' "$LOG_FILE" \| grep -v 'prompt eval time' \| tail -1 \| grep -oP` | `~/Nymphs-Brain/logs/lms.log` |

### 6.4 Output Contract

Each query prints **exactly one line** of plain text to stdout. If the metric cannot be determined, the script outputs `"—"`. No extra output, no logging to stdout (all debug would go to stderr).

### 6.5 Key Variables

```bash
LOG_FILE="$HOME/Nymphs-Brain/logs/lms.log"    # llama-server log file
LMS_START="$HOME/Nymphs-Brain/bin/lms-start"  # Server start script (referenced, not used directly)
```

---

## 7. Integration with NymphsCore

### 7.1 Relationship to Nymphs-Brain

The Monitor is a companion tool to the **Nymphs-Brain** project. Nymphs-Brain manages the llama-server lifecycle inside WSL, and the Monitor provides a visual dashboard for its runtime metrics.

**Key integration points:**
- The Monitor reads from `~/Nymphs-Brain/logs/lms.log` — the log file written by Nymphs-Brain's llama-server
- The helper script is deployed to `~/Nymphs-Brain/scripts/` — part of the Nymphs-Brain directory structure
- The Monitor targets the `NymphsCore` WSL distro — the same distro that Nymphs-Brain runs in

### 7.2 Relationship to NymphsCore Manager

The **NymphsCore Manager** (`Manager/apps/NymphsCoreManager/`) is the main Windows installation and configuration tool. The Monitor is a separate, standalone application that:
- Does not depend on the Manager at runtime
- Can run independently as long as the `NymphsCore` WSL distro exists and `llama-server` is running
- Is not installed or managed by the Manager (it's a separate concern)

### 7.3 WSL Distro Dependency

The Monitor is hardcoded to use the `NymphsCore` WSL distro via the `WslDistro` constant. This is the custom WSL distro created and managed by the NymphsCore installation process. If the distro name changes or a different distro is used, the constant must be updated.

---

## 8. Configuration

### 8.1 Hardcoded Constants (in `Form1.cs`)

| Constant | Value | Purpose |
|---|---|---|
| `WslDistro` | `"NymphsCore"` | WSL distro name to target |
| `WslScriptPath` | `"~/Nymphs-Brain/scripts/monitor_query.sh"` | Remote script path inside WSL |
| Timer Interval | `5000` ms | Auto-refresh polling interval |
| WSL Query Timeout | `8000` ms | Max wait for `wsl.exe` to return |
| Deploy Check Timeout | `5000` ms | Max wait for script existence check |
| Mutex Name | `"Global\\LlamaServerMonitor_Mutex"` | Single-instance guard |

### 8.2 Project Configuration (`.csproj`)

| Setting | Value |
|---|---|
| Target Framework | `net8.0-windows` |
| Output Type | `WinExe` (windowed, no console) |
| Publish Single File | `true` |
| Self-Contained | `false` (requires .NET 8 Desktop Runtime on target) |
| Enable Windows Targeting | `true` (can build from Linux/WSL) |

---

## 9. Building & Publishing

### 9.1 Build from Source

```bash
cd NymphsCore/Monitor
dotnet restore
dotnet build -c Release
```

Output: `bin/Release/net8.0-windows/LlamaServerMonitor.exe`

### 9.2 Publish as Single-File

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o publish
```

Output: `publish/LlamaServerMonitor.exe` + `publish/monitor_query.sh` (bundled automatically)

The published folder can be copied to any Windows machine with .NET 8 Desktop Runtime installed.

---

## 10. Extending the Monitor

### 10.1 Adding a New Metric

1. **Add a query case to `monitor_query.sh`:**
   ```bash
   new-metric)
       # Extract your metric
       echo "value"
       ;;
   ```

2. **Add a label field in `MainForm`:**
   ```csharp
   private Label? _newMetricLabel;
   ```

3. **Create the UI element in `BuildUI()`:**
   ```csharp
   Controls.Add(CreateLabel("New Metric:", 20, y));
   _newMetricLabel = CreateLabel("—", valueX, y, SomeColor);
   Controls.Add(_newMetricLabel);
   y += 30;
   ```

4. **Query in `CheckServer()`:**
   ```csharp
   var newMetric = await WslQuery("new-metric");
   ```

5. **Update the label in the `Invoke()` block:**
   ```csharp
   _newMetricLabel!.Text = string.IsNullOrEmpty(newMetric) ? "—" : newMetric;
   ```

6. **Reset on offline:**
   ```csharp
   _newMetricLabel!.Text = "—";
   ```

---

## 11. Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| All metrics show "—" | llama-server not running or log not at expected path | Verify server is running, check `~/Nymphs-Brain/logs/lms.log` exists |
| Status stays "Checking..." | `wsl.exe` not in PATH | Ensure WSL is installed on Windows |
| Script not found in WSL | Auto-deployment failed | Manually copy `monitor_query.sh` to `~/Nymphs-Brain/scripts/` in the NymphsCore distro |
| Wrong distro error | `WslDistro` constant doesn't match installed distro | Edit constant in `Form1.cs` to match your distro name |
| GPU metrics show "—" | Log doesn't contain GPU data or no NVIDIA GPU | Server may be running CPU-only; verify `nvidia-smi` works in WSL |
| App won't start second time | Mutex still held (crash) | Check for orphaned processes in Task Manager |

---

## 12. Summary

The Llama Server Monitor is a self-contained Windows application that bridges into WSL via `wsl.exe` subprocess calls to extract and display real-time metrics from a llama-server instance. It requires zero configuration, auto-deploys its helper script on first run, and runs as a single-instance system tray application. Its design is intentionally simple: no servers, no sockets, no dependencies beyond `wsl.exe` and .NET 8 Desktop Runtime.