# NymphsCore Manager

This folder contains the Windows manager app for the lean `NymphsCore` WSL runtime.

## What To Download

You need:

- `NymphsCoreManager-win-x64.zip`

Current manager download:

- [NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/modular/Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip)

The manager bootstraps a fresh Ubuntu WSL base locally. A prebuilt distro package is not required.

Optional maintainer shortcut: if you already have a compatible `NymphsCore.tar`, place it in the extracted manager folder next to `NymphsCoreManager.exe` to use the faster prebuilt path.

## Setup

1. Download `NymphsCoreManager-win-x64.zip`.
2. Extract the zip to a normal folder on Windows.
3. Run `NymphsCoreManager.exe`.
4. Let the manager create or reuse the dedicated `NymphsCore` WSL distro.

Your folder should look like this:

```text
NymphsCoreManager-win-x64/
  NymphsCoreManager.exe
  scripts/
    ...
```

## Run The Manager

1. Double-click `NymphsCoreManager.exe`.
2. If Windows asks for administrator permission, click `Yes`.
3. Run the system checks.
4. Continue through the install steps.
5. After a successful install, use the finish page to:
   - fetch models later without reinstalling
   - run smoke tests for `Z-Image` and `TRELLIS.2`
6. To repair, refresh, or install optional experimental modules later, rerun the latest `NymphsCore Manager`.

## Important Notes

- Do not run the manager from inside the zip.
- Extract it first.
- `NymphsCore.tar` is optional and is not shown as a required system check.
- If the tar is missing, the app bootstraps a fresh Ubuntu base locally.
- If an existing `NymphsCore` install is already present, the manager can reuse it for:
  - update checks
  - model downloads
  - smoke tests
  - repair / refresh reruns
- `Nymphs-Brain` is optional and experimental. If selected, it installs inside WSL at `/home/nymph/Nymphs-Brain`; it is not required for the Blender backend.
- if `Nymphs-Brain` is selected, the Manager exposes a dedicated Brain page with:
  - `Start Brain`
  - `Stop Brain`
  - `Start/Stop WebUI`
  - `Manage Models`
  - `Update Stack`
- Brain local model selection is handled by `lms-model`; optional remote llm-wrapper configuration is handled from the Brain page.
- `Update Stack` refreshes the installed Brain wrapper scripts before updating llama-server, LM Studio model management, and Open WebUI packages.
- the Brain install and Brain page actions use LM Studio CLI for model fetch/management, then launch `llama-server` on `http://localhost:8000/v1` for inference
- Open WebUI is intended to open on `http://localhost:8081`

## If Something Fails

- Use the `Show Logs` button in the manager.
- The manager also writes logs under:

```text
%LOCALAPPDATA%\NymphsCore\
```

- If you need help, send the newest `installer-run-*.log` file and a screenshot of the manager window.

## Advanced / Developer Note

If you are trying to rebuild the manager from source, use:

```text
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
```

If you are inside WSL and want to compile the Windows WPF app without a Linux `.NET` install, call the Windows SDK directly with `dotnet.exe` and a `\\wsl.localhost\...` project path, for example:

```text
dotnet.exe build '\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\NymphsCoreManager.csproj' -c Debug
```

This works because the actual compiler is the Windows-side .NET SDK. If a `wslpath` conversion produces a bad path, the explicit `\\wsl.localhost\...` UNC path is the safer fallback.

That is not required for normal users. Maintainers should keep the release zip at `publish/NymphsCoreManager-win-x64.zip`, not inside `publish/win-x64/`. The build does not require a bundled `NymphsCore.tar`.
