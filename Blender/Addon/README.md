# Nymphs3D Blender Addon

![Nymphs3D2 Blender header](assets/readme-header.png)

This repo contains the Blender-side frontend code for Nymphs3D.

## Documentation

- [Roadmap](./ROADMAP.md)
  - current future-direction note for texturing lanes, alternate backends, and image-generation-to-shape ideas
- [Retexturing User Guide](./docs/USER_GUIDE_RETEXTURING.md)
  - current `2mv` vs `2.1` retexture workflows and when to use each one
- [`2mv` Texture Controls Notes](./docs/TEXTURE_CONTROLS_2MV_NOTES.md)
  - what `2mv` texture controls are live now, what is still internal, and what may be worth exposing later
- [Image-to-Mesh Repo Survey (April 2026)](./docs/IMAGE_TO_MESH_REPO_SURVEY_2026-04.md)
  - repo-native markdown version of the local PDF survey
- [Original Image-to-Mesh Survey PDF](./docs/image_to_mesh_repos.pdf)
  - original PDF stored inside the repo for reference

## Current Layout

- repo root
  - Blender extension source for `Nymphs3D2`
  - contains the live addon entrypoint and manifest used for packaging
- `dist/`
  - locally built installable test zips

This repo is separate from the backend/helper repo at:

- [Babyjawz/Nymphs3D](https://github.com/Babyjawz/Nymphs3D)

The backend/helper repo is responsible for:

- local Windows + WSL setup
- backend cloning and repair
- runtime verification
- the `NymphsCore Manager` install and repair flow

This addon repo is responsible for:

- Blender UI and workflow logic
- client-side server communication
- Blender-side generation and import flow

## Nymphs3D2 Extension

The repo root is now the `Nymphs3D2` Blender extension package.

The supported install and update path is the separate extension feed repo.

This source repo can still work as a manual `Install from Disk` fallback when downloaded as a zip, but it should be treated as source first, not as the primary published distribution channel.

## Install Directly From Blender

Public extension repo:

- [Babyjawz/Nymphs3D2-Extensions](https://github.com/Babyjawz/Nymphs3D2-Extensions)

Repository URL for Blender:

```text
https://raw.githubusercontent.com/Babyjawz/Nymphs3D2-Extensions/main/index.json
```

Use the `main` feed URL only.
Old branch URLs such as `.../exp/2mv-remake/index.json` are obsolete because the public extension repo now publishes from `main` only.

Current public build:

- `1.1.109` published as `Nymphs3D2`

Short install guide:

1. Open Blender.
2. Go to `Edit -> Preferences -> System -> Network` and make sure `Allow Online Access` is enabled.
3. Go to `Edit -> Preferences -> Extensions`.
4. Add a custom remote repository and use this URL:

```text
https://raw.githubusercontent.com/Babyjawz/Nymphs3D2-Extensions/main/index.json
```

5. Refresh remote data if Blender does not load it immediately.
6. Search for `Nymphs3D2` and install it.

Notes:

- this public repo does not need an access token
- this is the preferred install and update path
- the current default managed runtime target is `NymphsCore` with WSL user `nymph`
- if needed, you can still try a manual `Install from Disk` from a source repo zip, but the packaged extension feed is the intended route

For Blender 4.2+ you can build the installable extension zip with Blender's extension command:

```text
blender --command extension build --source-dir . --output-dir dist
```

That produces a zip named from the manifest, which you can then install in Blender with `Install from Disk`.

The current extension package uses Blender's bundled Python standard library HTTP stack and does not need third-party Python wheels for networking.
