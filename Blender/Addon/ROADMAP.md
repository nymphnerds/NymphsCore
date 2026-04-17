# Roadmap

This note tracks likely future directions for the Blender addon product surface,
especially where the three kept backend families are strong, weak, or likely to
be supplemented later.

## Current Baseline

- keep `Hunyuan 2mv` as the practical default shape and retexture workflow
- keep `Z-Image` via `Nunchaku` as the image-generation and multiview handoff
  lane
- keep official `TRELLIS.2` as the higher-end image-to-3D lane worth continued
  investment

Current product reading:

- `Hunyuan 2mv` is the stronger everyday workflow
- `Z-Image` is the lightweight prompt-to-image front end that can feed the
  broader workflow
- `TRELLIS.2` is the higher-end lane with the strongest long-term upside for
  richer image-to-3D output
- latest is not automatically best for the real user workflow

## Near-Term Product Work

### Distribution and repo structure

Current testing setup:

- keep the public `Nymphs3D2-Extensions` repo working as the easiest test install/update path
- keep Blender pointed at the public extension feed during active testing because this makes install/update verification fast
- keep this source repo usable as a manual fallback for local zip builds and `Install from Disk`

Future paid-product direction:

- use a separate public-facing `Nymphs3D2` repo or site for the product README, user guide, screenshots, FAQ, and customer-facing release notes
- keep the live addon source in a private source repo
- stop using a public GitHub extension feed as the long-term paid distribution channel
- move the installable extension feed to a controlled customer-only distribution surface

Why this split matters:

- the public-facing product/docs surface should be easy to discover and easy to read
- the paid extension package should not be downloadable for free from the same public repo that markets it
- the source repo, docs surface, and customer update feed are different responsibilities and should stay separate

Likely target structure:

- public product/docs repo or site = forward-facing `Nymphs3D2` surface
- private addon source repo = active development and internal release prep
- controlled extension feed = authenticated `index.json` + packaged zips for paying users

Practical note:

- for now, direct Blender pulls from the public extension repo remain useful for test iteration
- when the product surface is ready for sale, replace that public test feed with a gated customer update path

Sketch of the likely paid-feed architecture:

- keep git as the source-of-truth for addon development and release preparation
- do not try to make Blender talk directly to a private git repo as the customer update source
- build the release zip from the private source repo
- publish the built zip and generated `index.json` to an authenticated HTTP endpoint
- let Blender talk to that HTTP endpoint with a bearer token

Rough shape:

- private git repo = addon source, changelog, release prep
- release pipeline = build zip, version it, generate/update `index.json`
- controlled HTTP feed = serves `index.json` and package zips to Blender
- customer token = grants access to the feed without exposing the package publicly

What this solves:

- one canonical archive per release
- Blender-native install/update flow from the Extensions menu
- no need to upload and maintain matching archives across multiple storefronts
- no free public download path from the forward-facing docs surface

Implementation note:

- storefronts should handle payment and entitlement
- the authenticated feed should handle delivery
- git can stay behind the scenes as the internal source/release system rather than the public update surface

### Retexturing polish

- continue improving the `2mv` texture controls that are already proving useful
- keep tightening the `Z-Image` to `2mv` handoff so the image-first workflow
  stays lightweight and direct
- improve user-facing guidance about when to use `2mv` versus `TRELLIS.2`, and
  when to start from `Z-Image`
- make the `TRELLIS.2` retexture story explicit in the UI so users can tell the
  difference between:
  - texturing a shape generated in the same request
  - retexturing an already-selected mesh
- keep the `TRELLIS.2` selected-mesh retexture path visible but clearly framed
  around its real contract:
  - selected mesh in Blender
  - one guidance image
  - no multiview or text-only retexture path yet
- keep same-pass `TRELLIS.2` shape + texture generation as a normal first-class
  workflow instead of forcing users into a later retexture pass
- bring the texture panel up to the same UI quality as the image and shape
  panels before expanding deeper workflow branches

### Prompt and preset UX redesign

This needs a real product pass rather than more one-off wording tweaks.

Current user problems:

- the prompt starter system is split across style / pose / negative prompt
  ideas in a way that feels harder to understand than writing directly
- the main image-generation panel becomes too tall once prompts fill up
- the wider prompt editor is helpful, but still does not feel like a clean
  direct-writing surface
- seed needs plainer explanation for non-technical users

Near-term direction:

- keep direct inline editing available in the main panel
- keep optional prompt previews collapsed by default
- add obvious clear/reset actions
- simplify the starter system so it feels like one coherent preset lane rather
  than several partial systems

Later design goal:

- move toward a cleaner writing-first prompt surface where presets assist the
  user rather than dominating the workflow

### Material and import polish

- improve post-import material handling where useful
- consider Blender-side PBR tweak controls after import
- keep the public addon package lean and free of local-dev baggage
- treat Blender-side PBR/node work as downstream finishing, not as the first
  answer to unclear mesh or texture workflow decisions
- investigate bad `TRELLIS.2` exported PBR/material states before designing a
  user-facing PBR panel around assumptions that may be wrong
- record and investigate whether the current `TRELLIS.2` roughness / reflectivity
  output is:
  - a valid binary mask-style workflow
  - an export simplification
  - or a broken material-data path
- use the now-successful real `TRELLIS.2` selected-mesh retexture pass as the
  baseline test case for that investigation

### Intercept texture maps before final packing

This is a strong future direction for the Blender workflow.

Possible workflow:

- let the backend generate texture maps
- intercept those maps before the final baked/packed handoff
- import the raw maps into Blender
- build Blender nodes from those raw maps before the final packaged result is locked in

Why it is interesting:

- it would make the texture workflow less of a black box
- it would allow Blender-side material editing before the final handoff is baked in
- it would create a cleaner path for roughness / metallic / albedo tweaking before export
- it may eventually support alternative "raw maps + node graph" workflows alongside the current backend-packed asset flow

## Medium-Term Exploration

### Independent texture backends

Nymphs3D does not need to stay locked to Hunyuan texturing forever.

Possible future direction:

- keep `Hunyuan 2mv` and `TRELLIS.2` as shape-generation lanes
- plug in a completely separate texturing backend if a stronger or more practical texture path emerges

Reasons this matters:

- `2mv` is practical but not ideal for richer PBR material output
- `TRELLIS.2` is promising for richer output but remains heavier and more
  demanding
- a future texture backend may outperform both while still fitting the Nymphs3D Blender workflow better

### Image generation before shape generation

Another strong future direction is image generation that can feed the shape generators more deliberately.

Possible use cases:

- prompt-to-image, then image-to-shape
- character concept generation, then shape generation
- generating cleaner front-view or product-style guide images before sending
  them to `2mv` or `TRELLIS.2`

### `TRELLIS.2` as a kept addon lane

`TRELLIS.2` is now one of the backend families worth keeping in the addon, but
it should still be treated as a distinct higher-end lane rather than forced to
imitate the lighter `2mv` workflow too closely.

Why it is interesting:

- it appears to offer a stronger high-end image-to-3D path
- it has a separate texturing pipeline that can texture an existing mesh from a
  guidance image
- it targets full PBR-style output, which lines up with the longer-term goal of
  richer material workflows

How it should be handled inside the addon:

- the current `Nymphs3D2` addon is shaped around the existing local backend API
  contract, so the `TRELLIS.2` adapter should stay thin and explicit
- upstream `TRELLIS.2` currently exposes Python pipelines and Gradio demos, not
  the same stable local API surface the current addon expects
- keeping the `TRELLIS.2` request/launch contract narrow makes it easier to
  tell whether failures come from `TRELLIS.2`, the adapter service, or the
  Blender UI

Current practical caution:

- upstream currently describes `TRELLIS.2` as Linux-tested and recommends at
  least `24 GB` VRAM
- that makes it more realistic as a high-end or future-hardware lane than as an
  immediate replacement for the current `16 GB`-friendly workflows
- the right practical emphasis remains:
  - thin local adapter service
  - image-to-3D first
  - shape-conditioned texturing second

Potential future adaptation path:

- if the official single-image path keeps proving worthwhile, evaluate a
  dedicated `Nymphs` fork of the official `TRELLIS.2` repo rather than trying
  to build the long-term product around community wrapper behavior
- the main reason to consider a fork would be to explore real multi-image
  conditioning that the current public inference entrypoints do not expose yet
- the most interesting future fork targets are:
  - real multi-image or multiview-conditioned shape generation
  - real multi-image-guided mesh texturing
  - shape-conditioned texturing flows that can use more than one guidance view
- that should stay explicitly experimental until the official single-image lane
  is judged stable enough to deserve deeper product investment
- if this work happens, keep the Blender-side contract thin and let the
  multi-image logic live in the adapter service or the `TRELLIS.2` fork rather
  than hardcoding it into the current Hunyuan-shaped addon request flow

If `TRELLIS.2` proves stable and meaningfully better on real user cases, the
lessons from that extension can later feed back into the broader `Nymphs`
product family without destabilizing the current `Nymphs3D2` workflow.

## Specific Brainstorms Worth Keeping Alive

### Local Stable Diffusion backend

A local Stable Diffusion backend is a strong candidate for future integration.

Why it is interesting:

- it could generate or refine guide images before shape generation
- it could produce cleaner style-consistent input for `2mv` / `TRELLIS.2`
  shape generation
- it could help create better texture guide images for selected-mesh retexture

Potential roles:

- image generation for shape input
- image cleanup / enhancement before shape generation
- texture guidance image generation
- prompt-driven concept iteration inside the Blender workflow

### Future Hunyuan or non-Hunyuan texture backends

If a stronger public texturing model appears later, the addon should be able to treat texturing as a pluggable lane rather than something inseparable from shape generation.

Possible direction:

- preserve current shape backends
- add alternate texture backends behind clear workflow choices
- let the user pick the practical lane versus the high-end lane

## Caution Notes

- do not assume a newer model is automatically the better practical workflow
- do not overfit the product surface to a single backend generation
- keep the Blender UI honest about what each backend is actually good at

## Current Guiding Principle

Strong shape generation and strong texturing do not have to come from the same backend forever.

That split should remain an open design possibility.
