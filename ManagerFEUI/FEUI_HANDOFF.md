# NymphsCore ManagerFEUI — Handoff Document
**Created:** 2026-04-28 (Sydney, UTC+10:00)
**Last Updated:** 2026-04-29 03:59 AEST
**Status:** Full codebase audit completed. Architecture improved since last handoff (script-based monitoring), but several binding bugs and design issues discovered.

---

## 🚨 CRITICAL: Issues Discovered in Code Audit

### Bug #1: MainWindow.xaml references non-existent `ViewLogsCommand` (L51)
**Severity: HIGH — Binding silently fails at runtime**
```xml
<!-- MainWindow.xaml line 51 -->
<Button Style="{StaticResource NavButton}" Content="View Logs"
        Command="{Binding ViewLogsCommand}"/>
```
The ViewModel defines `ShowLogsCommand` (line 143 of DashboardViewModel.cs), NOT `ViewLogsCommand`. The "View Logs" button in the sidebar will do nothing when clicked.

**Fix**: Change `ViewLogsCommand` → `ShowLogsCommand` in MainWindow.xaml line 51.

---

### Bug #2: MainWindow status bar is hardcoded, NOT bound (L76-77)
**Severity: MEDIUM — Status always shows "Connected" in green**
```xml
<!-- MainWindow.xaml lines 76-77 -->
<Ellipse Width="8" Height="8" Fill="{StaticResource GreenAccent}" Margin="0,0,8,0"/>
<TextBlock Text="Connected" FontSize="12" Foreground="{StaticResource GreenAccent}"/>
```
Should bind to `StatusColor` and `StatusText` from the ViewModel:
```xml
<Ellipse Width="8" Height="8" Fill="{Binding StatusColor}" Margin="0,0,8,0"/>
<TextBlock Text="{Binding StatusText}" FontSize="12" Foreground="{Binding StatusColor}"/>
```

---

### Bug #3: RAM card shows WSL-wide memory, not llama-server memory
**Severity: CLARIFICATION NEEDED — May be intentional**

`monitor_query.sh` line 27 computes:
```bash
MEM_USED_KB=$(awk '/^MemTotal:/{total=$2} /^MemAvailable:/{avail=$2} END {print total-avail}' /proc/meminfo)
```
This is **total WSL memory used by ALL processes**, not just llama-server. The card title says "WSL Memory" which is technically accurate. If the original intent was to show per-process RSS memory for llama-server (as the old handoff described), this is a regression from the previous architecture.

**Decision needed**: Is "WSL Memory" (all WSL processes) the correct metric, or should it be llama-server-specific?

---

### Bug #4: CPU card shows Windows system CPU, not llama-server CPU
**Severity: CLARIFICATION NEEDED — May be intentional**

`LlamaMonitorService.GetWindowsCpuPercent()` uses a Windows `PerformanceCounter("Processor", "% Processor Time", "_Total")` which measures **entire Windows host CPU**, not the llama-server process. The subtitle says "System CPU" which is honest, but the old architecture measured per-process CPU inside WSL.

**Decision needed**: Should CPU show Windows system-wide or WSL llama-server process?

---

### Bug #5: `ViewModelBase.cs` is dead code
`DashboardViewModel` implements `INotifyPropertyChanged` directly instead of inheriting from `ViewModelBase`. The base class exists but is unused.

---

### Bug #6: `ServerManagerService` properties never populated
The following properties exist but are **never set** anywhere in the codebase:
- `Model` — always `""`
- `ContextSize` — always `""`
- `GpuVram` — always `""`
- `GpuTemp` — always `""`

The health script (`health_query.sh`) doesn't query for model info. These will always display as "—" in the UI.

---

### Bug #7: DashboardPage DataContext set by accident, not by design
`DashboardPage` is instantiated as `new DashboardPage()` without explicitly setting `DataContext`. It works because WPF's DataContext inheritance propagates the MainWindow's DataContext (DashboardViewModel) down to child elements. This is fragile — if the page creation pattern changes, bindings will break silently.

---

## 1. Project Overview

The goal is to build **NymphsCore ManagerFEUI** — a WPF desktop application that serves as a **monitoring and control dashboard** for a NymphsCore WSL (Windows Subsystem for Linux) installation.

### Key Requirements
- **Position-independent**: Auto-detect the NymphsCore WSL installation regardless of where the app is located
- **Dashboard-first**: Main screen shows system metrics, live logs
- **Navigation**: Sidebar with all tool sections
- **Control capabilities**: Start/stop servers, manage Brain modules, runtime tools
- **Install/repair as options**: Keep start/repair/install processes as accessible pages, not the primary flow

---

## 2. Architecture

### Technology Stack
- **Framework**: .NET 8.0 Windows (WPF, XAML)
- **Language**: C# 12
- **Target Platform**: Windows 10/11 with WSL2
- **Project Config**: `EnableWindowsTargeting=true` (can be built from Linux/WSL)

### Project Structure
```
ManagerFEUI/
├── App.xaml / App.xaml.cs              # Entry point, crash handlers, global resources (dark theme)
├── ManagerFEUI.csproj                  # Project file (net8.0-windows, WinExe, UseWPF)
├── ViewModelBase.cs                    # Base INotifyPropertyChanged (UNUSED — dead code)
├── HANDOFF.md                          # This file
├── monitor_query.sh                    # Batched WSL monitoring script (deployed to /tmp/llama_monitor.sh)
├── health_query.sh                     # Batched WSL health check script (deployed to /tmp/llama_health.sh)
├── Controls/
│   ├── LiveLogView.cs                  # Auto-scrolling log display (TextBox in ScrollViewer)
│   ├── MetricCard.cs                   # Semicircle gauge arc + value display (Canvas-based)
│   └── SparklineView.cs                # Sparkline chart control (PLACEHOLDER)
├── Models/
│   ├── MetricCardData.cs               # Observable metric card model (Title, Percentage, DisplayValue, Sub, Color)
│   └── MetricPoint.cs                  # Single (value, timestamp) data point
├── Services/
│   ├── LlamaMonitorService.cs          # Polls WSL via monitor_query.sh script every 2s
│   ├── ServerManagerService.cs         # Server start/stop/restart via health_query.sh every 3s
│   └── WslService.cs                   # WSL distro lifecycle (start/stop/reboot/status) every 3s
├── ViewModels/
│   └── DashboardViewModel.cs           # Main VM: navigation, metrics, commands, status
└── Views/
    ├── MainWindow.xaml / .cs           # Shell: sidebar nav + ContentFrame + status bar
    ├── DashboardPage.xaml / .cs        # Dashboard: 7 metric cards (UniformGrid 3-col), logs
    ├── LogsPage.xaml / .cs             # Full-screen log viewer
    └── PlaceholderPage.xaml / .cs      # Generic placeholder for future pages
```

### Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                     MONITORING PATH                             │
│                                                                 │
│  monitor_query.sh (inside WSL, TSV output)                      │
│       ↓ ExecuteScript()                                         │
│  LlamaMonitorService.ReadStatus() [parse TSV, clamp 0-100]     │
│       ↓ ServiceStatus record                                    │
│  LlamaMonitorService.Poll() [add to history, fire events]       │
│       ↓ OnHistoryUpdated(cpu_list, mem_list, vram_list)         │
│  DashboardViewModel.UpdateHistory() [set display text]          │
│       ↓ UpdateMetricCardsFromData() [push to MetricCardData]    │
│  MetricCards ObservableCollection                              │
│       ↓ XAML binding                                            │
│  MetricCard control [gauge arc + text]                          │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      HEALTH PATH                                │
│                                                                 │
│  health_query.sh (inside WSL, TSV output)                       │
│       ↓ ExecuteScriptAsync()                                    │
│  ServerManagerService.HealthCheck() [parse TSV]                 │
│       ↓ SetState() + OnStateChanged()                           │
│       ↓ OnStatusUpdated()                                       │
│  DashboardViewModel.UpdateFromServerManager()                   │
│       → Updates: TPS, Queue, Uptime, Model, Context, GpuTemp    │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      WSL LIFECYCLE PATH                         │
│                                                                 │
│  WslService.CheckWslStatus() [wsl -l -q, wsl -l -r -q]         │
│       ↓ SetState() + OnStateChanged()                           │
│  DashboardViewModel.UpdateWslState()                            │
│       → Updates: WslStatusText, WslStatusColor                  │
└─────────────────────────────────────────────────────────────────┘
```

### Key Design Decisions
1. **Script-based monitoring**: Instead of 10+ separate `wsl.exe` calls per poll, scripts are deployed once to WSL `/tmp/` and executed as single bash scripts. Scripts output TSV (tab-separated values) for simple parsing.

2. **Script deployment**: Scripts are base64-encoded and piped into WSL to avoid ALL shell interpretation issues with quotes, variables, and special characters.

3. **Dual monitoring services**: `LlamaMonitorService` (2s interval) handles metrics; `ServerManagerService` (3s interval) handles health/uptime. Separate timers, separate scripts.

4. **CPU measured on Windows side**: Uses `PerformanceCounter` for system-wide Windows CPU instead of querying WSL process CPU. This avoids a WSL call but measures the wrong thing (Windows host, not llama-server).

5. **Observable Collections**: Metric data flows via `ObservableCollection<T>` for real-time UI updates.

6. **RelayCommand Pattern**: Simple `Action`-based ICommand implementation defined inline in DashboardViewModel.cs.

---

## 3. Scripts (Batched WSL Commands)

### monitor_query.sh — Deployed to `/tmp/llama_monitor.sh`
**Output**: TSV with 9 fields
```
reachable | pid | mem_used_kb | mem_total_kb | vram_used_mb | vram_total_mb | queue | prompt_tps | decode_tps
```

| Field | Source | Notes |
|-------|--------|-------|
| `reachable` | `nc -z localhost 8080` | 1 or 0 |
| `pid` | `pgrep` → `ss` → `lsof` fallback chain | May be empty |
| `mem_used_kb` | `/proc/meminfo` MemTotal-MemAvailable | **WSL-wide, NOT per-process** |
| `mem_total_kb` | `/proc/meminfo` MemTotal | Total system RAM |
| `vram_used_mb` | `nvidia-smi` summed across all GPUs | Multi-GPU safe |
| `vram_total_mb` | `nvidia-smi` summed across all GPUs | Multi-GPU safe |
| `queue` | `curl /v1/queue.info \| jq` | API-based |
| `prompt_tps` | `curl /metrics \| grep` | API-based |
| `decode_tps` | `curl /metrics \| grep` | API-based |

### health_query.sh — Deployed to `/tmp/llama_health.sh`
**Output**: TSV with 5 fields
```
state | pid | queue | tps | etime
```

| Field | Source | Notes |
|-------|--------|-------|
| `state` | PID existence check | "RUNNING" or "STOPPED" |
| `pid` | Same fallback chain as monitor | — |
| `queue` | `curl /v1/queue.info` | — |
| `tps` | `curl /metrics` avg_decode_tps | — |
| `etime` | `ps -p PID -o etime=` | Format: `MM:SS`, `HH:MM:SS`, or `D-HH:MM:SS` |

---

## 4. Bugs Fixed In-Code

### Percentage clamping (FIXED)
All three gauge metrics (CPU, RAM, VRAM) are clamped to `[0, 100]`:
- `LlamaMonitorService.cs` line 97: CPU `Math.Clamp(val, 0.0, 100.0)`
- `LlamaMonitorService.cs` line 234: RAM `Math.Clamp(memPercent, 0.0, 100.0)`
- `LlamaMonitorService.cs` line 244: VRAM `Math.Clamp(vramPercent, 0.0, 100.0)`
- `MetricCard.cs` line 176: UI `Math.Clamp(Percentage / 100.0, 0.0, 1.0)`

### MetricCard value alignment (FIXED)
- Gauge row uses a two-column Grid (Star/Star)
- Left column: 80x80 Canvas with semicircle gauge, centered
- Right column: TextBlock with `HorizontalAlignment=Stretch` and `TextAlignment=Center`
- Arc math: 180-degree semicircle, start at left (180°), sweep counterclockwise

### RAM/VRAM percentage calculation (FIXED from handoff era)
The old handoff described raw KB/MB values being displayed as percentages. The current code correctly divides used/total before displaying.

### Script deployment via base64 (FIXED from handoff era)
The old handoff described issues with shell variable expansion when passing commands via `bash -c "$cmd"`. The current approach base64-encodes the entire script and decodes it inside WSL, avoiding all interpretation issues.

---

## 5. Build Instructions

### Prerequisites
- .NET 8.0 SDK
- Windows 10/11 (WPF requires Windows at runtime)
- `EnableWindowsTargeting=true` allows building from Linux/WSL

### Build Command
```bash
cd "NymphsCore/ManagerFEUI"
dotnet build -c Release
dotnet publish -c Release -o ./publish
```

### Run Command (from Windows)
```bash
dotnet run --project "NymphsCore/ManagerFEUI/ManagerFEUI.csproj"
```

### Deployed Files
When built, `monitor_query.sh` and `health_query.sh` are copied to the output directory (via `CopyToOutputDirectory=PreserveNewest` in the .csproj).

---

## 6. Current State of Each Page

### ✅ DashboardPage (IMPLEMENTED)
- System Overview section with 7 metric cards in 3-column UniformGrid
- Cards: CPU Usage, WSL Memory, VRAM Usage, API Latency, Token Gen, Queue Depth, Uptime
- Gauge arcs for CPU/RAM/VRAM with proper percentage display (clamped 0-100%)
- Recent Activity log section at bottom (LiveLogView control)
- **BUG**: DataContext inherited from MainWindow, not explicitly set

### ✅ LogsPage (IMPLEMENTED)
- Full-screen log viewer with LiveLogView control

### ⬜ Runtime Tools (PLACEHOLDER)
- Shows "Runtime Tools — Coming Soon"

### ⬜ Brain (PLACEHOLDER)
- Shows "Brain — Coming Soon"

### ⬜ Addons (PLACEHOLDER)
- Shows "Addons — Coming Soon"

### ⬜ Installer (PLACEHOLDER)
- Shows "Installer — Coming Soon"

---

## 7. Theme / Visual Design

### Color Palette (from App.xaml)
| Token | Hex | Usage |
|-------|-----|-------|
| `BgDeep` | `#0a0f0d` | Main background |
| `BgCard` | `#121a16` | Card background |
| `BgCardHover` | `#182220` | Card hover state |
| `BgSidebar` | `#0d1512` | Sidebar background |
| `BorderBrush` | `#1e2e26` | All borders |
| `GreenAccent` | `#2dd4a8` | Primary accent, connected state |
| `GreenAccentDim` | `#1a8a6a` | Dim accent |
| `RedAlert` | `#ff4466` | Error state |
| `AmberWarn` | `#ffaa33` | Warning state |
| `TextPrimary` | `#e8f0ec` | Primary text |
| `TextSecondary` | `#7a9488` | Secondary text |
| `TextDim` | `#4a6658` | Dim text |

### Fonts
- Header: `Inter, Segoe UI, sans-serif`
- Body: `Inter, Segoe UI, sans-serif`
- Mono: `JetBrains Mono, Consolas, monospace`

### Styles Defined
- `DashboardCard` — Card border with hover effect
- `SectionHeader` — 13px semibold text
- `MetricValue` — 32px bold text
- `LogText` — 11px monospace
- `NavButton` — Transparent sidebar nav button with hover/press states
- `PrimaryButton` — Green accent action button
- `DarkScrollViewer` / `DarkScrollBar` — Custom scrollbar styling

---

## 8. Next Agent Checklist

### Priority 1: Quick Fixes (BLOCKING)
- [ ] **Fix `ViewLogsCommand` → `ShowLogsCommand`** in MainWindow.xaml line 51
- [ ] **Bind status bar** to `StatusText`/`StatusColor` in MainWindow.xaml lines 76-77
- [ ] **Remove dead code**: Either use `ViewModelBase` or delete it

### Priority 2: Design Decisions
- [ ] **Clarify RAM metric**: Should it be WSL-wide (current) or llama-server process only?
- [ ] **Clarify CPU metric**: Should it be Windows system (current) or WSL llama-server process?
- [ ] **Decide on SparklineView**: Currently a placeholder — implement or remove?

### Priority 3: Feature Gaps
- [ ] **Populate ServerManagerService properties**: Model, ContextSize, GpuTemp, GpuVram are never set
- [ ] **Extend health_query.sh**: Add model info queries (e.g., curl /v1/models)
- [ ] **Set DataContext explicitly** on DashboardPage for robustness

### Priority 4: Implement Remaining Pages
- [ ] Runtime Tools page
- [ ] Brain page
- [ ] Addons page
- [ ] Installer page (migrate from existing NymphsCoreManager)

### Priority 5: Debugging
- [ ] Check `/tmp/llama_monitor_debug.log` on a running system to verify script outputs
- [ ] Verify script deployment succeeds (check WSL `/tmp/llama_monitor.sh` exists after first app launch)
- [ ] Test with llama-server not running (verify graceful degradation)

---

## 9. Key Files to Inspect First

| File | Why |
|------|-----|
| `Views/MainWindow.xaml:51` | `ViewLogsCommand` typo — binding silently fails |
| `Views/MainWindow.xaml:76-77` | Hardcoded status bar — never reflects real state |
| `Services/LlamaMonitorService.cs` | Core metric parsing, percentage clamping, debug logging |
| `ViewModels/DashboardViewModel.cs` | Data transformation, navigation, command wiring |
| `monitor_query.sh` | The batched WSL monitoring script — verify TSV output format |
| `health_query.sh` | The batched WSL health script — verify etime parsing |
| `Controls/MetricCard.cs` | Custom gauge rendering, arc geometry math |
| `Models/MetricCardData.cs` | Observable model that binds to MetricCard DP's |

---

## 10. Agent Audit Notes (2026-04-29)

This audit was performed by reading all 18 source files in the project. Key observations:

- The codebase has matured significantly from the handoff-era. The script-based approach is well-designed and avoids the shell escaping issues that plagued the old architecture.
- The percentage display bugs described in the original handoff (RAM=9577.9%, VRAM=30480.0%) have been fixed in code. The clamping logic is correct and present at multiple layers (service, ViewModel, control).
- The new issues found are mostly XAML binding bugs (wrong command name, hardcoded status) that are trivial to fix but have a noticeable impact on UX.
- The CPU/RAM measurement scope changes (Windows system CPU, WSL-wide RAM) may be intentional design choices rather than bugs — a decision needs to be made explicitly.
- Debug logging to `/tmp/llama_monitor_debug.log` is an excellent feature for troubleshooting and should be leveraged before making further changes.