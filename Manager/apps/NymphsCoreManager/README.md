# NymphsCore Manager

This folder contains the Windows Manager app for the modular `NymphsCore` runtime.

## What To Download

You need:

- `NymphsCoreManager-win-x64.zip`

Current manager download:

- [NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/modular/Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip)

The manager bootstraps a fresh Ubuntu WSL base locally for Base Runtime. Optional modules are installed later from registry cards.

Optional maintainer shortcut: if you already have a compatible `NymphsCore.tar`, place it in the extracted manager folder next to `NymphsCoreManager.exe` to use the faster prebuilt path.

## Setup

1. Download `NymphsCoreManager-win-x64.zip`.
2. Extract the zip to a normal folder on Windows.
3. Run `NymphsCoreManager.exe`.
4. Open `Base Runtime` and let the manager create or reuse the dedicated `NymphsCore` WSL distro.
5. Return Home and install modules from their cards.

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
3. Use `Base Runtime` for WSL readiness, install/repair, current state, and unregister.
4. Use module cards for install/update/uninstall/start/stop/open/logs.
5. Use `Open Module UI` on installed modules that provide `ui.manager_ui`.

## Important Notes

- Do not run the manager from inside the zip.
- Extract it first.
- `NymphsCore.tar` is optional and is not shown as a required system check.
- If the tar is missing, the app bootstraps a fresh Ubuntu base locally.
- If an existing `NymphsCore` install is already present, the manager can reuse it for Base Runtime repair and module lifecycle actions.
- Module installed state is based on `.nymph-module-version`, not just whether an install folder exists.
- Installed modules may expose custom Manager UI through `ui.manager_ui`; current support is WebView2-hosted `local_html`.
- Long module UI actions should route to the Manager Logs page so stdout, stderr, and download progress stay visible.
- The Fetch Models HF token field is persisted in `%LOCALAPPDATA%\NymphsCore\shared-secrets.json` and is not echoed to logs.
- Keep future module UI work aligned with [Nymph Module UI Standard](../../../docs/NYMPH_MODULE_UI_STANDARD.md).

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
