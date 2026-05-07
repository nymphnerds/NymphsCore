# Rauty Module Lifecycle Handoff

Date: 2026-05-07
Updated: 2026-05-07 19:05 BST

Branch: `rauty`

## UX Decision: Available Cards Open Info First

Available module cards should not immediately ask to install.

Correct flow:

```text
Available card click -> module detail page -> manifest-backed info -> Install Module button
```

The module detail page should pull public pre-install information from the registry manifest:

```text
nymphs-registry -> module manifest_url -> module repo nymph.json
```

Minimum useful pre-install info:

- name
- description
- category
- packaging kind
- remote version
- source/archive/repo summary
- install path target where known
- available manager contract actions

This has been wired in source for the generic module page. Keep it simple: card opens the page; right rail owns install/update/uninstall/delete actions.

## Critical 2026-05-07 Follow-Up: Keep Lifecycle Launching Simple

The Manager must not build large generated `bash -lc` lifecycle strings for install/uninstall/delete.

The bad pattern caused user-facing errors such as:

```text
/bin/bash: -c: line 1: conditional binary operator expected
/bin/bash: -c: line 1: syntax error near `0'
```

This happened before the module helper could run correctly.

Source has been simplified so install/uninstall helpers are launched as separate WSL processes:

```text
rm -f /tmp/nymphs-manager-<action>-<module>.sh
curl -fsSL <helper-url> -o /tmp/nymphs-manager-<action>-<module>.sh
chmod +x /tmp/nymphs-manager-<action>-<module>.sh
/bin/bash /tmp/nymphs-manager-<action>-<module>.sh --module <module> ...
rm -f /tmp/nymphs-manager-<action>-<module>.sh
```

No `awk`, no inline `$?`, no conditional cleanup logic in one giant shell string.

Next session:

- rebuild Manager before testing this fix
- retest WORBI install/uninstall/delete only
- do not test destructive lifecycle actions on TRELLIS, Z-Image, LoRA, or Brain
- if a helper succeeds but UI state lags, fix state refresh separately; do not add shell complexity back into the launcher

## Critical 2026-05-07 Install Success Reporting Fix

WORBI exposed a bad success boundary in the registry install path.

Observed user-facing failure:

```text
Installed worbi.
WORBI install warning: Module registry install failed for 'worbi' with exit code 1.
```

The module install had completed and printed:

```text
WORBI installed successfully.
installed_module_version=6.2.55
Installed worbi.
```

but the Manager still surfaced the action as failed.

The `rauty` helper:

```text
Manager/scripts/install_nymph_module_from_registry.sh
```

must be treated as the contract boundary. It now:

- runs the module manifest `install` entrypoint with `set +e`
- captures the entrypoint exit code immediately
- prints `ERROR: install entrypoint failed...` and exits nonzero only when that entrypoint fails
- prints `Installed <module>.` only after a clean entrypoint exit
- explicitly `exit 0` after success

This helper must be pushed before testing from the Manager UI, because the current Manager build downloads the helper from:

```text
https://raw.githubusercontent.com/nymphnerds/NymphsCore/rauty/Manager/scripts/install_nymph_module_from_registry.sh
```

Future module repos should also print their final success/version marker only after all wrapper scripts and runtime files are installed.

## Critical 2026-05-07 Incident: TRELLIS Was Deleted By Module Purge Routing

Do not skip this in the next session.

During WORBI lifecycle testing, the managed `NymphsCore` distro lost:

```text
/home/nymph/TRELLIS.2
```

The user did **not** intend to delete TRELLIS. They were working on WORBI.

The confirmed destructive path was:

```text
Manager UI -> Delete Module + Data -> uninstall_nymph_module.sh --module trellis --yes --purge
```

The generic helper then mapped:

```text
trellis -> /home/nymph/TRELLIS.2
```

and removed the folder.

The likely UI culprit is that the module page/right-rail actions were bound to the mutable live `SelectedModule`. Refresh/rebuild operations can replace that module reference while a module page is open or while state is being rebuilt. That makes it possible for a destructive right-side action to target a different module than the user believes they are operating on.

### Emergency Guards Added In Source

The Manager source now has a safer module-page target:

```text
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
```

Key changes:

- added stable `DisplayedModule`
- module pages set `DisplayedModule` when opened
- manager contract actions use `DisplayedModule`, not live `SelectedModule`
- right rail action buttons bind to `DisplayedModule`
- `Delete Module + Data` is hidden unless the displayed module is `worbi`
- purge is blocked in ViewModel for all non-WORBI modules
- destructive actions now append an audit line with requested/displayed/selected module IDs

The XAML page now binds module-page content and action command parameters to:

```text
DisplayedModule
```

instead of:

```text
SelectedModule
```

Important: these source edits need a fresh Manager rebuild before the running app is trusted.

### Emergency Guard Added In Remote Helper

`Manager/scripts/uninstall_nymph_module.sh` on `rauty` now blocks destructive purge for non-WORBI modules:

```bash
if [[ "${PURGE}" -eq 1 && "${MODULE_ID}" != "worbi" ]]; then
  echo "ERROR: destructive purge is temporarily disabled for ${MODULE_ID} while module routing is being audited." >&2
  echo "No files were deleted."
  exit 4
fi
```

This has already been pushed to `origin/rauty`, so even older Manager builds that fetch the current remote helper should not be able to purge `trellis`, `zimage`, `lora`, or `brain`.

### Current Safety Rule

Until the rebuilt Manager is verified:

- do **not** use `Delete Module + Data` on anything except WORBI
- avoid destructive lifecycle testing late in a session
- prefer module-owned uninstall scripts where available
- use `Uninstall Module` only when the module has a confirmed backup/preserve path
- verify the target module ID in logs before accepting any destructive action

## TRELLIS Recovery Status After Incident

TRELLIS was copied back into the real managed distro from the dev WSL copy.

Source/dev copy:

```text
\\wsl.localhost\NymphsCore_Lite\home\nymph\TRELLIS.2
```

Managed/runtime target:

```text
\\wsl.localhost\NymphsCore\home\nymph\TRELLIS.2
```

Robocopy transferred roughly:

```text
8.004 GB
7506 dirs copied
64467 files copied
4 files failed
```

The copy damaged the venv Python symlinks by turning them into empty 0-byte files:

```text
/home/nymph/TRELLIS.2/.venv/bin/python
/home/nymph/TRELLIS.2/.venv/bin/python3
/home/nymph/TRELLIS.2/.venv/bin/python3.10
```

These were repaired in the managed distro with:

```powershell
wsl.exe -d NymphsCore --user nymph -- bash -lc 'cd /home/nymph/TRELLIS.2/.venv/bin && rm -f python python3 python3.10 && ln -s /usr/bin/python3.10 python && ln -s /usr/bin/python3.10 python3 && ln -s /usr/bin/python3.10 python3.10 && ./python -V'
```

Verified result:

```text
Python 3.10.20
```

Core TRELLIS imports were verified inside the real managed `NymphsCore` distro:

```text
trellis2_gguf ok
gguf ok
rembg ok
open3d ok
pymeshlab ok
meshlib ok
```

Next session should still verify adapter files:

```powershell
wsl.exe -d NymphsCore --user nymph -- bash -lc 'test -f /home/nymph/TRELLIS.2/scripts/api_server_trellis_gguf.py && test -f /home/nymph/TRELLIS.2/scripts/trellis_gguf_common.py && echo trellis_adapter_ok'
```

If that prints `trellis_adapter_ok`, TRELLIS is likely restored enough for runtime testing.

Do not reinstall TRELLIS from scratch unless adapter/runtime verification fails; the copied venv imports are currently alive.

## Immediate Next Session Checklist

1. Do not click destructive Manager buttons in the old running app.
2. Verify TRELLIS adapter files in `NymphsCore`.
3. Rebuild Manager from the `rauty` source after the `DisplayedModule` safety patch.
4. Launch the rebuilt Manager from the publish folder.
5. Confirm `Delete Module + Data` is hidden for TRELLIS, Z-Image, LoRA, and Brain.
6. Confirm `Delete Module + Data` is visible only for WORBI.
7. Confirm the Manager contract actions operate on the page's displayed module.
8. Use WORBI only for delete/uninstall/install lifecycle testing until another module has its own safe contract.
9. Update this handoff before any further destructive lifecycle work.

## Critical WSL Distinction

Read this before doing any module lifecycle testing.

There are two different WSL contexts involved:

```text
NymphsCore_Lite = development/source checkout used by Codex and the IDE
NymphsCore      = actual managed runtime distro used by NymphsCore Manager
```

The source tree currently being edited lives here:

```text
\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore
```

That is only the development checkout.

The Manager runtime target is the canonical managed distro:

```text
NymphsCore
```

The Manager source confirms this:

```text
InstallerWorkflowService.ManagedDistroName = "NymphsCore"
InstallSettings.DistroName defaults to "NymphsCore"
Module actions run through: wsl.exe -d NymphsCore --user nymph -- ...
```

Do not validate module installs, starts, stops, status, logs, or updates only inside `NymphsCore_Lite`.

For lifecycle tests, always target `NymphsCore` explicitly:

```powershell
wsl.exe -d NymphsCore --user nymph -- bash -lc "/home/nymph/.local/bin/worbi-status"
```

or from Linux shell:

```bash
wsl.exe -d NymphsCore --user nymph -- bash -lc '/home/nymph/.local/bin/worbi-status'
```

The recent WORBI confusion happened because WORBI was manually updated first in the dev WSL context, while the Manager was still correctly reading the older install inside the real `NymphsCore` distro.

## 2026-05-07 Late Fix: Update Loops And Stop Fallbacks

The WORBI update flow exposed two important generic module rules:

1. The installed version must come from the real installed module marker first.
2. Stop/status scripts must tolerate unmanaged or previously-started processes.

For WORBI, the marker is:

```text
/home/nymph/worbi/.nymph-module-version
```

Manager update checks now read that marker before cached manifests such as:

```text
/home/nymph/.cache/nymphs-modules/worbi.nymph.json
/home/nymph/.cache/nymphs-modules/repos/worbi/nymph.json
```

This matters because a successful install can still look stale if the UI compares against the wrong cached file. The user-facing symptom was:

```text
WORBI installed successfully.
Installed worbi.
```

while the page still showed:

```text
Installed: 6.2.52
Remote: 6.2.53
Update available
```

WORBI `6.2.54` also makes the module installer print:

```text
installed_module_version=6.2.54
```

Future module installers should do the same kind of explicit version-stamp output.

For stop scripts, do not rely only on PID files. The script should also:

- inspect process working directories under the install root
- inspect command lines for module-specific server commands
- inspect the module port when the app is responding
- use a bounded port/process fallback before reporting failure

This is especially important after dev testing, Manager restarts, manual terminal launches, or updates from older wrappers.

## What This Handoff Covers

This captures the current Rauty manager/module work after the first real WORBI registry lifecycle test.

Custom module pages are intentionally parked for later. The current focus is the foundation:

- discover modules from `nymphs-registry`
- show installed vs available modules
- install available modules from their own repos
- uninstall modules while preserving user data
- delete/purge modules when explicitly requested
- update an existing module by rerunning the registry install flow

## Current Module Repo Shape

Module repos live under:

```text
github.com/nymphnerds/
```

Current registry modules:

```text
worbi
zimage
trellis
brain
lora
```

Registry:

```text
https://github.com/nymphnerds/nymphs-registry
```

WORBI:

```text
https://github.com/nymphnerds/worbi
```

## Manager Work Added On Rauty

The WPF manager now has a first-pass module lifecycle path:

- Available module cards are clickable install cards.
- Installed module cards still open module detail pages.
- The available install path runs:

```text
nymphs-registry -> module nymph.json -> trusted module repo -> install entrypoint
```

New manager scripts:

```text
Manager/scripts/install_nymph_module_from_registry.sh
Manager/scripts/uninstall_nymph_module.sh
```

Important behavior:

- `install_nymph_module_from_registry.sh` only accepts trusted `nymphnerds` raw GitHub manifest URLs.
- It clones/updates module repos into `~/.cache/nymphs-modules/repos/<module-id>` inside the managed WSL distro.
- It reads `entrypoints.install` from the module manifest and runs it.
- `uninstall_nymph_module.sh` supports `--dry-run`, `--yes`, and `--purge`.
- Default uninstall preserves known module data into `~/NymphsModuleBackups`.

The manager also got:

- `AI Toolkit` renamed to `LoRA` / `lora` in the new shell.
- LoRA local install detection against `~/ZImage-Trainer`.
- module page actions for uninstall and delete/purge.
- window resize down to one-card compact width.

## WORBI Test WSL State

The test distro is:

```text
NymphsCore
```

WORBI has been tested in that distro at:

```text
/home/nymph/worbi
```

Current verified managed-distro version before the successful 6.2.52 update:

```text
WORBI 6.2.51
```

Verified in `NymphsCore`:

```bash
wsl.exe -d NymphsCore --user nymph -- bash -lc 'grep "\"version\"" /home/nymph/.cache/nymphs-modules/worbi.nymph.json'
```

Expected:

```text
"version": "6.2.51"
```

Before the latest wrapper hardening, WORBI was installed at `6.2.50` in the real `NymphsCore` distro and could not start because the packaged `worbi-start` tried to run:

```text
/home/nymph/worbi/src/index.js
```

The real server entrypoint is:

```text
/home/nymph/worbi/server/src/index.js
```

That stale wrapper was replaced in the real `NymphsCore` distro after pulling the WORBI module repo and rerunning its installer there.

Important: the current wrapper no longer has the missing `src/index.js` path bug. It starts from:

```text
cd /home/nymph/worbi/server
node src/index.js
```

## WORBI 6.2.52 Lifecycle Wrapper Fix

The stale path bug is fixed, and the next WORBI-side wrapper hardening has now been pushed to `nymphnerds/worbi`.

Pushed module commit:

```text
f493d8f Fix WORBI manager lifecycle wrappers
```

This bumps the module manifest to:

```text
6.2.52
```

What changed:

- `worbi-start` now uses a detach strategy intended to survive `wsl.exe` returning.
- `worbi-start` writes/repairs `~/worbi/logs/worbi-server.pid`.
- `worbi-stop` now handles stale/missing PID files and looks for the matching WORBI server process before reporting stopped.
- `worbi-status` reports `running-unmanaged` / `responding` instead of falsely reporting stopped when the health endpoint is alive.
- `install_worbi.sh` writes `~/worbi/.nymph-module-version` so Manager update checks compare the Nymph module wrapper release, not only the bundled app package version.

Important distinction:

- WORBI app archive can remain `packages/worbi-6.2.51.tar.gz` for this wrapper-only release.
- The Nymph module release is `6.2.52`.
- Rebuild the tarball only when WORBI app files change.

Expected Manager flow after this push:

```text
Check for Updates -> WORBI 6.2.51 installed / 6.2.52 remote -> Update Module -> rerun WORBI install script -> wrapper scripts refreshed -> installed module marker becomes 6.2.52
```

This flow has now been exercised through the Manager UI. The installer completed successfully and printed:

```text
Node.js found: v18.20.8
Archive: /tmp/.../worbi-6.2.51.tar.gz
Install: /home/nymph/worbi
Existing installation found. Preserving user data...
Installing server dependencies...
WORBI installed successfully.
App: http://localhost:8082
Logs: /home/nymph/worbi/logs/
Installed worbi.
```

The page still showed `WORBI: update needs attention` after that successful output because the Manager bundled the install step and follow-up refresh/check step into one `try` block. That has been patched so installer success displays as:

```text
WORBI: update finished
```

Any later refresh/update-check error should now appear as a smaller state refresh warning instead of making the whole update look failed.

If testing outside the UI, run the update inside the actual managed distro, not the dev distro:

```bash
wsl.exe -d NymphsCore --user nymph -- bash -lc 'set -euo pipefail; git -C /home/nymph/.cache/nymphs-modules/repos/worbi pull --ff-only; cp /home/nymph/.cache/nymphs-modules/repos/worbi/nymph.json /home/nymph/.cache/nymphs-modules/worbi.nymph.json; bash /home/nymph/.cache/nymphs-modules/repos/worbi/scripts/install_worbi.sh; /home/nymph/.local/bin/worbi-status'
```

## WORBI Repo Fixes Pushed / Relevant

Relevant WORBI commits include:

```text
f493d8f Fix WORBI manager lifecycle wrappers
8c6957f latest main at time of managed-distro pull
d2751f1 Make WORBI manager wrappers authoritative
6b20a2b Read package archive from manifest
7c31e56 Release WORBI 6.2.50 - Fix profile selector loop on refresh
```

Why they matter:

- `7c31e56` bumped WORBI to `6.2.50`.
- `6b20a2b` fixed the installer so it reads `source.archive` from `nymph.json` instead of hardcoding `worbi-6.2.49.tar.gz`.
- `d2751f1` makes `worbi-start`, `worbi-stop`, `worbi-status`, `worbi-open`, and `worbi-logs` come from the module repo after install, so the Manager contract stays stable even if app package internals move.
- Later WORBI main includes `6.2.51`, and that version has been installed into the real managed `NymphsCore` distro.
- `f493d8f` is the first wrapper-only module update: it bumps `nymph.json.version` to `6.2.52` and lets Manager pull lifecycle-script fixes without a Manager rebuild.

## Managed Distro Update Command Used

Because the dev checkout is in `NymphsCore_Lite` and the real runtime is `NymphsCore`, this direct managed-distro update was used:

```bash
wsl.exe -d NymphsCore --user nymph -- bash -lc 'set -euo pipefail; git -C /home/nymph/.cache/nymphs-modules/repos/worbi pull --ff-only; cp /home/nymph/.cache/nymphs-modules/repos/worbi/nymph.json /home/nymph/.cache/nymphs-modules/worbi.nymph.json; bash /home/nymph/.cache/nymphs-modules/repos/worbi/scripts/install_worbi.sh; grep "\"version\"" /home/nymph/.cache/nymphs-modules/worbi.nymph.json'
```

Output confirmed:

```text
Archive: .../worbi-6.2.51.tar.gz
Existing installation found. Preserving user data...
WORBI installed successfully.
"version": "6.2.51"
```

This is the command pattern to use when validating a module in the actual Manager target distro.

## Manager Script Launcher Lessons From WORBI Update

The WORBI update exposed an important Manager-side contract issue.

Bad pattern:

```text
wsl.exe -d NymphsCore -- bash -lc "<large generated shell command containing nested heredocs>"
```

Why it failed:

- `install_nymph_module_from_registry.sh` contains Python heredocs.
- Injecting that whole script into another heredoc corrupted the shell text.
- The visible failures were misleading, for example:

```text
read_manifest_url: command not found
SyntaxError: unexpected character after line continuation character
/bin/bash: line 1: : No such file or directory
```

Better Rauty pattern:

```text
wsl.exe -d NymphsCore --user nymph -- /bin/bash -lc "
  curl -fsSL <manager-helper-script-url> -o /tmp/nymphs-manager-install-worbi.sh
  chmod +x /tmp/nymphs-manager-install-worbi.sh
  /bin/bash /tmp/nymphs-manager-install-worbi.sh --module worbi
"
```

Implementation notes:

- keep the `wsl.exe ... bash -lc` command small
- stage helper scripts inside the managed distro
- use literal temp paths for critical script staging
- print `fetching_install_script=...`, `staged_install_script=...`, and `staged_install_bytes=...` before running the helper
- treat module install/update success separately from follow-up refresh/check warnings

Current Rauty caveat:

```text
RautyManagerScriptsBaseUrl = https://raw.githubusercontent.com/nymphnerds/NymphsCore/rauty/Manager/scripts
```

Before merging Rauty to `main`, decide whether this should:

- switch to `main`
- follow the Manager app's release channel
- become configurable in settings
- copy scripts into the managed distro during Manager setup and run those local copies

## Last Verified Manager Build

Last known debug build from an earlier point passed:

```text
dotnet build NymphsCoreManager.csproj -c Debug
0 warnings
0 errors
```

Since then, additional Manager source edits were made. Rebuild again before treating the current UI behavior as final.

## Manager Changes In Progress After Latest Test

The Manager source has been adjusted on `rauty` to make module lifecycle clearer:

- `// LIVE DETAIL` now takes the main wide space on module pages.
- `// MODULE FACTS` moved to the right rail under actions.
- `Update Module` appears only when a registry check finds a newer remote version.
- Install/update flows stream installer progress into `// LIVE DETAIL`.
- Registry install failures now include exit code and captured output instead of a generic failure message.
- Installed module version should be read from cached installed manifest rather than saying `Manifest not wired yet`.
- `// start` attempts to open a browser from the URL returned by the module output.

These source edits still need a normal Windows build/rebuild before the running app reflects all of them.

## Next Test To Run

After rebuilding the Manager, test against the real `NymphsCore` distro only.

First verify version:

```bash
wsl.exe -d NymphsCore --user nymph -- bash -lc '/home/nymph/.local/bin/worbi-status'
```

Expected version after the successful 6.2.52 update:

```text
version=6.2.52
```

Then test Manager page:

```text
// status
// start
// open
// logs
// stop
```

Expected eventual healthy status:

```text
installed=true
version=6.2.52
running=true
health=ok
url=http://localhost:8082
```

If `// start` hangs in the Manager or reports started then stopped, inspect/fix the WORBI repo start wrapper detach strategy. This is a WORBI module script problem, not a Manager distro-targeting problem.

## Hard Rule For Future Agents

When checking module lifecycle state, never infer managed-runtime state from files under:

```text
\\wsl.localhost\NymphsCore_Lite\home\nymph\...
```

That is the development checkout.

The Manager runtime truth lives under:

```text
\\wsl.localhost\NymphsCore\home\nymph\...
```

or via:

```bash
wsl.exe -d NymphsCore --user nymph -- ...
```

If a module appears updated in `NymphsCore_Lite` but the Manager still shows old data, check `NymphsCore` before changing code.

## Notes For Future Custom Pages

Do not migrate rich custom module pages yet unless the lifecycle loop stays solid.

Recommended order:

1. Rebuild Manager from the `rauty` source checkout after the success-label/script-launcher patches.
2. Repeat uninstall/reinstall/update for one more simple module.
3. Decide how Manager helper scripts should be sourced after Rauty merges to `main`.
4. Then migrate custom pages one at a time from `main`.

The custom pages should remain module-specific. The generic module page is only a fallback.
