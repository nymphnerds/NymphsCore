# Manager Release Publish Notes

Use this note when rebuilding and pushing the Windows manager executable and archive.

## Current Release Rule

The committed manager archive is intentionally a **no-tar zip**.

- Commit the rebuilt manager executable.
- Commit the rebuilt release zip.
- Do not bundle `NymphsCore.tar` or any legacy `Nymphs3D2.tar` inside the zip.
- Users place `NymphsCore.tar` beside `NymphsCoreManager.exe` after extracting the zip.

## Expected Tracked Release Files

```text
Manager/apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip
Manager/apps/Nymphs3DInstaller/publish/win-x64/NymphsCoreManager.exe
Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts/
```

Do not commit a second copy of the zip under `publish/win-x64/`. The top-level
`publish/NymphsCoreManager-win-x64.zip` is the download archive shown in GitHub.

If new installer helper scripts are added under `Manager/scripts`, make sure the published
`scripts/` folder and the zip include them. For example, the optional Nymphs-Brain module
requires:

```text
Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts/install_nymphs_brain.sh
```

## Preferred Build Command

Run from Windows PowerShell in:

```text
Manager/apps/Nymphs3DInstaller
```

Use:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1 -SkipPayloadTar
```

The `-SkipPayloadTar` switch is important. It keeps the release archive small and prevents
accidentally committing the base distro tar into the zip.

## Verify Before Commit

From WSL, verify the zip includes the executable and required scripts:

```bash
unzip -l Manager/apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip \
  | rg 'NymphsCoreManager\.exe|scripts/install_nymphs_brain\.sh'
```

Verify the zip does not contain tar payloads:

```bash
unzip -l Manager/apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip \
  | rg 'NymphsCore\.tar|Nymphs3D2\.tar'
```

The second command should print nothing.

## Commit And Push

Check the changed files:

```bash
git status --short
git diff --stat
```

For a normal release artifact update, commit the changed publish artifacts plus any release
script/documentation changes:

```bash
git add \
  Manager/apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip \
  Manager/apps/Nymphs3DInstaller/publish/win-x64/NymphsCoreManager.exe \
  Manager/apps/Nymphs3DInstaller/publish/win-x64/scripts

git commit -m "Update published manager release"
git push
```

If only the exe changed and the zip was not rebuilt, do not stop there. Rebuild the no-tar zip
and push it too.
