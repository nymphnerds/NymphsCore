# NymphsCore Runtime Crash Handoff - 2026-05-29

## Situation

User installed Base Runtime successfully with shared CUDA 13.0, then installed
Brain and Nymphs Image successfully. Pixal3D install was attempted afterwards.
During Pixal3D's PyTorch/TRELLIS dependency install, the external drive hosting
the managed `NymphsCore` WSL VHD disconnected or became unavailable.

Pixal3D install log showed:

```text
ERROR: Could not install packages due to an OSError: [Errno 5] Input/output error
/tmp/nymphs-manager-install-pixal3d.sh: error reading input file: Input/output error
```

After that, the Windows Manager began freezing during shell/module refresh.
User reports Brain and Nymphs Image are installed and should not be treated as
lost. Only Pixal3D install should be considered damaged or incomplete.

## Drive And WSL State Checked

Read-only checks were run before stopping:

```text
H: label Cooler, exFAT, Healthy, OK
H:\WSL\NymphsCore exists
H:\WSL\NymphsCore\ext4.vhdx exists, size about 39.5 GB
Last write time around the Pixal failure: 2026-05-29 20:51:08
```

Windows disk health was also healthy/online:

```text
StudioR mini RAID0 / USB / Healthy / Online
```

WSL registration showed:

```text
NymphsCore_Lite  Running  2
NymphsCore       Stopped  2
```

Interpretation: the physical drive reports healthy. The managed `NymphsCore`
WSL distro/VHD is likely in a bad or inconsistent state after the interrupted
Pixal3D install. Do not assume Base Runtime is conceptually broken.

## Do Not Do Automatically

- Do not unregister `NymphsCore` unless the user explicitly approves it.
- Do not delete `H:\WSL\NymphsCore` or `ext4.vhdx`.
- Do not reinstall Base Runtime as a first response.
- Do not change Base Runtime CUDA scripts unless there is a new, specific CUDA
  failure after the runtime is mountable again.
- Do not change module installers while diagnosing the Manager startup freeze.
- Do not treat Brain/Image as uninstalled just because Pixal3D failed.

## Pushed State

Core repo: `/home/nymph/NymphsCore`

Pushed commits on `main`:

```text
64a74b0 Preserve module marker grouping during startup
aaa4a5d Avoid WSL probes during manager startup
137b132 Handle unavailable managed WSL runtime
29c4899 Document Base Runtime CUDA ownership
```

Registry repo: `/home/nymph/NymphsModules/nymphs-registry`

Pushed registry commit:

```text
07b7472 Bump manager to 0.9.69
```

Registry points Manager at Core commit `64a74b0` with Manager version `0.9.69`.
That build still showed a bad startup state for the user: the window could stay
disabled on "Refreshing the modular shell..." and module cards did not reliably
render.

## Local-Only Changes Not Pushed

There are local changes in `/home/nymph/NymphsCore` after `64a74b0`.

Purpose: Manager `0.9.70` hotfix attempt to keep startup independent from
WSL-backed file reads when the registered runtime is not marked usable.

Modified tracked files:

```text
CHANGELOG.md
Manager/apps/NymphsCoreManager/NymphsCoreManager.csproj
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip
Manager/apps/NymphsCoreManager/publish/win-x64/NymphsCoreManager.exe
Manager/apps/NymphsCoreManager/publish/win-x64/NymphsCoreManager.pdb
Manager/apps/NymphsCoreManager/publish/win-x64/scripts/bootstrap_fresh_distro_root.sh
```

The local hotfix changed:

- Version bumped from `0.9.69` to `0.9.70`.
- Startup no longer awaits the Manager manifest/update check before rendering.
- Startup skips installed module manifest scans when `ManagedDistroDetected`
  but `ManagedDistroUsable` is false.
- Startup skips module marker scans in that registered-but-not-usable state.
- Runtime monitor text was softened from "Unavailable" to "Registered" so the
  UI does not imply Base Runtime is globally failed before live probing.

Local build status:

```text
dotnet build ... -p:EnableWindowsTargeting=true
Build succeeded, 0 warnings, 0 errors
```

Local publish/zip was created manually:

```text
/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip
sha256: 179679659b58591d8396a01683c2c98823dd3b7271dd8f26969eadb55050fff2
```

Important: user told Codex to stop and restart before these `0.9.70` changes
were committed or pushed. Do not push them blindly. Review first.

## Unrelated Untracked Files

Do not remove or include these without explicit user approval:

```text
docs/Ideas/NYMPHS_WORLD_*.md
home/assets/wiki/*.png
tmp/
```

## Likely Root Problem

Manager startup and refresh still had synchronous or awaited paths that could
touch `\\wsl.localhost\NymphsCore` or run WSL-related probes before the UI was
usable. If the registered `NymphsCore` distro points at a stopped/bad VHD, those
reads can hang the app even if the physical drive is healthy.

The Manager must handle this state:

```text
Windows says distro is registered
VHD exists but WSL cannot mount or browse it cleanly yet
Manager still opens and registry module cards render
User can choose repair/unregister/reinstall later
```

## Safe Recovery Order After Restart

1. Let the user restart Windows first.
2. Try opening Manager once.
3. If Manager opens:
   - Check whether module cards render.
   - Brain/Image should not be shown as deleted unless markers are actually
     unreadable after the runtime is usable.
   - Pixal3D may need repair/reinstall because install was interrupted.
4. If Manager still hangs:
   - Do not unregister.
   - Collect fresh Manager log lines first.
   - Continue the startup freeze fix in Manager only.
5. If WSL cannot mount `NymphsCore` after restart:
   - Protect `H:\WSL\NymphsCore\ext4.vhdx`.
   - Diagnose WSL/VHD mount separately.
   - Only unregister/reinstall if the user explicitly accepts losing/recreating
     that managed runtime.

## Key Design Rule To Preserve

Base Runtime owns shared native CUDA:

```text
/usr/local/cuda-13.0
/etc/profile.d/nymphscore-cuda.sh
```

Modules may still download PyTorch `nvidia-*` CUDA wheels as Python package
dependencies. Those are not native CUDA toolkit installs.

Brain and Image working after Base Runtime CUDA means the CUDA ownership model
was basically correct. The immediate bug is Manager resilience after an
interrupted Pixal3D install and unhealthy/stopped WSL runtime state.

