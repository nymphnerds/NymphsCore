# Manager And Brain Handoff

Date: 2026-04-21
Branch: `improve/manager-installer-reliability`

## Scope

This handoff summarizes analysis of:

- the `NymphsCore Manager` installer and launcher UI
- the optional `Nymphs-Brain` stack
- the relationship between core runtime tools and Brain-specific tools

This document intentionally excludes user-specific local model inventory and other machine-specific details that are not useful for product-level planning.

## Main Conclusion

The stack is viable, but the biggest issues are separation of concerns, supportability, and long-running workflow UX.

The clearest product/UI conclusion is:

- `Nymphs-Brain` should become its own first-class page in the Manager
- `Runtime Tools` should focus only on the core backend stack
- Brain-specific controls, logs, and terminals should move off the shared Runtime Tools page

## Current Structure

Relevant app files:

- `apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs`
- `apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`
- `apps/Nymphs3DInstaller/Services/ProcessRunner.cs`
- `apps/Nymphs3DInstaller/Views/MainWindow.xaml`

Relevant scripts:

- `scripts/run_finalize_in_distro.ps1`
- `scripts/finalize_imported_distro.sh`
- `scripts/runtime_tools_status.sh`
- `scripts/install_nymphs_brain.sh`

Current architecture:

- WPF Manager app on Windows
- PowerShell orchestration into `wsl.exe`
- shell scripts running inside the managed distro
- Runtime Tools page covering backend status / repair / smoke tests
- Brain tools currently appended onto Runtime Tools rather than treated as their own subsystem

## Architectural Findings

### 1. The UI and workflow logic are too centralized

The Manager is heavily concentrated into a few large files:

- `MainWindowViewModel.cs`
- `InstallerWorkflowService.cs`
- `MainWindow.xaml`

This increases risk when adding new branches or changing state transitions. Product copy, install logic, runtime actions, and Brain-specific behavior are all mixed together.

Suggested direction:

- split by feature area
- separate install flow, runtime tools, and Brain tools
- reduce the amount of state branching in one view model

### 2. Progress handling for long-running tasks is weak

Current subprocess handling is line-buffered. Many long-running tools use carriage-return progress or sparse output, which makes the Manager look frozen even when it is actively working.

This affects:

- large downloads
- model fetches
- package installs
- some LM Studio and shell-driven actions

Suggested direction:

- support raw-stream progress handling
- treat `\r` progress as visible updates
- add explicit machine-readable progress lines from scripts where practical

### 3. Long-running actions are not meaningfully cancelable

Many expensive operations run with no real cancellation flow from the UI.

This creates a bad experience when:

- installs hang
- WSL commands stall
- network downloads stall
- model fetches take much longer than expected

Suggested direction:

- introduce a shared cancellation source
- add a visible `Cancel` action for install/repair/prefetch/Brain tasks
- define cleanup behavior for interrupted subprocesses

### 4. Brain installation is too dependent on live upstream state

The Brain installer currently installs several dependencies from the network at latest available versions.

That is convenient for experimentation, but weak for a supportable installer.

Suggested direction:

- pin Python dependencies
- pin npm package versions
- pin or version the LM Studio install path/strategy
- separate `stable` from `experimental` behavior explicitly

### 5. Release and path resolution still contain dev-oriented behavior

The Manager still has fallback behavior and assumptions that are helpful during development but risky in a release path.

Suggested direction:

- remove hidden local/dev payload fallbacks from release logic
- keep release behavior deterministic
- make packaging assumptions visible and testable

### 6. Docs and release instructions have drifted

There are already signs that documentation and script behavior are not fully aligned.

Suggested direction:

- align README, release instructions, and actual script parameters
- remove stale paths and stale doc references
- make support instructions match the current product flow exactly

## Product And UX Findings

### 1. Runtime Tools is overloaded

The current Runtime Tools page mixes:

- core backend status
- fetch actions
- smoke tests
- HF token input
- logs
- Brain status
- Brain actions

This makes the page harder to understand and makes Brain feel bolted on.

### 2. Brain is a separate subsystem and should be treated that way

Brain has:

- different runtime concerns
- different logs
- different start/stop behavior
- different user goals
- different mental model than the core 3D/image backends

Because of that, it should not live as a subsection of Runtime Tools.

## Recommended UI Direction

### Sidebar

Recommended top-level sections:

- `Install`
- `Runtime Tools`
- `Brain`
- `README`
- `Footprint`
- `Addon Guide`
- `Show Logs`

### Runtime Tools

Keep this page focused on:

- `Hunyuan 2mv`
- `Z-Image`
- `TRELLIS.2`
- fetch missing models
- run smoke tests
- inspect backend readiness

### Brain Page

Create a dedicated `Brain` page with:

- install/status summary
- current model summary
- context information if available
- `Start LLM`
- `Stop LLM`
- `Open WebUI`
- `Manage Models`
- `Refresh`
- `Open Brain Logs`
- `Open Brain Terminal`
- `Open Model Manager Terminal`
- `Open Brain Shell`

Recommended information cards:

- `LLM Server`
- `Open WebUI`
- `MCP Gateway`

Recommended lower section:

- `Brain Activity`
- either a Brain-specific log panel or last-output panel

## Terminal Recommendation

A true embedded interactive terminal inside the Brain page is technically possible, but it should not be a phase-1 change.

Reasons:

- terminal emulation in WPF is non-trivial
- stdin, resize, scrollback, copy/paste, colors, and WSL interop all become a separate subsystem
- it adds a lot of fragility relative to the value of the first release

Recommended phase-1 solution:

- keep terminal interaction external
- launch focused Windows Terminal windows or tabs from the Brain page
- use dedicated buttons for Brain shell / model manager / logs

This gives most of the value with much lower implementation risk.

## Repo Ownership And Naming Note

Separate from the Manager/Brain UI conclusions, the repo-owner migration was completed and smoke-tested on 2026-04-21.

Current state:

- active managed repos now live under `nymphnerds`
- Manager defaults and bundled publish scripts point at `nymphnerds` instead of `Babyjawz`
- `Z-Image` remains the user-facing label
- `Nymphs2D2` remains the repo/backend name so the 2D backend can keep growing beyond the current `Z-Image` runtime
- the helper checkout now points at `nymphnerds/NymphsCore`

Validated by repair logs:

- `installer-run-20260421-101315.log` exposed the old remote mismatches
- `installer-run-20260421-103334.log` confirmed backend repo URL fixes but showed helper checkout permission issues
- `installer-run-20260421-104532.log` confirmed:
  - no active `Babyjawz` pulls
  - `Hunyuan3D-2` up to date
  - `Z-Image backend` up to date
  - install completed successfully

Remaining caution:

- `/opt/nymphs3d/Nymphs3D` is still the live helper checkout path and still carries legacy `Nymphs3D` naming in paths and env vars
- that naming cleanup should stay a later pass; it is no longer blocking the owner migration

## Recommended Next Phase

Priority order:

1. Add first-class `Act` / `Plan` model visibility and controls to the Brain page
2. Improve progress handling for long-running tasks
3. Add cancellation support
4. Pin Brain installer dependencies
5. Remove release-time dev fallbacks
6. Clean doc/release drift
7. Decide whether to do a broader `Nymphs3D` -> `NymphsCore` naming cleanup now that repo-owner migration is stable

## Current Brain UI State

As of the `brain-activity` branch work:

- `Nymphs-Brain` now has its own dedicated left-sidebar page instead of living under `Runtime Tools`
- the Brain page has:
  - status cards for `LLM Server`, `MCP Gateway`, `Open WebUI`, and `Current Model`
  - Brain-specific activity log panel
  - stack control buttons for starting/stopping Brain services, WebUI control, model management, and stack updates
- the left sidebar artwork swaps to `NymphBrain.png` on the Brain page

## Current Brain Stack Behavior

The Brain stack is no longer limited to a single hardcoded model selection.

It now supports:

- an `Act` model profile
- an optional `Plan` model profile
- loading either one model or both models from the Linux-side Brain stack

Current behavior:

- `Act` is the primary/default role
- `Plan` is optional; if it is blank, only `Act` loads
- `lms-start` reads the saved model-role config and loads `Act`, then `Plan` if configured
- `lms-model` is now role-aware and can:
  - set `Act` from a downloaded model
  - set `Plan` from a downloaded model
  - download a new model for `Act`
  - download a new model for `Plan`
  - clear `Act`
  - clear `Plan`

Important implementation note:

- `Manage Models` is now a configuration surface, not an immediate unload/reload surface
- selecting a model role saves the profile and asks the user to restart the Brain LLM to apply the new Plan/Act model set

Useful live commands:

- `~/Nymphs-Brain/bin/lms-get-profile`
- `~/Nymphs-Brain/bin/lms-set-profile act MODEL_KEY [CONTEXT]`
- `~/Nymphs-Brain/bin/lms-set-profile plan MODEL_KEY [CONTEXT]`
- `~/Nymphs-Brain/bin/lms-set-profile act clear`
- `~/Nymphs-Brain/bin/lms-set-profile plan clear`
- `~/Nymphs-Brain/bin/brain-status`

## Testing / Risk Notes

The current Manager appears to rely heavily on manual and smoke-test validation rather than automated app-level tests.

That means:

- UI/state refactors should be done incrementally
- install/repair/Brain flows should be retested after each structural change
- progress/cancellation changes should be tested against real long-running tasks

## Bottom Line

The stack is promising and the product direction is coherent.

The most important structural product change is:

- `Nymphs-Brain` should become a first-class page with its own controls and terminal affordances

The most important technical reliability changes are:

- better progress reporting
- cancellation support
- less live-upstream dependency drift
- clearer separation between release behavior and dev conveniences
