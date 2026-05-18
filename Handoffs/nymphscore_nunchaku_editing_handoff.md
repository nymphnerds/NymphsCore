# NymphsCore / Nymphs2D2 Handoff: Adding Image Editing to Nunchaku Z-Image Turbo

## Executive summary

Your current stack **does not support true img2img when `Z_IMAGE_RUNTIME=nunchaku`**, but it **can realistically be extended to support image-guided editing**.

The most practical first step is **not** forcing latent img2img immediately. Instead, add a **ControlNet-guided edit mode** for Nunchaku, because the available evidence already shows that:

- Nunchaku + Z-Image Turbo can accept an **input image**
- it can use that image to derive **control conditions** such as Canny
- it can run an **official Z-Image Turbo ControlNet** workflow
- this gives an edit-like workflow that preserves scene structure while remaining fast

A second, more experimental phase would be to try **true img2img** by loading `ZImageImg2ImgPipeline` with the Nunchaku transformer.

---

## What is true right now

### In the current Nymphs2D2 code

The backend explicitly disables img2img for Nunchaku:

- `supports_img2img()` returns false for `nunchaku`
- `_ensure_img2img()` raises an error for the Nunchaku runtime
- the Nunchaku path loads only the txt2img Z-Image pipeline
- the non-Nunchaku path loads `ZImageImg2ImgPipeline`

That means the current limitation is partly a **backend policy/implementation choice**, not proof that Nunchaku is fundamentally incapable of image-guided workflows.

### Evidence from Nunchaku / ComfyUI docs

The Z-Image Turbo ControlNet workflow shows:

- input image loading
- preprocessing from the input image
- use of an official Z-Image Turbo ControlNet model
- generation from a fresh latent guided by that control signal

This is **not true img2img**, because it starts from a new latent rather than the original image latent.

But it **is** image-conditioned generation, and it strongly supports adding an **editing-like mode** to your system.

---

## Important distinction

### True img2img

True img2img typically means:

- encode the source image into latent space
- add noise according to a `strength` value
- denoise toward the prompt
- preserve more of the source image directly

This is best for:

- close variations
- style transfer with source preservation
- detail-faithful edits

### ControlNet-guided editing

ControlNet-guided editing typically means:

- take a source image
- preprocess it into a condition image such as Canny/depth/pose
- start from a fresh latent
- guide generation with the condition image and prompt

This is best for:

- layout preservation
- scene restructuring with speed
- fast "edit-like" results
- strong compositional adherence

This is the best first implementation target for Nunchaku in your stack.

---

## Recommended roadmap

## Phase 1: Add `controlnet_edit` for Nunchaku

### Goal
Add a new backend mode that accepts an input image and uses Nunchaku Z-Image Turbo + ControlNet for structure-guided image editing.

### Why this first

- strongest evidence of compatibility already exists
- lower engineering risk than true img2img
- likely to remain fast on your hardware
- avoids fighting unsupported latent-edit internals immediately

### Proposed API shape

Suggested new mode names:

- `controlnet_edit`
- `guided_img_edit`
- `structure_edit`

Suggested inputs:

- `prompt`
- `input_image`
- `negative_prompt` (optional)
- `preprocessor` (`canny`, `depth`, etc.)
- `control_strength`
- `steps`
- `cfg`
- `width`, `height` or `max_dim`
- `seed`

Optional later additions:

- `preprocessor_params`
- `scheduler`
- `shift`
- `batch_size`

### Expected behavior

- preserve major composition / edges / scene structure
- allow restyling and relighting
- allow changes like interior style, time of day, materials, mood
- not preserve all source details exactly

### Likely code touchpoints

#### 1. API layer
Add a new request mode in the API server instead of trying to shoehorn this into the existing `img2img` mode.

Reason: this is not semantically the same as latent img2img, and keeping them separate avoids confusion.

#### 2. Model manager
Add support for loading:

- Nunchaku Z-Image model
- official ControlNet patch / model
- any required VAE / CLIP / patch components

Implement a dedicated loader path, for example:

- `_ensure_controlnet_edit()`
- `supports_controlnet_edit()`

#### 3. Image preprocessing
Add preprocessing helpers for:

- Canny
- Depth
- possibly lineart / softedge later

Start with **Canny first** because it is already demonstrated and easiest to reason about.

#### 4. Inference path
Create a Nunchaku-specific inference function that:

- loads and resizes the input image
- preprocesses it into the control image
- constructs the conditioned generation request
- samples from a fresh latent
- decodes and returns the output

### Suggested implementation order

1. Canny-only version
2. expose control strength
3. add depth
4. add optional LoRA support
5. add mask-aware variants later if feasible

---

## Phase 2: Experimental true img2img for Nunchaku

### Goal
Attempt to enable a real `image + strength` pipeline under Nunchaku.

### What would need to change

At minimum:

- allow `supports_img2img()` to return true for Nunchaku
- remove the hard failure in `_ensure_img2img()`
- try instantiating `ZImageImg2ImgPipeline`
- inject `NunchakuZImageTransformer2DModel` into that pipeline
- verify the pipeline call accepts `image=...` and `strength=...`

### Why this is risky

This may fail because:

- the Nunchaku transformer may only have been validated for txt2img
- img2img latent preparation may assume non-Nunchaku internals
- scheduler interactions may differ
- VAE encode/decode path may create memory/performance issues
- the codebase may need additional latent handling not currently exposed

### Recommendation

Treat this as a **research spike**, not the main deliverable.

Do it only after a working `controlnet_edit` mode exists.

---

## What to call things in the UI / API

Avoid calling the ControlNet mode `img2img`, because users will expect source-latent preservation.

Better labels:

- `Guided Edit`
- `Structure-Guided Edit`
- `Control Edit`

Then reserve `img2img` for the true latent-edit path if you ever get it working.

---

## Practical user expectations

### What the Nunchaku ControlNet edit mode should do well

- room / environment restyling
- scene preservation with aesthetic changes
- architecture / interior redesign from the same composition
- relighting, time-of-day, texture/material reinterpretation
- fast generation with strong structural adherence

### What it will not do as well as true img2img/inpaint

- exact detail preservation
- local object replacement with pixel-faithful retention
- subtle edits of small regions without changing nearby content
- source-faithful masked edits

---

## Suggested MVP

### MVP scope

Implement a single new endpoint / mode with:

- Nunchaku runtime only
- Z-Image Turbo only
- one preprocessor: `canny`
- one ControlNet path
- one returned output image

### MVP request example

```json
{
  "mode": "controlnet_edit",
  "prompt": "Modern warm interior lighting, walnut finishes, minimalist furniture, photorealistic",
  "input_image": "<uploaded image>",
  "preprocessor": "canny",
  "control_strength": 0.9,
  "steps": 8,
  "cfg": 1.0,
  "seed": 12345
}
```

### MVP success criteria

- accepts an input image
- returns an image preserving composition
- runs materially faster than non-Nunchaku img2img
- exposes enough control to be useful without overcomplicating the API

---

## Suggested experimentation plan

### Test matrix

Run the same image through:

1. current txt2img without control
2. Nunchaku + Canny ControlNet edit
3. non-Nunchaku img2img

Compare on:

- speed
- VRAM
- composition retention
- detail preservation
- prompt responsiveness

### Prompts to test

- "same room at night with warm cinematic lighting"
- "convert to Japandi interior style"
- "replace furnishings with luxury modern furniture"
- "make it a brutalist concrete loft"

### Metrics worth tracking

- end-to-end latency
- peak VRAM
- failure rate / OOM
- visual adherence to source composition
- amount of unwanted drift

---

## Bottom line

### Strongest conclusion

Your evidence supports this claim:

> Nunchaku in this stack can likely be extended to support **image-guided editing workflows** for Z-Image Turbo, even though it does not currently support **true img2img**.

### Recommended next move

1. **Implement `controlnet_edit` first**
2. keep it separate from true `img2img`
3. only then try a Nunchaku-backed `ZImageImg2ImgPipeline` experiment

That gives you the highest chance of getting something fast, useful, and stable.

---

## One-sentence handoff

If you only keep one sentence from this document, keep this one:

**Do not start by forcing Nunchaku true img2img; start by adding a Nunchaku Z-Image Turbo ControlNet-based edit mode, because the evidence already shows that image-conditioned generation works there and it is the most realistic path to fast editing in your system.**