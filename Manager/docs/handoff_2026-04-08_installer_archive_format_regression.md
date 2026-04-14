# Handoff 2026-04-08 Installer Archive Format Regression

This handoff captures the packaging regression discovered after the
`76350fe` installer refresh on `base_distro_v2`.

## What Was Observed

- The latest screenshot from `2026-04-08 08:44:55` shows the prior session
  reporting:
  - commit `76350fe`
  - refreshed artifact:
    - `apps/Nymphs3DInstaller/publish/win-x64/Nymphs3DInstaller-win-x64.zip`
  - user follow-up:
    - `the archive is corrupt`

## Local Verification

The file currently on disk at:

- `apps/Nymphs3DInstaller/publish/win-x64/Nymphs3DInstaller-win-x64.zip`

is not a real zip archive.

Verification performed from the Ubuntu dev shell:

- `file .../Nymphs3DInstaller-win-x64.zip`

Result:

- `POSIX tar archive (GNU)`

Additional check:

- `tar -tf .../Nymphs3DInstaller-win-x64.zip`

This successfully lists installer contents, confirming the artifact is a tarball
stored under a `.zip` filename.

## Why This Matters

- beginner-facing docs point users to download the `.zip`
- Windows users will expect Explorer and normal unzip tooling to open it
- a tarball with a `.zip` extension explains the corruption/invalid archive
  report exactly

## Source Fix Applied

File changed:

- `apps/Nymphs3DInstaller/build-release.ps1`

Change made:

- stop relying on `Compress-Archive`
- build the release archive via `.NET` `System.IO.Compression.ZipFile`
- create the archive in a temp path outside `publish/win-x64`
- validate the output starts with the `PK` zip header bytes before moving it
  into place

Reason:

- this makes the archive format deterministic and prevents silently publishing a
  non-zip file under a `.zip` name

## Rebuild Outcome

The artifact was then rebuilt successfully from Windows PowerShell against the
same repo checkout.

Post-rebuild verification from Linux:

- `file .../Nymphs3DInstaller-win-x64.zip`
  - `Zip archive data`
- first four bytes:
  - `504b0304`
- Python `zipfile.testzip()` result:
  - `None`

That confirms the refreshed tracked archive is now a real zip and passes an
integrity check.

## Safe Resume Point

- the bad tar-disguised-as-zip issue has been reproduced, fixed in source, and
  rebuilt into a valid installer zip
- `build-release.ps1` now has an archive-format guard
- the refreshed `Nymphs3DInstaller-win-x64.zip` is ready to commit/push
