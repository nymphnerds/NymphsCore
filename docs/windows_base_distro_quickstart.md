# Windows Base Distro Quickstart

This is the simplest current workflow for building and testing the smaller
`Nymphs3D2` base distro on `D:`.

It uses a fresh small Ubuntu builder distro and avoids exporting the full huge
day-to-day `Ubuntu` install.

## Before You Start

- use **Windows PowerShell as Administrator** or **Command Prompt as Administrator**
- keep your normal working distro alone
- make sure `D:` has plenty of free space

## Step 1

From the repo root, run:

- `dev/1_BUILD_BASE_DISTRO_ON_D.bat`

This creates:

- builder distro name: `Ubuntu-24.04`
- builder distro location: `D:\WSL\Nymphs3D2-Builder`

It bootstraps only:

- `/opt/nymphs3d/Nymphs3D`
- `/opt/nymphs3d/runtime/Hunyuan3D-2`
- `/opt/nymphs3d/runtime/Hunyuan3D-2.1`

It intentionally does not bake in:

- giant model caches
- Python virtual environments
- helper model downloads

## Step 2

Run:

- `dev/2_EXPORT_BASE_DISTRO_TO_D.bat`

This exports the prepared builder distro to:

- `D:\WSL\Nymphs3D2.tar`

It also removes the temporary builder distro when export finishes.

## Step 3

Run:

- `dev/3_TEST_IMPORT_BASE_DISTRO_ON_D.bat`

This imports the tar as a separate test distro:

- distro name: `Nymphs3D2-Test`
- install location: `D:\WSL\Nymphs3D2-Test`

This test import should not touch your normal `Ubuntu` distro.

## Step 4

Run:

- `dev/4_LIGHT_FINALIZE_TEST_DISTRO.bat`

This runs a light finalize pass inside `Nymphs3D2-Test` from Windows, so you do
not need to type commands into the Linux root shell.

This light pass:

- installs system dependencies
- skips CUDA
- skips backend Python environment creation
- skips model downloads
- skips verification

## Quick Safety Check

After the test import, open Windows PowerShell and run:

```text
wsl -l -v
```

You should see your normal distro plus:

- `Nymphs3D2-Test` after the test import

You may briefly see:

- `Ubuntu-24.04` while the temporary builder exists during Step 1

## Cleanup Later

If you want to remove the test import later:

```text
wsl --unregister Nymphs3D2-Test
```

If you want to remove the builder later:

```text
wsl --unregister Ubuntu-24.04
```

Then remove the matching `D:\WSL\...` folders if they still exist.
