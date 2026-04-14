# Handoff 2026-04-07 Nymphs3D2 Launch Blocker

This handoff captures the current end-of-session state after the Blender
extension startup regression, the `1.0.10` hotfix publish, and the unresolved
problem where launching against the managed `Nymphs3D2` distro does not become
healthy even though launching against a normal Ubuntu distro works.

## Current Repo State

### Addon Repo

- repo: `Babyjawz/Nymphs3D-Blender-Addon`
- branch: `main`
- latest pushed commit at handoff time:
  - `766cb84`
  - `Hotfix Blender startup regression in WSL target UI`

### Extensions Repo

- repo: `Babyjawz/Nymphs3D2-Extensions`
- branch: `main`
- latest pushed commit at handoff time:
  - `8a6109c`
  - `Publish Nymphs3D2 Blender 5.1 Test 1.0.10`

### Main Repo

- repo: `Babyjawz/Nymphs3D`
- branch: `base_distro_v2`

## Published Addon Status

The currently published extension version is:

- `1.0.10`

Why that matters:

- `1.0.9` was bad and could prevent Blender from opening
- `1.0.10` keeps the `Distro` dropdown and `WSL Target` placement
- `1.0.10` rolls `User` back to plain text for startup safety

Backup tags now pushed in the extensions repo:

- `backup-extension-1.0.8-2026-04-07`
- `backup-extension-1.0.9-2026-04-07`

## What The Addon Is Doing

The addon does not have special logic for Ubuntu versus `Nymphs3D2`.

For local WSL launch it simply:

1. resolves `Distro` and `User`
2. runs:
   - `wsl -d <distro> -u <user> -- bash -lc ...`
3. inside that shell:
   - `cd ~/Hunyuan3D-2` or `~/Hunyuan3D-2.1`
   - `source .venv/bin/activate`
   - `python api_server_mv.py` or `python api_server.py`

Important implication:

- if Ubuntu launches but `Nymphs3D2` stays at `Launch requested.`, Blender is
  probably not the root problem
- the more likely problem is that the `Nymphs3D2` distro is missing required
  runtime state or does not match the expected user/path layout

## Expected Managed Distro Health

The installer scripts expect a healthy managed distro to have:

- default Linux user:
  - `nymphs3d`
- home repos:
  - `~/Hunyuan3D-2`
  - `~/Hunyuan3D-2.1`
- Python envs:
  - `~/Hunyuan3D-2/.venv`
  - `~/Hunyuan3D-2.1/.venv`
- CUDA path:
  - `/usr/local/cuda-13.0`

Those assumptions are explicitly validated by:

- `scripts/check_fresh_install.ps1`

Repair/finalize path already exists in:

- `scripts/run_finalize_in_distro.ps1`

## What Was Observed

### Confirmed

- Blender startup regression from `1.0.9` was real
- removing the local installed extension folder restored Blender startup
- Ubuntu launches from the addon
- `Nymphs3D2` does not successfully come up healthy from the addon
- the Server panel remains at `Launch requested.` / startup wait behavior

### Important Correction

At one point a VHD was found at:

- `D:\WSL\Nymphs3D2\ext4.vhdx`

That should **not** currently be treated as proof of a healthy user-installed
managed distro.

User correction:

- that location was there to make a tar for the installer

So the earlier inference that this was definitely the live installed managed
distro was incorrect.

## Limitation Hit From This Ubuntu Session

From the current Ubuntu dev shell, attempts to query Windows WSL directly
failed.

Observed failures:

- PowerShell scripts invoking `wsl.exe` from the UNC-hosted script path failed
  with `Access is denied`
- direct `wsl.exe` / `cmd.exe` / `powershell.exe` calls from this Ubuntu
  session failed with:
  - `WSL ... UtilBindVsockAnyPort:307: socket failed 1`

Practical result:

- the managed `Nymphs3D2` distro could not be inspected from this Ubuntu
  session
- the installer health check could not be run successfully from here
- no reliable inside-the-distro verdict was reached in this session

## Most Likely Current Root Problem

The most likely issue is still:

- the managed `Nymphs3D2` distro on the Windows side is not fully finalized or
  does not match expected runtime layout

That could mean one or more of:

- wrong default Linux user
- missing `Hunyuan3D-2` repo
- missing `Hunyuan3D-2.1` repo
- missing `.venv`
- missing CUDA
- finalize step incomplete or failed

## Recommended Next Steps

These should be run from a real Windows PowerShell context, not from the Ubuntu
dev shell that hit the WSL-vsock issue.

1. Verify whether a real installed `Nymphs3D2` distro exists on the Windows
   machine:
   - `wsl -l -v`
2. If it exists, run the managed-distro health check:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\check_fresh_install.ps1 -DistroName Nymphs3D2`
3. If that fails, run repair/finalize:
   - `powershell -ExecutionPolicy Bypass -File .\scripts\run_finalize_in_distro.ps1 -DistroName Nymphs3D2 -LinuxUser nymphs3d`
4. After that, retry launch from Blender against:
   - `Distro = Nymphs3D2`
   - `User = nymphs3d`
5. If Blender still hangs, capture the exact Server panel text:
   - `Detail`
   - `Backend`
   - `Launch State`

## Safe Resume Point

When resuming:

- treat addon `1.0.10` as the current safe published build
- treat `1.0.9` as a broken release
- do not assume `D:\WSL\Nymphs3D2\ext4.vhdx` is the real user install
- verify the managed distro from Windows directly before making more addon
  changes
