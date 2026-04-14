# NymphsCore

The central hub for the NymphNerds game development backend. This monorepo contains all the core tools, packages, and addons that power our pipeline.

---

## Structure

```
NymphsCore/
├── Manager/        — WSL backend, C# installer, and setup scripts
├── Blender/
│   └── Addon/      — Classic Blender Addon (installed via the Manager)
└── Unity/
    └── TDC-Camera/ — Top-down camera controller Unity package
```

---

## Why This Structure?

Previously the codebase was split across several repos under the `Babyjawz` account with confusing names. This monorepo brings everything together under `nymphnerds` so the whole team works from one place, with one clone, and one push.

The old `Babyjawz` repos are kept alive as backups and because the installer references some of their URLs — don't rename or delete them.

### Why is the Blender Extension a separate repo?
Blender Extensions (Blender 4.2+) must have `blender_manifest.toml` at the **root** of a repo to support direct Git URL installs from inside Blender. Unlike Unity, Blender doesn't support a `?path=` subfolder parameter. So each Blender Extension needs its own repo — see [`NymphsExt`](https://github.com/nymphnerds/NymphsExt).

The classic **Blender Addon** (`Blender/Addon/`) is different — it's installed by the Manager app automatically, so it doesn't need to be Blender-accessible directly and lives fine as a subfolder here.

---

## Using the Unity Package

In Unity's Package Manager, click **Add package from Git URL** and paste:
```
https://github.com/nymphnerds/NymphsCore.git?path=/Unity/TDC-Camera
```

Or add it directly to `Packages/manifest.json`:
```json
"com.nymphnerds.tdc-camera": "https://github.com/nymphnerds/NymphsCore.git?path=/Unity/TDC-Camera"
```

---

## Adding Things in Future

**New Unity package** — add a folder under `Unity/YourPackageName/` with a `package.json` at its root. The install URL will be:
```
https://github.com/nymphnerds/NymphsCore.git?path=/Unity/YourPackageName
```

**New Blender Extension** — create a new repo on `nymphnerds` with `blender_manifest.toml` at the root. Same pattern as `NymphsExt`.

**New classic Blender Addon** — add a folder under `Blender/` and update the Manager scripts to copy it into place.

---

## Contributing

```bash
git clone https://github.com/nymphnerds/NymphsCore.git
```

No submodules. Push normally. Full history from all original repos is preserved.

---

## Related Repos

| Repo | Purpose |
|---|---|
| [NymphsExt](https://raw.githubusercontent.com/nymphnerds/NymphsExt/main/index.json) | Blender Extensions (install via repository URL) |
| [Nymphs3D](https://github.com/Babyjawz/Nymphs3D) | Original Manager repo (backup) |
| [Nymphs3D-Blender-Addon](https://github.com/Babyjawz/Nymphs3D-Blender-Addon) | Original Addon repo (backup) |
| [Nymphs-TDC-Unity](https://github.com/Babyjawz/Nymphs-TDC-Unity) | Original Unity repo (backup) |
