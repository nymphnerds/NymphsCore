# Nymphs World Researched Implementation Blueprint

Date: 2026-05-27

This is a new implementation blueprint for Nymphs World. It is not a summary of
`NYMPHS_WORLD_GRAND_PLAN.md`, and it does not replace that file. The grand plan
is the vision archive. This document is the buildable plan.

LoRA training is intentionally out of scope for this blueprint. It can remain a
future optional module, but it should not shape the first Nymphs World design.

## 1. The Core Decision

Build Nymphs World as a new project-first Nymph module, not as a direct rename
of WORBI.

WORBI is valuable source material, especially for editor behavior, local file
workflows, tags, image generation hooks, AI tools, and worldbuilding UX. But the
research makes one thing clear: Nymphs World needs a cleaner foundation than
WORBI's current app shape.

The best foundation is:

```text
local project vault
  -> readable Markdown and JSON source files
  -> fast indexer and diagnostics
  -> stable entity, note, decision, relation, asset, task, and export IDs
  -> quiet module capabilities behind world actions
  -> entity-centered UI
```

Nymphs World should treat installed modules like Nymphs Image, Pixal3D, TRELLIS,
and Brain as supporting capabilities. It should not become a generation dashboard,
a pile of embedded module UIs, or a place where backend output folders pretend
to be the world.

## 2. Read This First

If you only use one part of this document to start building, use this map:

```text
1. New module/app shell
2. Project vault creator
3. Entity Markdown/frontmatter schema
4. Indexer and diagnostics
5. Scratch and decision capture
6. Relationship/canon status model
7. Story and systems pages
8. Quiet references/assets support
9. Export/diagnostic reports
```

The first implementation target should be one vertical slice:

```text
scratch idea
  -> promote to entity
  -> link to faction/location/system
  -> mark canon decision
  -> attach optional reference
  -> include in story/system/export report
```

Everything else, including image and 3D generation, should wait until this loop
feels good.

## 3. What Research Changed

The original grand plan is full of good ideas, but it is too wide to implement
directly. The research pass narrowed it into a spine:

- Chronicler proves the importance of an offline Markdown vault, indexing,
  backlinks, frontmatter, image handling, diagnostics, and local ownership.
- WORBI proves the usefulness of a rich writing shell, information panel,
  file-safe workspace APIs, image generation bridge, AI tool permissions, and
  worldbuilding-specific workflows.
- 3DGenStudio proves that asset production needs stages, versions, lineage, and
  workflow output capture. That is useful later, but it should sit behind the
  worldbuilding surface rather than define it.
- NymphsCore Manager and registry prove that modules should stay manifest-owned:
  install, start, stop, status, model fetch, logs, UI, and output roots come from
  module contracts rather than hardcoded app knowledge.
- Nymphs Image already has sidecar metadata, recent outputs, batches, parts
  workflows, image modes, OpenRouter/Gemini hooks, and local output browsing.
  Nymphs World should borrow the provenance model, not make image generation the
  main product.
- Pixal3D and TRELLIS.2 are powerful supporting backends for later asset work.
  Their staged runtime needs matter when the user deliberately asks for 3D, but
  they should stay behind entity/reference actions.
- Brain is valuable as a contextual action engine, not as a permanent chat box
  bolted onto every page.
- Obsidian, Twine/Yarn, Godot, and Blender reinforce a practical rule: keep the
  source formats inspectable, linkable, and exportable. Do not hide the world in
  opaque runtime state.

## 4. Product Shape

Nymphs World is a worldbuilding and game pre-production workspace.

It should help a small team move from rough ideas to a coherent world:

```text
scratch note
  -> entity page
  -> relationship
  -> canon decision
  -> scene/system use
  -> optional reference or asset
  -> export/report
```

The first screen should be the actual workspace, not a landing page. The main
experience should be the world itself: entities, scratch, story, systems, assets,
and search.

Recommended top-level navigation:

```text
World
Scratch
Story
Systems
Assets
Jobs
Search
Exports
```

Keep module controls secondary. A module is a tool the user invokes from the
world, usually through a small contextual action. It is not the place where the
world lives.

## 5. Non-Negotiable Design Rules

### 5.1 Source Of Truth

Readable project files are the source of truth whenever practical.

Use SQLite or an embedded database only as an index/cache if performance later
demands it. The first version should be recoverable by opening the project
folder in a text editor.

### 5.2 Stable IDs

Every durable object needs an ID:

```text
world.20260527.0001
entity.character.nyra
asset.20260527.0009
job.zimage.20260527.0004
export.godot.20260527.0001
```

Paths can change. IDs should not.

### 5.3 Promotion Beats Mutation

Generated backend outputs should be imported or promoted into the project.

Nymphs World should keep the original module output path and metadata, then copy
or register a selected version in the project asset store. This prevents the
project from depending on temporary backend folders.

### 5.4 Scratch Is First-Class

Do not force every idea to become canon immediately.

Scratch needs loose notes, pasted references, questions, rejected ideas,
decisions, and maybe-later material. Promotion should be a deliberate action.

### 5.5 Diagnostics Are A Feature

Broken links, missing assets, orphan metadata, missing source jobs, duplicate
IDs, invalid frontmatter, and stale exports should have a normal diagnostics
view from the beginning.

### 5.6 Module UIs Are References, Not The Main Shell

Nymphs Image, Pixal3D, TRELLIS, and Brain can still expose their own full UIs.
Nymphs World should call their stable APIs or module contracts for world-aware
actions, then import the result into the vault.

## 6. Project Vault Layout

Recommended first layout:

```text
MyWorld.nw/
  world.nymphworld.json
  scratch/
    inbox.md
    decisions.md
    questions.md
  entities/
    characters/
    factions/
    locations/
    items/
    creatures/
    quests/
    systems/
  story/
    arcs/
    scenes/
    dialogue/
  assets/
    library/
    by-entity/
    raw-imports/
    approved/
  jobs/
    zimage/
    pixal3d/
    trellis/
    brain/
  exports/
    godot/
    blender/
    generic/
  .nymphworld/
    index/
    diagnostics/
    thumbnails/
    cache/
```

The `.nymphworld/` folder may contain caches and derived state. It should not be
the only place important world data exists.

## 7. World Manifest

`world.nymphworld.json` should be small and stable:

```json
{
  "schema": "nymphworld.project.v1",
  "id": "world.20260527.0001",
  "name": "Untitled World",
  "created_at": "2026-05-27T00:00:00Z",
  "updated_at": "2026-05-27T00:00:00Z",
  "default_export_targets": ["godot", "blender"],
  "indexes": {
    "entities": ".nymphworld/index/entities.json",
    "assets": ".nymphworld/index/assets.json",
    "links": ".nymphworld/index/links.json"
  }
}
```

Do not put live module state here. Module runtime state belongs to NymphsCore and
module status endpoints.

## 8. Entity Page Contract

Entity pages should be Markdown with YAML frontmatter. The body stays human
readable. The frontmatter carries machine-readable identity and state.

Example:

```markdown
---
id: entity.character.nyra
type: character
name: Nyra
status: draft
tags:
  - forest-scout
  - player-facing
links:
  faction: entity.faction.greenwardens
assets:
  hero: asset.20260527.0009
  approved:
    - asset.20260527.0014
---

# Nyra

## Canon

The current accepted description lives here.

## Scratch

Loose thoughts can live here until promoted or deleted.
```

Recommended entity statuses:

```text
scratch
draft
review
canon
asset-ready
exported
retired
```

Recommended first entity types:

```text
character
faction
location
item
creature
quest
scene
system
resource
```

## 9. Asset Manifest Contract

Every promoted asset should get a sidecar manifest. Do not rely only on the file
name or folder location.

Example:

```json
{
  "schema": "nymphworld.asset.v1",
  "id": "asset.20260527.0009",
  "entity_ids": ["entity.character.nyra"],
  "kind": "image",
  "role": "portrait",
  "status": "approved-reference",
  "files": {
    "primary": "assets/by-entity/characters/nyra/portrait_0009.png",
    "thumbnail": ".nymphworld/thumbnails/asset.20260527.0009.webp"
  },
  "source": {
    "module_id": "zimage",
    "module_version": "0.1.97",
    "output_path": "/home/nymph/NymphsData/outputs/zimage/...",
    "job_id": "job.zimage.20260527.0004",
    "prompt": "Nyra, practical forest scout, readable game silhouette",
    "settings": {
      "width": 1024,
      "height": 1024,
      "seed": 42
    }
  },
  "review": {
    "approved_by": "local-user",
    "approved_at": "2026-05-27T00:00:00Z",
    "notes": "Good silhouette and costume direction."
  }
}
```

The actual asset file can be image, GLB, texture map, audio, video, or future
format. The manifest shape should stay consistent.

## 10. Job Record Contract

Every generation request launched from Nymphs World should create a job record
before calling the backend.

Example:

```json
{
  "schema": "nymphworld.job.v1",
  "id": "job.pixal3d.20260527.0002",
  "module_id": "pixal3d",
  "module_url": "http://127.0.0.1:8097",
  "status": "complete",
  "created_at": "2026-05-27T00:00:00Z",
  "finished_at": "2026-05-27T00:12:30Z",
  "input_assets": ["asset.20260527.0009"],
  "output_assets": ["asset.20260527.0015"],
  "request": {
    "profile": "low_vram_1024",
    "seed": 42,
    "texture_size": 2048,
    "target_faces": 100000
  },
  "backend": {
    "health_checked": true,
    "source_prepared": true,
    "warmup_required": false
  }
}
```

Jobs give the project memory. The user should be able to answer:

- What created this image?
- What prompt/settings were used?
- What source image made this mesh?
- Which assets are approved, rejected, or merely candidates?

## 11. Link And Graph Model

The graph should be derived from source files and manifests.

Do not create a separate graph file as the only truth. Use an indexer to derive:

```text
entity -> entity links from frontmatter and Markdown wikilinks
entity -> asset links from asset manifests
asset -> asset links from source jobs
job -> module links from job records
scene -> character/location/item links from story files
export -> asset links from export manifests
```

Useful edge types:

```text
mentions
belongs_to
uses
generated_from
variant_of
approved_for
exports_to
appears_in
requires
conflicts_with
```

The graph view can come later. The derived graph index should exist early.

## 12. UI Blueprint

### 12.1 Shell

Use a calm, work-focused shell:

```text
left nav        stable project areas
main surface    current page/editor/asset workflow
right context   entity facts, linked assets, jobs, diagnostics
bottom strip    active jobs and module health
```

Do not make the home page a marketing surface. Open directly into the project.

### 12.2 Entity Page

The entity page is the center of the product.

Recommended regions:

```text
title and status
canon body
scratch block
linked entities
asset strip
job history
export readiness
right-side context/actions
```

Primary actions should be contextual:

```text
Generate image
Attach reference
Promote asset
Make 3D asset
Create scene
Add to export
Ask Brain
```

### 12.3 Scratch

Scratch should feel low-friction:

```text
Inbox
Questions
Decisions
References
Maybe Later
Rejected
```

Important action:

```text
Promote to entity
Promote to asset brief
Promote to system rule
Promote to scene
```

### 12.4 Assets

The assets view should be a production library, not just a folder browser.

Useful filters:

```text
entity
kind
role
status
source module
created date
approved only
missing source
needs review
```

Useful statuses:

```text
raw
candidate
review
approved-reference
approved-game-asset
exported
rejected
archived
```

### 12.5 Jobs

The jobs view should show:

```text
active jobs
recent jobs
failed jobs
module source
input asset
output asset
settings
open raw output
promote output
retry
```

## 13. Module Integration Plan

### 13.1 Nymphs Image

Use Nymphs Image first.

Why:

- It already has `/generate`.
- It already stores output files under `NymphsData`.
- It already records lightweight sidecar metadata and batches.
- It supports txt2img, img2img, Gemini, and parts workflows.

Nymphs World action:

```text
Entity page -> Generate image -> call Nymphs Image -> import chosen output
```

Minimum integration:

- check `/health`
- check `/server_info`
- call `/generate`
- poll `/active_task`
- read `/api/outputs`
- copy/promote selected image into project assets
- write job record and asset manifest

Do not move the Nymphs Image UI wholesale into Nymphs World.

### 13.2 Pixal3D

Pixal3D should be the default image-to-3D backend for approved reference images
when installed and healthy.

Nymphs World action:

```text
Approved image asset -> Make 3D asset -> Pixal3D staged workflow -> promote GLB
```

Minimum integration:

- check `/health` and `/server_info`
- expose warmup state from `/warmup_status`
- call `/api/warmup` when needed
- call `/api/preprocess` for source prep
- call `/api/generate_3d`
- call `/api/extract_glb_api`
- poll `/progress`
- preserve backend memory controls: Clear GPU Memory and kill/restart path
- write job record and GLB asset manifest

Do not hide Pixal3D's staged nature. Warmup, prep, generation, and export are
real states and should be visible.

### 13.3 TRELLIS.2

TRELLIS.2 should be an alternate 3D backend and retexture path.

Use it when:

- Pixal3D is not installed
- GGUF quant/runtime path is preferred
- mesh retexture is needed
- the workflow needs TRELLIS profiles or Blender-addon-style compatibility

Minimum integration:

- check `/health`, `/server_info`, `/active_task`
- call `/generate`
- support image-to-3D and mesh retexture payloads
- preserve preprocessing settings, especially square image conditioning
- write job record and asset manifest

### 13.4 Brain

Brain should appear as contextual actions:

```text
Summarize this entity
Find contradictions
Draft variations
Extract entities from scratch
Turn this into a quest brief
Make a prompt from this entity
Audit export readiness
```

Brain actions need permissions:

```text
read current page
read linked pages
read whole project
write scratch
write entity draft
write asset brief
modify canon
```

Default should be narrow: current page plus explicit linked context.

Open WebUI can remain available as an escape hatch, but it should not be the
primary Nymphs World interface.

## 14. Implementation Phases

### Phase 0: Freeze The Spine

Goal: create the project decision record before building.

Tasks:

- create new Nymphs World module/repo shape
- choose app stack deliberately
- define `world.nymphworld.json`
- define entity, asset, job, and export schemas
- define vault path rules
- define module bridge interface

Acceptance:

- a new empty project can be created
- schema examples validate
- no generated assets are stored in backend source folders

### Phase 1: Vault, Indexer, Diagnostics

Goal: make the project folder trustworthy.

Tasks:

- scan Markdown, JSON manifests, images, and GLBs
- parse YAML frontmatter
- derive entity index
- derive asset index
- derive backlinks and broken links
- detect duplicate IDs
- detect missing files and orphan sidecars
- write `.nymphworld/index/*.json`
- render diagnostics view

Acceptance:

- user can create/open a world folder
- entity files appear in the app
- broken links and invalid frontmatter are visible
- changing a file on disk updates the index

### Phase 2: Entity Pages And Scratch

Goal: make the world pleasant to write in.

Tasks:

- implement entity page editor/viewer
- implement scratch inbox
- implement promote scratch to entity
- implement status transitions
- implement linked asset strip
- implement right-side context panel

Acceptance:

- user can create a character, location, item, and quest
- user can link entities with wikilinks or frontmatter
- user can promote scratch into an entity without manual file surgery

### Phase 3: Asset Library And Promotion

Goal: make assets durable.

Tasks:

- implement asset library view
- implement attach local file
- implement asset manifest writer
- implement thumbnails
- implement asset status changes
- implement entity asset links
- implement raw import to project copy

Acceptance:

- user can attach an image to an entity
- asset manifest records file, entity, role, and status
- diagnostics find missing asset files

### Phase 4: Nymphs Image Vertical Slice

Goal: complete the first AI production loop.

Vertical slice:

```text
character page
  -> generate portrait
  -> see generated candidates
  -> promote one image
  -> attach it as approved reference
  -> preserve prompt/settings/source job
```

Tasks:

- module health check
- generation form with essential settings only
- `/generate` call
- active task/progress display
- output picker
- asset promotion
- job record

Acceptance:

- an approved image can be traced back to its prompt, settings, module version,
  raw output path, and source entity

### Phase 5: Pixal3D And TRELLIS 3D Slice

Goal: turn an approved image into a tracked 3D asset.

Vertical slice:

```text
approved portrait/reference
  -> make 3D asset
  -> prep source
  -> generate
  -> export GLB
  -> promote GLB
  -> attach to entity
```

Tasks:

- Pixal3D health/warmup/prep/generate/export integration
- TRELLIS fallback generation integration
- GLB preview
- GLB asset manifest
- source image linkage
- memory/reset actions exposed when needed

Acceptance:

- a GLB asset can be traced back to source image, backend, settings, and job
- failed 3D jobs leave readable diagnostics
- user can retry without losing the source asset

### Phase 6: Story And Systems

Goal: make gameplay/narrative planning useful without exploding scope.

Tasks:

- scene pages
- dialogue notes
- quest briefs
- system rule pages
- entity references inside scenes
- story arc index

Acceptance:

- scene pages can link characters, locations, items, and quests
- exports can list missing required links and assets

### Phase 7: Export Packages

Goal: ship structured project slices to tools and engines.

Start with simple export manifests:

```text
exports/godot/
exports/blender/
exports/generic/
```

First exports should include:

- selected entities
- approved assets
- GLBs
- textures
- prompts/provenance if requested
- manifest JSON
- validation report

Acceptance:

- exported folder can be inspected without Nymphs World
- Godot/Blender-targeted exports use GLB where possible
- missing assets are reported before export

## 15. First Sprint Recommendation

Build one complete loop instead of many partial screens:

```text
Create character
  -> write canon and scratch
  -> generate image through Nymphs Image
  -> promote image to approved reference
  -> generate GLB through Pixal3D
  -> promote GLB to approved candidate
  -> produce export manifest
```

This proves:

- vault
- IDs
- editor
- indexer
- asset manifests
- job records
- module bridge
- provenance
- diagnostics
- export basics

Do not start with a giant graph view, full game design system, map renderer,
audio/video generation, or training workflow.

## 16. Implementation Risks

### Risk: WORBI Gravity

WORBI has useful features, but porting the whole app would preserve too many
old assumptions.

Mitigation:

- reuse ideas and selected components
- do not reuse the whole storage model as the NW source of truth
- avoid another giant all-purpose `App.tsx`

### Risk: Hidden State

Module outputs, localStorage, and generated sidecars can drift.

Mitigation:

- promote assets into the project
- write asset/job manifests immediately
- use diagnostics from day one

### Risk: Pixal3D Runtime Fragility

Pixal3D needs careful memory/lifecycle behavior.

Mitigation:

- preserve warmup, source prep, generate, export, and reset as visible states
- do not fire invisible repeated runs
- keep job records even when backend calls fail

### Risk: Scope Explosion

The grand plan includes many future modules and media types.

Mitigation:

- implement images and 3D first
- treat audio, video, animation, Brain-Train, and LoRA training as future lanes
- build schemas that can accept future asset kinds without implementing them now

## 17. What Is Out Of Scope For MVP

Out of scope:

- LoRA training
- Brain-Train adapters
- full audio generation
- full video generation
- animation generation
- full engine runtime integration
- live game simulation
- full map editor
- full 3D mesh editor inside Nymphs World
- replacing Blender or Godot
- copying Chronicler source code

Allowed later:

- optional LoRA consistency module
- audio reference assets
- cutscene/animatic references
- richer graph views
- engine-specific exporters
- Blender asset library helpers

## 18. Source Lessons To Keep

### Chronicler

Keep:

- offline local vault
- Markdown-first pages
- frontmatter
- wikilinks and backlinks
- file watcher and indexer
- diagnostics
- image/media awareness

Do not copy code. Chronicler is source-available, not open-source.

### WORBI

Keep:

- rich writing shell
- information panel concept
- safe workspace file APIs
- image generation bridge idea
- AI tool permission model
- tags and relationships
- maintenance/diagnostic mindset
- dialogue/scene planning ideas

Change:

- move canonical project state out of browser localStorage
- avoid hidden graph truth that drifts from source files
- make assets/project data entity-aware
- avoid one large all-purpose app surface

### 3DGenStudio

Keep:

- production stages
- asset versions
- parent-child output lineage
- visual graph as optional power view
- workflow output capture
- project asset library

Change:

- use project-readable manifests instead of SQLite as the only truth
- adapt stages to worldbuilding/game production, not only mesh pipelines

### NymphsCore Manager And Registry

Keep:

- manifest-owned module contracts
- module action groups
- module artifact roots
- generic Manager shell
- module-owned install/status/start/stop/log/fetch behavior

Change:

- Nymphs World should use module capabilities, not become a module installer

### Nymphs Image

Keep:

- generated output sidecars
- batch metadata
- parts workflow
- `/generate`, `/active_task`, `/api/outputs`
- OpenRouter/Gemini support as optional image lanes

Change:

- import/promote outputs into the world project

### Pixal3D

Keep:

- source image prep
- warmup as real state
- generation and export separation
- GLB output
- Clear GPU Memory/reset path
- low-VRAM profile awareness

Change:

- use from entity asset actions, not as the user's main project home

### TRELLIS.2

Keep:

- image-to-3D alternate path
- retexture path
- profiles and GGUF quant choices
- square source preprocessing
- GLB export

Change:

- treat as backend capability behind asset actions

### Brain

Keep:

- contextual local assistant
- MCP/tool bridge idea
- Open WebUI as optional surface
- model management via module actions

Change:

- make Brain page-aware and permissioned
- avoid always-on chat as the main UX

## 19. Research References

Primary local plan:

- `/home/nymph/NymphsCore/docs/Ideas/NYMPHS_WORLD_GRAND_PLAN.md`

Linked source repositories:

- WORBI app source: https://github.com/rauty79/WORBI
- WORBI Nymph module: https://github.com/nymphnerds/worbi
- Chronicler: https://github.com/mak-kirkland/chronicler
- 3DGenStudio: https://github.com/visualbruno/3DGenStudio
- NymphsCore: https://github.com/nymphnerds/NymphsCore
- Nymphs registry: https://github.com/nymphnerds/nymphs-registry
- Nymphs Image: https://github.com/nymphnerds/zimage
- TRELLIS module: https://github.com/nymphnerds/trellis
- Pixal3D module: https://github.com/nymphnerds/Pixal3D
- Pixal3D upstream: https://github.com/TencentARC/Pixal3D
- Microsoft TRELLIS.2: https://github.com/microsoft/TRELLIS.2
- Brain module: https://github.com/nymphnerds/brain
- Brain-Train module: https://github.com/nymphnerds/brain-train
- LoRA module: https://github.com/nymphnerds/lora

Comparable tool references:

- Obsidian data storage: https://obsidian.md/help/data-storage
- Obsidian internal links: https://obsidian.md/help/links
- Obsidian properties: https://help.obsidian.md/properties
- Yarn Spinner nodes/options: https://docs.yarnspinner.dev/v/2.1/getting-started/writing-in-yarn/lines-nodes-and-options
- Twine passage links: https://twinery.org/reference/en/editing-stories/linking-passages.html
- Godot 3D scene import: https://docs.godotengine.org/en/4.0/getting_started/workflow/assets/importing_scenes.html
- Blender asset libraries: https://docs.blender.org/manual/en/5.0/files/asset_libraries/index.html

## 20. The Short Version

Implement Nymphs World as a readable local vault plus an indexer and asset/job
ledger. Build one complete entity-to-image-to-3D-to-export loop before widening
the product.

The winning spine is:

```text
world file
  -> entity page
  -> asset manifest
  -> job record
  -> module output import
  -> diagnostics
  -> export manifest
```

Everything else can grow from that without collapsing into a giant, fragile app.
