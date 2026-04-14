# Handoff 2026-04-07 Installer And Addon Follow-Up

This handoff captures the current state after the Windows installer repair
/resume work, repeated installer artifact refreshes, Hugging Face token
troubleshooting, and the first round of launcher/addon backend shutdown fixes.

## Main Repo State

- repo: `nymphnerds/NymphsCore`
- branch: `base_distro_v2`
- current pushed HEAD at handoff:
  - `35c14f7`
  - `Fix packaged script path conversion for installer finalize`

Recent important commits now on the branch:

- `cb571cd`
  - repair existing `Nymphs3D2` installs instead of unregistering them
  - add installer HF token field and initial model-download UX updates
- `d993ab6`
  - stop tracking standalone installer exe
- `ea7feec`
  - remove direct exe download from the beginner guide
- `fa98628`
  - fix installer finalize token forwarding in PowerShell
- `edbc0bf`
  - refresh installer zip and harden launcher backend shutdown
- `7438533`
  - fix installer HF token probe quoting
- `83a7204`
  - sync packaged scripts into distro and improve model prefetch logging
- `35c14f7`
  - fix packaged script path conversion for installer finalize

The main repo is clean at this handoff.

## Blender Addon Repo State

- repo: `nymphnerds/NymphsCore` (Blender/Addon/)
- branch: `main`
- current local HEAD at handoff:
  - `766cb84`
- repo is not clean:
  - modified: `Nymphs3D2.py`
  - untracked: `docs/handoff_2026-04-07_extension_update_crash.md`

Important:

- `Nymphs3D2.py` has local uncommitted backend lifecycle fixes.
- The untracked handoff doc in the addon repo is unrelated and was left alone.

## What Was Fixed In The Installer

### Existing `Nymphs3D2` repair/continue

The installer app no longer always unregisters `Nymphs3D2` on rerun.
It now detects the managed distro and follows a repair/continue path.

Relevant files:

- `apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs`
- `apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`
- `scripts/import_base_distro.ps1`

### HF token field and model download messaging

The installer UI now has an optional Hugging Face token field.
The app logs whether a token was provided, and the install step explains that
anonymous downloads still work but can be slower or more rate-limited.

Relevant files:

- `apps/Nymphs3DInstaller/Views/MainWindow.xaml`
- `apps/Nymphs3DInstaller/Views/MainWindow.xaml.cs`
- `apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs`
- `apps/Nymphs3DInstaller/Models/InstallSettings.cs`
- `apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`

### Installer now refreshes helper scripts inside the distro

This turned out to matter a lot.

Before the latest fixes, the installer could run stale shell scripts already
present inside `Nymphs3D2` at:

- `/opt/nymphs3d/Nymphs3D/scripts/...`

That meant changing the Windows-extracted installer scripts alone did not always
affect the actual finalize run.

The latest wrapper now:

- converts the packaged Windows `scripts` path to a WSL `/mnt/<drive>/...` path
- copies the packaged `.sh` files into `/opt/nymphs3d/Nymphs3D/scripts`
  before finalize
- continues even if the sync step fails, but logs that clearly

Relevant file:

- `scripts/run_finalize_in_distro.ps1`

### Installer model prefetch logging

The prefetch path used to rely on Hugging Face tqdm-style output, which renders
poorly in the WPF live log and can look frozen at `0%`.

The latest packaged script now:

- sets `HF_HUB_DISABLE_PROGRESS_BARS=1`
- prints installer-friendly line-based status
- prints `Starting snapshot download for ...`
- prints `Still downloading ...` every 10 seconds
- keeps anonymous download behavior valid

Relevant file:

- `scripts/prefetch_models.sh`

## What Is Still Not Fully Solved

### HF token authentication

This is the main open installer problem.

What we observed in real logs:

- installer UI shows token provided
- runtime wrapper shows token detected on the Windows side
- actual Hugging Face downloader still reported:
  - `Warning: You are sending unauthenticated requests to the HF Hub`

Meaning:

- token handoff still looked broken in the older runs
- some of those runs were also using stale in-distro scripts

The latest branch now attempts two protections:

- explicit inline `export NYMPHS3D_HF_TOKEN=...` inside the WSL bash session
- script sync into `/opt/nymphs3d/Nymphs3D/scripts` before finalize

This needs a fresh real-world retest using the latest zip from `35c14f7`.

### Model prefetch appearing hung

This should be significantly better after `83a7204` and `35c14f7`, because the
installer should now use the refreshed packaged `prefetch_models.sh` in the
distro and therefore emit line-based heartbeat logs instead of tqdm bars.

This also needs a fresh retest from a newly extracted zip.

### Fresh status after the latest wrapper fixes

A later real run got materially farther than the earlier broken runs.

Observed improvements from the latest installer iterations:

- no more broken `C:Usersbabyj/Hunyuan3D-2` runtime path
- no more `python: command not found` failure in `install_hunyuan_2.sh`
- `Hunyuan3D-2` install progressed correctly under:
  - `/home/nymphs3d/Hunyuan3D-2`
- the installer advanced into `Hunyuan3D-2.1` venv creation and large PyTorch
  wheel downloads

That means the following wrapper/integration failures were likely fixed:

- bad Windows path leaking into Linux runtime paths
- stale in-distro helper script path being used for the active finalize flow
- `python` resolution assumption in the 2.0 installer path

At that point the installer was downloading:

- `torch-2.5.1+cu124` (~908 MB)
- `nvidia_cudnn_cu12` (~665 MB)

So the run had clearly moved past the previous hard blocker class.

### Remaining open question: `Hunyuan3D-2.1` Python version

One thing still looked suspicious in the later run:

- `Hunyuan3D-2.1` venv was recreated as Python 3.10

That may be wrong.

Earlier expectations in this repo suggested:

- `Hunyuan3D-2` should use Python 3.10
- `Hunyuan3D-2.1` should use Python 3.11

This needs confirmation before more installer churn.

If the install later fails inside the 2.1 environment, this Python-version
mismatch should be treated as the first place to inspect.

## Latest Zip Artifact

The current tracked installer zip on `base_distro_v2` is:

- `apps/Nymphs3DInstaller/publish/win-x64/Nymphs3DInstaller-win-x64.zip`

Important expected behavior from the latest zip:

- no tar rebuild required
- fresh extract required
- installer should log:
  - `Syncing packaged helper scripts into the distro before finalize...`
  - `Packaged helper scripts synced into the distro.`
  - `Installer-time Hugging Face token is visible inside WSL (...)`
  - `Hugging Face progress bars disabled for installer logging...`
  - `Starting snapshot download for ...`
  - `Still downloading ...`

If those lines do not appear, the wrong artifact or a stale extracted folder is
still being used.

## Launcher State

The launcher changes are already committed and pushed in the main repo.

Current launcher hardening:

- stop path now does broad WSL-side cleanup, not only parent-process terminate
- start path also does cleanup first
- active launch options are retained so stop can target the correct distro/user
/port
- local URLs are normalized to `127.0.0.1`

Relevant file:

- `launcher/frontend/hunyuan_launcher.py`

## Addon State

The Blender addon has local uncommitted fixes in `Nymphs3D2.py`.

These local changes do the following:

- remember the launched backend target, not only the process handle
- use the actual launched distro/user/port for addon shutdown cleanup
- stop hardcoding port `8080` during addon atexit cleanup
- normalize local API root to `http://127.0.0.1:<port>`
- allow helper functions to resolve distro/user from stored dict targets

These local addon changes have not been committed or pushed yet.

## Why The Addon Needs Another Pass

The addon and launcher both currently assume the post-install runtime layout:

- `~/Hunyuan3D-2`
- `~/Hunyuan3D-2.1`
- distro name default: `Nymphs3D2`
- user default: `nymphs3d`

That does match the current installer/finalize scripts.

So the remaining addon risk is probably not the repo path itself.
The real risks are:

- lifecycle mismatch when stopping/closing
- detached child API servers surviving
- addon starting against a partially finalized distro
- addon assumptions drifting from the packaged launcher behavior over time

## Recommended Next Steps

1. Fresh-extract the latest zip from `35c14f7`.
2. Retest installer runtime setup with and without an HF token.
3. Confirm the new lines appear:
   - script sync
   - WSL token visibility
   - line-based model download heartbeats
4. If HF still shows anonymous access after those new logs, inspect the exact
   in-distro env seen by `prefetch_models.sh`.
5. Then switch to the addon repo and finish the backend lifecycle work in
   `Nymphs3D2.py`.
6. After addon changes are committed, verify addon behavior against a fresh
   `Nymphs3D2` install, not only against the local Ubuntu dev environment.

Updated practical next-step order after the latest installer run:

1. Let the current installer run finish if possible.
2. If it fails next, capture the first failure after the large 2.1 wheel
   downloads.
3. Check whether `Hunyuan3D-2.1` is truly meant to be Python 3.10 or 3.11 in
   the current shipped setup.
4. Do not rebuild `Nymphs3D2.tar` unless a failure clearly points at base image
   contents rather than installer/finalize behavior.

## Safe Mental Model For Continuation

- `Nymphs3D2.tar` does not need rebuilding for the issues worked today.
- Today’s problems were installer wrapper / script sync / token handoff issues.
- The main repo source of truth is `base_distro_v2` at `35c14f7`.
- The addon repo still has uncommitted local work that must not be forgotten.
