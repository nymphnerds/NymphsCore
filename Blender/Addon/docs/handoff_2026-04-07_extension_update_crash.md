# Handoff 2026-04-07 Extension Update Crash

This handoff captures the current end-of-session state for the Blender addon
and extension feed after the WSL target UX pass, the WSL user dropdown work,
and a Blender crash encountered while trying to update the extension.

## Repos And Current Pushed State

- addon source repo: `nymphnerds/NymphsCore` (Blender/Addon/)
- branch: `main`
- latest pushed commit at handoff time:
  - `8bcbca1`
  - `Add WSL user dropdown and move WSL target controls`

- extension feed repo: `nymphnerds/NymphsExt`
- branch: `main`
- latest pushed commit at handoff time:
  - `db71c19`
  - `Publish Nymphs3D2 Blender 5.1 Test 1.0.9`

- backup tag pushed for the previous published state:
  - `backup-extension-1.0.8-2026-04-07`

## What Was Published

Version `1.0.9` is now the published extension build.

Feed metadata now points to:

- package: `nymphs3d2-blender-5.1-test-1.0.9.zip`
- archive size: `16427`
- archive hash:
  - `sha256:0a87ab19773027fd587b7c0ba3358618db63d993c319e2b93c9474fd86058e44`

Relevant files:

- `Nymphs3D2.py`
- `blender_manifest.toml`
- `README.md`
- `CHANGELOG.md`
- `Nymphs3D2-Extensions/index.json`
- `Nymphs3D2-Extensions/index.html`

## User-Facing Changes In 1.0.9

### WSL Target Controls

The WSL target controls were moved out of `Startup Settings` and into the main
Server controls next to `API Root`.

The visible labels were shortened to:

- `Distro`
- `User`

This was done because the previous `WSL Distro` and `WSL User` labels were too
hard to read in Blender's narrow sidebar.

### WSL User Dropdown

The `User` field is no longer free text.

It now queries the selected distro for likely users by calling:

- `wsl -d <distro> -u root -- bash -lc "getent passwd | ..."`

The intended behavior is:

- default to `nymphs3d` for the managed installer flow
- include detected human users and `root`
- reduce typo risk when launching against a different distro

### Launch Feedback

There is also improved launch-probe feedback in the addon.

If the backend process is alive but `/server_info` has not responded yet, the
panel now reports that state explicitly instead of just looking stuck.

## Problem Encountered

While trying to update the extension in Blender, Blender crashed.

At handoff time:

- the new extension build is already pushed
- the feed is already updated to `1.0.9`
- the crash itself has not been root-caused yet
- this means the code publish is complete, but the install/update path still
  needs real validation after reboot

Important constraint:

- the crash happened during extension update, not during git publishing

## Suggested Next Step After Reboot

1. Start Blender clean after the PC restart.
2. Check whether the currently installed addon is still the old version or left
   in a broken partial-update state.
3. Try updating/installing `1.0.9` again.
4. Confirm that the Server panel now shows:
   - `API Root`
   - `WSL Target`
   - `Distro`
   - `User`
5. Confirm that the `User` field is a dropdown populated from the selected
   distro.
6. Test `Start Server` against a non-`Nymphs3D2` distro.
7. If Blender crashes again during update, capture the exact stage:
   - refreshing repository
   - downloading package
   - installing package
   - enabling/reloading addon

## Practical Follow-Up If It Crashes Again

If the update crash reproduces, the next session should focus on whether the
problem is:

- Blender's extension update/reload path
- the packaged `1.0.9` archive contents
- Blender keeping the old addon loaded while replacing files
- a manifest/package compatibility issue

Useful follow-up checks:

- confirm the installed version visible in Blender after restart
- try a clean disable/remove/reinstall flow instead of in-place update
- if needed, install the published `1.0.9` zip directly from disk
- compare Blender behavior between:
  - update from feed
  - remove then install from feed
  - install from disk

## Current Local Working Tree Notes

At handoff time:

- `Nymphs3D-Blender-Addon` is clean
- `Nymphs3D2-Extensions` only has one unrelated local untracked file:
  - `nymphs3d2-1.0.2.zip`

That file was intentionally left alone and was not part of the `1.0.9` publish.

## Safe Resume Point

When continuing after reboot, assume:

- GitHub is the source of truth for `1.0.9`
- the code and extension feed are already published
- the remaining work is Blender-side validation of the update/install path
- the first thing to verify is whether Blender crashes only on update, or also
  on clean install
