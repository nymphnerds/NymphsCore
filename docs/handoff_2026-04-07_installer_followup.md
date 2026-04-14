# Handoff 2026-04-07 Installer Follow-Up

This handoff captures the current end-of-session state for the
`base_distro_v2` branch after multiple Windows installer fixes, packaging
fixes, README rewrites, and the first guided WSL bootstrap pass.

## Branch And Repo State

- repo: `Babyjawz/Nymphs3D`
- branch: `base_distro_v2`
- latest pushed commit at handoff time:
  - `3416153`
  - `Refresh installer release artifacts`

Recent important commits now on the branch:

- `f8343fa`
  - add guided WSL bootstrap path
- `a7ec140`
  - remove unused WSL setup field
- `3416153`
  - refresh installer release artifacts
- `972c412`
  - add repo overview back to root README
- `3465fca`
  - rewrite root README for end users
- `4a63545`
  - rewrite installer README for end users
- `85bd04e`
  - unify installer sidebar button styling
- `526e27d`
  - refresh installer release artifacts
- `d8af684`
  - refresh installer release artifacts
- `93e1d8c`
  - tidy installer sidebar button spacing
- `378e96a`
  - fix installer release script tar path array
- `14e3de1`
  - improve installer diagnostics and packaging

## What Changed

### Installer App UX

The WPF installer app now has:

- clearer missing tar handling
- explicit Google Drive link for `Nymphs3D2.tar`
- tar lookup next to `Nymphs3DInstaller.exe`
- failed system checks shown in red
- warning when existing WSL distros are detected
- cleaner WSL status text that no longer surfaces the confusing WSL1 note
- always-visible `Show Logs` button
- sidebar button styling unified so `Show Logs` matches the other buttons

### Packaging

`apps/Nymphs3DInstaller/build-release.ps1` now:

- publishes the installer executable
- copies the required `scripts/` folder into the release output
- optionally copies a local `Nymphs3D2.tar` into the release folder if present
- creates `publish/win-x64/Nymphs3DInstaller-win-x64.zip`

The release folder is intended to look like:

```text
publish/win-x64/
  Nymphs3DInstaller.exe
  scripts/
    ...
  Nymphs3DInstaller-win-x64.zip
```

If `Nymphs3D2.tar` is not bundled locally at build time, the user must place it
next to `Nymphs3DInstaller.exe` manually after download.

### README Rewrites

Both of these were rewritten to be noob-first instead of source-build-first:

- `README.md`
- `apps/Nymphs3DInstaller/README.md`

The root README now explains:

- what this repo is
- what to download
- where to put `Nymphs3D2.tar`
- how to run the installer
- how to get logs if something fails

## Critical Design Direction

The product direction is now:

- installer manages a dedicated distro:
  - `Nymphs3D2`
- installer should not modify an existing regular Ubuntu distro
- users who already have `Ubuntu` WSL should still get a separate managed
  `Nymphs3D2` install
- users with no WSL at all should be guided through WSL setup first
- the installer should not leave the user with an extra default Ubuntu distro
  just for bootstrap

That means:

- keep separate managed distro behavior
- do not restore arbitrary existing-distro install as the main flow
- do add a real noob-first WSL platform bootstrap path

## Guided WSL Bootstrap Work

The first pass of guided WSL bootstrap is now in the app.

Current implemented behavior:

- the system check identifies missing/unready WSL as a special blocking state
- the primary action on the system-check step becomes:
  - `Set Up WSL`
- that path calls:
  - `wsl --install --no-distribution`
- the intent is:
  - install/enable WSL itself
  - do not install a separate Ubuntu distro
  - then continue later into `Nymphs3D2` import

Current code areas involved:

- `apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`
- `apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs`
- `apps/Nymphs3DInstaller/Models/SystemCheckItem.cs`

## Important Current Limitation

The guided WSL bootstrap path has been added, but it still needs real
end-to-end validation on a machine that does not already have working WSL.

Questions that still need confirmation:

- does `wsl --install --no-distribution` behave correctly on the target Windows
  versions for noob users
- does Windows require reboot in common cases
- should the installer stop with a clearer reboot-required summary
- should the installer auto-rerun checks after reboot or just tell the user to
  rerun manually
- is additional messaging needed for first-time WSL platform activation

## Latest Built Artifacts On Branch

The branch currently includes refreshed release artifacts:

- `apps/Nymphs3DInstaller/publish/win-x64/Nymphs3DInstaller.exe`
- `apps/Nymphs3DInstaller/publish/win-x64/Nymphs3DInstaller-win-x64.zip`

These were rebuilt and pushed after the current source changes.

## Recommended Next Steps

1. From the real working Ubuntu shell, pull `base_distro_v2`.
2. Review the current guided WSL bootstrap implementation.
3. Test the installer on a Windows machine with:
   - no WSL at all
   - no existing `Nymphs3D2`
4. Confirm the exact behavior of:
   - `Set Up WSL`
   - reboot-required cases
   - rerun-after-WSL-setup flow
5. If the WSL bootstrap UX is still rough, improve:
   - failure summary
   - reboot messaging
   - system-check copy
6. Only after that, decide whether `base_distro_v2` is ready to merge to
   `main`.

## Safe Mental Model For Continuation

When continuing from Ubuntu, assume:

- the GitHub branch is the source of truth
- the current local `Nymphs3D2` distro can be treated as disposable
- the real working development environment is the separate Ubuntu WSL
- the installer should preserve that Ubuntu and manage only `Nymphs3D2`
