# NymphsCore Manager

This folder contains the Windows manager app for the lean `NymphsCore` WSL runtime.

## What To Download

You need:

- `NymphsCoreManager-win-x64.zip`
- `NymphsCore.tar`

Current manager download:

- [NymphsCoreManager-win-x64.zip](https://github.com/nymphnerds/NymphsCore/raw/main/Manager/apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip)

Current base distro package:

- [NymphsCore.tar](https://drive.google.com/file/d/1PIE9LJCcb06MCQ9G4T5ywrBJ8DWeqR5a/view?usp=drive_link)
- Download it separately, then place it in the extracted manager folder next to `NymphsCoreManager.exe`.

The manager zip is intentionally a no-tar archive. It contains the app and helper scripts only.

## Setup

1. Download `NymphsCoreManager-win-x64.zip`.
2. Extract the zip to a normal folder on Windows.
3. Download `NymphsCore.tar` from the Google Drive link above.
4. Place `NymphsCore.tar` in the same extracted folder as `NymphsCoreManager.exe`.

Your folder should look like this:

```text
NymphsCoreManager-win-x64/
  NymphsCoreManager.exe
  NymphsCore.tar
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
   - run smoke tests for `Hunyuan 2mv`, `Z-Image`, and `TRELLIS.2`
6. To repair, refresh, or install optional experimental modules later, rerun the latest `NymphsCore Manager`.

## Important Notes

- Do not run the manager from inside the zip.
- Extract it first.
- `NymphsCore.tar` must be next to `NymphsCoreManager.exe`.
- If the tar is missing, the app shows the download link and tells you where to place it.
- If an existing `NymphsCore` install is already present, the manager can reuse it for:
  - update checks
  - model downloads
  - smoke tests
  - repair / refresh reruns
- `Nymphs-Brain` is optional and experimental. If selected, it installs inside WSL at `/home/nymph/Nymphs-Brain`; it is not required for the Blender backend.
- if `Nymphs-Brain` is selected, Runtime Tools also exposes:
  - `Start LLM`
  - `Open WebUI`
  - `Manage Models`
  - `Stop LLM`
- the Brain install and Runtime Tools actions use LM Studio's normal CLI behavior for model fetch and server start, so no separate manual daemon bootstrap step should be needed
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
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -SkipPayloadTar
```

That is not required for normal users. Maintainers should keep the release zip at `publish/NymphsCoreManager-win-x64.zip`, not inside `publish/win-x64/`.
