# Local Vision Planner Handoff - Nymphs Image Parts Extraction

Date: 2026-05-24
Status: research and implementation handoff, no code implemented in this document

## Goal

Replace the expensive Gemini Flash planning step in Nymphs Image part extraction with a local vision-language planner where practical, while keeping the proven parts prompt/schema and the current Manager/Blender shared prompt system intact.

Important distinction:

- A local VLM can replace `Plan Parts`: look at a source image and return structured JSON describing extractable parts.
- A local VLM can also help LoRA captioning: produce captions/tags for training datasets.
- A local VLM cannot by itself replace `Extract Parts`, because extraction currently asks an image-edit/generation model to redraw isolated part assets. Replacing that second stage needs a local image-edit model or a segmentation/crop pipeline.

## Current Parts Flow

Current source paths:

- zimage backend: `/home/nymph/NymphsModules/zimage/api_server.py`
- shared parts prompt/schema: `/home/nymph/NymphsModules/zimage/shared_image_parts.py`
- installed shared prompt target: `~/NymphsData/config/image_prompt_templates/parts.py`
- UI: `/home/nymph/NymphsModules/zimage/nymph_image.html`

Current endpoints:

- `POST /api/parts/plan`
- `POST /api/parts/extract`

Current behavior:

1. Parts UI sends a source image, planner model id, max parts, and guidance to `/api/parts/plan`.
2. `_part_plan_worker()` resolves an OpenRouter key and sends the image plus `part_planning_prompt()` to Gemini Flash.
3. The text response is parsed as JSON and normalized through `shared_image_parts.py`.
4. The backend forces/cleans required entries such as `anatomy_base` and optional `eyeball`.
5. `/api/parts/extract` loops selected parts and sends one image-edit request per part through Gemini image generation.

The safest local replacement point is step 2 only. Keep steps 3 and 4 unchanged so local and cloud planners produce the same normalized part objects.

## Brain Fit

Brain source path:

- `/home/nymph/NymphsModules/brain`

Current Brain design:

- Uses LM Studio CLI for model download/selection.
- Stores local models under `~/Nymphs-Brain/models`.
- Starts local `llama-server` on `http://127.0.0.1:8000`.
- Exposes OpenAI-compatible `/v1/models` and `/v1/chat/completions`.
- `scripts/install_brain.sh` already detects likely vision model names such as `qwen3vl`, `qwen2vl`, `llava`, `internvl`, `pixtral`, `molmo`, and `smolvlm`.
- Brain's generated `lms-start` already searches for a sibling `mmproj` file and starts llama-server with `--mmproj` when present.
- Brain's model manager already has an `ensure_mmproj_for_model()` helper that tries to find and copy a matching projector file beside a downloaded vision GGUF.

This means Nymphs Image should not add its own VLM downloader. The cleaner contract is:

- Brain owns local VLM download, selection, start, and model status.
- Nymphs Image calls Brain's running OpenAI-compatible endpoint when the Parts planner is set to `Brain Local Vision`.
- If Brain is not running or the loaded model is not vision-capable, Nymphs Image shows a plain action message and leaves Gemini/OpenRouter available as fallback.

## Recommended Model Path

### Primary default: Qwen3-VL-4B-Instruct-GGUF

Use:

```text
Qwen/Qwen3-VL-4B-Instruct-GGUF:Q4_K_M
```

Why this is the best first target:

- Official Qwen GGUF repo, Apache-2.0 license.
- Designed for multimodal image-text work, with GGUF files split into language model and `mmproj` vision encoder.
- The model card documents llama.cpp usage with `llama-server -hf Qwen/Qwen3-VL-4B-Instruct-GGUF:Q4_K_M`.
- Qwen3-VL is positioned by Qwen as stronger than earlier Qwen-VL generations, with better visual perception, spatial reasoning, OCR, and long-context multimodal reasoning.
- 4B is a good middle point for this workflow: stronger than 2B for fantasy character/object decomposition, still much lighter than 8B/32B.

Use case fit:

- parts planning JSON
- part names/categories/priorities
- rough normalized boxes
- image captions for LoRA datasets
- style/material descriptions

Do not expect it to generate isolated part images. It should plan, not draw.

### Quality option: Qwen3-VL-8B-Instruct-GGUF

Use when local VRAM/system memory allows:

```text
Qwen/Qwen3-VL-8B-Instruct-GGUF:Q4_K_M
```

This should be the first quality upgrade if 4B misses parts, merges clothing/accessories incorrectly, or produces weaker structured JSON.

### Lightweight fallback: Qwen3-VL-2B-Instruct-GGUF

Use:

```text
Qwen/Qwen3-VL-2B-Instruct-GGUF:Q4_K_M
```

Good for smoke tests and low VRAM. It may be too weak for complex fantasy RPG characters with layered clothing, props, hair, facial hair, armor, and held objects.

### Stable fallback: Qwen2.5-VL-7B-Instruct-GGUF

Use:

```text
ggml-org/Qwen2.5-VL-7B-Instruct-GGUF
```

Qwen2.5-VL is mature and specifically documented as strong at object localization with bounding boxes/points and structured extraction. It is a sensible fallback if Qwen3-VL GGUF/mmproj handling in Brain is rough.

### Do not use as the first Brain planner: Florence-2

Florence-2 is excellent, but it is not the right first integration through Brain's llama-server path.

Why it still matters:

- It is small: `large-ft` is about 0.77B parameters.
- It supports captioning, detailed captioning, object detection, dense region captioning, OCR, and region proposals.
- It could be a future sidecar service for fast region proposals or LoRA dataset captions.

Why not first:

- It is a Transformers prompt-task model, not the same chat-style GGUF/llama-server flow Brain already owns.
- Adding it would mean a second local vision runtime path, which should wait until the Brain VLM path is proven.

### Do not use as the first parts planner: JoyCaption

JoyCaption is very relevant for LoRA captioning, but not ideal for part planning.

Why it matters:

- It is built for diffusion/LoRA training captions.
- It can produce descriptive captions and training-oriented descriptions.

Why not first for parts:

- It is captioning-focused, not localization/planning-focused.
- It does not naturally solve the `parts` JSON schema with normalized boxes and categories.
- The known high-performance path is vLLM/HF-style, not Brain's existing GGUF/llama-server path.

## Local Image-Edit Stage

If the goal becomes replacing Gemini for `Extract Parts` too, the planner model is not enough.

Candidates worth a separate investigation:

- `Qwen/Qwen-Image-Edit` or newer Qwen Image Edit variants: local instruction image editing, likely strong fit for isolated part redraws, heavier runtime.
- `FLUX.1 Kontext [dev]`: open-weight image editing, strong character/style preservation, but license and runtime weight need careful review.
- Segmentation/crop assist: Qwen3-VL/Florence-2 boxes plus SAM/RMBG could crop existing pixels, but this will not redraw hidden parts, infer anatomy, remove clothing cleanly, or create a new isolated asset when the source image lacks full visibility.

Recommendation: do not mix this into the first local planner implementation. First replace only planning, measure quality, then decide whether local extraction is worth a separate image-edit module path.

## Proposed UI Shape

Parts mode planner dropdown should become provider-aware:

```text
Planner
- Gemini 2.5 Flash
- Gemini 3.1 Pro
- Brain Local Vision
```

When `Brain Local Vision` is selected:

- show loaded Brain model from `GET http://127.0.0.1:8000/v1/models`
- show a compact message if Brain is stopped or no vision model is loaded
- do not show OpenRouter key fields in Parts
- keep the same `Plan Parts` button and same resulting checklist UI
- keep `Extract` unchanged for now

Backend planner routing should be simple:

```text
/api/parts/plan
  planner_provider=openrouter -> existing Gemini/OpenRouter path
  planner_provider=brain_local -> new Brain local VLM path
```

The local Brain path should call:

```text
POST http://127.0.0.1:8000/v1/chat/completions
```

Use an OpenAI-compatible multimodal message with:

- one text block containing the existing `part_planning_prompt()`
- one `image_url` block containing the source image data URL
- low temperature for reliable JSON
- response cleanup through the existing `_extract_json_payload()` and `_normalize_part_plan()` functions

## Download Path Through Brain

Preferred end-user path:

1. Open Brain module.
2. Use `Manage Models`.
3. Search/download one of:
   - `Qwen/Qwen3-VL-4B-Instruct-GGUF`
   - `Qwen/Qwen3-VL-8B-Instruct-GGUF`
   - `Qwen/Qwen3-VL-2B-Instruct-GGUF`
   - `ggml-org/Qwen2.5-VL-7B-Instruct-GGUF`
4. Select `Q4_K_M` first unless there is a clear reason to use Q8/FP16.
5. Let Brain save the model selection into `~/Nymphs-Brain/bin/lms-start`.
6. Start Brain.
7. Confirm the Brain log reports a detected multimodal projector.
8. In Nymphs Image Parts mode, choose `Brain Local Vision` and run `Plan Parts`.

Implementation note: Brain already tries to copy/download mmproj automatically for likely vision models. If this fails, the Manager should show a direct next step: place the matching `mmproj` GGUF beside the model GGUF or choose a Qwen official GGUF repo that includes it.

## Acceptance Tests

Use the same 10 to 20 source images for Gemini and Brain Local Vision.

Required pass conditions:

- returns valid JSON every time, with no markdown fences after cleanup
- includes `anatomy_base` when character/body is inferable
- returns practical part count within max parts
- does not create face-detail clutter unless requested
- respects `eye_part`, `base_face`, and `base_eyes` toggles
- normalized boxes are valid `[x_min, y_min, x_max, y_max]` values from 0 to 1
- current normalizer still repairs missing/weak planner output
- local planner failure falls back without breaking existing Gemini extraction

Quality comparison notes to collect:

- missing armor/robe/belt/hair/accessory parts
- merged parts that should be separate
- hallucinated parts not visible in the source
- bad body type inference for squat/stout/chibi/elderly characters
- JSON repairs required per run
- time per plan
- VRAM/RAM use while Brain is serving the model

## Implementation Tasks

1. Add `brain_local` as a planner provider in the zimage backend, separate from the existing `planner_model` OpenRouter id.
2. Add a local Brain multimodal client helper in `api_server.py`.
3. Keep `shared_image_parts.py` as the only schema/prompt source for both cloud and local planning.
4. Add a compact Brain status probe for `http://127.0.0.1:8000/v1/models`.
5. Update Parts UI planner dropdown to show provider labels instead of only Gemini model ids.
6. Add a disabled/help state when Brain is stopped or a non-vision model is loaded.
7. Add a `Brain Local Vision` smoke test with a known source image and expected JSON shape.
8. Later, consider a separate local image-edit extraction stage only after local planning is proven.

## Guardrails

- Do not duplicate Brain model downloads inside zimage.
- Do not move OpenRouter key fields into Parts.
- Do not fork the parts schema.
- Do not replace extraction until planning quality is measured.
- Treat bounding boxes as hints, not truth.
- Keep Gemini available because local VLM JSON quality may vary by quant, prompt, and source image complexity.
- Avoid huge default models. Start with Qwen3-VL 4B Q4_K_M, then offer 8B for quality.

## Research Sources

- Qwen3-VL GitHub: Qwen3-VL visual perception, spatial reasoning, OCR, grounding, and model-family notes. https://github.com/QwenLM/Qwen3-VL
- Qwen3-VL technical report: dense 2B/4B/8B/32B and MoE variants, strong multimodal reasoning, and improved spatial-temporal modeling. https://arxiv.org/abs/2511.21631
- Qwen3-VL-4B-Instruct-GGUF model card: official GGUF, Apache-2.0, llama.cpp usage, split LLM and `mmproj` vision encoder, Q4_K_M option. https://huggingface.co/Qwen/Qwen3-VL-4B-Instruct-GGUF
- Qwen3-VL-2B-Instruct-GGUF model card: official smaller GGUF with llama.cpp/OpenAI-compatible usage. https://huggingface.co/Qwen/Qwen3-VL-2B-Instruct-GGUF
- llama.cpp multimodal docs: multimodal GGUF with `-hf`, `--mmproj`, and supported Qwen vision GGUF flows. https://github.com/ggml-org/llama.cpp/blob/master/docs/multimodal.md
- Qwen2.5-VL technical report: visual recognition, object localization using boxes/points, structured data extraction, and dynamic resolution processing. https://arxiv.org/abs/2502.13923
- FiftyOne Qwen3-VL docs: detection mode returning bounding-box coordinates through 2D grounding. https://docs.voxel51.com/api/fiftyone.utils.qwen3_vl.html
- Florence-2 model card and paper: compact prompt-based model for captioning, object detection, grounding, segmentation, OCR, and region proposals. https://huggingface.co/microsoft/Florence-2-large-ft and https://arxiv.org/abs/2311.06242
- JoyCaption model card: captioning-focused VLM for diffusion/LoRA training captions, better treated as a LoRA-captioning tool than a parts planner. https://huggingface.co/fancyfeast/llama-joycaption-beta-one-hf-llava
- Qwen Image Edit and FLUX Kontext research leads for future local extraction image editing, not first planner replacement. https://huggingface.co/qwen/qwen-image-edit and https://bfl.ai/announcements/flux-1-kontext-dev
