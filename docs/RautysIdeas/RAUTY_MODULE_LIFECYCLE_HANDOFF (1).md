# Rauty Continuation Handoff

Date: 2026-05-07
Branch: `rauty`

This is the single handoff to use when continuing the Manager/module work.

## Current State

- Manager is being rebuilt into a modular Nymph shell.
- WORBI is the live test module for registry install/update/uninstall/delete.
- `nymphnerds/nymphs-registry` is the trusted catalog.
- `nymphnerds/worbi` is the first real module repo.
- Current source has fixes for:
  - responsive module card layout
  - WORBI registry lifecycle flow
  - direct/simple WSL helper launching
  - module page actions using stable `DisplayedModule`
  - destructive purge disabled for non-WORBI modules
  - available module cards opening detail pages before install
  - manifest-backed pre-install module info

## Critical WSL Rule

Always distinguish these:

```text
NymphsCore_Lite = dev/source WSL checkout
NymphsCore      = real managed runtime WSL used by Manager
```

Commands that test installs, deletes, runtime state, or module files must target:

```text
wsl.exe -d NymphsCore --user nymph -- ...
```

The source/build path is usually under:

```text
\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore
```

## Build Command

After C#/XAML changes, rebuild from Windows:

```powershell
powershell -ExecutionPolicy Bypass -File "\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1"
```

Codex may not be able to build from WSL if Windows interop reports:

```text
dotnet.exe: cannot execute binary file
```

## Safety Incident

During WORBI lifecycle testing, `/home/nymph/TRELLIS.2` in the real `NymphsCore` distro was deleted unintentionally.

Confirmed destructive path:

```text
Delete Module + Data -> uninstall_nymph_module.sh --module trellis --yes --purge
```

Likely root cause:

- module actions were using mutable `SelectedModule`
- refresh/rebuild could swap the selected module under an open page

Fixes added:

- module page now uses stable `DisplayedModule`
- destructive action audit lines were added
- `Delete Module + Data` is visible only for WORBI
- remote uninstall helper blocks purge for non-WORBI modules

Do not test destructive actions on TRELLIS, Z-Image, LoRA, or Brain until the shell is proven with WORBI or a tiny dummy module.

## TRELLIS Recovery

TRELLIS was copied back into the real `NymphsCore` distro.

The copied venv Python files became empty and were repaired with symlinks:

```powershell
wsl.exe -d NymphsCore --user nymph -- bash -lc 'cd /home/nymph/TRELLIS.2/.venv/bin && rm -f python python3 python3.10 && ln -s /usr/bin/python3.10 python && ln -s /usr/bin/python3.10 python3 && ln -s /usr/bin/python3.10 python3.10 && ./python -V'
```

Verified imports:

```text
trellis2_gguf ok
gguf ok
rembg ok
open3d ok
pymeshlab ok
meshlib ok
```

Still worth verifying:

```powershell
wsl.exe -d NymphsCore --user nymph -- bash -lc 'test -f /home/nymph/TRELLIS.2/scripts/api_server_trellis_gguf.py && test -f /home/nymph/TRELLIS.2/scripts/trellis_gguf_common.py && echo trellis_adapter_ok'
```

## Module Repo Rules

Every module should have one clean repo:

```text
github.com/nymphnerds/<module>
```

Every module repo must include:

```text
nymph.json
```

`nymph.json` is the Manager contract. It should define:

- `id`
- `name`
- `version`
- `description`
- `category`
- `kind`
- `source`
- `entrypoints`
- runtime URLs/paths
- uninstall/purge support, if any

Normal module update flow:

```text
edit module repo -> bump nymph.json version -> push -> Manager Check for Updates -> Update Module
```

The Manager should not need editing for normal module updates.

Core principle:

```text
Module repos own module behavior.
Manager owns only the generic contract and UI shell.
```

## Script Rules

Module lifecycle scripts should stay boring:

```text
install
status
start
stop
open
logs
uninstall
```

Install scripts must preserve user data by default.

Install scripts should print the final version marker only after all files, wrappers, and runtime scripts are installed:

```text
installed_module_version=x.y.z
```

Installed version should be read from:

```text
<install_root>/.nymph-module-version
```

Cached manifests are fallback info only.

## Manager Launcher Rule

Do not use giant generated `bash -lc` strings for install/uninstall/delete.

The simple path is:

```text
rm -f /tmp/nymphs-manager-<action>-<module>.sh
curl -fsSL <helper-url> -o /tmp/nymphs-manager-<action>-<module>.sh
chmod +x /tmp/nymphs-manager-<action>-<module>.sh
/bin/bash /tmp/nymphs-manager-<action>-<module>.sh --module <module> ...
rm -f /tmp/nymphs-manager-<action>-<module>.sh
```

No `awk`, no inline `$?`, no complex conditional cleanup in one string.

## UX Rules

- Home shows installed and available modules.
- Cards should be consistent and responsive.
- Available module cards open a detail page first.
- Install is a right-rail action from the detail page.
- Detail pages should show manifest-backed info before install.
- Installed-only actions stay hidden until installed.
- Module-specific manager contract buttons live in `// MANAGER CONTRACT`.
- Universal right-rail actions stay sparse.
- Dedicated module logs are good, but do not overload `// LIVE DETAIL`.

## Current Important Commits

Recent useful commits on `rauty`:

```text
d1d8029 Document module repo rules
adc8a2e Open available modules before install
478a5e9 Simplify manager module lifecycle launching
d91dd34 Harden registry module install success handling
d77d2e9 Disable destructive purge for runtime modules
7a63832 Make module uninstall delete idempotent
39bb134 Prefer module-owned uninstall entrypoints
```

## Next Session Checklist

1. Start here, not from the older handoffs.
2. Rebuild Manager from Windows if testing latest C#/XAML changes.
3. Test WORBI only:
   - card opens detail page
   - manifest info appears
   - install works
   - uninstall works
   - delete + data works only for WORBI
   - Home card state refreshes after lifecycle actions
4. Verify TRELLIS adapter files after recovery.
5. Do not test destructive actions on heavy modules.
6. Once WORBI is boring and reliable, create or migrate the next module repo.
