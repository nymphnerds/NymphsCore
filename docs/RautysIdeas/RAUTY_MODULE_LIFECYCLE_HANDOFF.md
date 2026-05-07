# Rauty Module Lifecycle Handoff

Date: 2026-05-07

Branch: `rauty`

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

Before the latest wrapper hardening, WORBI was updated to the `6.2.50` archive but could not start because the packaged `worbi-start` tried to run:

```text
/home/nymph/worbi/src/index.js
```

The real server entrypoint is:

```text
/home/nymph/worbi/server/src/index.js
```

That is now fixed in the WORBI repo by making the manager-facing wrappers authoritative.

## WORBI Repo Fixes Pushed

Latest relevant WORBI commits:

```text
d2751f1 Make WORBI manager wrappers authoritative
6b20a2b Read package archive from manifest
7c31e56 Release WORBI 6.2.50 - Fix profile selector loop on refresh
```

Why they matter:

- `7c31e56` bumped WORBI to `6.2.50`.
- `6b20a2b` fixed the installer so it reads `source.archive` from `nymph.json` instead of hardcoding `worbi-6.2.49.tar.gz`.
- `d2751f1` makes `worbi-start`, `worbi-stop`, `worbi-status`, `worbi-open`, and `worbi-logs` come from the module repo after install, so the Manager contract stays stable even if app package internals move.

## Last Verified Manager Build

Debug build passed:

```text
dotnet build NymphsCoreManager.csproj -c Debug
0 warnings
0 errors
```

## Important Current Blocker

This Codex sandbox lost Windows/WSL interop after the last test:

```text
wsl.exe: cannot execute binary file: Exec format error
```

Because of that, the final live rerun against `NymphsCore` after `d2751f1` was not completed in this session.

The WORBI repo is fixed and pushed. The next run should fetch `d2751f1` and repair the installed wrappers.

## Next Test To Run

Once WSL interop is available again, run the WORBI update from the manager or directly in the test distro:

```bash
/tmp/install_nymph_module_from_registry.sh --module worbi
```

If `/tmp/install_nymph_module_from_registry.sh` is missing, copy the current manager script into the `NymphsCore` distro first or trigger it through the manager.

Expected install output should include:

```text
Archive: .../worbi-6.2.50.tar.gz
Installed worbi.
```

Then verify:

```bash
/home/nymph/.local/bin/worbi-start
/home/nymph/.local/bin/worbi-status
```

Expected status:

```text
installed=true
running=true
health=ok
url=http://localhost:8082
```

## Notes For Future Custom Pages

Do not migrate rich custom module pages yet unless the lifecycle loop stays solid.

Recommended order:

1. Finish WORBI update verification after `d2751f1`.
2. Make the manager check/display manifest version vs installed version.
3. Repeat uninstall/reinstall/update for one more simple module.
4. Then migrate custom pages one at a time from `main`.

The custom pages should remain module-specific. The generic module page is only a fallback.
