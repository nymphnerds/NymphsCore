# NymphsCore ManagerFEUI — Handoff Document
**Created:** 2026-04-28 (Sydney, UTC+10:00)
**Last Updated:** 2026-04-28 21:46 AEST
**Status:** Code fixes applied for percentage calculations — APP REBUILT BUT RUNTIME STILL SHOWS INCORRECT VALUES (RAM=9577.9%, VRAM=30480.0%). Raw WSL command output may be malformed or the wrong processes are targeted. Next agent must debug the actual WSL command outputs.

---

## 🚨 CRITICAL: Immediate Issues for Next Agent

### Screenshot Evidence (Captured 2026-04-28 ~7:40 PM AEST)
The running app shows these IMPOSSIBLE values:
| Metric | Displayed | Expected |
|--------|-----------|----------|
| CPU Usage | 36.6% | ✅ Reasonable |
| RAM Usage | **9577.9%** | ❌ Should be 0-100% |
| VRAM Usage | **30480.0%** | ❌ Should be 0-100% |
| API Latency | -- | Acceptable when no load |
| Token Gen | 0.0 | Acceptable when idle |
| Queue Depth | 0 | Acceptable when idle |
| Uptime | 0m 2s | ❌ Server running ~1h13m |

### Root Cause Hypothesis
The code fixes in `LlamaMonitorService.cs` correctly compute percentages AND clamp to [0,100]. However the screenshot shows values FAR above 100%, which means **the clamping is NOT taking effect**. This suggests either:

1. **The app was NOT rebuilt after the fixes** — the old binary is still running
2. **The `Execute()` method returns malformed data** — e.g., `nvidia-smi` returns multiple lines, awk fails, or the command output includes unexpected text that `ParseDouble` returns 0 for, but somehow the raw number gets through unclamped
3. **A different code path is populating the metrics** — check if `DashboardViewModel` has a separate metric source

### Debug Steps (Priority Order)
1. **Verify the build**: Run `dotnet build -c Release` and confirm the binary timestamp is after the code fixes
2. **Add debug logging** to `LlamaMonitorService.Execute()` — write every command and its raw output to a file:
   ```csharp
   File.AppendAllText("/tmp/monitor_debug.log", $"[{DateTime.Now}] CMD: {command}\nOUT: {result}\n---");
   ```
3. **Manually run the WSL commands** from Windows CMD/PowerShell:
   ```bash
   wsl.exe -d NymphsCore bash -c "pgrep -f llama-server | head -1"
   wsl.exe -d NymphsCore bash -c "nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits -i 0"
   wsl.exe -d NymphsCore bash -c "awk '/MemTotal/{print \$2}' /proc/meminfo"
   ```
4. **Check if `pgrep -f llama-server` returns anything** — if the process is named differently (e.g., `llama-cli`, `server`, or a full path), all per-process metrics will be 0

---

## 1. Project Overview

The goal is to build **NymphsCore ManagerFEUI** — a WPF desktop application that serves as a **monitoring and control dashboard** for a NymphsCore WSL (Windows Subsystem for Linux) installation. This replaces the existing `NymphsCoreManager` (a 6-step WinForms installer) with a proper dashboard UI that provides ongoing monitoring, control, and management capabilities.

### Key Requirements
- **Position-independent**: Auto-detect the NymphsCore WSL installation regardless of where the app is located
- **Dashboard-first**: Main screen shows system metrics, server status, and live logs
- **Navigation**: Sidebar with all tool sections
- **Control capabilities**: Start/stop servers, manage Brain modules, runtime tools
- **Install/repair as options**: Keep start/repair/install processes as accessible pages, not the primary flow
- **Build output**: Finished .exe built into the same folder as the ManagerFEUI project

### Existing NymphsCoreManager
- Located at: `F:\Code Archive\NymphsCore\NymphsCoreManager\` (Windows path reference)
- Uses PowerShell scripts inside WSL for all server operations
- 6-step installer flow: Welcome → Install/Repair → Start/Stop/Restart → Finish

---

## 2. Architecture

### Technology Stack
- **Framework**: .NET 8.0 Windows (WPF, XAML)
- **Language**: C# 12
- **Target Platform**: Windows 10/11 with WSL2
- **IDE**: Visual Studio Code (cross-platform development via dotnet CLI)

### Project Structure
```
ManagerFEUI/
├── App.xaml / App.xaml.cs          # Application entry point, MainWindow initialization
├── ManagerFEUI.csproj              # Project file (net8.0-windows, OutputPath=.)
├── ViewModelBase.cs                # Base class with PropertyChanged event
├── HANDOFF.md                      # This file
├── Controls/
│   ├── LiveLogView.cs              # Auto-scrolling log display control
│   ├── MetricCard.cs               # Gauge arc + value display control (fixed alignment)
│   └── SparklineView.cs            # Sparkline chart control (placeholder)
├── Models/
│   ├── MetricCardData.cs           # Observable metric card model
│   └── MetricPoint.cs              # Single (value, timestamp) data point
├── Services/
│   ├── LlamaMonitorService.cs      # WSL polling for CPU/RAM/VRAM metrics
│   ├── ServerManagerService.cs     # Server start/stop/restart/repair via WSL
│   └── WslService.cs              # WSL distro lifecycle (start/stop/reboot/status)
├── ViewModels/
│   └── DashboardViewModel.cs       # Main VM: navigation, metrics, commands, status, WSL lifecycle
└── Views/
    ├── MainWindow.xaml / .cs       # Shell: sidebar nav + ContentFrame + status bar
    ├── DashboardPage.xaml / .cs    # Dashboard: metrics grid, server controls, logs
    ├── LogsPage.xaml / .cs         # Full-screen log viewer with live scrolling
    └── PlaceholderPage.xaml / .cs  # Generic placeholder for future pages
```

### Key Design Decisions
1. **WPF over WinForms**: Proper MVVM, data binding, modern UI
2. **WSL Bridge**: All server operations go through `wsl.exe -d NymphsCore bash -c "..."`
3. **Dual Monitoring**: LlamaMonitorService polls WSL directly; ServerManagerService tracks state
4. **Observable Collections**: Metric data flows via `ObservableCollection<T>` for real-time UI updates
5. **RelayCommand Pattern**: Simple ICommand implementation for all user actions
6. **WslService**: Dedicated service for WSL distro lifecycle (start/stop/reboot/status check)

---

## 3. Bugs Fixed In-Code

### Bug #1: RAM Usage percentage calculation (FIXED IN CODE)
**Root Cause**: Raw RSS memory in KB was displayed directly as percentage instead of computing `(process_mem_kb / total_system_mem_kb) * 100`.

**Fix Applied** (LlamaMonitorService.cs lines 97-105):
```csharp
var memKbRaw = Execute("pgrep -f llama-server | xargs -I{} ps -p {} -o rss= 2>/dev/null | awk '{s+=$1} END {print s+0}'").Trim();
var memKb = ParseLong(memKbRaw);
var totalMemRaw = Execute("awk '/MemTotal/{print $2}' /proc/meminfo 2>/dev/null").Trim();
var totalMemKb = ParseLong(totalMemRaw);
double memPercent = 0;
if (totalMemKb > 1000 && memKb > 0)
{
    memPercent = Math.Clamp((memKb / (double)totalMemKb) * 100.0, 0.0, 100.0);
}
```

### Bug #2: VRAM Usage percentage calculation (FIXED IN CODE)
**Root Cause**: Raw VRAM values in MB displayed as percentages without division.

**Fix Applied** (LlamaMonitorService.cs lines 108-116):
```csharp
var vramUsedRaw = Execute("nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits -i 0 2>/dev/null | head -1").Trim();
var vramUsed = ParseDouble(vramUsedRaw);
var vramTotalRaw = Execute("nvidia-smi --query-gpu=memory.total --format=csv,noheader,nounits -i 0 2>/dev/null | head -1").Trim();
var vramTotal = ParseDouble(vramTotalRaw);
double vramPercent = 0;
if (vramTotal > 100 && vramUsed > 0)
{
    vramPercent = Math.Clamp((vramUsed / vramTotal) * 100.0, 0.0, 100.0);
}
```

### Bug #3: CPU Usage clamping (FIXED IN CODE)
```csharp
var cpuRaw = ParseDouble(Execute($"ps -p {pid} -o %cpu= 2>/dev/null"));
double cpu = Math.Clamp(cpuRaw, 0.0, 100.0);
```

### Bug #4: MetricCard value alignment (FIXED IN CODE)
- Gauge row uses a two-column Grid (Star/Star)
- Left column: 80x80 Canvas with the semicircle gauge, centered
- Right column: TextBlock with `HorizontalAlignment=Stretch` and `TextAlignment=Center`
- Arc math: proper 180-degree semicircle from right-to-left (0→π radians), centered at (40,40) with radius 35

### Bug #5: MetricCard gauge showing >100% visually (FIXED IN CODE)
- `double sweep = Math.Min(Math.Max(0, progress), 1.0) * 180.0;` with clamping to [0,1]

---

## 4. BUGS STILL OBSERVED AT RUNTIME

### Runtime Bug #1: RAM/VRAM showing impossible percentages (9577.9%, 30480.0%)
**Status**: Code has Math.Clamp(0, value, 100) but UI shows values > 100%, meaning clamping is NOT effective.

**Possible causes** (in priority order):
1. **Old binary still running** — app not rebuilt after fix
2. **DashboardViewModel transforms the data** — check if it multiplies or misinterprets the ServiceStatus values before passing to MetricCardData
3. **MetricCardData display logic is wrong** — check how `Percentage` and `DisplayValue` are computed from the raw metric values
4. **Data binding issue** — wrong property bound, or a second data source overriding

**Debug approach**: Add debug logging in both LlamaMonitorService AND DashboardViewModel to trace the value from WSL command → ServiceStatus → MetricCardData → UI.

### Runtime Bug #2: Uptime shows "0m 2s" instead of actual server runtime
**Observed**: Uptime card shows `0m 2s` right after app start, even though the server has been running for ~1h13m.

**Root cause** (DashboardViewModel.cs):
```csharp
UptimeText = FormatUptime(ServerManager.Uptime);
```
`ServerManager.Uptime` tracks elapsed time since the "Start Server" button was pressed **in the current UI session**, NOT the actual server process runtime.

**Fix needed**: Query actual process elapsed time from WSL:
```csharp
var uptimeRaw = Execute($"ps -p {pid} -o etime= 2>/dev/null").Trim(); // returns "01:12:34" format
```
Then parse and display this instead of `ServerManager.Uptime`.

### Runtime Bug #3: Process discovery may fail
**Issue**: `pgrep -f llama-server` may return empty if the process is named differently.

**Fix needed**: Add fallback process discovery by port:
```bash
pid=$(pgrep -f llama-server | head -1)
if [ -z "$pid" ]; then
  pid=$(ss -tlnp 'sport = :8080' 2>/dev/null | grep -oP 'pid=\K\d+' | head -1)
fi
```

### Runtime Bug #4: WSL commands too slow (10+ process spawns per poll)
**Observed**: Each 2-second poll spawns 10+ separate `wsl.exe` processes.

**Commands per poll**:
1. `nc -z localhost 8080`
2. `pgrep -f llama-server`
3. `ps -p {pid} -o %cpu=`
4. `pgrep -f llama-server | xargs ... awk`
5. `awk '/MemTotal/{print $2}' /proc/meminfo`
6. `nvidia-smi --query-gpu=memory.used`
7. `nvidia-smi --query-gpu=memory.total`
8. `curl ... queue.info | jq`
9. `curl ... metrics | grep avg_prompt_tps`
10. `curl ... metrics | grep avg_decode_tps`

**Fix needed**: Batch all commands into a single WSL heredoc that outputs JSON:
```bash
wsl.exe -d NymphsCore bash << 'EOF'
{
  "reachable": $(nc -z localhost 8080 2>/dev/null && echo 1 || echo 0),
  "pid": $(pgrep -f llama-server | head -1),
  ...
}
EOF
```

---

## 5. Build Instructions

### Prerequisites
- .NET 8.0 SDK
- Windows 10/11 (WPF requires Windows)
- Visual Studio Code (optional, any editor works)

### Build Command
```bash
cd "NymphsCore/ManagerFEUI"
dotnet build -c Release
dotnet publish -c Release -o ./publish
```

### Run Command
```bash
dotnet run --project "NymphsCore/ManagerFEUI/ManagerFEUI.csproj"
```

---

## 6. Current State of Each Page

### ✅ DashboardPage (IMPLEMENTED)
- System Overview section with 7 metric cards (CPU, RAM, VRAM, Latency, Token Gen, Queue, Uptime)
- Gauge arcs for CPU/RAM/VRAM with proper percentage display (0-100% clamped in code)
- Server control buttons: Start, Stop, View Logs
- Status indicator in header (Connected/Disconnected)
- Recent Activity log section at bottom

### ✅ LogsPage (IMPLEMENTED)
- Full-screen log viewer
- Auto-scrolling live log display
- Uses LiveLogView custom control

### ⬜ Runtime Tools (PLACEHOLDER)
- Shows "Runtime Tools — Coming Soon" placeholder

### ⬜ Brain (PLACEHOLDER)
- Shows "Brain — Coming Soon" placeholder

### ⬜ Addons (PLACEHOLDER)
- Shows "Addons — Coming Soon" placeholder

### ⬜ Installer (PLACEHOLDER)
- Shows "Installer — Coming Soon" placeholder

---

## 7. WSL Integration Points

All WSL communication goes through `wsl.exe -d NymphsCore bash` with commands piped via stdin:

| Operation | Command |
|-----------|---------|
| Check server reachability | `nc -z localhost 8080; echo $?` |
| Find server PID | `pgrep -f llama-server \| head -1` |
| CPU usage | `ps -p {pid} -o %cpu=` |
| RAM usage (all processes) | `pgrep -f llama-server \| xargs -I{} ps -p {} -o rss= \| awk` |
| Total system RAM | `awk '/MemTotal/{print $2}' /proc/meminfo` |
| VRAM used | `nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits -i 0` |
| VRAM total | `nvidia-smi --query-gpu=memory.total --format=csv,noheader,nounits -i 0` |
| Queue depth | `curl -s http://localhost:8080/v1/queue.info \| jq .size` |
| Token gen (prompt) | `curl -s http://localhost:8080/metrics \| grep avg_prompt_tps` |
| Token gen (decode) | `curl -s http://localhost:8080/metrics \| grep avg_decode_tps` |

### Important: Execute() method detail
Commands are NOT passed via `bash -c "$cmd"` (which caused shell variable expansion issues with `$1`, `$2`, `$?`). Instead, commands are piped via standard input to `bash`:
```csharp
var psi = new ProcessStartInfo
{
    FileName = "wsl.exe",
    Arguments = "-d NymphsCore bash",
    RedirectStandardInput = true,
    ...
};
p.StandardInput.WriteLine(command);
```

---

## 8. Next Agent Checklist

### Priority 1: Fix the percentage display (BLOCKING)
- [ ] Rebuild the project with `dotnet build -c Release`
- [ ] Add debug logging to `LlamaMonitorService.Execute()` to capture raw command outputs
- [ ] Trace the metric value flow: WSL command → ParseDouble/ParseLong → ServiceStatus → DashboardViewModel → MetricCardData → MetricCard UI
- [ ] Verify `Math.Clamp` is actually being reached (add logging before/after)
- [ ] Check if DashboardViewModel is transforming the values incorrectly

### Priority 2: Fix Uptime
- [ ] Replace `ServerManager.Uptime` with actual process elapsed time from `ps -p {pid} -o etime=`
- [ ] Parse the `HH:MM:SS` or `MM:SS` format into a display string

### Priority 3: Improve WSL performance
- [ ] Batch all monitoring commands into a single WSL heredoc call
- [ ] Return JSON, parse with a lightweight JSON reader

### Priority 4: Robust process discovery
- [ ] Add fallback PID discovery by port (ss/netstat)
- [ ] Handle the case where llama-server is not the process name

### Priority 5: Implement remaining pages
- [ ] Runtime Tools page
- [ ] Brain page
- [ ] Addons page
- [ ] Installer page (migrate from existing NymphsCoreManager)

---

## 9. Key Files to Inspect First

| File | Why |
|------|-----|
| `Services/LlamaMonitorService.cs` | Contains the metric calculation code — add debug logging here |
| `ViewModels/DashboardViewModel.cs` | Transforms ServiceStatus into MetricCardData — check for value corruption |
| `Models/MetricCardData.cs` | Defines how Percentage/DisplayValue are computed |
| `Controls/MetricCard.cs` | Custom WPF control that renders the gauge arc and text |
| `Views/DashboardPage.xaml` | Layout: UniformGrid Columns=3, binds to MetricCards collection |