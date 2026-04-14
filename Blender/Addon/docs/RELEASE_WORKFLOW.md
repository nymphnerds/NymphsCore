# Addon Release Workflow

This repo layout works best when each repo has one clear job:

- `Nymphs3D-Blender-Addon`
  - source of truth for addon code
- `Nymphs3D2-Extensions`
  - published extension feed and release zips
- `Nymphs3D`
  - installer, launcher, and platform integration

If you want the non-technical version first, read:

- `docs/DEV_WORKFLOW_FOR_HUMANS.md`

## Rollback Strategy

Use git commits and annotated tags in `Nymphs3D-Blender-Addon` for rollback.
Do not rely on random local backup folders or copied Python files.

Recommended rule:

- every meaningful addon checkpoint:
  - commit in `Nymphs3D-Blender-Addon`
- every release candidate or known-good point:
  - add a backup tag in `Nymphs3D-Blender-Addon`
- every published addon build:
  - package from `Nymphs3D-Blender-Addon`
  - copy the zip into `Nymphs3D2-Extensions`
  - update `index.json` in `Nymphs3D2-Extensions`

## Tooling

Use:

- `scripts/addon_release.py`

It supports three commands:

- `backup-tag`
- `build`
- `publish`

## Normal Dev Cycle

1. Make addon code changes in `Nymphs3D-Blender-Addon`.
2. Commit the working source when it reaches a real checkpoint.
3. Create a rollback tag for known-good source:

```bash
python3 scripts/addon_release.py backup-tag
```

4. When you want a release zip for Blender, publish from source:

```bash
python3 scripts/addon_release.py publish --tag-source
```

That does all of this:

- verifies the addon repo is clean
- verifies the extensions repo is clean
- creates a backup tag in the addon source repo
- builds the addon zip into `dist/`
- copies the zip into `../Nymphs3D2-Extensions/`
- refreshes `../Nymphs3D2-Extensions/index.json`

## Build Only

If you only want a local package without touching the extensions repo:

```bash
python3 scripts/addon_release.py build
```

## Why This Is Better

- rollback points live in the source repo, where source rollback actually
  matters
- the extensions repo stays focused on published artifacts
- local scratch copies become unnecessary
- release metadata stays tied to the package that was actually built

## Existing Local Snapshot Folders

The repo currently ignores local scratch history:

- `.codex_snapshots/`
- `.local_experiments/`

Those can still exist for temporary experiments, but they should not be treated
as the real backup system anymore.
