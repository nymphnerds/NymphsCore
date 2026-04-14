# Handoff 2026-04-10 Installer Cleanup Follow-Up

Date: `2026-04-10`  
Primary branch family: `exp/2mv-remake`

## Purpose

This note captures the current installer-cleanup and updater-follow-up state
after the kept-stack cleanup pass.

It is the right restart point if another session needs to continue the helper
repo cleanup, installer updater work, or remake-branch packaging without
re-reading the whole session.

## What Changed

### 1. Public remake-branch installer link was fixed

Problem:

- the remake-branch README still linked to `raw/main`
- that caused testers to download the old `main` installer archive even when
  they thought they were testing `exp/2mv-remake`

Fix:

- `README.md` now points installer and launcher downloads at
  `raw/exp/2mv-remake/...`

Result:

- downloading from the remake-branch README now actually fetches the remake
  branch artifact

### 2. Updater dirty-state handling was relaxed for generated output

Observed local problem:

- the updater treated `Nymphs2D2/Prompts/` as a dirty repo state
- that blocked auto-update even though it was user/runtime content, not code

Fix:

- `scripts/managed_repo_utils.sh`
- `apps/Nymphs3DInstaller/publish/win-x64/scripts/managed_repo_utils.sh`

Ignored untracked paths now include:

- `Prompts/`
- `output/`
- `outputs/`
- `gradio_cache/`
- `gradio_cache_tex/`
- `__pycache__/`

Important boundary:

- tracked code edits still block auto-update
- this cleanup only relaxes untracked generated/user-output folders

### 3. Legacy cleanup support was added

New script:

- `scripts/prune_legacy_runtime.sh`

Published copy:

- `apps/Nymphs3DInstaller/publish/win-x64/scripts/prune_legacy_runtime.sh`

Current purpose:

- prune obsolete `Hunyuan3D-2.1` repo/runtime leftovers
- prune old `Nymphs2D2/.venv`
- prune stale bridge/cache leftovers such as `HunyuanDiT`

This script is now called by:

- `scripts/finalize_imported_distro.sh`
- `scripts/install_all.sh`

and the corresponding published installer copies.

### 4. Hunyuan backend local dirty state was normalized

The updater was still blocked by a tracked local change in:

- `/home/nymphs3d/Hunyuan3D-2/api_server.py`

That change was the intended removal of the old Hunyuan text-to-image bridge.

It has now been committed and pushed to:

- `Babyjawz/Hunyuan3D-2`
- branch `exp/2mv-remake`
- commit `3d16dc1`

Result:

- `Hunyuan3D-2` should no longer appear dirty on the local test machine for
  that reason

### 5. Installer archive was rebuilt and pushed

The remake-branch installer archive was rebuilt and pushed after the updater
dirty-filter change so testers can retest using a branch-correct archive.

Relevant helper-repo commit:

- `90a2a33` `Ignore generated repo outputs during updater checks`

## What Was Verified

### Local updater interpretation before the filter change

Observed updater state from screenshots:

- the first screenshot that mentioned `Hunyuan3D-2.1` was not a valid remake
  branch test, because the branch README link was still downloading the `main`
  archive
- the later screenshot without `2.1` confirmed the remake-branch archive was
  then being tested correctly

### Local repo state behind the second screenshot

At the time of the second screenshot:

- `Hunyuan3D-2` was dirty because `api_server.py` was modified
- `Nymphs2D2` was dirty only because of untracked `Prompts/`

After the updater filter change:

- `Nymphs2D2` no longer counts as dirty because of `Prompts/`
- `Hunyuan3D-2` dirty state was resolved by pushing the intended code change

## Final Updater State After Follow-Up

The updater summary now reflects the real managed stack:

- `Hunyuan3D-2`
- `Nymphs2D2`
- `TRELLIS.2`

The dirty-state logic also now treats the current TRELLIS managed layout as
normal by ignoring repo-local runtime content such as:

- `models/`
- local adapter `scripts/`
- `output/`

## Recommended Next Step

If another session continues this line, the next targeted task should be:

1. retest the existing-install updater UI from a fresh extracted archive
2. confirm the three-backend summary is readable for a non-technical tester
3. only then decide whether the updater wording needs another UX pass

## Current Bottom Line

As of this handoff:

- remake-branch README downloads now point at the correct branch artifact
- generated output no longer falsely dirties `Nymphs2D2` during updater checks
- the pushed Hunyuan backend no longer needs a local-only dirty override
- legacy `2.1` cleanup support exists
- the updater now reports the intended three-backend stack
- the next question is just whether the UX wording is good enough for external
  testers
