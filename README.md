# NymphsCore

The central hub for the NymphNerds game development backend. This repo contains the core runtime, Manager, and Blender addon source that power the local pipeline.

---

## Structure

```
NymphsCore/
├── Manager/        — WSL backend, C# installer, and setup scripts
└── Blender/
    └── Addon/      — Blender addon source and extension build tooling
```

---

## Why This Structure?

Previously the codebase was split across several repos under the `Babyjawz` account with confusing names. NymphsCore now keeps the local runtime, Manager, and Blender addon source together under `nymphnerds`.

The old `Babyjawz` repos are kept alive as backups and because the installer references some of their URLs — don't rename or delete them.

### Why is the Blender Extension a separate repo?
Blender Extensions (Blender 4.2+) must have `blender_manifest.toml` at the **root** of a repo to support direct Git URL installs from inside Blender. Unlike Unity, Blender doesn't support a `?path=` subfolder parameter. So each Blender Extension needs its own repo — see [`NymphsExt`](https://github.com/nymphnerds/NymphsExt).

The classic **Blender Addon** (`Blender/Addon/`) is different — it's installed by the Manager app automatically, so it doesn't need to be Blender-accessible directly and lives fine as a subfolder here.

The current addon workflow includes guided image part extraction: generate or choose a master image, plan extractable character parts, select the parts to keep, and generate separate references for the anatomy base, clothing, hair, props, and optional eyeball assets. The source addon lives in `Blender/Addon/`, while the public extension feed is mirrored through `NymphsExt`.

---

## Unity Packages

Unity packages now live in their own repo:

```
https://github.com/nymphnerds/unity-packages
```

Install the top-down controller from Unity Package Manager with:

```
https://github.com/nymphnerds/unity-packages.git?path=/TDC-Camera
```

Or add it directly to `Packages/manifest.json`:
```json
"com.nymphs.topdown-controller": "https://github.com/nymphnerds/unity-packages.git?path=/TDC-Camera"
```

---

## Adding Things in Future

**New Unity package** — add it to [`nymphnerds/unity-packages`](https://github.com/nymphnerds/unity-packages), not this repo.

**New Blender Extension** — create a new repo on `nymphnerds` with `blender_manifest.toml` at the root. Same pattern as `NymphsExt`.

**New classic Blender Addon** — add a folder under `Blender/` and update the Manager scripts to copy it into place.

---

## Contributing

```bash
git clone https://github.com/nymphnerds/NymphsCore.git
```

No submodules. Push normally. Unity package development happens in [`nymphnerds/unity-packages`](https://github.com/nymphnerds/unity-packages).

## Changelog

See [`CHANGELOG.md`](CHANGELOG.md) for the full NymphsCore change history across the Manager, Blender addon, extension publishing flow, and Unity package migration.

---

## Related Repos

| Repo | Purpose |
|---|---|
| [NymphsExt](https://raw.githubusercontent.com/nymphnerds/NymphsExt/main/index.json) | Blender Extensions (install via repository URL) |
| [unity-packages](https://github.com/nymphnerds/unity-packages) | Unity packages, including `TDC-Camera` |
| [Nymphs3D](https://github.com/Babyjawz/Nymphs3D) | Original Manager repo (backup) |
| [Nymphs3D-Blender-Addon](https://github.com/Babyjawz/Nymphs3D-Blender-Addon) | Original Addon repo (backup) |
| [Nymphs-TDC-Unity](https://github.com/Babyjawz/Nymphs-TDC-Unity) | Original Unity repo (backup) |
