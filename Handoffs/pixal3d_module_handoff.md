# Pixal3D Module And Blender Addon Handoff

Date: 2026-05-17

## Goal

Research whether TencentARC/Pixal3D can become a Nymph module, whether it can
use the current TRELLIS GGUF fork as its backbone/runtime, and what the Blender
addon needs to expose it cleanly.

Short answer:

- A Pixal3D module is feasible.
- Pixal3D should be treated as requiring a TRELLIS.2-style backbone/runtime
  environment, not the current TRELLIS GGUF adapter runtime.
- Pixal3D inference appears to use `TencentARC/Pixal3D` checkpoints and does not
  appear to require the full regular `microsoft/TRELLIS.2-4B` release weights as
  a separate inference dependency.
- The Blender addon should treat Pixal3D as a separate 3D runtime/backend, not
  as another GGUF quant under the existing TRELLIS.2 GGUF runtime.

## Build Handoff V1: Achievable Implementation Plan

This is the implementation-grade plan. The research notes below explain why
these decisions were made; this section is what to hand to the implementer.

### Final Build Decision

Build Pixal3D as a separate Nymph module:

```text
module id:        pixal3d
install root:     $HOME/Pixal3D
venv:             $HOME/Pixal3D/.venv
port:             8096
api script:       scripts/api_server_pixal3d.py
model repo:       TencentARC/Pixal3D
default profile:  low_vram_1024
runtime profile:  Python 3.10 + Torch 2.6/cu124 + CUDA Toolkit 12.4 first
output format:    binary GLB response from POST /generate
```

Use the official TRELLIS.2 runtime install surface as the base recipe, but keep
Pixal3D independent from the TRELLIS GGUF module service and venv.

Do this:

- Vendor or sync the upstream `TencentARC/Pixal3D` repo into `$HOME/Pixal3D`.
- Build a dedicated Pixal3D venv.
- Use a tested CUDA/Torch stack. The conservative upstream/HF-demo profile is
  Python 3.10, Torch 2.6.0/cu124, torchvision 0.21.0, CUDA Toolkit 12.4. Local
  validation on 2026-05-17 also proved the Nymph CUDA 13 profile works:
  Torch 2.11.0+cu130, CUDA Toolkit 13.0, RTX 4080 SUPER.
- Install TRELLIS.2 native runtime pieces into that venv:
  `flash-attn`, `nvdiffrast`, `cumesh`, `o-voxel`, `flex_gemm`, and the normal
  Python basics.
- Hard-require `ATTN_BACKEND=flash_attn` for the Pixal3D module. Do not silently
  fall back to SDPA.
- Install Pixal3D extra deps:
  `git+https://github.com/microsoft/MoGe.git`, `diffusers==0.37.1`,
  `accelerate==1.13.0`, `gradio`, `plyfile==1.1.3`, and a NATTEN wheel matching
  the chosen Torch/CUDA ABI. For local CUDA 13 validation this was
  `natten==0.21.6+torch2110cu130`.
- For auto camera estimation, use MoGe's pinned `utils3d` git dependency
  (`EasternJournalist/utils3d@3fab839f0be9931dac7c8488eb0e1600c236e183`), not
  only the Pixal3D README `utils3d-0.0.2` wheel. The README wheel lacks the
  `utils3d.pt` alias that MoGe calls.
- Fetch Pixal3D and auxiliary models into `$HOME/NymphsData` caches.
- Add Pixal3D as a full third Blender service with start/stop/probe/generate UI.

Do not do this in v1:

- Do not require the TRELLIS GGUF module to be installed.
- Do not run Pixal3D through `trellis2_gguf`.
- Do not share the TRELLIS GGUF venv.
- Do not implement selected-mesh retexture through Pixal3D yet.
- Do not create a custom Manager WebView UI just for model selection.
- Do not return JSON from Pixal3D `/generate` in v1; the current Blender shape
  worker expects mesh bytes.

### Official Source Conclusions

Official Pixal3D README:

- `main` is the latest branch and is based on TRELLIS.2.
- Installation says to follow TRELLIS.2 installation first.
- Then install Pixal3D `requirements.txt`.
- Then install the `utils3d` wheel. Local validation found that MoGe auto-FOV
  additionally needs MoGe's pinned `utils3d` git commit because it exposes
  `utils3d.pt`.
- Low-VRAM mode is supported.
- Default resolution is 1536 normally and 1024 in low-VRAM mode.
- Upstream documents `ATTN_BACKEND=sdpa` as a fallback when flash-attn is absent,
  but the Nymph Pixal3D module should not expose or auto-use that fallback.
- `requirements-hfdemo.txt` is specifically for Hugging Face Spaces/H-series
  hardware and should not be the normal local install recipe.

Official TRELLIS.2 README/install surface:

- Linux and NVIDIA GPU are the tested local target.
- CUDA toolkit is required to compile several packages.
- Official setup flags include:
  `--basic --flash-attn --nvdiffrast --nvdiffrec --cumesh --o-voxel --flexgemm`.
- TRELLIS.2 uses `o_voxel` for GLB export and `CuMesh`/`FlexGEMM`/`nvdiffrast`
  for native geometry/rendering paths.
- The official TRELLIS.2 pretrained model is `microsoft/TRELLIS.2-4B`, but that
  is the official TRELLIS.2 model dependency, not an observed separate Pixal3D
  inference dependency.

Local source comparison:

- `/home/nymph/NymphsModules/trellis/trellis2` matches a fresh official
  `microsoft/TRELLIS.2` clone at commit `5565d240...` in the compared source
  tree.
- Your TRELLIS module adds GGUF adapter scripts around that source:
  `scripts/api_server_trellis_gguf.py` and `scripts/trellis_gguf_common.py`.
- Pixal3D is not just official TRELLIS.2 with different weights. Compared to
  official `trellis2`, it adds and changes projection-specific code:
  `pixal3d/pipelines/pixal3d_image_to_3d.py`,
  `pixal3d/trainers/flow_matching/mixins/image_conditioned_proj.py`,
  `pixal3d/modules/attention/proj_attention.py`,
  `pixal3d/modules/sparse/attention/proj_attention.py`, and related modified
  transformer/attention paths.

Conclusion:

```text
The existing TRELLIS module install recipe is useful.
The existing TRELLIS GGUF service/runtime path is not the Pixal3D base.
The Pixal3D module must install/run the Pixal3D package itself.
```

### Deliverable File Tree

Create:

```text
NymphsModules/pixal3d/
  nymph.json
  README.md
  docs/
    PIXAL3D_MODULE_NOTES.md
  scripts/
    _pixal3d_common.sh
    install_pixal3d.sh
    pixal3d_update.sh
    pixal3d_status.sh
    pixal3d_start.sh
    pixal3d_stop.sh
    pixal3d_logs.sh
    pixal3d_open.sh
    pixal3d_open_outputs.sh
    pixal3d_open_weights.sh
    pixal3d_fetch_models.sh
    pixal3d_smoke_test.sh
    pixal3d_uninstall.sh
    api_server_pixal3d.py
    pixal3d_model_common.py
```

The install step should copy/sync the module repo into:

```text
$HOME/Pixal3D
```

The installed root must contain:

```text
$HOME/Pixal3D/nymph.json
$HOME/Pixal3D/scripts/*.sh
$HOME/Pixal3D/scripts/api_server_pixal3d.py
$HOME/Pixal3D/pixal3d/
$HOME/Pixal3D/inference.py
$HOME/Pixal3D/app.py
$HOME/Pixal3D/requirements.txt
$HOME/Pixal3D/.venv/
$HOME/Pixal3D/.nymph-module-version
```

### Data And Cache Layout

Use:

```text
$HOME/NymphsData/outputs/pixal3d
$HOME/NymphsData/logs/pixal3d
$HOME/NymphsData/config/pixal3d
$HOME/NymphsData/cache/huggingface
$HOME/NymphsData/cache/huggingface-home
$HOME/NymphsData/cache/torch-hub
```

Do not store reusable model files under `$HOME/Pixal3D` except transient
runtime cache files that belong to the source checkout. This keeps normal
uninstall safe.

### Manifest Contract

`nymph.json` must expose:

```json
{
  "manifest_version": 1,
  "id": "pixal3d",
  "name": "Pixal3D",
  "short_name": "PX",
  "version": "0.1.0",
  "description": "Local Pixal3D image-to-3D backend packaged as an installable Nymph module.",
  "category": "3d",
  "packaging": "repo",
  "install": {
    "title": "PIXAL3D RUNTIME OPTIONS",
    "root": "$HOME/Pixal3D",
    "entrypoint": "scripts/install_pixal3d.sh",
    "version_marker": "$HOME/Pixal3D/.nymph-module-version",
    "installed_markers": [
      "$HOME/Pixal3D/.nymph-module-version"
    ]
  },
  "runtime": {
    "host": "127.0.0.1",
    "port": 8096,
    "health_url": "http://127.0.0.1:8096/health",
    "server_info_url": "http://127.0.0.1:8096/server_info"
  }
}
```

The module detail page must carry license/access instructions before install or
model fetch. Put them in `overview.body` so the Manager's standard detail page
shows them without needing a custom WebView UI. Also expose the BRIA access form
as an explicit `overview.links` item; do not rely on users finding the link in a
long paragraph.

Use "acknowledgement" wording, not "waiver" wording. The module cannot waive or
override Pixal3D's or BRIA's license terms; it can only make the restrictions
clear and require the user to acknowledge that they are responsible for a
permitted use case before fetching gated/non-commercial weights.

Required module detail-page notices:

```text
License / use restrictions:
- Pixal3D's local LICENSE grants use only for academic purposes.
- Pixal3D's local LICENSE forbids commercial or production use.
- Pixal3D's local LICENSE states that Pixal3D is not intended for use within
  the European Union.
- Pixal3D is provided as-is, without warranty. Users are responsible for
  confirming that their use complies with Pixal3D, BRIA RMBG, Hugging Face, and
  any other dependency terms.

BRIA RMBG-2.0 access:
- Pixal3D's official pipeline uses briaai/RMBG-2.0 for background removal.
- briaai/RMBG-2.0 is gated on Hugging Face and released for non-commercial use.
- The module details page must include a direct BRIA access-form link:
  https://huggingface.co/briaai/RMBG-2.0
- A Hugging Face token alone is not enough. The same HF account must first open
  https://huggingface.co/briaai/RMBG-2.0, fill in BRIA's access form, and agree
  to BRIA's non-commercial/license terms.
- BRIA's form says contact information such as email and username may be shared
  with the repository authors.
- If the form is not accepted, model fetch or first run fails with
  GatedRepoError / HTTP 401 or 403.
```

Suggested `overview` shape:

```json
{
  "overview": {
    "body": "Pixal3D generates textured 3D assets from a single image using a TRELLIS.2-style backbone and Pixal3D projection pipeline.\\n\\nLicense and access notice:\\n- Pixal3D is for academic use only under its local LICENSE. It is not licensed for commercial or production use.\\n- Pixal3D's local LICENSE says it is not intended for use within the European Union.\\n- The official Pixal3D pipeline uses briaai/RMBG-2.0 for background removal. That model is gated on Hugging Face and released for non-commercial use.\\n- Before Fetch Models, open the BRIA RMBG-2.0 access form with the same Hugging Face account as your token, complete BRIA's form, and agree to BRIA's terms. BRIA's form indicates contact information may be shared with the repository authors.\\n- The user is responsible for confirming their intended use is allowed by Pixal3D, BRIA RMBG, Hugging Face, and dependency licenses.\\n\\nModel fetch guide:\\n- Install sets up code and runtime only. Fetch Models downloads Pixal3D checkpoints and auxiliary models.\\n- Start with Low VRAM 1024 on 16 GB GPUs.\\n- Fetch Models requires the license/access acknowledgement in the action form.",
    "links": [
      {
        "label": "Pixal3D model/license",
        "url": "https://huggingface.co/TencentARC/Pixal3D"
      },
      {
        "label": "BRIA RMBG-2.0 access form",
        "url": "https://huggingface.co/briaai/RMBG-2.0"
      },
      {
        "label": "BRIA RMBG-2.0 CC BY-NC license",
        "url": "https://creativecommons.org/licenses/by-nc/4.0/"
      }
    ]
  }
}
```

Add entrypoints:

```json
{
  "entrypoints": {
    "install": "scripts/install_pixal3d.sh",
    "update": "scripts/pixal3d_update.sh",
    "status": "scripts/pixal3d_status.sh",
    "start": "scripts/pixal3d_start.sh",
    "stop": "scripts/pixal3d_stop.sh",
    "open": "scripts/pixal3d_open.sh",
    "logs": "scripts/pixal3d_logs.sh",
    "fetch_models": "scripts/pixal3d_fetch_models.sh",
    "smoke_test": "scripts/pixal3d_smoke_test.sh",
    "uninstall": "scripts/pixal3d_uninstall.sh",
    "open_weights": "scripts/pixal3d_open_weights.sh",
    "open_outputs": "scripts/pixal3d_open_outputs.sh"
  }
}
```

Use native action groups for model/profile fetch:

```json
{
  "ui": {
    "sort_order": 45,
    "manager_action_groups": [
      {
        "id": "model_fetch",
        "title": "Model Fetch",
        "layout": "compact",
        "entrypoint": "fetch_models",
        "result": "show_logs",
        "visibility": "installed",
        "description": "Fetch Pixal3D checkpoints and auxiliary camera/background/projection models into shared NymphsData caches.",
        "fields": [
          {
            "name": "profile",
            "type": "select",
            "label": "Profile",
            "arg": "--profile",
            "default": "low_vram_1024",
            "options": [
              {
                "label": "Low VRAM 1024",
                "value": "low_vram_1024",
                "description": "Recommended first test; lower peak VRAM and 1024 cascade"
              },
              {
                "label": "Standard 1536",
                "value": "standard_1536",
                "description": "Heavier profile for high VRAM systems"
              }
            ]
          },
          {
            "name": "hf_token",
            "type": "secret",
            "label": "Hugging Face token",
            "secret_id": "huggingface.token",
            "env": "NYMPHS3D_HF_TOKEN",
            "optional": true
          },
          {
            "name": "license_ack",
            "type": "select",
            "label": "License/access acknowledgement",
            "arg": "--license-ack",
            "default": "no",
            "options": [
              {
                "label": "Not yet",
                "value": "no",
                "description": "Do not fetch gated/non-commercial Pixal3D assets yet"
              },
              {
                "label": "I acknowledge",
                "value": "yes",
                "description": "I understand Pixal3D is academic-only, not for commercial/production use, not intended for EU use, and BRIA RMBG requires its HF access form"
              }
            ]
          }
        ],
        "submit": {
          "label": "Fetch Models"
        }
      }
    ]
  }
}
```

### Install Script Contract

`install_pixal3d.sh` should:

1. Install system packages:
   `python3.10`, `python3.10-venv`, `python3.10-dev`, `git`, `curl`, `cmake`,
   `build-essential`, `pkg-config`, `libegl1-mesa-dev`, `libgl1`,
   `libglib2.0-0`, `ccache`, `ninja`, `libjpeg-dev`.
2. Sync module/upstream source into `$HOME/Pixal3D`.
3. Ensure the official `o-voxel/third_party/eigen` submodule or equivalent
   source exists for building `o_voxel`.
4. Create `$HOME/Pixal3D/.venv` with Python 3.10.
5. Install PyTorch. Two profiles are now known:
   - Conservative upstream/HF-demo profile:
     `torch==2.6.0 torchvision==0.21.0 --index-url cu124`, native extensions
     built with CUDA Toolkit 12.4.
   - Locally validated Nymph CUDA 13 profile:
     `torch==2.11.0+cu130 torchvision==0.26.0+cu130`, native extensions built
     with CUDA Toolkit 13.0.
6. Install official TRELLIS.2 basics:
   `imageio`, `imageio-ffmpeg`, `tqdm`, `easydict`, `opencv-python-headless`,
   `ninja`, `trimesh`, `transformers`, `gradio==6.0.1`, `tensorboard`,
   `pandas`, `lpips`, `zstandard`, `kornia`, `timm`.
7. Install `utils3d`. For full auto-camera support, prefer MoGe's pinned git
   dependency:
   `git+https://github.com/EasternJournalist/utils3d.git@3fab839f0be9931dac7c8488eb0e1600c236e183`.
   Pixal3D's README wheel (`utils3d-0.0.2`) worked for manual-FOV generation but
   failed MoGe inference because it lacks `utils3d.pt`.
8. Install Pixal3D requirements:
   `git+https://github.com/microsoft/MoGe.git`,
   `diffusers==0.37.1`, `accelerate==1.13.0`, `gradio`, `plyfile==1.1.3`.
   Install a NATTEN build matching the selected Torch/CUDA stack. The CUDA 13
   validation used `natten==0.21.6+torch2110cu130` from `https://whl.natten.org`.
9. Install native runtime extensions:
   `flash-attn`, `nvdiffrast`, `cumesh`, `flex_gemm`, `o-voxel`.
10. Compile-check:
   `scripts/api_server_pixal3d.py`, import `pixal3d`, import `o_voxel`,
   import `cumesh`, import `flex_gemm`, import `nvdiffrast.torch`, import
   `flash_attn`, and run a tiny CUDA flash-attn kernel smoke.
11. Write `$HOME/Pixal3D/.nymph-module-version`.

Install risk policy:

- If `flash-attn` fails to import or its CUDA kernel smoke fails, install should
  fail clearly. Do not continue with `ATTN_BACKEND=sdpa`.
- If NATTEN does not support the chosen Torch/CUDA ABI, fail with a clear
  message and point to the tested profile.
- Do not use `requirements-hfdemo.txt` by default. It is hardware-specific.

### Status Script Contract

`pixal3d_status.sh` must be fast enough for Manager status timeouts.

It should emit:

```text
id=pixal3d
name=Pixal3D
installed=true|false
runtime_present=true|false
data_present=true|false
version=...
env_ready=true|false
adapter_ready=true|false
runtime_ready=true|false
models_ready=true|false|unknown
aux_models_ready=true|false|unknown
running=true|false
state=available|installed|running|needs_attention
health=ok|unknown|degraded|model-download-needed|unreachable
url=http://127.0.0.1:8096
install_root=/home/nymph/Pixal3D
venv=/home/nymph/Pixal3D/.venv
logs_dir=/home/nymph/NymphsData/logs/pixal3d
outputs_dir=/home/nymph/NymphsData/outputs/pixal3d
config_dir=/home/nymph/NymphsData/config/pixal3d
hf_cache_dir=/home/nymph/NymphsData/cache/huggingface
torch_hub_dir=/home/nymph/NymphsData/cache/torch-hub
profile=low_vram_1024
marker=/home/nymph/Pixal3D/.nymph-module-version
detail=...
```

Lightweight runtime import check:

```text
import pixal3d
import o_voxel
import cumesh
import flex_gemm
import nvdiffrast.torch
```

Do not instantiate Pixal3D, DINO, MoGe, RMBG, or NAF in status.

### Model Fetch Contract

`pixal3d_fetch_models.sh` should accept:

```text
--profile low_vram_1024|standard_1536
--hf-token <token>
--license-ack yes|no
```

`--license-ack yes` should be required before downloading `TencentARC/Pixal3D`
or `briaai/RMBG-2.0`. If it is omitted or set to `no`, fail before network
downloads with a targeted message:

```text
LICENSE ACK REQUIRED:
Pixal3D is academic-only, not licensed for commercial or production use, and
its LICENSE says it is not intended for use within the European Union.
BRIA RMBG-2.0 is gated/non-commercial and requires the Hugging Face access form:
https://huggingface.co/briaai/RMBG-2.0
Rerun Fetch Models after selecting "I acknowledge" in the module action form.
```

It should download:

```text
TencentARC/Pixal3D
Ruicheng/moge-2-vitl
camenduru/dinov3-vitl16-pretrain-lvd1689m
briaai/RMBG-2.0
valeoai/NAF through torch.hub
```

`briaai/RMBG-2.0` is a gated Hugging Face model. A Hugging Face token is
necessary but not sufficient: the same HF account must first open
`https://huggingface.co/briaai/RMBG-2.0`, fill in BRIA's access form, and agree
to the non-commercial/license terms. If the form has not been accepted, fetch or
first pipeline load fails with `GatedRepoError` / HTTP `401` or `403` even when a
token is supplied. The fetch script should detect this and print a targeted
message instead of reporting a generic model-download failure.

Main Pixal3D `snapshot_download` allow patterns:

```text
pipeline.json
ckpts/*.json
ckpts/*.safetensors
README.md
```

Progress output should use readable lines:

```text
MODEL FETCH STARTED: step=1/5 repo=TencentARC/Pixal3D
MODEL FETCH STATUS: step=1/5 cache_total=...
MODEL FETCH COMPLETE: step=1/5 repo=TencentARC/Pixal3D root=...
```

After fetch, write:

```text
$HOME/NymphsData/config/pixal3d/profile.env
```

with:

```text
PIXAL3D_PROFILE=low_vram_1024
PIXAL3D_LOW_VRAM=1
PIXAL3D_RESOLUTION=1024
PIXAL3D_MODEL_REPO=TencentARC/Pixal3D
```

### API Server Contract

`api_server_pixal3d.py` should mirror the TRELLIS GGUF API shape because the
Blender addon already knows how to talk to it.

Endpoints:

```text
GET  /health
GET  /server_info
GET  /active_task
POST /generate
```

`POST /generate` must return raw GLB bytes with:

```text
Content-Type: model/gltf-binary
```

The current Blender `_job_worker` treats JSON from `/generate` as an error for
shape/texture jobs, then writes response bytes to a local `.glb` and imports it.
So binary GLB is required for full addon integration without rewriting the job
worker.

`/server_info` response:

```json
{
  "status": "ready",
  "backend": "Pixal3D",
  "model_path": "TencentARC/Pixal3D",
  "resolved_model_path": "/home/nymph/NymphsData/cache/huggingface/...",
  "subfolder": "",
  "enable_tex": true,
  "mesh_retexture": false,
  "enable_t23d": false,
  "texture_only": false,
  "low_vram": true,
  "resolution": 1024,
  "supported_resolutions": [1024, 1536],
  "attention_backend": "flash_attn",
  "sparse_conv_backend": "flex_gemm",
  "model_ready": true,
  "aux_models_ready": true,
  "runtime_distro": "NymphsCore",
  "runtime_user": "nymph",
  "hf_home": "/home/nymph/NymphsData/cache/huggingface-home",
  "torch_home": "/home/nymph/NymphsData/cache/torch-hub"
}
```

`/active_task` response should match current addon progress parsing style:

```json
{
  "status": "idle",
  "stage": "",
  "detail": "",
  "progress_current": null,
  "progress_total": null,
  "progress_percent": null,
  "message": ""
}
```

Generation flow:

1. Read JSON body.
2. Decode `image` base64.
3. Reject requests containing `mesh` with HTTP 400 in v1:
   Pixal3D selected-mesh retexture is not implemented.
4. Lazy-load `Pixal3DImageTo3DPipeline.from_pretrained(model_path)`.
5. Build four `DinoV3ProjFeatureExtractor` models.
6. Apply low-VRAM mode if configured.
7. Preprocess image via Pixal3D pipeline.
8. Estimate camera with MoGe, unless manual FOV is supplied.
9. Run `pipeline.run(..., pipeline_type="1024_cascade"|"1536_cascade")`.
10. Export GLB with `o_voxel.postprocess.to_glb`.
11. Export with `extension_webp=False` for Blender compatibility unless testing
    proves WebP imports cleanly.
12. Return raw GLB bytes.

Payload mapping:

```text
image                         -> required base64 image
seed                          -> Pixal3D seed
max_num_tokens                -> Pixal3D max_num_tokens
pipeline_type=1024_cascade    -> resolution=1024
pipeline_type=1536_cascade    -> resolution=1536
pixal3d_resolution            -> overrides pipeline_type if set
pixal3d_low_vram              -> low_vram
pixal3d_manual_fov            -> manual_fov
pixal3d_mesh_scale            -> mesh_scale
pixal3d_extend_pixel          -> extend_pixel
pixal3d_image_resolution      -> image_resolution
ss_sampling_steps             -> ss_sampling_steps
ss_guidance_strength          -> ss_guidance_strength
ss_guidance_rescale           -> ss_guidance_rescale
ss_rescale_t                  -> ss_rescale_t
shape_sampling_steps          -> shape_slat_sampling_steps
shape_guidance_strength       -> shape_slat_guidance_strength
shape_guidance_rescale        -> shape_slat_guidance_rescale
shape_rescale_t               -> shape_slat_rescale_t
tex_sampling_steps            -> tex_slat_sampling_steps
tex_guidance_strength         -> tex_slat_guidance_strength
tex_guidance_rescale          -> tex_slat_guidance_rescale
tex_rescale_t                 -> tex_slat_rescale_t
decimation_target             -> GLB export decimation_target
texture_size                  -> GLB export texture_size
```

Ignore unsupported TRELLIS GGUF-only fields without failing:

```text
gguf_quant
foreground_ratio
sparse_structure_resolution
sampler
sparse_structure_sampler
shape_sampler
tex_sampler
texture_alpha_mode
texture_double_sided
texture_bake_vertices
texture_custom_normals
texture_uv_method
texture_uv_angle
texture_inpainting
```

### Full Blender Addon Integration

File:

```text
NymphsAddon/Nymphs.py
```

Add constants:

```python
DEFAULT_REPO_PIXAL3D_PATH = "~/Pixal3D"
DEFAULT_PIXAL3D_PYTHON_PATH = "~/Pixal3D/.venv/bin/python"
DEFAULT_PIXAL3D_PORT = "8096"
DEFAULT_PIXAL3D_RESOLUTION = "1024"
```

Extend service maps:

```python
SERVICE_LABELS = {
    "n2d2": "Z-Image",
    "trellis": "TRELLIS.2 GGUF",
    "pixal3d": "Pixal3D",
}

SERVICE_PROP_PREFIXES = {
    "n2d2": "service_n2d2",
    "trellis": "service_trellis",
    "pixal3d": "service_pixal3d",
}

SERVICE_ORDER = ("n2d2", "trellis", "pixal3d")
```

Add state properties:

```python
shape_3d_runtime
service_pixal3d_enabled
service_pixal3d_port
service_pixal3d_show
service_pixal3d_launch_state
service_pixal3d_launch_detail
service_pixal3d_backend_summary
repo_pixal3d_path
pixal3d_python_path
pixal3d_low_vram
pixal3d_resolution
pixal3d_manual_fov
pixal3d_mesh_scale
pixal3d_extend_pixel
pixal3d_image_resolution
```

Change:

```python
def _selected_3d_service_key(state):
    value = getattr(state, "shape_3d_runtime", "trellis")
    return value if value in {"trellis", "pixal3d"} else "trellis"
```

Keep:

```python
def _selected_texture_service_key(state):
    return "trellis"
```

Reason: v1 Pixal3D generates image-to-textured-3D, but does not retexture an
arbitrary selected mesh.

Extend `_service_port` defaults:

```python
if service_key == "pixal3d":
    default = DEFAULT_PIXAL3D_PORT
elif service_key == "trellis":
    default = DEFAULT_TRELLIS_PORT
else:
    default = DEFAULT_N2D2_PORT
```

Extend `_compose_wsl_launch` with a Pixal3D branch:

```text
repo_path:     state.repo_pixal3d_path or ~/Pixal3D
python_path:   state.pixal3d_python_path or ~/Pixal3D/.venv/bin/python
script:        scripts/api_server_pixal3d.py
host/port:     0.0.0.0:<service_pixal3d_port>
log:           $HOME/NymphsData/logs/pixal3d/pixal3d-server.log
env:
  CUDA_HOME=/usr/local/cuda-12.4
  PIXAL3D_OUTPUT_DIR
  PIXAL3D_LOG_DIR
  PIXAL3D_CONFIG_DIR
  PIXAL3D_PID_FILE
  PIXAL3D_PORT
  PIXAL3D_HOST
  HF_HOME
  HF_HUB_CACHE
  NYMPHS3D_HF_CACHE_DIR
  TORCH_HOME
  ATTN_BACKEND
args:
  --host 0.0.0.0
  --port <port>
  --python-path <python_path>
  --model-path TencentARC/Pixal3D
  --resolution 1024|1536
  --low-vram or --no-low-vram
```

Extend `_stop_shell_for_service`:

```text
fuser -k <port>/tcp
pkill -f "scripts/api_server_pixal3d.py --host 0.0.0.0 --port <port>"
pkill -f "python scripts/api_server_pixal3d.py"
pkill -f "python3 scripts/api_server_pixal3d.py"
```

Extend backend parsing:

```python
def _backend_family(info):
    backend_name = str(info.get("backend", "")).strip().lower()
    if backend_name == "pixal3d":
        return "Pixal3D"
    ...
```

This must happen before any fallback that checks for `trellis` in text blobs.

Extend capabilities:

```python
if family == "Pixal3D":
    texture_enabled = bool(info.get("enable_tex", True))
    return {
        "family": "Pixal3D",
        "shape": not texture_only,
        "texture": texture_enabled,
        "retexture": bool(info.get("mesh_retexture", False)),
        "multiview": False,
        "text": False,
        "texture_only": texture_only,
    }
```

Extend summary parsing and fallback:

- `_summarize_server_info(info)` must produce:

```text
Pixal3D | TencentARC/Pixal3D | low_vram=true | resolution=1024 | attn=flash_attn | shape=true | tex=true | retexture=false | mv=false | text=false
```

- `_service_capabilities_from_summary` must recognize `"Pixal3D |"` and set
  `family="Pixal3D"`.
- `_fallback_server_capabilities` should return shape+texture for Pixal3D when
  Pixal3D is the selected runtime and launch is in progress.

Shape payload:

- Replace shape path use of `_with_trellis_runtime_payload` with a generic
  `_with_3d_runtime_payload`.
- For TRELLIS, keep adding `gguf_quant`.
- For Pixal3D, add:

```python
payload["pixal3d_low_vram"] = state.pixal3d_low_vram
payload["pixal3d_resolution"] = int(state.pixal3d_resolution)
payload["pixal3d_manual_fov"] = float(state.pixal3d_manual_fov)
payload["pixal3d_mesh_scale"] = float(state.pixal3d_mesh_scale)
payload["pixal3d_extend_pixel"] = int(state.pixal3d_extend_pixel)
payload["pixal3d_image_resolution"] = int(state.pixal3d_image_resolution)
```

Do not change `_job_worker` in v1. It already supports Pixal3D if Pixal3D
returns binary GLB bytes.

Server panel UI:

- `_draw_service_block` should show Pixal3D card and config details.
- For `pixal3d`, show:
  - Runtime: Pixal3D
  - Loaded: low_vram/resolution/attention if present in summary
  - Repo Path
  - Python Path
  - Server Port
- Do not show GGUF Quant in Pixal3D service card.

Shape panel UI:

- Add runtime selector at the top:
  `TRELLIS.2 GGUF` or `Pixal3D`.
- If selected service is TRELLIS:
  - show GGUF Quant
  - show existing TRELLIS controls
- If selected service is Pixal3D:
  - hide GGUF Quant
  - hide TRELLIS-only controls:
    foreground ratio, sparse structure resolution, sampler name dropdowns,
    GGUF material/UV/inpainting controls
  - show Pixal3D controls:
    low VRAM, resolution, manual FOV, mesh scale, extend pixel,
    image resolution
  - keep shared controls:
    source image, seed, shape texture checkbox, sampling steps/guidance values,
    max tokens, texture size, decimation target

Texture panel UI:

- Keep selected-mesh retexture locked to TRELLIS in v1.
- If TRELLIS is not running, texture panel should show TRELLIS start controls,
  not Pixal3D controls.

Probe/launch lifecycle:

- `_background_primary_probe` and `_background_server_probe` already use
  `service_key`; make sure they store Pixal3D service status through
  `_service_changes("pixal3d", ...)`.
- Only store available GGUF quants for `service_key == "trellis"`.
- When selected shape runtime is Pixal3D, mirror Pixal3D status into the primary
  `backend_family`, `server_supports_shape`, and `server_supports_texture`
  state fields.

Addon edit checklist by existing symbol:

```text
Constants:
  DEFAULT_REPO_PIXAL3D_PATH
  DEFAULT_PIXAL3D_PYTHON_PATH
  DEFAULT_PIXAL3D_PORT
  DEFAULT_PIXAL3D_RESOLUTION

Service maps:
  SERVICE_LABELS
  SERVICE_PROP_PREFIXES
  SERVICE_ORDER

Selection/routing:
  _selected_3d_service_key
  _selected_texture_service_key
  _service_port
  _service_api_root
  _service_runtime_is_available
  _fallback_server_capabilities
  _service_capabilities_from_summary

Payload:
  _with_trellis_runtime_payload -> replace shape use with _with_3d_runtime_payload
  _build_shape_payload
  _build_texture_payload stays TRELLIS-only

Backend info:
  _backend_family
  _server_capabilities_from_info
  _summarize_server_info
  _backend_display_name if needed

Launch/stop/probe:
  _compose_wsl_launch
  _stop_shell_for_service
  _backend_lifecycle
  _background_primary_probe
  _background_server_probe
  _background_active_probe should work unchanged if /active_task matches

State properties:
  class NymphsV2State

UI draw:
  _draw_service_block
  NYMPHSV2_PT_server if it assumes exactly two services
  NYMPHSV2_PT_shape
  NYMPHSV2_PT_texture should remain TRELLIS retexture

Operators:
  NYMPHSV2_OT_start_service should work after service maps
  NYMPHSV2_OT_stop_service should work after service maps
  NYMPHSV2_OT_probe_services should work if it loops SERVICE_ORDER
  NYMPHSV2_OT_run_shape_request should work after payload/capability updates
  NYMPHSV2_OT_run_texture_request should remain TRELLIS-only

Do not change in v1:
  _job_worker, as long as Pixal3D /generate returns binary GLB bytes
  _import_result, unless Blender material import proves Pixal3D GLB needs
  another material restoration path
```

### Validation Checklist

Module-only:

```bash
$HOME/Pixal3D/scripts/pixal3d_status.sh
$HOME/Pixal3D/scripts/pixal3d_fetch_models.sh --profile low_vram_1024
$HOME/Pixal3D/scripts/pixal3d_start.sh
curl -s http://127.0.0.1:8096/server_info
curl -s http://127.0.0.1:8096/active_task
```

API binary response:

```bash
python - <<'PY'
import base64, json, urllib.request
image = base64.b64encode(open('/home/nymph/Pixal3D/assets/images/0_img.png','rb').read()).decode()
payload = json.dumps({
    "image": image,
    "seed": 42,
    "texture": True,
    "pixal3d_low_vram": True,
    "pixal3d_resolution": 1024
}).encode()
req = urllib.request.Request(
    'http://127.0.0.1:8096/generate',
    data=payload,
    headers={'Content-Type': 'application/json'},
    method='POST',
)
with urllib.request.urlopen(req, timeout=1800) as r:
    print(r.status, r.headers.get('Content-Type'), r.headers.get('Content-Length'))
    open('/tmp/pixal3d-smoke.glb','wb').write(r.read())
PY
```

Expected:

```text
HTTP 200
Content-Type: model/gltf-binary
/tmp/pixal3d-smoke.glb imports into Blender with material/texture visible
```

Addon:

1. Install Pixal3D module in Manager.
2. Fetch Pixal3D models.
3. Open Blender.
4. Nymphs Server panel shows Z-Image, TRELLIS.2 GGUF, and Pixal3D.
5. Start Pixal3D.
6. Shape panel runtime selector chooses Pixal3D.
7. GGUF Quant is hidden.
8. Pixal3D controls are visible.
9. Generate Shape imports a textured GLB.
10. Switch runtime back to TRELLIS.2 GGUF.
11. TRELLIS shape generation still works.
12. Texture panel still retextures selected meshes with TRELLIS only.

### V1 Done Definition

V1 is complete when:

- Pixal3D module installs in NymphsCore from `nymph.json`.
- Status is reliable before install, after install, before fetch, after fetch,
  while running, and after stop.
- Pixal3D fetch is resumable and stores caches under `NymphsData`.
- Pixal3D API returns binary GLB from `/generate`.
- Blender addon can start/stop/probe Pixal3D as a third service.
- Blender addon can select Pixal3D for shape generation.
- Blender imports Pixal3D textured output.
- Existing TRELLIS GGUF and Z-Image paths still work.
- Selected-mesh texture panel remains TRELLIS-only and stable.

## Sources Checked

External:

- Pixal3D repo: `https://github.com/TencentARC/Pixal3D`
- Pixal3D README raw: `https://raw.githubusercontent.com/TencentARC/Pixal3D/master/README.md`
- Pixal3D model repo: `https://huggingface.co/TencentARC/Pixal3D`
- Pixal3D model config: `https://huggingface.co/TencentARC/Pixal3D/raw/main/pipeline.json`
- Pixal3D ckpts folder: `https://huggingface.co/TencentARC/Pixal3D/tree/main/ckpts`

Local cloned upstream snapshot during research:

```text
repo: TencentARC/Pixal3D
commit: 6a4b9f3787e3692257604937cd45cb54e80ac9a0
short:  6a4b9f3 2026-05-16 fix: align inference.py GLB export params with app.py (texture_size=4096, decimation=1M)
```

Local Nymph references:

```text
NymphsCore/docs/NYMPHS_MODULE_MAKING_GUIDE.md
NymphsCore/docs/NYMPH_MODULE_UI_STANDARD.md
NymphsModules/trellis/nymph.json
NymphsModules/trellis/docs/TRELLIS_MODULE_MIGRATION_NOTES.md
NymphsModules/trellis/scripts/api_server_trellis_gguf.py
NymphsModules/trellis/scripts/trellis_gguf_common.py
NymphsAddon/Nymphs.py
Handoffs/trellis_gguf_session_handoff.md
Handoffs/trellis_unexposed_hooks.md
```

## Upstream Pixal3D Facts

Pixal3D README says the `main` branch is the latest version and is based on the
TRELLIS.2 backbone. The README install instructions say to follow the TRELLIS.2
installation first, then install Pixal3D's extra requirements.

Important distinction for the backbone question:

```text
"based on TRELLIS.2 backbone" means Pixal3D expects TRELLIS.2-style code,
native extensions, and runtime behavior. It does not, by itself, mean the
regular Microsoft TRELLIS.2 released model weights are an extra Pixal3D
inference requirement.
```

The upstream inference entrypoint loads:

```python
MODEL_PATH = "TencentARC/Pixal3D"
pipeline = Pixal3DImageTo3DPipeline.from_pretrained(model_path)
```

It also loads these auxiliary models:

```text
Ruicheng/moge-2-vitl
camenduru/dinov3-vitl16-pretrain-lvd1689m
briaai/RMBG-2.0
NAF via valeoai/NAF torch hub path in projection feature extractor
```

Access note: `briaai/RMBG-2.0` is gated. During local validation on
2026-05-17, Pixal3D reached pipeline construction and then failed at
`AutoModelForImageSegmentation.from_pretrained("briaai/RMBG-2.0")` with
`GatedRepoError` until the user accepted BRIA's HF access form. Document this in
Manager UI/help text: the token must belong to an account that has already been
granted repository access.

Pixal3D model config currently names `Trellis2ImageTo3DPipeline`, but its model
entries are Pixal3D-local checkpoint paths:

```text
ckpts/ss_dec_conv3d_16l8_fp16
ckpts/ss_flow_img_dit_1_3B_64_bf16
ckpts/shape_dec_next_dc_f16c32_fp16
ckpts/slat_flow_img2shape_dit_1_3B_512_bf16
ckpts/slat_flow_img2shape_dit_1_3B_1024_bf16
ckpts/tex_dec_next_dc_f16c32_fp16
ckpts/slat_flow_imgshape2tex_dit_1_3B_1024_bf16
```

The Hugging Face `ckpts` folder is about `24 GB`. The listed `.safetensors`
files include:

```text
shape_dec_next_dc_f16c32_fp16.safetensors       ~948 MB
slat_flow_img2shape_dit_1_3B_1024_bf16          ~5.55 GB
slat_flow_img2shape_dit_1_3B_512_bf16           ~5.55 GB
slat_flow_imgshape2tex_dit_1_3B_1024_bf16       ~5.55 GB
ss_dec_conv3d_16l8_fp16                         ~148 MB
ss_flow_img_dit_1_3B_64_bf16                    ~5.36 GB
tex_dec_next_dc_f16c32_fp16                     ~948 MB
```

No GGUF files were found in `TencentARC/Pixal3D`.

## Answer: GGUF Fork Runtime Or Regular TRELLIS.2 Runtime?

The practical answer:

```text
Build Pixal3D against a TRELLIS.2-style runtime/backbone environment, not the
current TRELLIS GGUF adapter runtime.

Do not plan on the current TRELLIS GGUF fork being a drop-in Pixal3D backbone
unless it still exposes the original TRELLIS.2 pipeline classes, native modules,
and package layout Pixal3D imports.

Do not plan to fetch the full regular microsoft/TRELLIS.2-4B model bundle as a
separate required Pixal3D inference model dependency, based on the current
upstream Pixal3D code and HF config.
```

Pixal3D module model dependencies:

```text
TencentARC/Pixal3D
Ruicheng/moge-2-vitl
camenduru/dinov3-vitl16-pretrain-lvd1689m
briaai/RMBG-2.0
NAF support dependency, if not pulled automatically by torch hub
```

Not part of the Pixal3D inference dependency set:

```text
Aero-Ex/Trellis2-GGUF
current TRELLIS GGUF Q4_K_M/Q5_K_M/Q6_K/Q8_0 files
```

Possible exception:

- Some training/dataset code in the repo still contains older defaults pointing
  at `microsoft/TRELLIS.2-4B`, `JeffreyXiang/TRELLIS.2-4B`, or older TRELLIS
  repos. Those paths are not the main inference path. Keep the first module
  scope inference-only.

## Why The Current TRELLIS GGUF Runtime Is Not Enough

Current Nymph TRELLIS GGUF module is hardwired around:

```text
GGUF_MODEL_REPO_ID = Aero-Ex/Trellis2-GGUF
VALID_GGUF_QUANTS = Q4_K_M, Q5_K_M, Q6_K, Q8_0
from trellis2_gguf.pipelines import Trellis2ImageTo3DPipeline
model root layout: pipeline.json, Vision, shape/*.gguf, refiner/*.gguf, texture/*.gguf
```

Pixal3D uses:

```text
Pixal3DImageTo3DPipeline
TencentARC/Pixal3D safetensors checkpoints
projection-aware DINOv3 feature extractors
camera params from MoGe or manual FOV
only 1024_cascade and 1536_cascade pipeline types in Pixal3D proj mode
```

So Pixal3D should not be implemented by reusing the current GGUF adapter
runtime path.

The actual compatibility risk is the fork/runtime surface, not the model weight
format. Pixal3D imports the Pixal3D/TRELLIS.2-style pipeline and native runtime
stack directly. A GGUF-focused fork that replaced the loader with
`trellis2_gguf`, removed original pipeline classes, or changed native extension
layout will not be enough as the Pixal3D base runtime.

## Runtime Behavior To Preserve

Upstream Pixal3D inference flow:

1. Load Pixal3D pipeline from `TencentARC/Pixal3D`.
2. Build separate `DinoV3ProjFeatureExtractor` models for:
   - sparse structure
   - shape 512
   - shape 1024
   - texture 1024
3. Preprocess image with Pixal3D/TRELLIS-style background removal and crop.
4. Estimate camera params with MoGe unless user provides manual FOV.
5. Run Pixal3D projection-mode cascade pipeline:
   - `1024_cascade` in low-VRAM mode by default
   - `1536_cascade` in standard mode by default
6. Export textured GLB through `o_voxel.postprocess.to_glb`.
7. Apply Blender-friendly orientation transform.

Important upstream knobs:

```text
seed
low_vram
resolution: 1024 or 1536
manual_fov
mesh_scale
extend_pixel
image_resolution
max_num_tokens
ss_guidance_strength
ss_guidance_rescale
ss_sampling_steps
ss_rescale_t
shape_slat_guidance_strength
shape_slat_guidance_rescale
shape_slat_sampling_steps
shape_slat_rescale_t
tex_slat_guidance_strength
tex_slat_guidance_rescale
tex_slat_sampling_steps
tex_slat_rescale_t
decimation_target
texture_size
```

## Proposed Module

Module identity:

```text
id: pixal3d
name: Pixal3D
short_name: PX
category: 3d
install root: $HOME/Pixal3D
runtime port: 8096
data root: $HOME/NymphsData
outputs: $HOME/NymphsData/outputs/pixal3d
logs: $HOME/NymphsData/logs/pixal3d
config: $HOME/NymphsData/config/pixal3d
HF cache: $HOME/NymphsData/cache/huggingface
```

Recommended repo layout:

```text
nymph.json
README.md
docs/PIXAL3D_MODULE_HANDOFF.md
scripts/
  _pixal3d_common.sh
  install_pixal3d.sh
  pixal3d_status.sh
  pixal3d_start.sh
  pixal3d_stop.sh
  pixal3d_logs.sh
  pixal3d_open.sh
  pixal3d_open_outputs.sh
  pixal3d_open_weights.sh
  pixal3d_fetch_models.sh
  pixal3d_smoke_test.sh
  pixal3d_uninstall.sh
  api_server_pixal3d.py
ui/
  manager.html optional
```

Keep first implementation inference-only. Do not port Pixal3D training code.

## Proposed Manifest Shape

Use `NymphsModules/trellis/nymph.json` as the direct template, but replace the
GGUF quant action group with Pixal3D model fetch/profile controls.

Install fields:

```text
flash_attn_cuda_archs
flash_attn_jobs
flash_attn_nvcc_threads
```

These can likely mirror TRELLIS because upstream Pixal3D says to follow
TRELLIS.2 install first and uses `ATTN_BACKEND=flash_attn` by default.

Model fetch action group fields:

```text
profile:
  recommended_low_vram
  full_quality

hf_token:
  optional secret

license_ack:
  required select; must be "yes" before Fetch Models downloads Pixal3D or RMBG
```

Potential fetch modes:

```text
core_pixal3d      -> TencentARC/Pixal3D only
recommended       -> Pixal3D + MoGe + DINOv3 + RMBG + NAF if resolvable
all               -> same as recommended plus any optional app/demo assets
```

Be explicit in UI copy that the checkpoint download is large, around 24 GB for
Pixal3D ckpts before auxiliary model caches.

## Backend API Contract

Expose the same minimal HTTP contract as TRELLIS so the Blender addon can reuse
as much as possible:

```text
GET  /health
GET  /server_info
GET  /active_task
POST /generate
```

`/server_info` should return something like:

```json
{
  "status": "ready",
  "backend": "Pixal3D",
  "model_path": "TencentARC/Pixal3D",
  "resolved_model_path": "...",
  "subfolder": "main",
  "enable_tex": true,
  "mesh_retexture": false,
  "enable_t23d": false,
  "texture_only": false,
  "supports_manual_fov": true,
  "supports_low_vram": true,
  "supports_resolution": ["1024_cascade", "1536_cascade"],
  "model_ready": true,
  "aux_models_ready": true,
  "attention_backend": "flash_attn",
  "runtime_distro": "...",
  "runtime_user": "...",
  "hf_home": "..."
}
```

For first pass, `mesh_retexture=false`. Pixal3D's public app generates a textured
asset from an image and exports GLB. It does not expose the same selected-mesh
retexture path as the current TRELLIS GGUF adapter.

`POST /generate` should accept current addon-compatible fields where practical:

```text
image: base64 image
seed
remove_background
texture
max_num_tokens
texture_size
decimation_target
ss_sampling_steps
ss_guidance_strength
ss_guidance_rescale
ss_rescale_t
shape_sampling_steps
shape_guidance_strength
shape_guidance_rescale
shape_rescale_t
tex_sampling_steps
tex_guidance_strength
tex_guidance_rescale
tex_rescale_t
```

Add Pixal3D-specific optional fields:

```text
pixal3d_low_vram: bool
pixal3d_resolution: 1024|1536
pixal3d_manual_fov: float degrees or radians, choose one and document
pixal3d_mesh_scale: float
pixal3d_extend_pixel: int
pixal3d_image_resolution: int
```

Server should convert current TRELLIS-style payload names into upstream
Pixal3D names internally:

```text
shape_sampling_steps           -> shape_slat_sampling_steps
shape_guidance_strength        -> shape_slat_guidance_strength
shape_guidance_rescale         -> shape_slat_guidance_rescale
shape_rescale_t                -> shape_slat_rescale_t
pipeline_type 1024_cascade     -> resolution 1024
pipeline_type 1536_cascade     -> resolution 1536
```

Ignore unsupported fields harmlessly:

```text
gguf_quant
foreground_ratio
sparse_structure_resolution
sampler overrides
texture_alpha_mode
texture_double_sided
texture_uv_method
texture_inpainting
texture_bake_vertices
texture_custom_normals
retexture mesh payload
```

## Backend Implementation Notes

Do not shell out to `inference.py` for every request in the production adapter
unless a first smoke test needs the shortest path. A persistent API server is
better because it can cache:

```text
Pixal3D pipeline
projection DINOv3 feature extractors
RMBG model
possibly MoGe in CPU/low-VRAM mode
```

Use upstream's low-VRAM mode by default for a first Nymph module because the
current product target has cared about 16 GB class GPUs. Standard mode can be an
advanced module setting after validation.

Upstream low-VRAM behavior:

- keeps models on CPU and moves each stage to GPU on demand
- defaults to 1024 resolution
- can force 1536 with `--resolution 1536`, but this should be treated as risky
  until tested locally

Recommended first defaults:

```text
low_vram=true
resolution=1024
max_num_tokens=49152
texture_size=2048 or 4096 after local test
decimation_target=500000 or 1000000 after local test
manual_fov disabled / auto MoGe
mesh_scale=1.0
extend_pixel=0
image_resolution=512
```

## Blender Addon Addition Plan

Current addon 3D shape path is hard-coded to one selected service:

```python
def _selected_3d_service_key(state):
    return "trellis"
```

Current service registry only has:

```python
SERVICE_LABELS = {
    "n2d2": "Z-Image",
    "trellis": "TRELLIS.2 GGUF",
}
SERVICE_ORDER = ("n2d2", "trellis")
```

Add Pixal3D as a separate service:

```python
DEFAULT_PIXAL3D_PORT = "8096"
DEFAULT_REPO_PIXAL3D_PATH = "~/Pixal3D"
DEFAULT_PIXAL3D_PYTHON_PATH = "~/Pixal3D/.venv/bin/python"

SERVICE_LABELS["pixal3d"] = "Pixal3D"
SERVICE_PROP_PREFIXES["pixal3d"] = "service_pixal3d"
SERVICE_ORDER = ("n2d2", "trellis", "pixal3d")
```

Add state fields:

```text
service_pixal3d_enabled
service_pixal3d_port
service_pixal3d_show
service_pixal3d_launch_state
service_pixal3d_launch_detail
service_pixal3d_backend_summary
repo_pixal3d_path
pixal3d_python_path
pixal3d_low_vram
pixal3d_resolution
pixal3d_manual_fov
pixal3d_mesh_scale
pixal3d_extend_pixel
pixal3d_image_resolution
```

Add a user-facing selector for 3D runtime:

```text
Shape Runtime:
  TRELLIS.2 GGUF
  Pixal3D
```

Then make:

```python
def _selected_3d_service_key(state):
    return "pixal3d" if state.shape_backend == "PIXAL3D" else "trellis"
```

Texture selected-mesh retexture should stay on TRELLIS for first pass:

```python
def _selected_texture_service_key(state):
    return "trellis"
```

Pixal3D should not appear in the `Nymphs Texture` selected-mesh retexture panel
until a real backend method exists for mesh retexturing.

## Blender Addon Launch Plan

Extend `_service_port` default selection:

```text
n2d2    -> 8090
trellis -> 8095
pixal3d -> 8096
```

Extend `_compose_wsl_launch` with a Pixal3D branch:

```text
repo_path = state.repo_pixal3d_path or ~/Pixal3D
python_path = state.pixal3d_python_path or ~/Pixal3D/.venv/bin/python
script_name = scripts/api_server_pixal3d.py
PIXAL3D_OUTPUT_DIR=$HOME/NymphsData/outputs/pixal3d
PIXAL3D_LOG_DIR=$HOME/NymphsData/logs/pixal3d
PIXAL3D_CONFIG_DIR=$HOME/NymphsData/config/pixal3d
PIXAL3D_PORT=<port>
PIXAL3D_HOST=0.0.0.0
PIXAL3D_LOW_VRAM=1/0
```

Launch args:

```text
--host 0.0.0.0
--port 8096
--python-path <python_path>
--low-vram or --no-low-vram
--resolution 1024|1536
```

Extend `_stop_shell_for_service`:

```text
pkill -f "scripts/api_server_pixal3d.py --host 0.0.0.0 --port <port>"
```

Extend `_runtime_vram_estimate`:

```text
Pixal3D low-VRAM: 16 GB target, resolution 1024, MoGe camera estimation
Pixal3D standard: high VRAM, resolution 1536
```

## Blender Addon Capability Parsing

Extend `_backend_family(info)`:

```python
if backend_name == "pixal3d":
    return "Pixal3D"
```

Extend `_server_capabilities_from_info(info)`:

```text
family=Pixal3D
shape=true unless texture_only
texture=enable_tex true
retexture=false for first pass
multiview=false
text=false
```

Extend `_summarize_server_info(info)` to display:

```text
Pixal3D | TencentARC/Pixal3D | low_vram=true | resolution=1024 | attn=flash_attn | shape=true | tex=true | retexture=false
```

Do not let Pixal3D summaries accidentally become `TRELLIS.2` only because
`model_path` or `subfolder` contains "trellis". Use explicit `backend=Pixal3D`
from the server.

## Blender Addon Payload Plan

Current `_build_shape_payload` can mostly work if the Pixal3D API accepts
TRELLIS-style fields and ignores unsupported GGUF-only fields.

Add a backend-specific payload extender:

```python
def _with_3d_runtime_payload(state, payload):
    service_key = _selected_3d_service_key(state)
    if service_key == "trellis":
        payload["gguf_quant"] = state.trellis_gguf_quant
    elif service_key == "pixal3d":
        payload["pixal3d_low_vram"] = state.pixal3d_low_vram
        payload["pixal3d_resolution"] = state.pixal3d_resolution
        payload["pixal3d_manual_fov"] = state.pixal3d_manual_fov
        payload["pixal3d_mesh_scale"] = state.pixal3d_mesh_scale
        payload["pixal3d_extend_pixel"] = state.pixal3d_extend_pixel
        payload["pixal3d_image_resolution"] = state.pixal3d_image_resolution
    return payload
```

Then replace `_with_trellis_runtime_payload(...)` calls in shape payload path.
Keep texture/retexture payload TRELLIS-specific for now.

UI gating:

- Hide `GGUF Quant` when Pixal3D is selected.
- Hide GGUF-only controls for Pixal3D:
  - foreground ratio
  - sparse structure resolution
  - global/stage sampler name overrides
  - GGUF texture material controls
  - UV/inpainting controls from the GGUF adapter
- Show Pixal3D controls:
  - Low VRAM
  - Resolution 1024/1536
  - Manual FOV optional
  - Mesh scale
  - Extend pixel
  - Image resolution

Suggested first UI copy:

```text
Pixal3D uses camera-aware pixel projection. Auto camera uses MoGe; manual FOV is
for correcting difficult source images.
```

## Manager Module UI

A full custom Manager UI is optional for the first pass. Native action groups in
`nymph.json` are enough for:

```text
Fetch Models
Low-VRAM profile selector
Open Weights
Open Outputs
Smoke Test
Logs
```

If a `ui/manager.html` is added later, keep it for compact status/test controls,
not for long downloads during page load. Long fetches should use
`nymphs-module-action://fetch_models` or native `manager_action_groups`.

## Validation Plan

Phase 1: local upstream smoke test outside Nymph module.

```bash
cd /tmp/Pixal3D
python inference.py --image assets/images/0_img.png --output /tmp/pixal3d-test.glb --low_vram --resolution 1024
```

Phase 2: module install/fetch/status/start.

```bash
~/Pixal3D/scripts/pixal3d_status.sh
~/Pixal3D/scripts/pixal3d_fetch_models.sh
~/Pixal3D/scripts/pixal3d_start.sh
curl -s http://127.0.0.1:8096/server_info
curl -s http://127.0.0.1:8096/active_task
```

Phase 3: API generate smoke.

- Send a small image as base64 to `/generate`.
- Confirm GLB bytes return.
- Confirm output imports into Blender with correct orientation.
- Confirm textures are visible in Blender. Upstream CLI exports with
  `extension_webp=True`; Nymph TRELLIS GGUF disabled WebP for Blender
  compatibility. Test both; prefer standard PNG embedded GLB if WebP imports as
  geometry-only on target Blender versions.

Phase 4: addon integration.

- Start Pixal3D from `Nymphs Server`.
- Select Pixal3D in `Nymphs Shape`.
- Generate Shape + Texture.
- Confirm progress polling via `/active_task`.
- Confirm output imports and result folder opens.
- Confirm `Nymphs Texture` still uses TRELLIS for selected-mesh retexture.

### Local Validation Results 2026-05-17

Validation environment:

```text
GPU: NVIDIA GeForce RTX 4080 SUPER, compute capability 8.9
Driver: 595.79, driver-reported CUDA 13.2
Toolkit: /usr/local/cuda-13.0, nvcc V13.0.88
Python: 3.10.20
Torch: 2.11.0+cu130
Torch CUDA runtime: 13.0
torchvision: 0.26.0+cu130
Transformers: 4.57.3
tokenizers: 0.22.1
huggingface-hub: 0.36.2
NATTEN: 0.21.6+torch2110cu130 / import reports 0.21.6
diffusers: 0.37.1
accelerate: 1.13.0
gradio: 6.0.1
plyfile: 1.1.3
opencv-python: 4.13.0.92
MoGe: 2.0.0
pipeline:
  git+https://github.com/EasternJournalist/pipeline.git@866f059d2a05cde05e4a52211ec5051fd5f276d6
utils3d:
  git+https://github.com/EasternJournalist/utils3d.git@3fab839f0be9931dac7c8488eb0e1600c236e183
```

Dependency health check:

```text
pip check: No broken requirements found.
```

Validated imports/native deps:

```text
flash_attn 2.8.3
nvdiffrast.torch
cumesh
flex_gemm
o_voxel
natten
moge
pixal3d.pipelines.pixal3d_image_to_3d
```

FlashAttention validation:

```text
Upstream `inference.py` uses `os.environ.setdefault("ATTN_BACKEND", "flash_attn")`.
The successful smoke commands did not set `ATTN_BACKEND=sdpa`, so Pixal3D used
the upstream default `flash_attn` backend.
Additional CUDA kernel smoke on 2026-05-17:
flash_attn_qkvpacked_func -> output shape (2, 128, 8, 64), dtype torch.float16,
all finite.
```

The generation logs also explicitly printed:

```text
[SPARSE] Conv backend: flex_gemm; Attention backend: flash_attn
[ATTENTION] Using backend: flash_attn
```

End-to-end smoke tests:

```text
1. Official Pixal3D + unauthenticated official RMBG:
   command:
     python inference.py --image assets/images/0_img.png --low_vram --resolution 1024 --fov 0.8575560450553894
   result:
     FAILED before generation
   reason:
     briaai/RMBG-2.0 is gated. Without authenticated accepted access, Hugging Face
     returned GatedRepoError / HTTP 401 for config.json.

2. Public BiRefNet substitute before Transformers pin:
   command:
     python inference.py --image assets/images/0_img.png --low_vram --resolution 1024 --fov 0.8575560450553894
   result:
     FAILED during DINO feature extraction
   reason:
     transformers==5.5.4 exposes a DINOv3 model shape Pixal3D does not expect:
     AttributeError: 'DINOv3ViTModel' object has no attribute 'layer'

3. Public BiRefNet substitute after Transformers pin:
   command:
     python inference.py --image assets/images/0_img.png --low_vram --resolution 1024 --fov 0.8575560450553894
   result:
     PASSED
   output:
     /home/nymph/NymphsData/outputs/pixal3d-validation/pixal3d_smoke_public_birefnet_lowvram_1024_after_tf_pin.glb
     37,646,140 bytes, glTF binary v2

4. Official Pixal3D + official gated BRIA RMBG after HF access form acceptance:
   command:
     python inference.py --image assets/images/0_img.png --low_vram --resolution 1024 --fov 0.8575560450553894
   result:
     PASSED
   output:
     /home/nymph/NymphsData/outputs/pixal3d-validation/pixal3d_smoke_official_rmbg_lowvram_1024.glb
     37,431,340 bytes, glTF binary v2
```

Successful run behavior observed in logs:

```text
Pipeline loaded from TencentARC/Pixal3D for the official smoke.
Low-VRAM mode was enabled.
Manual FOV was used: 49.13 degrees / 0.8576 rad, distance=1.0938.
Pipeline type was 1024_cascade.
Sampling stages completed:
  - sparse structure (proj), 12 steps
  - shape SLat (proj), 12 steps
  - HR shape SLat (proj, 1024), 12 steps
  - texture SLat (proj), 12 steps
GLB extraction completed all 6 phases:
  - Building BVH
  - Cleaning mesh
  - Parameterizing new mesh
  - Sampling attributes
  - Finalizing mesh
```

GLB validation:

```text
file:
  both successful outputs are glTF binary model, version 2

trimesh:
  both outputs opened as trimesh.Scene with 1 geometry
```

Warnings seen but not blockers:

```text
transformers/timm emitted FutureWarning messages for old timm import paths.
cumesh emitted a torch.cross deprecation warning during remeshing.
Hugging Face warned that remote code files were downloaded for BiRefNet/RMBG.
Module implementation should pin revisions where practical when using
trust_remote_code-style model code.
```

MoGe auto camera validation:

```text
MoGeModel.from_pretrained("Ruicheng/moge-2-vitl") loaded on CUDA.
get_camera_params_wild_moge(...) returned:
camera_angle_x=0.4432972744377575
distance=2.218759536743164
mesh_scale=1.0
```

Dependency corrections discovered during validation:

```text
CUDA 13 works for this stack:
torch==2.11.0+cu130, torchvision==0.26.0+cu130, natten==0.21.6+torch2110cu130,
flash_attn==2.8.3, CUDA Toolkit 13.0, RTX 4080 SUPER.

transformers==5.5.4 fails Pixal3D DINO extraction:
AttributeError: 'DINOv3ViTModel' object has no attribute 'layer'

transformers==4.57.3 works and exposes DINOv3ViTModel.layer.

The README-level `natten==0.21.0` is not the validated CUDA 13 target. Use the
Torch/CUDA-matched NATTEN wheel from https://whl.natten.org.

Pixal3D README utils3d-0.0.2 wheel works for manual-FOV generation but fails
MoGe auto camera:
ModuleNotFoundError: No module named 'utils3d.pt'

MoGe's pinned utils3d commit works:
git+https://github.com/EasternJournalist/utils3d.git@3fab839f0be9931dac7c8488eb0e1600c236e183

Do not launch upstream `app.py` unmodified for the Nymph service. Its `__main__`
block force-reinstalls the Pixal3D README `utils3d-0.0.2` wheel, which can undo
the MoGe-compatible `utils3d` install and break auto camera estimation. Build the
Nymph API server around `inference.py`/pipeline calls instead.
```

Implementation lessons learned:

```text
- Treat CUDA 13 as validated, not speculative, for the exact package set above.
- Treat flash-attn as required. Do not add a silent SDPA fallback in Nymph.
- Fetch Models must explicitly handle BRIA RMBG gated access. Token plus accepted
  HF form is required; token alone can still fail.
- The module details page must explain Pixal3D academic-only/non-commercial,
  no-production, not-for-EU license language and link directly to the BRIA form.
- Use an acknowledgement gate before downloading Pixal3D/RMBG weights; do not
  call it a waiver.
- Pin Transformers to 4.57.3 until Pixal3D updates its DINO extraction code for
  newer Transformers model internals.
- Use MoGe's pinned utils3d commit for auto camera. If using the Pixal3D README
  utils3d wheel, expect only manual-FOV validation to work.
- Do not base Pixal3D on the TRELLIS GGUF loader. It needs the TRELLIS.2-style
  native/runtime surface but loads TencentARC/Pixal3D checkpoints directly.
- Keep NAF prefetch in the fetch path; first use downloads the GitHub repo and
  naf_release.pth into the torch hub cache.
- Standard 1536 and Blender texture import remain unvalidated; keep low_vram
  1024 as the default first profile.
```

## Risks And Open Questions

- VRAM: upstream standard mode is likely too heavy for a 16 GB default; keep
  low-VRAM default until tested.
- Model size: Pixal3D ckpts are around 24 GB before auxiliary models.
- Dependency friction: MoGe and Pixal3D disagree in docs around `utils3d`. For
  auto camera support, use MoGe's pinned git commit or add an explicit
  `utils3d.pt` compatibility shim. Prefer a separate Pixal3D venv/module root.
- `flash_attn`: upstream defaults `ATTN_BACKEND=flash_attn`. Although upstream
  documents `ATTN_BACKEND=sdpa` as a fallback, this Nymph module should hard
  require flash-attn and fail fast if import or CUDA kernel smoke fails.
- NAF: confirm whether `torch.hub.load("valeoai/NAF", "naf", ...)` needs a
  separate prefetch path for offline/managed installs.
- Blender GLB textures: test `extension_webp=True` vs `False`.
- Retexture: do not promise selected-mesh retexture until implemented and tested.
- Registry: publish module repo first, then add `pixal3d` entry to
  `NymphsModules/nymphs-registry/nymphs.json`.

## Recommended First Implementation Order

1. Create `NymphsModules/pixal3d` from the TRELLIS module shape.
2. Build `install_pixal3d.sh` with separate venv and Pixal3D requirements.
3. Build `pixal3d_fetch_models.sh` for `TencentARC/Pixal3D` and auxiliary model
   caches.
4. Build `api_server_pixal3d.py` using upstream `inference.py` logic but with a
   persistent process and Nymph-compatible `/server_info`, `/active_task`, and
   `/generate`.
5. Validate low-VRAM 1024 generation from curl.
6. Add addon service entry for Pixal3D on port 8096.
7. Add shape runtime selector and Pixal3D-specific payload/UI controls.
8. Test Blender import and texture visibility.
9. Add registry entry after module repo is pushed and raw `nymph.json` opens.

## Deep Dive Addendum: Nymphs Backbone Contracts

This addendum is based on a code read of the current Manager, module examples,
and Blender addon, plus a fresh clone of `TencentARC/Pixal3D` at:

```text
6a4b9f3787e3692257604937cd45cb54e80ac9a0
6a4b9f3 2026-05-16 fix: align inference.py GLB export params with app.py (texture_size=4096, decimation=1M)
```

The main architectural conclusion:

```text
Pixal3D should be its own Nymph module and its own Blender service.
Do not bolt it onto the TRELLIS GGUF quant switch.
Do not require the existing TRELLIS GGUF module to be installed.
```

Reason:

- The Manager can discover and run any module from `nymph.json` plus lifecycle
  scripts.
- The addon hardcodes service keys, backend labels, ports, paths, and payload
  behavior.
- Pixal3D shares TRELLIS.2 code lineage, but it is not a GGUF backend and it
  does not load `Aero-Ex/Trellis2-GGUF`.
- Pixal3D upstream assumes a TRELLIS.2 base environment for native packages such
  as `o_voxel`, `cumesh`, `flex_gemm`, `nvdiffrast`, and attention/sparse
  kernels. The Nymph module must own that install recipe instead of assuming
  some other module's venv happens to exist.

Docs audited for the module standard:

```text
NymphsCore/docs/NYMPHS_MODULE_MAKING_GUIDE.md
NymphsCore/docs/NYMPH_MODULE_UI_STANDARD.md
```

Relevant doc rules applied here:

- The Manager owns the shell, registry, installed/available grouping, and
  standard lifecycle rail.
- The module owns install/update/uninstall/status/start/stop/open/logs,
  dependency setup, model fetch, custom actions, and runtime/data folders.
- Model fetch must be a module-owned action, preferably a native
  `ui.manager_action_groups` form.
- Generated outputs, logs, config, and reusable caches should live under
  `$HOME/NymphsData`.
- `Delete Data` must not blindly wipe shared caches like Hugging Face cache.
- Custom `ui/manager.html` is optional. Do not build one just to choose model
  downloads; native action groups are the intended fit.
- Status must be fast, timeout-safe, and safe when files are missing.

## Manager Backbone Findings

Manager manifest model:

```text
NymphModuleManifestInfo
  Id
  Name
  ShortName
  Category
  Kind
  Version
  Description
  OverviewDetail
  OverviewLinks
  ManifestUrl
  RepositoryUrl
  SourceSummary
  InstallRoot
  InstallFields
  InstallOptionsTitle
  ManagerUiTitle
  Capabilities
  ManagerActions
  ManagerActionGroups
  DevCapabilities
  SortOrder
```

The Manager reads:

- `install.root` or `install.path`, falling back to `runtime.install_root`.
- `install.fields` using the same field parser as action groups.
- `entrypoints` for standard lifecycle actions.
- `ui.manager_actions` for native buttons.
- `ui.manager_action_groups` for action forms such as model fetch.
- `ui.manager_ui` only when a custom installed UI is provided.

The Manager does not need hardcoded C# changes for Pixal3D if the module follows
the normal manifest/script contract. The Blender addon does need changes because
it has fixed service keys: `n2d2` and `trellis`.

## Manager Action Resolution

After install, Manager prefers the installed conventional script path:

```text
$INSTALL_ROOT/scripts/<module_id>_<action>.sh
```

For `pixal3d`, that means:

```text
$HOME/Pixal3D/scripts/pixal3d_status.sh
$HOME/Pixal3D/scripts/pixal3d_start.sh
$HOME/Pixal3D/scripts/pixal3d_stop.sh
$HOME/Pixal3D/scripts/pixal3d_logs.sh
...
```

If a conventional script is missing, Manager can resolve entrypoints from
`$INSTALL_ROOT/nymph.json` or the cached repo manifest. For a robust module, keep
both:

- conventional script names
- explicit `entrypoints` in `nymph.json`

Status actions are wrapped with a short timeout:

```text
Manager status timeout: 8 seconds overall
shell action status timeout prefix: timeout 6s
```

Therefore `pixal3d_status.sh` must not import torch, instantiate Pixal3D, scan
huge HF caches deeply, or touch the network. It should check marker files,
import readiness with a short lightweight Python command only after the venv is
present, and report `models_ready=false` quickly if cache files are missing.

## Status Output Contract

Manager parses only `key=value` lines. Important keys are:

```text
id
name
installed
runtime_present
data_present
version
env_ready
adapter_ready
runtime_ready
models_ready
running
state
health
url
install_root
venv
logs_dir
outputs_dir
config_dir
hf_cache_dir
marker
detail
```

Recommended Pixal3D status output:

```text
id=pixal3d
name=Pixal3D
installed=true
runtime_present=true
data_present=false
version=0.1.0
env_ready=true
adapter_ready=true
runtime_ready=true
models_ready=false
aux_models_ready=false
running=false
state=needs_attention
health=model-download-needed
url=http://127.0.0.1:8096
install_root=/home/nymph/Pixal3D
venv=/home/nymph/Pixal3D/.venv
logs_dir=/home/nymph/NymphsData/logs/pixal3d
outputs_dir=/home/nymph/NymphsData/outputs/pixal3d
config_dir=/home/nymph/NymphsData/config/pixal3d
hf_cache_dir=/home/nymph/NymphsData/cache/huggingface
torch_hub_dir=/home/nymph/NymphsData/cache/torch-hub
model_repo=TencentARC/Pixal3D
aux_repos=Ruicheng/moge-2-vitl,camenduru/dinov3-vitl16-pretrain-lvd1689m,briaai/RMBG-2.0,valeoai/NAF
profile=low_vram_1024
marker=/home/nymph/Pixal3D/.nymph-module-version
detail=Runtime exists, but Pixal3D model files are incomplete. Use Fetch Models.
```

Manager UI behavior to rely on:

- `models_ready=false` becomes "Model download needed".
- `runtime_present=true` and `data_present=true` show secondary details.
- `url` appears in module secondary status when installed.
- `health=model-download-needed` or `health=degraded` makes the card require
  attention.
- `running=true` makes the card green.

## Pixal3D `nymph.json` Blueprint

Recommended identity:

```json
{
  "manifest_version": 1,
  "id": "pixal3d",
  "name": "Pixal3D",
  "short_name": "PX",
  "version": "0.1.0",
  "description": "Local Pixal3D image-to-3D backend packaged as an installable Nymph module.",
  "category": "3d",
  "packaging": "repo"
}
```

Recommended install/runtime roots:

```json
{
  "install": {
    "title": "PIXAL3D RUNTIME OPTIONS",
    "root": "$HOME/Pixal3D",
    "entrypoint": "scripts/install_pixal3d.sh",
    "version_marker": "$HOME/Pixal3D/.nymph-module-version",
    "installed_markers": [
      "$HOME/Pixal3D/.nymph-module-version"
    ]
  },
  "runtime": {
    "host": "127.0.0.1",
    "port": 8096,
    "health_url": "http://127.0.0.1:8096/health",
    "server_info_url": "http://127.0.0.1:8096/server_info"
  }
}
```

Recommended artifacts:

```json
{
  "artifacts": {
    "models_root": "$HOME/NymphsData/models",
    "cache_root": "$HOME/NymphsData/cache",
    "outputs_root": "$HOME/NymphsData/outputs/pixal3d",
    "logs_root": "$HOME/NymphsData/logs/pixal3d",
    "config_root": "$HOME/NymphsData/config/pixal3d",
    "huggingface_cache": "$HOME/NymphsData/cache/huggingface",
    "torch_hub": "$HOME/NymphsData/cache/torch-hub",
    "legacy_outputs_root": "$HOME/Pixal3D/outputs",
    "legacy_logs_root": "$HOME/Pixal3D/logs"
  }
}
```

Recommended entrypoints:

```json
{
  "entrypoints": {
    "install": "scripts/install_pixal3d.sh",
    "update": "scripts/pixal3d_update.sh",
    "status": "scripts/pixal3d_status.sh",
    "start": "scripts/pixal3d_start.sh",
    "stop": "scripts/pixal3d_stop.sh",
    "open": "scripts/pixal3d_open.sh",
    "logs": "scripts/pixal3d_logs.sh",
    "fetch_models": "scripts/pixal3d_fetch_models.sh",
    "smoke_test": "scripts/pixal3d_smoke_test.sh",
    "uninstall": "scripts/pixal3d_uninstall.sh",
    "open_weights": "scripts/pixal3d_open_weights.sh",
    "open_outputs": "scripts/pixal3d_open_outputs.sh"
  }
}
```

Recommended Manager actions:

```json
{
  "ui": {
    "sort_order": 45,
    "manager_action_groups": [
      {
        "id": "model_fetch",
        "title": "Model Fetch",
        "layout": "compact",
        "entrypoint": "fetch_models",
        "result": "show_logs",
        "visibility": "installed",
        "description": "Install sets up the Pixal3D runtime only. Fetch Models downloads TencentARC/Pixal3D and auxiliary camera/background/projection models. This does not download TRELLIS GGUF quants.",
        "fields": [
          {
            "name": "profile",
            "type": "select",
            "label": "Profile",
            "arg": "--profile",
            "default": "low_vram_1024",
            "options": [
              {
                "label": "Low VRAM 1024",
                "value": "low_vram_1024",
                "description": "Recommended first test; lower peak VRAM and 1024 cascade"
              },
              {
                "label": "Standard 1536",
                "value": "standard_1536",
                "description": "Heavier profile for high VRAM systems"
              }
            ]
          },
          {
            "name": "hf_token",
            "type": "secret",
            "label": "Hugging Face token",
            "secret_id": "huggingface.token",
            "env": "NYMPHS3D_HF_TOKEN",
            "optional": true
          },
          {
            "name": "license_ack",
            "type": "select",
            "label": "License/access acknowledgement",
            "arg": "--license-ack",
            "default": "no",
            "options": [
              {
                "label": "Not yet",
                "value": "no",
                "description": "Do not fetch gated/non-commercial Pixal3D assets yet"
              },
              {
                "label": "I acknowledge",
                "value": "yes",
                "description": "I understand Pixal3D is academic-only, not for commercial/production use, not intended for EU use, and BRIA RMBG requires its HF access form"
              }
            ]
          }
        ],
        "submit": {
          "label": "Fetch Models"
        }
      }
    ]
  }
}
```

## Module Directory Layout

Recommended first repo layout:

```text
NymphsModules/pixal3d/
  nymph.json
  README.md
  docs/
    PIXAL3D_MODULE_NOTES.md
  scripts/
    _pixal3d_common.sh
    install_pixal3d.sh
    pixal3d_update.sh
    pixal3d_status.sh
    pixal3d_start.sh
    pixal3d_stop.sh
    pixal3d_logs.sh
    pixal3d_open.sh
    pixal3d_open_outputs.sh
    pixal3d_open_weights.sh
    pixal3d_fetch_models.sh
    pixal3d_smoke_test.sh
    pixal3d_uninstall.sh
    api_server_pixal3d.py
    pixal3d_model_common.py
  patches/
    README.md
```

If we vendor/derive from upstream Pixal3D directly, the installed root should
contain upstream code plus Nymph scripts:

```text
$HOME/Pixal3D/
  pixal3d/
  assets/
  inference.py
  app.py
  requirements.txt
  scripts/
  .venv/
  .nymph-module-version
```

## `_pixal3d_common.sh` Blueprint

Core variables:

```bash
PIXAL3D_INSTALL_ROOT="${PIXAL3D_INSTALL_ROOT:-${NYMPHS3D_PIXAL3D_DIR:-$HOME/Pixal3D}}"
PIXAL3D_VENV_DIR="${PIXAL3D_VENV_DIR:-$PIXAL3D_INSTALL_ROOT/.venv}"
NYMPHS_DATA_ROOT="${NYMPHS_DATA_ROOT:-$HOME/NymphsData}"
PIXAL3D_CONFIG_DIR="${PIXAL3D_CONFIG_DIR:-$NYMPHS_DATA_ROOT/config/pixal3d}"
PIXAL3D_PRESET_FILE="${PIXAL3D_PRESET_FILE:-$PIXAL3D_CONFIG_DIR/profile.env}"
PIXAL3D_OUTPUT_DIR="${PIXAL3D_OUTPUT_DIR:-$NYMPHS_DATA_ROOT/outputs/pixal3d}"
PIXAL3D_LOG_DIR="${PIXAL3D_LOG_DIR:-$NYMPHS_DATA_ROOT/logs/pixal3d}"
PIXAL3D_PID_FILE="${PIXAL3D_PID_FILE:-$PIXAL3D_LOG_DIR/pixal3d.pid}"
PIXAL3D_HOST="${PIXAL3D_HOST:-127.0.0.1}"
PIXAL3D_PORT="${PIXAL3D_PORT:-8096}"
PIXAL3D_SERVER_URL="${PIXAL3D_SERVER_URL:-http://${PIXAL3D_HOST}:${PIXAL3D_PORT}}"
PIXAL3D_MODEL_REPO_ID="${PIXAL3D_MODEL_REPO_ID:-TencentARC/Pixal3D}"
PIXAL3D_MOGE_REPO_ID="${PIXAL3D_MOGE_REPO_ID:-Ruicheng/moge-2-vitl}"
PIXAL3D_DINOV3_REPO_ID="${PIXAL3D_DINOV3_REPO_ID:-camenduru/dinov3-vitl16-pretrain-lvd1689m}"
PIXAL3D_RMBG_REPO_ID="${PIXAL3D_RMBG_REPO_ID:-briaai/RMBG-2.0}"
```

Environment exports:

```bash
export OPENCV_IO_ENABLE_OPENEXR="${OPENCV_IO_ENABLE_OPENEXR:-1}"
export PYTORCH_CUDA_ALLOC_CONF="${PYTORCH_CUDA_ALLOC_CONF:-expandable_segments:True}"
export HF_HUB_DISABLE_XET="${HF_HUB_DISABLE_XET:-1}"
export NYMPHS3D_HF_CACHE_DIR="${NYMPHS3D_HF_CACHE_DIR:-$NYMPHS_DATA_ROOT/cache/huggingface}"
export HF_HOME="${HF_HOME:-$NYMPHS_DATA_ROOT/cache/huggingface-home}"
export HF_HUB_CACHE="${HF_HUB_CACHE:-$NYMPHS3D_HF_CACHE_DIR}"
export TORCH_HOME="${TORCH_HOME:-$NYMPHS_DATA_ROOT/cache/torch-hub}"
export PIXAL3D_OUTPUT_DIR
export PIXAL3D_MODEL_REPO_ID
```

Functions should mirror TRELLIS:

```bash
pixal3d_ensure_data_dirs
pixal3d_python
pixal3d_pip
pixal3d_is_running
pixal3d_probe_url
pixal3d_site_packages_dir
```

## Install Script Design

The install script should be conservative and separate from TRELLIS GGUF:

```text
1. Install system packages:
   python3.10, python3.10-venv, python3.10-dev, git, curl, cmake,
   build-essential, pkg-config, libegl1-mesa-dev, libgl1, libglib2.0-0,
   ccache, ninja.

2. Sync module/upstream source into $HOME/Pixal3D.

3. Create $HOME/Pixal3D/.venv with Python 3.10.

4. Install/upgrade pip, setuptools, wheel, ninja.

5. Install PyTorch. Start with the same torch policy as TRELLIS module unless
   Pixal3D dependency testing proves a stricter pin is required.

6. Install base TRELLIS/Pixal3D Python deps:
   imageio, imageio-ffmpeg, tqdm, easydict, opencv-python-headless, trimesh,
   transformers, gradio, tensorboard, pandas, lpips, zstandard, kornia, timm,
   psutil, plyfile, diffusers==0.37.1, accelerate==1.13.0.

7. Install Pixal3D extra deps:
   git+https://github.com/microsoft/MoGe.git
   natten==0.21.0
   https://github.com/LDYang694/Storages/releases/download/20260430/utils3d-0.0.2-py3-none-any.whl

8. Install native TRELLIS-style runtime pieces:
   flash-attn
   CuMesh
   FlexGEMM
   nvdiffrast
   o-voxel

9. Compile-check scripts/api_server_pixal3d.py.

10. Write .nymph-module-version.
```

Critical install decision:

```text
Do not rely on $HOME/TRELLIS.2/.venv.
Do not import packages from the TRELLIS GGUF module.
```

Why: module installs should be independently repairable and uninstallable. Also,
Pixal3D may need different versions of `natten`, `MoGe`, `diffusers`, and DINOv3
dependencies than the GGUF adapter.

Native package source choices:

- Best first pass: reuse the TRELLIS module's pinned install strategy for
  `CuMesh`, `FlexGEMM`, `nvdiffrast`, `utils3d`, and `o-voxel`.
- Investigate whether upstream Pixal3D works with prebuilt wheels from
  `requirements-hfdemo.txt`, but do not make H-series demo wheels the default
  for local Nymph installs. They are documented upstream as HF demo specific.
- Do not expose `ATTN_BACKEND=sdpa` as an install/runtime fallback for this
  module. The module should set/require `ATTN_BACKEND=flash_attn` and fail fast
  if flash-attn is unavailable.

## Model Fetch Design

Fetch script should download all model files into shared Nymph cache roots, not
inside disposable runtime directories:

```text
$HOME/NymphsData/cache/huggingface
$HOME/NymphsData/cache/huggingface-home
$HOME/NymphsData/cache/torch-hub
```

Required main repo:

```text
TencentARC/Pixal3D
```

Allow patterns for snapshot download:

```text
pipeline.json
ckpts/*.json
ckpts/*.safetensors
README.md
```

Auxiliary repos:

```text
Ruicheng/moge-2-vitl
camenduru/dinov3-vitl16-pretrain-lvd1689m
briaai/RMBG-2.0
valeoai/NAF via torch.hub
```

Fetch script should emit progress lines similar to TRELLIS:

```text
MODEL FETCH STARTED: step=1/5 repo=TencentARC/Pixal3D
MODEL FETCH STATUS: step=1/5 status=downloading cache_total=...
MODEL FETCH COMPLETE: step=1/5 repo=TencentARC/Pixal3D root=...
```

Recommended steps:

```text
1. Main Pixal3D ckpts.
2. DINOv3 model.
3. MoGe model.
4. RMBG model.
5. NAF torch hub prefetch.
```

RMBG fetch must handle the gated-model case explicitly. Suggested behavior:

```text
MODEL FETCH NEEDS ACCESS: repo=briaai/RMBG-2.0
Open https://huggingface.co/briaai/RMBG-2.0, complete BRIA's access form,
then rerun Fetch Models with a Hugging Face token from the same account.
```

Fetch should also refuse before any download unless the Manager action passes
`--license-ack yes`:

```text
LICENSE ACK REQUIRED:
Pixal3D permits academic use only, forbids commercial/production use, and says
it is not intended for use within the European Union.
BRIA RMBG-2.0 requires the Hugging Face access form:
https://huggingface.co/briaai/RMBG-2.0
Select "I acknowledge" in Fetch Models before downloading gated/non-commercial
weights.
```

NAF prefetch can be implemented by running a small Python snippet in the venv:

```python
import os
import torch
os.environ.setdefault("TORCH_HOME", os.path.expanduser("~/NymphsData/cache/torch-hub"))
torch.hub.load("valeoai/NAF", "naf", pretrained=True, device="cpu", trust_repo=True)
```

Status should not require the NAF hub call. The fetch/smoke test can verify it.

## API Server Blueprint

File:

```text
scripts/api_server_pixal3d.py
```

Required endpoints:

```text
GET  /health
GET  /server_info
GET  /active_task
POST /generate
```

Optional helpful endpoints:

```text
GET /outputs
GET /config
POST /reload
```

`/server_info` should return:

```json
{
  "status": "ok",
  "backend": "Pixal3D",
  "model_path": "TencentARC/Pixal3D",
  "subfolder": "",
  "device": "cuda",
  "dtype": "bf16/fp16 mixed",
  "enable_tex": true,
  "mesh_retexture": false,
  "texture_only": false,
  "low_vram": true,
  "resolution": 1024,
  "supported_resolutions": [1024, 1536],
  "attention_backend": "flash_attn",
  "model_cache_ready": true,
  "aux_models_ready": true,
  "runtime_distro": "NymphsCore",
  "output_dir": "/home/nymph/NymphsData/outputs/pixal3d"
}
```

`/active_task` should match addon expectations:

```json
{
  "id": "uuid-or-empty",
  "status": "idle|queued|processing|complete|failed",
  "stage": "Sampling HR shape SLat",
  "detail": "Pixal3D 1024 cascade",
  "progress": "3/5",
  "error": ""
}
```

The server should keep one active task at a time. Reject concurrent `/generate`
with HTTP 409 or queue exactly one request only if the addon already handles
queued state.

## API Generate Mapping

Accept current TRELLIS-style shape payloads so the addon patch can be smaller:

```text
image
seed
pipeline_type
max_num_tokens
texture_size
decimation_target
ss_sampling_steps
ss_guidance_strength
ss_guidance_rescale
ss_rescale_t
shape_sampling_steps
shape_guidance_strength
shape_guidance_rescale
shape_rescale_t
tex_sampling_steps
tex_guidance_strength
tex_guidance_rescale
tex_rescale_t
```

Map to Pixal3D names:

```text
shape_sampling_steps        -> shape_slat_sampling_steps
shape_guidance_strength     -> shape_slat_guidance_strength
shape_guidance_rescale      -> shape_slat_guidance_rescale
shape_rescale_t             -> shape_slat_rescale_t
tex_sampling_steps          -> tex_slat_sampling_steps
tex_guidance_strength       -> tex_slat_guidance_strength
tex_guidance_rescale        -> tex_slat_guidance_rescale
tex_rescale_t               -> tex_slat_rescale_t
pipeline_type=1024_cascade  -> resolution=1024
pipeline_type=1536_cascade  -> resolution=1536
```

Add Pixal3D-specific fields:

```text
pixal3d_low_vram
pixal3d_resolution
pixal3d_manual_fov
pixal3d_fov_unit
pixal3d_mesh_scale
pixal3d_extend_pixel
pixal3d_image_resolution
```

Ignore unsupported TRELLIS GGUF-only fields:

```text
gguf_quant
foreground_ratio
sparse_structure_resolution
sampler
sparse_structure_sampler
shape_sampler
tex_sampler
texture_alpha_mode
texture_double_sided
texture_bake_vertices
texture_custom_normals
texture_uv_method
texture_uv_angle
texture_inpainting
```

Return raw GLB bytes from `/generate`:

```text
HTTP 200
Content-Type: model/gltf-binary
Body: <binary .glb bytes>
```

The current Blender addon shape worker treats JSON from `/generate` as an
error, writes response bytes to a local `.glb`, and imports that file. A JSON
path/base64 response would require changing `_job_worker`, so do not use JSON as
the v1 generate response.

## Pixal3D Runtime Flow

Use upstream `inference.py` logic but refactor for a long-lived process:

```text
1. Decode incoming base64 image to a temp file or PIL image.
2. Initialize pipeline lazily on first request or at startup, depending on CLI.
3. Preprocess image with Pixal3D RMBG.
4. Estimate camera with MoGe unless manual FOV is provided.
5. Build sampler override dictionaries.
6. Run `pipeline.run(..., return_latent=True, pipeline_type="1024_cascade" or "1536_cascade")`.
7. Convert to GLB with `o_voxel.postprocess.to_glb`.
8. Apply upstream rotation transform.
9. Export GLB to `$PIXAL3D_OUTPUT_DIR` for logs/reuse.
10. Return the GLB file bytes as `model/gltf-binary`.
```

Default runtime profile:

```text
low_vram=true
resolution=1024
manual_fov=-1
mesh_scale=1.0
extend_pixel=0
image_resolution=512
max_num_tokens=49152
decimation_target=1000000
texture_size=4096 upstream, but test 2048/4096 in Blender
```

For Blender compatibility, test:

```text
glb.export(..., extension_webp=True)
glb.export(..., extension_webp=False)
```

If Blender imports geometry but not textures with WebP, default the Nymph API to
`extension_webp=False` even though upstream uses `True`.

## Blender Addon Patch Map

File:

```text
NymphsAddon/Nymphs.py
```

Add constants near current TRELLIS constants:

```python
DEFAULT_REPO_PIXAL3D_PATH = "~/Pixal3D"
DEFAULT_PIXAL3D_PYTHON_PATH = "~/Pixal3D/.venv/bin/python"
DEFAULT_PIXAL3D_PORT = "8096"
DEFAULT_PIXAL3D_RESOLUTION = "1024"
```

Extend service maps:

```python
SERVICE_LABELS = {
    "n2d2": "Z-Image",
    "trellis": "TRELLIS.2 GGUF",
    "pixal3d": "Pixal3D",
}

SERVICE_PROP_PREFIXES = {
    "n2d2": "service_n2d2",
    "trellis": "service_trellis",
    "pixal3d": "service_pixal3d",
}

SERVICE_ORDER = ("n2d2", "trellis", "pixal3d")
```

Add a 3D runtime selector instead of hardcoded `_selected_3d_service_key`:

```python
shape_3d_runtime: EnumProperty(
    name="Shape Runtime",
    items=(
        ("trellis", "TRELLIS.2 GGUF", "Use the quantized TRELLIS.2 GGUF backend"),
        ("pixal3d", "Pixal3D", "Use Pixal3D camera-aware pixel-projection backend"),
    ),
    default="trellis",
)
```

Then:

```python
def _selected_3d_service_key(state):
    value = getattr(state, "shape_3d_runtime", "trellis")
    return value if value in {"trellis", "pixal3d"} else "trellis"
```

Keep selected-mesh retexture on TRELLIS:

```python
def _selected_texture_service_key(state):
    return "trellis"
```

Add Pixal3D service state fields:

```python
service_pixal3d_enabled
service_pixal3d_port
service_pixal3d_show
service_pixal3d_launch_state
service_pixal3d_launch_detail
service_pixal3d_backend_summary
repo_pixal3d_path
pixal3d_python_path
pixal3d_low_vram
pixal3d_resolution
pixal3d_manual_fov
pixal3d_mesh_scale
pixal3d_extend_pixel
pixal3d_image_resolution
```

Extend `_service_port` default selection:

```python
if service_key == "pixal3d":
    default = DEFAULT_PIXAL3D_PORT
elif service_key == "trellis":
    default = DEFAULT_TRELLIS_PORT
else:
    default = DEFAULT_N2D2_PORT
```

Extend `_compose_wsl_launch` with a Pixal3D branch:

```text
repo_path: state.repo_pixal3d_path or ~/Pixal3D
python_path: state.pixal3d_python_path or ~/Pixal3D/.venv/bin/python
script: scripts/api_server_pixal3d.py
env:
  PIXAL3D_OUTPUT_DIR=$HOME/NymphsData/outputs/pixal3d
  PIXAL3D_LOG_DIR=$HOME/NymphsData/logs/pixal3d
  PIXAL3D_CONFIG_DIR=$HOME/NymphsData/config/pixal3d
  PIXAL3D_PID_FILE=$HOME/NymphsData/logs/pixal3d/pixal3d.pid
  PIXAL3D_PORT=<port>
  PIXAL3D_HOST=0.0.0.0
  TORCH_HOME=$HOME/NymphsData/cache/torch-hub
args:
  --host 0.0.0.0
  --port <port>
  --model-path TencentARC/Pixal3D
  --resolution 1024|1536
  --low-vram or --no-low-vram
```

Extend `_stop_shell_for_service`:

```text
pkill -f "scripts/api_server_pixal3d.py --host 0.0.0.0 --port <port>"
pkill -f "python scripts/api_server_pixal3d.py"
pkill -f "python3 scripts/api_server_pixal3d.py"
```

Extend backend family parsing:

```python
if backend_name == "pixal3d":
    return "Pixal3D"
```

Do this before fallback blob checks that might see "trellis" in a config path.

Extend capabilities:

```python
if family == "Pixal3D":
    return {
        "family": "Pixal3D",
        "shape": not texture_only,
        "texture": bool(info.get("enable_tex", True)),
        "retexture": bool(info.get("mesh_retexture", False)),
        "multiview": False,
        "text": False,
        "texture_only": texture_only,
    }
```

Extend server summary:

```text
Pixal3D | TencentARC/Pixal3D | low_vram=true | resolution=1024 | attn=flash_attn | shape=true | tex=true | retexture=false
```

## Addon UI Gating

Current shape panel draws `trellis_gguf_quant` before any workflow controls.
For Pixal3D:

- Show shape runtime selector first.
- If selected service is TRELLIS, show GGUF Quant.
- If selected service is Pixal3D, hide GGUF Quant.
- Draw service control row for the selected service.
- Reuse image path, seed, shape/texture checkbox, and sampler step controls
  where compatible.
- Hide TRELLIS-only material/UV/inpainting options for Pixal3D.
- Show Pixal3D camera controls near shape settings:
  - low VRAM toggle
  - resolution enum
  - manual FOV float; `-1` means auto MoGe
  - mesh scale
  - extend pixel
  - image resolution

Current texture panel should remain TRELLIS-only for first pass:

- Leave `_selected_texture_service_key(state)` returning `"trellis"`.
- Keep GGUF Quant in texture panel.
- Do not show Pixal3D there until selected-mesh retexture is actually
  implemented.

## Backbone Compatibility Answer

The precise answer to the backbone/runtime question:

```text
Pixal3D should not be built against the GGUF adapter runtime as its base. It
needs a TRELLIS.2-style code/native runtime surface.

The current TRELLIS GGUF fork is only usable as a base if it still preserves the
original TRELLIS.2/Pixal3D-compatible imports, native extensions, and pipeline
layout Pixal3D expects. If the fork is centered around `trellis2_gguf` loaders
and GGUF-specific model layout, treat it as incompatible for Pixal3D.

The regular released microsoft/TRELLIS.2-4B model weights do not appear to be a
separate Pixal3D inference dependency. Pixal3D inference loads
TencentARC/Pixal3D checkpoints.
```

More detail:

- `TencentARC/Pixal3D` provides its own safetensors checkpoint set.
- Its HF `pipeline.json` references `ckpts/...` under `TencentARC/Pixal3D`.
- The upstream inference path calls
  `Pixal3DImageTo3DPipeline.from_pretrained("TencentARC/Pixal3D")`.
- The current Nymph TRELLIS module loads
  `trellis2_gguf.pipelines.Trellis2ImageTo3DPipeline.from_pretrained(..., enable_gguf=True, gguf_quant=...)`.
- These are different loaders and weight formats.

## Acceptance Criteria

Module acceptance:

- `pixal3d_status.sh` completes under Manager status timeout.
- Install creates `$HOME/Pixal3D/.venv` and writes `.nymph-module-version`.
- Status reports `models_ready=false` before model fetch, without crashing.
- Fetch Models downloads main and auxiliary models into `NymphsData` caches.
- Start launches `http://127.0.0.1:8096`.
- `/server_info` returns `backend=Pixal3D`.
- `/active_task` returns idle/processing states compatible with addon polling.
- `/generate` accepts addon shape payload and returns a textured GLB.
- `pixal3d_stop.sh` stops the API process and cleans stale PID state.
- Uninstall preserves outputs/logs/cache by default.

Addon acceptance:

- Nymphs Server panel shows Pixal3D as a third service.
- Shape panel can select TRELLIS.2 GGUF or Pixal3D.
- TRELLIS behavior is unchanged when TRELLIS is selected.
- Pixal3D selection hides GGUF Quant and launches port 8096.
- Pixal3D generation imports a GLB into Blender.
- Texture panel still uses TRELLIS for selected-mesh retexture.

Research spike acceptance:

- Verify `natten==0.21.0` with the chosen Torch/CUDA stack.
- Verify `o_voxel`, `cumesh`, `flex_gemm`, and `nvdiffrast` build in the
  Pixal3D venv.
- Verify `ATTN_BACKEND=flash_attn` is active and a tiny flash-attn CUDA kernel
  smoke passes. Do not accept SDPA fallback for Pixal3D.
- Verify WebP vs non-WebP GLB texture import in target Blender.
- Measure VRAM for low_vram 1024 and standard 1536 on the user's actual GPU.
