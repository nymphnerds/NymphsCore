# Rauty Module Lifecycle Handoff

Date: 2026-05-07
Updated: 2026-05-07 14:57 BST

Branch: `rauty`

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

Current verified managed-distro version:

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

## WORBI Current Remaining Issue

The stale path bug is fixed, but one runtime issue remains under direct WSL testing:

```text
worbi-start reports started, then worbi-status can immediately report stopped
```

Observed behavior:

```text
WORBI started (PID: ...)
App: http://localhost:8082
```

Then:

```text
running=false
health=unknown
```

Manual test showed that launching the server with `setsid -f` keeps it alive after `wsl.exe` returns:

```bash
cd /home/nymph/worbi/server
setsid -f bash -lc "node src/index.js > /home/nymph/worbi/logs/worbi-server.log 2>&1"
curl http://127.0.0.1:8082/api/health
```

This returned:

```json
{"status":"ok", ...}
```

Next WORBI-side fix should make `scripts/worbi_start.sh` use a detach strategy that survives `wsl.exe` returning, for example `setsid -f`, and should write the correct Node PID to `~/worbi/logs/worbi-server.pid`.

## WORBI Repo Fixes Pushed / Relevant

Relevant WORBI commits include:

```text
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

Expected version:

```text
version=6.2.51
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
version=6.2.51
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

1. Fix WORBI start wrapper detach behavior in the WORBI module repo.
2. Rebuild Manager from the `rauty` source checkout.
3. Repeat uninstall/reinstall/update for one more simple module.
4. Then migrate custom pages one at a time from `main`.

The custom pages should remain module-specific. The generic module page is only a fallback.
