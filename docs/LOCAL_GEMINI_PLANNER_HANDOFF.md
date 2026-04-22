# Local Gemini-Style Planner Handoff

This handoff captures the idea of adding an open-source local alternative for the analysis work that Gemini Flash currently performs in the Blender addon, especially character part planning from a master image.

It is not a request to remove Gemini Flash. The recommended product shape is to keep Gemini Flash as the cloud/premium lane, keep Z-Image/Nunchaku as the fast local image-generation lane, and add local vision/edit capabilities as optional lanes.

## Current Gemini Responsibilities

In the addon, Gemini currently does several related but separable jobs:

- image generation through OpenRouter
- optional guide-image generation/editing
- 4-view image generation
- vision planning for character part extraction
- guided extraction image generation for each planned part

The most replaceable piece is the vision planning step.

Current planning flow:

1. The user picks a master character image.
2. The addon builds a strict JSON planning prompt with `_part_extraction_planning_prompt`.
3. `_openrouter_text_from_image` sends the image and prompt to a Gemini planner model.
4. `_part_planning_worker` parses and normalizes the returned JSON.
5. The user reviews the parts.
6. `_part_extraction_worker` sends one guided image request per part through Gemini image generation.

Important distinction:

- A local vision-language model can likely replace steps 1-4.
- It does not by itself replace step 6, because a planner model describes parts but does not generate isolated part images.

## Recommended Architecture

Use a two-model local stack rather than trying to find one model that does everything Gemini does.

```text
Gemini Flash cloud lane
  - keep as-is
  - best quality / least local hardware pressure

Z-Image + Nunchaku local lane
  - keep as-is
  - fast local text-to-image and current NymphsCore image workflow

Local vision planner lane
  - Qwen3-VL or similar
  - analyzes a master image and returns the existing part-plan JSON schema

Local image edit lane
  - Qwen-Image-Edit / HiDream / other edit-capable model
  - optional future replacement for Gemini guided image edits
```

This avoids making Z-Image carry jobs it is not designed for, while preserving Gemini as the known-good option.

## Brain As The GGUF Runtime Layer

The broader architecture idea is that `Nymphs-Brain` could become the optional local model-services layer for GGUF-shaped models.

This would keep the Blender addon and the core backend scripts simple:

```text
Blender addon
  -> asks for a capability: plan parts, edit image, run low-VRAM Trellis, etc.

Backend/runtime services
  -> keep their current native fast paths where they already work well

Nymphs-Brain
  -> owns optional local GGUF model loading and workflow orchestration
```

Why this is attractive:

- the addon should not become a model loader
- Z-Image should stay focused on the fast Nunchaku image lane
- TRELLIS official should stay focused on its current safetensors/PyTorch path
- Gemini should remain available as the reliable cloud lane
- experimental local GGUF models can be added without destabilizing the Lite installer baseline

Important caveat:

GGUF is a file/container format, not one universal runtime. Different GGUF families may need different engines:

- LLM/VLM GGUF: likely `llama.cpp`, LM Studio, Ollama, or compatible OpenAI-style server
- diffusion image GGUF: likely ComfyUI plus `ComfyUI-GGUF` style loaders
- TRELLIS GGUF: likely a specialized ComfyUI/Brain workflow or custom loader, not the current official TRELLIS loader

So the right mental model is not "Brain loads every GGUF with one loader." It is:

```text
Brain owns model profiles, downloads, health checks, and API routing.
The selected Brain runtime plugin loads the specific GGUF family.
```

Possible Brain profiles:

- `brain_vlm_planner`: Qwen3-VL for local character part analysis
- `brain_image_edit`: Qwen-Image-Edit or similar for local guided image edits
- `brain_trellis_gguf`: low-VRAM Trellis GGUF workflow if a reliable compatible workflow is proven

This should stay optional. It should not become part of the Lite default installer until the workflows are proven and the disk/VRAM cost is clear.

## Possible Direction: Capability Profiles And Model Swapping

One larger direction is to give NymphsCore some of ComfyUI's model-swapping flexibility without exposing users to node graphs.

The addon should ideally ask for a capability, not a hardcoded model:

```text
plan_character_parts
edit_image
extract_character_part
generate_image
image_to_3d
texture_mesh
```

Then a profile decides which runtime and model handle that capability.

Example:

```text
capability: plan_character_parts
profile: qwen3_vl_8b_q4
runtime: brain_vlm
```

Another example:

```text
capability: image_to_3d
profile: trellis2_gguf_q4
runtime: brain_comfy
```

This would give advanced users the ability to try different models, like ComfyUI users do, while keeping the public addon workflow simple.

Conceptual profile shape:

```json
{
  "id": "qwen_image_edit_2511_q4",
  "label": "Qwen Image Edit 2511 Q4",
  "capabilities": ["edit_image", "extract_character_part"],
  "runtime": "brain_comfy",
  "workflow": "qwen_image_edit_2511_gguf.json",
  "models": {
    "diffusion": "Qwen-Image-Edit-2511-Q4_K_M.gguf",
    "vae": "qwen_image_vae.safetensors",
    "text_encoder": "qwen_text_encoder.gguf"
  },
  "defaults": {
    "steps": 20,
    "guidance": 4.0
  }
}
```

Recommended first version:

- implement soft hot-swap only
- changing the active profile affects the next request
- the runtime can restart/reload if needed
- do not promise instant live model switching

Avoid at first:

- true live hot-swap across loaded models
- multiple heavy GGUF diffusion models resident at once
- exposing raw graph wiring in the Blender addon
- making Brain model profiles mandatory for the Lite baseline

Why soft hot-swap first:

- it gives most of the user value
- it is much safer for VRAM and RAM
- it works across different runtime families
- it avoids backend-specific unload/reload bugs

Long-term target:

```text
NymphsCore addon
  -> artist-friendly task buttons

Capability/profile layer
  -> selected model or workflow for each task

Runtime layer
  -> native backend, Brain/ComfyUI, Brain/VLM, Gemini, etc.
```

This would let NymphsCore keep its guided UX while allowing experimental users to try new local models without rewriting addon code.

## Candidate Models

### Qwen3-VL-8B-Instruct-GGUF

Best first candidate for the planner role.

Why it fits:

- open vision-language model
- Apache-2.0 license
- GGUF format is compatible with llama.cpp-style local serving
- can inspect an image and produce structured text or JSON
- suitable for "look at this character and list extractable parts" tasks

Expected role:

- replace Gemini for part analysis/planning only
- estimate part categories, names, rough bounding boxes, and extraction prompts
- return the same schema currently consumed by `_normalize_part_plan`

Source:

- https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct-GGUF

### Trellis2 GGUF Variants

Interesting candidate for a future low-VRAM 3D lane, but not a drop-in replacement for the current official TRELLIS backend.

Why it is interesting:

- users report low-VRAM Trellis GGUF workflows in ComfyUI-style stacks
- quantized model pieces may make 6-8 GB GPU testing possible
- it could give Lite users a smaller/lower-pressure 3D option than official TRELLIS.2

Why it is not drop-in:

- the current NymphsCore TRELLIS adapter calls the official TRELLIS.2 Python pipelines
- official TRELLIS.2 loading expects its normal config/model files, not arbitrary GGUF files
- GGUF Trellis likely needs a different loader/workflow graph
- the API may need an adapter shim so the addon can call it like a normal NymphsCore 3D backend

Expected role:

- optional experimental Brain-managed 3D runtime
- not a replacement for official TRELLIS until quality and API compatibility are proven
- best tested as `brain_trellis_gguf` with a clear separate endpoint

Source:

- https://huggingface.co/Aero-Ex/Trellis2-GGUF

### Qwen-Image-Edit-2511-GGUF

Best first candidate for a future local image-edit/extraction lane.

Why it fits:

- Apache-2.0 license
- image-to-image model
- improved character consistency over older Qwen-Image-Edit versions
- supports multi-image edit workflows
- designed for semantic and appearance edits
- GGUF variants make lower-memory testing possible through ComfyUI/Brain-style workflows

Expected role:

- not the planner
- possible local replacement for Gemini's guided per-part image edits
- likely needs a ComfyUI/Brain runtime wrapper rather than direct addon integration

Notes:

- It is still a 20B image model.
- Q4 variants are roughly 12-13 GB model files, so 8 GB GPUs may need CPU/RAM offload and will be slower.
- This should be treated as an optional local edit runtime, not a hard dependency for Lite.

Source:

- https://huggingface.co/unsloth/Qwen-Image-Edit-2511-GGUF
- https://unsloth.ai/docs/new/comfyui

### HiDream-E1.1

Interesting alternative local image editor.

Pros:

- MIT license
- designed for instruction-based image editing
- Diffusers-style integration

Cons:

- adds another heavy runtime path
- requires Flash Attention according to its quick-start docs
- may collide with the already-sensitive TRELLIS flash-attn install story

Source:

- https://huggingface.co/HiDream-ai/HiDream-E1-1

### FLUX.1 Kontext Dev

Technically strong but risky for product distribution.

Pros:

- strong instruction image editing
- good character/object/style reference behavior

Cons:

- non-commercial FLUX dev license
- gated Hugging Face access
- less suitable as a default NymphsCore dependency

Source:

- https://huggingface.co/black-forest-labs/FLUX.1-Kontext-dev

## Prototype Plan

### Phase 1: Local Planner Only

Goal:

- prove that a local VLM can replace Gemini for part analysis.

Do not touch image extraction yet.

Recommended path:

1. Add a small local planner service or Brain endpoint that accepts an image and prompt.
2. Serve Qwen3-VL-8B-Instruct-GGUF through llama.cpp, Ollama, LM Studio, or the existing Brain stack if it already supports vision input.
3. Reuse the current `_part_extraction_planning_prompt` unchanged as much as possible.
4. Return the exact existing JSON schema:

```json
{
  "parts": [
    {
      "id": "short_stable_slug",
      "display_name": "Human readable part name",
      "category": "anatomy_base | hair | clothing | armor | accessory | weapon | prop | face_feature",
      "priority": 1,
      "normalized_bbox": [0.0, 0.0, 1.0, 1.0],
      "extraction_prompt": "Specific instruction for isolating only this part from the master reference image"
    }
  ]
}
```

Addon integration should be hidden or dev-only at first:

- add no new public UI until quality is proven
- expose through an environment variable or debug flag first
- keep Gemini as the default planner

Possible endpoint shape:

```http
POST /v1/vision/part-plan
Content-Type: application/json

{
  "image": "<base64 or data URL>",
  "prompt": "<existing part extraction planning prompt>",
  "max_parts": 8,
  "temperature": 0.1
}
```

Response:

```json
{
  "parts": [...]
}
```

### Phase 2: Compare Against Gemini

Use a small fixed test set before building UI.

Suggested test set:

- simple humanoid character
- stylized/chibi character
- armored character with weapon
- robed/cloaked character
- character with small accessories or pouches
- creature/non-human body shape
- noisy image with busy background

Measure:

- JSON parse success rate
- anatomy_base included when appropriate
- hair included separately
- major clothing/armor included
- weapon/prop included when visible
- no combined multi-item parts
- no scenery/background parts
- useful extraction prompts
- rough bounding boxes are plausible

Acceptance target for first usable prototype:

- 90%+ valid JSON without manual repair
- 95%+ valid JSON after one automatic retry/repair prompt
- does not miss anatomy_base or hair on normal character images
- produces practical 4-8 part plans
- extraction prompts are good enough to feed Gemini or a local edit model

### Phase 3: Optional Local Image Edit Lane

Only after the planner works.

Goal:

- test whether Qwen-Image-Edit-2511-GGUF can produce isolated part images from the master image plus each planned extraction prompt.

Recommended runtime:

- Brain/ComfyUI wrapper rather than direct Blender addon model loading

Why:

- GGUF diffusion workflows need model files in specific folders
- workflows are easier to tune as JSON graphs
- this keeps Blender light and avoids turning the addon into a model runtime

Important:

- Do not remove Gemini extraction.
- Add local extraction as an optional backend after quality is proven.

### Phase 4: Optional Brain Trellis GGUF Lane

Only after the planner and image-edit experiments are separated cleanly.

Goal:

- test whether a Brain-managed Trellis GGUF workflow can provide a low-VRAM 3D path.

Recommended runtime:

- Brain/ComfyUI or another workflow runner that already supports the Trellis GGUF model family

Do not try to force this through the official TRELLIS.2 adapter unless the upstream loader supports those files directly.

Possible endpoint shape:

```http
POST /v1/brain/trellis/image-to-3d
Content-Type: application/json

{
  "image": "<base64 or data URL>",
  "profile": "trellis2_gguf_q4",
  "seed": 1234,
  "quality": "draft|balanced|high"
}
```

Response:

```json
{
  "mesh_path": "/path/to/output.glb",
  "preview_path": "/path/to/preview.png",
  "metadata_path": "/path/to/metadata.json"
}
```

Success criteria:

- produces usable GLB/OBJ output from the same image inputs as the current TRELLIS path
- runs on lower VRAM than official TRELLIS.2
- has predictable output paths and progress logs
- can be started/stopped from Brain without colliding with Z-Image or official TRELLIS
- does not increase the Lite default install footprint unless explicitly selected

## Addon Integration Points

Likely files:

- `Blender/Addon/Nymphs.py`
- `Blender/Addon/README.md`
- `Blender/Addon/docs/USER_GUIDE.md`
- `docs/BLENDER_ADDON_USER_GUIDE.md`

Current functions/classes to inspect:

- `_part_extraction_snapshot`
- `_openrouter_text_from_image`
- `_part_extraction_planning_prompt`
- `_part_planning_worker`
- `_normalize_part_plan`
- `_ensure_required_part_plan_entries`
- `_part_extraction_worker`
- `_gemini_request_image`
- `part_planner_model`
- `NYMPHSV2_OT_plan_character_parts`

Recommended implementation seam:

- introduce a planner provider abstraction rather than hard-wiring more provider logic into `_part_planning_worker`

Sketch:

```python
def _request_part_plan(snapshot, prompt):
    provider = snapshot.get("part_planner_provider") or "gemini"
    if provider == "local_vlm":
        return _local_vlm_text_from_image(snapshot, prompt)
    return _openrouter_text_from_image(
        snapshot["api_key"],
        snapshot["part_planner_model"],
        snapshot["guide_image_data_url"],
        prompt,
    )
```

Keep output normalization shared:

```text
raw provider response
  -> _extract_json_payload
  -> _normalize_part_plan
  -> _ensure_required_part_plan_entries
  -> existing plan JSON state
```

## Installer And Runtime Considerations

Do not add this to the Lite default installer until it is proven.

Reasons:

- Qwen3-VL is another large model family.
- Qwen-Image-Edit is much larger and may need ComfyUI/GGUF runtime dependencies.
- The Lite installer is currently focused on removing Hunyuan and removing the prewarmed tar dependency.
- Adding a new optional local vision stack now would blur the test surface.

Recommended packaging strategy:

- Phase 1: dev-only docs and manual setup
- Phase 2: optional Brain runtime profile
- Phase 3: installer checkbox or advanced option only after stable

Do not bake API tokens or private model credentials into the installer.

## Risks

- Local planner may miss small accessories or layered costume details.
- JSON compliance may need retries and repair.
- Bounding boxes may be approximate.
- Local VLM speed may be poor on lower-end machines.
- Qwen-Image-Edit local extraction may be slower than Gemini and may need careful ComfyUI graph tuning.
- A combined local planner/editor stack could be too heavy for the Lite baseline.

## Recommended Next Action

Build a tiny dev-only planner harness before touching addon UI.

Suggested output:

- input: image path
- input: existing planning prompt
- output: raw model text
- output: parsed JSON
- output: normalized plan JSON
- output: side-by-side comparison against a saved Gemini plan if available

This will answer the real question quickly:

> Can Qwen3-VL produce part plans that are good enough for NymphsCore's extraction workflow?

If yes, wire it behind an optional local planner provider. If no, keep Gemini for planning and revisit larger VLMs later.
