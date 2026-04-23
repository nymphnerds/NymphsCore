# Nymphs Blender Addon User Guide

Nymphs is a Blender addon for creating game-ready 3D asset drafts from images. It works with a managed local backend called `NymphsCore_Lite` on this Lite test branch, which runs inside WSL on Windows and provides the image, shape, and texture services used by Blender.

This guide is written for a normal user installing the product on one Windows PC. It explains the expected setup, how to install the backend and addon, how to start the runtimes, and how to run the main image-to-3D workflows.

## What You Are Installing

Nymphs has two parts:

- `NymphsCore Manager`, the Windows app that installs and maintains the local WSL backend.
- `Nymphs`, the Blender addon that talks to that backend from Blender's 3D View sidebar.

The backend is intentionally local. Model generation runs on the user's machine instead of a hosted cloud service. The normal managed install creates a dedicated WSL distro named:

```text
NymphsCore_Lite
```

The managed Linux user inside that distro is:

```text
nymph
```

The addon expects that managed runtime by default. Advanced users can change the target distro, Linux user, ports, and backend paths from the addon, but most users should leave those settings alone.

## Requirements

Supported product target:

- Windows x64
- Blender 4.2 or newer
- WSL 2
- NVIDIA GPU with working Windows driver and WSL CUDA support
- internet access for installation, model downloads, addon updates, and optional OpenRouter use
- at least `130 GB` free space for a comfortable install
- `150 GB` free space recommended

The large disk requirement comes from AI model files, Python environments, CUDA components, generated outputs, and update headroom. See the backend footprint note for deeper planning detail:

- [Install Disk And Model Footprint](../../../docs/FOOTPRINT.md)

## Install The Backend

Install the backend before trying to generate assets in Blender.

Download:

- `NymphsCoreManager-win-x64.zip`

Current source locations are documented in:

- [Manager README](../../../Manager/README.md)
- [NymphsCore Manager README](../../../Manager/apps/Nymphs3DInstaller/README.md)

Basic install:

1. Download `NymphsCoreManager-win-x64.zip`.
2. Extract the zip to a normal Windows folder.
3. Run `NymphsCoreManager.exe`.
4. Approve the Windows administrator prompt if it appears.
5. Follow the manager checks and install steps.
6. Let the manager finish the WSL bootstrap/import, dependency setup, and backend verification.

Optional maintainer shortcut: if you already have a compatible `NymphsCore.tar`, place it next to `NymphsCoreManager.exe` before launching. If no tar is present, the Lite manager bootstraps a fresh Ubuntu base locally.

The extracted folder should look like this:

```text
NymphsCoreManager-win-x64/
  NymphsCoreManager.exe
  scripts/
    ...
```

Do not run the manager from inside the zip. Extract it first.

The manager may offer to prefetch model files. Prefetching takes longer during installation but avoids a long first-use download later. Skipping prefetch is allowed, but the first real runtime launch or generation can take a long time while missing model files are downloaded.

## Install The Blender Addon

Use the Blender Extensions repository feed:

```text
https://raw.githubusercontent.com/nymphnerds/NymphsExt/main/index.json
```

Install steps:

1. Open Blender 4.2 or newer.
2. Go to `Edit > Preferences > System > Network`.
3. Enable `Allow Online Access`.
4. Go to `Edit > Preferences > Extensions`.
5. Add the remote repository URL above.
6. Refresh remote data if Blender does not load it immediately.
7. Search for `Nymphs`.
8. Install the extension.

After install, open the 3D View and press `N` if the sidebar is hidden. The addon panels are under the `Nymphs` tab.

## First Launch In Blender

Open the `Nymphs` sidebar. You should see:

- `Nymphs Server`
- `Nymphs Image`
- `Nymphs Shape`
- `Nymphs Texture`

Start in `Nymphs Server`.

1. Expand `Runtimes`.
2. Click `Refresh`.
3. Confirm the managed target is available.
4. Start only the runtime you need for the next task.

Default local ports:

| Runtime | Default Port | Purpose |
|---|---:|---|
| `Z-Image` | `8090` | local prompt-to-image generation |
| `TRELLIS.2` | `8094` | single-image shape, texture, and retexture |

The older general API field defaults to:

```text
http://127.0.0.1:8080
```

The current addon uses separate runtime cards and ports for the main services. If you are using the managed install, prefer the runtime cards instead of manually changing the API URL.

## Runtime Choices

Use `Z-Image` when:

- you want local prompt-to-image generation
- you want to create a reference image before making a mesh
- you want to edit from a local guide image with `Image to Image`

Use `Gemini Flash` when:

- you want OpenRouter-backed image generation
- you want guided image edits from a source image
- you want character part planning and extraction

Use `TRELLIS.2` when:

- you have one clean reference image
- you want the default single-image image-to-3D lane
- you want shape plus texture in one pass
- you want to retexture a selected mesh from one guidance image

## Recommended First Test

For a first proof that everything is working, use the simplest local path:

1. Open Blender.
2. Open the `Nymphs` sidebar.
3. In `Nymphs Server`, expand `Runtimes`.
4. Start `TRELLIS.2`.
5. Wait until it reports as ready.
6. Open `Nymphs Shape`.
7. Choose a clean source image.
8. Leave `Auto Remove Background` on.
9. Leave `Also Generate Texture` on.
10. Click `Generate Shape + Texture`.
11. Wait for the mesh to import into Blender.
12. Use `Open Folder` if you want to inspect the saved `.glb` and metadata.

Good first source images are simple, centered, and fully visible. Props, creatures, small buildings, and full-body characters on plain backgrounds are better first tests than crowded scenes, cropped images, or screenshots with heavy shadows.

## Nymphs Server Panel

`Nymphs Server` controls the local backend runtimes.

Top status area:

- current server status
- active runtimes
- GPU load and VRAM when available
- current image backend
- current running job and progress

`Runtimes` section:

- start and stop `Z-Image`
- start and stop `TRELLIS.2`
- refresh backend state
- stop all managed runtimes

`Config Details` for each runtime exposes the service port and backend paths. Most users should not edit these values. They exist for repair, advanced installs, and developer machines.

`Advanced` section:

- WSL distro target
- WSL user
- open-terminal launch toggle
- API URL
- runtime guidance and estimates
- GPU refresh details

Use `Stop All` before closing Blender if you want to make sure backend processes are shut down.

## Nymphs Image Panel

`Nymphs Image` creates reference images for the 3D workflows.

Image backends:

- `Z-Image`, local image generation in the managed runtime.
- `Gemini Flash`, image generation through OpenRouter.

### Local Z-Image Flow

1. Start `Z-Image` from the top of `Nymphs Image`, or from `Nymphs Server > Runtimes`.
2. Open `Nymphs Image`.
3. Choose `Z-Image`.
4. Choose a generation profile.
5. Choose a subject prompt preset.
6. Optionally choose a style preset.
7. Edit the prompt.
8. Set image size, steps, seed, and variants if needed.
9. Click `Generate Image`.

For local image-to-image, enable `Image to Image`, pick a guide image, and adjust `Strength`. Lower values stay closer to the guide; higher values transform more strongly. Z-Image outputs and their metadata are saved with `txt2img` or `img2img` in the filename.

### Gemini Flash Flow

1. Open `Nymphs Image`.
2. Choose `Gemini Flash`.
3. Paste an OpenRouter API key in the `API` field, or start Blender with `OPENROUTER_API_KEY` set in the environment.
4. Choose the Gemini model.
5. Choose aspect ratio and size where available.
6. Optionally enable `Guide Image` and pick an existing image.
7. Edit the prompt.
8. Click `Generate Image`.

Blender online access must be enabled for Gemini/OpenRouter calls.

## Prompt Presets And Styles

The addon includes packaged subject prompt presets for common asset types:

- clean asset references
- props
- characters
- character master references
- character part breakout prompts
- creatures
- buildings
- hard-surface assets

The addon also includes style presets such as clean anime, painterly fantasy, storybook inkwash, and watercolor-like looks.

Packaged subject and style presets now live together in one source folder:

```text
prompt_presets/
```

Style preset files use `kind: "style"` and may store their text in either `style` or `prompt`. Subject presets use the same folder and normally store their text in `prompt`. User-saved subject prompts, style prompts, and saved full prompts are also managed through one addon preset folder in Blender's user config.

Prompt tools:

- `Editor` opens a larger Blender text block for longer prompts.
- `Apply` pulls text block edits back into the visible prompt field.
- `Quick Edit` opens a small edit dialog.
- `Preview` shows the final prompt text.
- `Save Current` saves a reusable prompt.
- `Open` opens the preset folder.

Use presets as scaffolding, not cages. The strongest results usually come from a clear subject description plus one consistent style direction.

## Character Part Extraction

`Image Part Extraction` is available when the image backend is `Gemini Flash`.

Use it when a character needs separate references for base body, clothing, hair, accessories, weapons, props, or optional eyeballs.

Typical flow:

1. Generate or choose one complete master character image.
2. In `Nymphs Image`, choose `Gemini Flash`.
3. Expand `Image Part Extraction`.
4. Click `Choose` and select the master image.
5. Choose the planner model.
6. Set `Max Parts`.
7. Click `Plan`.
8. Review the part checklist.
9. Turn off parts you do not need.
10. Adjust symmetry checkboxes if needed.
11. Choose whether to include `Face`, `Eyes In Base`, or `Add Eyeball Part`.
12. Click `Extract Selected`.

Important options:

- `Style Lock` keeps extra pressure on preserving the current style.
- `Face` keeps facial structure on the anatomy base.
- `Eyes In Base` keeps finished eyes on the anatomy base when `Face` is on.
- `Add Eyeball Part` creates a separate reusable eyeball-only reference.

Changing the source image clears the old plan so a part checklist cannot accidentally belong to the wrong character.

## Nymphs Shape Panel

`Nymphs Shape` turns reference images into imported Blender meshes.

### TRELLIS.2 Shape

This is the current built-in 3D path.

1. Start `TRELLIS.2` in `Nymphs Server`.
2. Open `Nymphs Shape`.
3. Pick a source image.
4. Leave `Auto Remove Background` on for normal plain-background references.
5. Leave `Also Generate Texture` on if you want a textured result.
6. Choose a TRELLIS preset or leave the default.
7. Click `Generate Shape` or `Generate Shape + Texture`.

Useful TRELLIS options:

- `Resolution` chooses the pipeline lane.
- `Seed` repeats or relates a result.
- `Max Tokens` controls how much structure the scene can keep.
- `Early Pass`, `Shape Pass`, and `Texture Pass` controls tune guidance and steps.
- `Texture Size` controls exported texture map size.
- `Faces` controls decimation for textured export.

Most users should start with presets and only change seed, texture size, and obvious quality/runtime controls.

Use the best front or three-quarter image as the actual shape input. The addon no longer exposes the old Hunyuan multiview 3D runtime.

## Nymphs Texture Panel

`Nymphs Texture` retextures an existing selected mesh.

Use it when the shape is good but the surface needs another pass.

### TRELLIS.2 Retexture

1. Start `TRELLIS.2`.
2. Select one mesh in Blender.
3. Open `Nymphs Texture`.
4. Pick a texture guidance image.
5. Leave `Auto Remove Background` on for normal references.
6. Choose texture resolution, texture size, seed, steps, and image-follow strength.
7. Click `Retexture Selected Mesh`.

TRELLIS retexture is a good default when you have one strong guidance image.

## Output Files

Generated image outputs are saved under Blender's temporary directory in:

```text
nymphs_image_outputs
```

Generated mesh outputs are saved under Blender's temporary directory in:

```text
nymphs_shape_outputs
```

Use the addon `Open Folder` buttons instead of hunting for these manually. In `Nymphs Image`, `Open Folder` and `Clear Folder` sit below the generate action so the top status area stays focused on the latest result. Use `Clear Folder` when you want to remove generated outputs from that output area.

Imported meshes are brought into the current Blender scene. Retextured meshes are imported back near the source object's transform when possible.

## Model Downloads And Waiting

First launch can be slow. That is normal when:

- the manager did not prefetch all model files
- a runtime is being started for the first time
- Python packages are compiling or warming up
- a large model is loading into GPU memory
- the backend is waiting for `/server_info`

Do not repeatedly click start buttons while the runtime is launching. Watch the status area in `Nymphs Server`. If the runtime is still downloading or loading, let it finish.

## Updating And Repairing

Use the latest `NymphsCore Manager` to:

- repair an interrupted backend install
- refresh backend helper scripts
- run smoke tests
- download missing models
- update or verify the managed `NymphsCore_Lite` distro

Use Blender's Extensions system to update the addon.

If a runtime behaves strangely after an update:

1. Save your Blender file.
2. Click `Stop All` in `Nymphs Server`.
3. Restart Blender.
4. Start only the runtime you need.
5. Retry with a simple source image.

## Troubleshooting

### The addon panels do not appear

- Confirm the extension is installed and enabled.
- Open a 3D View.
- Press `N` to show the sidebar.
- Look for the `Nymphs` tab.
- Confirm Blender is 4.2 or newer.

### Blender cannot install the extension

- Enable `Allow Online Access` in Blender's network preferences.
- Check the extension repository URL.
- Refresh remote data in the Extensions preferences.
- Confirm the PC can reach GitHub.

### A runtime will not start

- Confirm the backend was installed with `NymphsCore Manager`.
- Confirm WSL has a distro named `NymphsCore_Lite`.
- Confirm the WSL user is `nymph`.
- Click `Refresh` in `Nymphs Server`.
- Check that another app is not using the same port.
- Use `Stop All`, then start only the needed runtime.
- Rerun the manager repair path if the runtime files are missing.

### The GPU is not detected

- Update the NVIDIA Windows driver.
- Confirm the GPU is visible inside WSL.
- Reboot Windows after driver or WSL changes.
- Rerun manager checks.

### The first run is taking a long time

- This is often a model download or model load.
- Check the manager or backend logs.
- Leave the runtime running until it reports ready.
- For future installs, choose model prefetch in the manager if available.

### Shape generation fails

- Use a cleaner source image.
- Keep the whole subject visible.
- Avoid busy backgrounds, cropped objects, and heavy shadows.
- Try `Auto Remove Background`.
- Try a lower texture size or lighter preset.
- Stop unused runtimes to free VRAM.

### Retexture fails

- Select exactly one mesh.
- Use a guidance image that clearly describes the desired surface.
- Confirm the selected backend reports texture and retexture support.
- Try a smaller texture size.

### Gemini Flash does not work

- Enable Blender online access.
- Confirm the OpenRouter API key is present.
- Confirm the key has access to the selected model.
- Try a lower-cost Gemini model first.
- Check the prompt for unsupported or ambiguous requests.

## Logs And Support

Manager logs are written under:

```text
%LOCALAPPDATA%\NymphsCore\
```

If you need support, provide:

- the newest `installer-run-*.log`
- a screenshot of the manager window
- Blender version
- addon version
- GPU model and VRAM
- which runtime was running
- the source image or prompt used
- the exact error text from the addon status area

The addon version is also visible in the extension metadata. The current source manifest records the extension id as:

```text
nymphs
```

## Practical Tips For Better Assets

Use reference images that are:

- centered
- fully visible
- isolated on a simple background
- clear in silhouette
- evenly lit
- not covered by text, watermarks, UI, or scene clutter

For characters:

- use a full-body master image
- keep limbs readable
- avoid props crossing the body unless the prop is meant to be part of the final asset
- use part extraction when clothing, hair, weapons, and props need separate handling

For props and creatures:

- start with a simple single-image `TRELLIS.2` test
- use multiview only when the object has important unseen sides
- retexture after the shape is acceptable rather than trying to solve every problem in the first pass

For production work:

- save Blender files before long generation runs
- keep source images and prompts with the asset
- use seeds when you find a promising direction
- keep generated outputs until you have chosen the result to continue with
