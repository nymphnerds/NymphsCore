# Nymphs World Product Implementation Plan

Status: clean restart from `NYMPHS_WORLD_GRAND_PLAN.md`.

This document is a practical implementation plan for Nymphs World. It uses the
grand plan as the source of intent, then folds in lessons from WORBI, Chronicler,
3DGenStudio, NymphsCore, Brain, Nymphs Image, TRELLIS.2, and Pixal3D.

It supersedes the earlier attempted implementation drafts in this folder.

## Essential Reading Before Implementation

Before starting any Nymphs World implementation that touches modules, module
actions, module status, Manager integration, installed UIs, lifecycle controls,
model fetch, generated outputs, or `$HOME/NymphsData`, read
`docs/NYMPHS_MODULE_MAKING_GUIDE.md`.

That guide is the source-of-truth NymphsCore module standard. This plan is
product direction, not a module contract. If this plan and the module guide
disagree, the module guide wins and this plan should be updated.

Use the current working Nymph modules as live references while implementing:

- `~/NymphsModules/worbi`
- `~/NymphsModules/zimage`
- `~/NymphsModules/trellis`
- `~/NymphsModules/brain`
- `~/NymphsModules/lora`
- installed module manifests under `~/Nymphs-Brain`, `~/Z-Image`, `~/Pixal3D`,
  and other installed module roots when available

Do not invent module registry data, remote repository URLs, manifest URLs,
status states, Manager behavior, or module lifecycle behavior. If a repo or
registry entry does not exist yet, keep the implementation local and explicit
until the real source URL is known and published.

Key implementation rule:

```text
Nymphs World may call modules, but it must preserve the NymphsCore module
standard: modules own their manifests, lifecycle scripts, status/start/stop/
open/log actions, model/artifact/cache paths, installed UI, and declared action
groups. The Manager and Nymphs World should stay generic.
```

## Working Base App Decision

Working decision date: 2026-05-28

Nymphs World should use WORBI as the base app unless implementation review
proves that this creates more risk than value.

This replaces the earlier open question about whether to fork/evolve WORBI or
start again. The current Nymphs World scratch UI is a prototype only; it should
not be polished into the product shell.

WORBI is the base because it already proves the app shape Nymphs World needs:

- a real creative workspace instead of a dashboard
- file explorer and project folders
- rich writing/editor surface
- templates and exports
- right-side assistant/tool panel
- AI tool permissions
- image-generation bridge
- story, quest, scene, dialogue, timeline, graph, and relationship surfaces

Chronicler remains an important reference, but mainly for vault behavior:

- local folder ownership
- readable Markdown files
- wikilinks and backlinks
- YAML/frontmatter infoboxes
- templates
- image embeds and galleries
- broken-link and vault diagnostics
- opening an existing world folder without locking the user into an opaque app

The working thesis is:

```text
WORBI base app
  + Chronicler-style vault discipline
  + NymphsCore module-standard lifecycle and action bridge
  = Nymphs World
```

Implementation should therefore start from the local WORBI source and evolve it
into Nymphs World:

- WORBI source: `~/NymphsModules/worbi-source`
- WORBI Nymph module wrapper: `~/NymphsModules/worbi`
- Chronicler source reference: `~/NymphsRefs/chronicler`

Do not copy Chronicler code. It is source-available, not open-source. Study it
for behavior, UX, and vault architecture only.

Do not blindly ship WORBI unchanged as Nymphs World. WORBI is already an
advanced working app and should be respected as the base. The job is to edit it
to fit the Nymphs World plan, preserve the working systems that fit, extend the
useful systems, and replace specific internals only where they block Nymphs
World features.

First implementation rule:

```text
Keep WORBI working while it becomes Nymphs World.
```

Preserve WORBI by default. Refactors should be justified by the plan, but they
may be deep where the current architecture blocks the product. The filesystem,
project format, metadata storage, asset handling, and module integration can be
rebuilt where needed. Start by renaming/reskinning, then align the project
model, add Nymphs World page/entity concepts, and make media generation a native
part of the workspace.

Media generation should be incorporated into the WORBI-derived workspace as
page-bound creative actions:

- generate or import a reference image from the current page
- attach generated images to the page/entity that requested them
- preserve prompts, seeds, settings, source files, and module provenance
- mark images as draft, candidate, approved, or rejected
- send approved images to TRELLIS.2 or Pixal3D
- attach generated 3D outputs back to the originating page/entity
- keep module status/start/stop/open/log handling aligned with the Nymphs module
  standard

## 1. Product Definition

Nymphs World is the project room for a game world inside NymphsCore.

It should open like a calm worldbuilding and writing app. The user should first
feel: "This is where my world lives." From there, the app grows into a
pre-production workspace: pages gain references, decisions, prompts, generated
images, 3D tests, module jobs, and export readiness, all attached to the world
objects they came from.

The main object is the world page, not the generated asset.

The app should answer five everyday questions:

1. What is in this world?
2. What are we still thinking through?
3. What connects to what?
4. What references and production assets belong to this thing?
5. What should we make or decide next?

The first mental model should stay simple:

```text
scratch notes -> world pages -> links -> context assets -> next actions
```

The deeper production model should appear only when the user asks for it:

```text
world page -> brief -> reference -> module job -> reviewed output -> export
```

## 2. What The Research Changes

The grand plan already has the right product hiding inside it. The important
correction is emphasis.

Nymphs World should not be framed as an asset generator with a wiki attached.
It should be framed as a world vault and writing workspace that can call the
NymphsCore modules from the exact page where their outputs are needed.

The best source lessons are:

| Source | Keep | Change for Nymphs World |
|---|---|---|
| Grand plan | World pages, scratch, canon states, module actions, page-bound assets, exports | Make the app feel natural and page-first before exposing production depth |
| WORBI | Rich editor, tabs, story tools, scene/dialogue work, AI tool permissions, exports, right-side context panel | Move canonical metadata out of localStorage and scattered registries into project files |
| Chronicler | Markdown vault, wikilinks, backlinks, infoboxes, file watcher/indexer, broken-link diagnostics, media resolution | Use as a vault architecture reference, not as code to copy |
| 3DGenStudio | Production board thinking, task/job visibility, asset pipeline awareness | Keep this as an optional production lens, not the main screen |
| NymphsCore modules | Manifest actions, status/start/stop/open/logs, module-owned runtimes | Nymphs World calls modules through a generic bridge and imports outputs back into the project |
| Brain | Context-aware assistant, model/tool orchestration, local knowledge access | Make Brain page-aware and permissioned instead of a permanently central chat panel |
| Nymphs Image | Local image generation, presets, parts, batch metadata, outputs under `NymphsData` | Use for page-context reference creation and image variants |
| TRELLIS.2 and Pixal3D | Image-to-3D and retexture backends with server info, active task, generate endpoints | Use from approved page images and import GLB outputs with provenance |

LoRA and Brain training are parked as future optional capabilities. They should
not shape the first implementation.

## 3. First Usable Version

The first useful Nymphs World build should let a user make a small playable
world bible without touching a module dashboard.

It should support:

1. Create or open a Nymphs World project folder.
2. Write scratch notes.
3. Promote a scratch note into a real world page.
4. Create pages for characters, places, factions, story arcs, scenes, quests,
   items, systems, and lore.
5. Link pages with `[[wikilinks]]`.
6. See backlinks and related pages.
7. Add lightweight frontmatter: title, type, status, tags, summary, image.
8. Attach references and media to a page.
9. Ask Brain about the current page or selected group of pages.
10. Generate or import a reference image into the current page.
11. Mark an image as approved.
12. Send an approved image to TRELLIS.2 or Pixal3D.
13. Import the 3D output back onto the page.
14. See a simple "needs work" list for missing summaries, broken links, orphan
    media, unresolved questions, and outputs waiting for review.

The first successful demo should be:

```text
Create project
Create five world pages
Link them
Attach references
Ask Brain for contradictions or next questions
Generate one page image
Approve it
Generate one GLB from it
See everything still attached to the original page
Export a compact world bible folder
```

## 4. Main App Areas

Nymphs World should have a few stable areas, not a sprawling dashboard.

### 4.1 Scratch

Scratch is where uncertain work belongs.

It should support:

- inbox notes
- rough ideas
- open questions
- decisions
- references
- prompt experiments
- rejected ideas
- canon candidates
- maybe-later material
- module outputs waiting for review

Scratch items can be promoted into:

- a canon page
- a draft page
- a task
- a reference asset
- a story beat
- an archive note

This matters because worldbuilding is not always clean. The app should let a
team think before forcing structure.

### 4.2 World Pages

World pages are the main surface of the app.

Every important thing gets a readable Markdown page with optional structured
frontmatter and sidecar metadata. A page can be almost empty at first, then
become more structured as it matures.

Useful page types:

- character
- place
- faction
- story arc
- quest
- scene
- dialogue thread
- timeline event
- map
- item
- system
- lore

The page type should help the app offer relevant actions. It should not trap the
user inside rigid forms.

### 4.3 Story

Story should reuse the strongest WORBI ideas, especially scenes, timelines,
branching dialogue, and story bible export.

The story area should have two equal surfaces:

- readable pages for canon, prose, notes, and export
- a node-based graph for designing arcs, scenes, dialogue, branches, conditions,
  and outcomes

Recommended hierarchy:

```text
story arc -> act/chapter -> quest arc -> beat -> scene -> dialogue thread
```

The node graph is not just a decorative relationship map. It is a core authoring
tool. It should let the user build story arcs from connected beats, attach scenes
to those beats, design dialogue as branching node trees, and see how choices,
flags, consequences, and later story states connect.

The normal page view remains the readable canon record. The graph is where the
structure is worked out.

### 4.4 Systems

Systems pages are for game design thinking.

They should handle things like:

- stats
- flags
- reputation
- progression
- crafting
- inventory
- abilities
- economy
- encounter notes
- conditions and outcomes

At first, these should be flexible pages with helpful templates. They can become
structured exporters later.

### 4.5 Assets

Assets are media and production files attached to world pages.

An asset can be:

- imported reference
- generated image
- prompt
- mask or part image
- approved concept
- GLB or other 3D output
- audio reference
- animation note
- export artifact

Assets should live in the project, but preserve provenance from the module that
made them. The raw module output can stay under `NymphsData`; the project gets a
canonical copy or reference plus metadata.

## 5. UI Shape

The app should feel closer to a calm writing room than a production cockpit.
Production depth appears in context.

Use this shell:

```text
left rail       major areas
left panel      world tree, search results, map list, story list, asset filters
main surface    selected page, editor, story view, map, focused production view
right panel     infobox, backlinks, assets, actions, module status, open questions
bottom strip    running jobs only
```

Default areas:

1. World
2. Search
3. Map
4. Story Graph
5. Systems
6. Assets
7. Reports

The default first screen should be the world itself, not a marketing page and
not a module dashboard.

### 5.1 Entity Page Layout

Every world page should have the same dependable shape:

```text
header: name, type, status, key actions
tabs: overview, details, assets, story, production
body: editor or focused view
right panel: infobox, relationships, backlinks, media, actions
```

For early builds, tabs can be light:

- Overview: normal Markdown page
- Details: structured fields and frontmatter editor
- Assets: references, generated outputs, imported files
- Story: story graph, scene graph, dialogue graph, and timeline connections
- Production: jobs, approvals, export status

### 5.2 Right Panel

The right panel should answer: "What matters about this page right now?"

It should show:

- page infobox
- tags
- status and maturity
- backlinks
- outgoing links
- related pages
- attached assets
- open questions
- quick actions
- module readiness
- recent jobs
- export readiness

WORBI already proves this kind of panel is useful. Nymphs World should make the
panel project-backed and page-aware.

### 5.3 Module Actions

Module actions should feel like actions on the page.

Examples:

- Character page: create portrait reference, create expression sheet, ask Brain
  for contradictions, send approved image to Pixal3D.
- Place page: create mood image, create landmark reference, attach map region,
  generate 3D blockout from approved concept.
- Scene page: ask Brain for blocking notes, create style frame, open dialogue
  designer, export scene brief.
- Item page: create concept image, generate 3D prop, export asset brief.

The module UI can be opened for advanced work, but the first path should be a
Nymphs World action drawer inside the page context.

### 5.4 Story Graph Surface

The Story Graph is a central feature, not a late optional view.

It should open from the Story rail and from the Story tab on relevant pages.
It should support both high-level arc planning and detailed dialogue work.

Core node types:

- story arc
- act or chapter
- quest arc
- beat
- scene
- dialogue thread
- dialogue line
- player choice
- condition
- outcome
- flag or variable
- item/stat/reputation change

Core interactions:

- create nodes from pages or promote nodes into pages
- drag nodes freely
- connect beats, scenes, choices, and outcomes
- collapse a dialogue thread into a single story node
- expand a scene into its dialogue graph
- mark branches as draft, candidate, canon, deprecated, or exported
- filter by character, faction, location, quest, status, or flag
- open any node's source page in the main editor
- edit selected node details in the right panel

The graph must write back to project files. It should not become a hidden
database separate from the world vault.

### 5.5 Recommended Node Graph Foundation

Recommended implementation foundation:

```text
@xyflow/react (React Flow) + project-owned graph JSON + optional ELK/Dagre layout
```

Why this is the best fit:

- WORBI is already React/TypeScript, so React Flow fits the likely app base.
- React Flow is MIT licensed, mature, actively maintained, and built for
  node-based editors with custom React nodes.
- It already handles the hard interaction layer: pan, zoom, drag, select,
  connect, delete, minimap, controls, background, node resizing/toolbars, and
  custom node/edge rendering.
- It does not force a dataflow/runtime model. Nymphs World can keep its own
  story graph schema and save it directly into project files.
- If the app ever moves toward Svelte/Tauri, the same maintainers provide
  Svelte Flow, so the architectural idea survives a frontend choice change.

Use React Flow as the canvas/editor library, not as the data model. Nymphs World
should own graph semantics:

```text
StoryGraph
  nodes: story_arc | act | quest_arc | beat | scene | dialogue_line | choice | condition | outcome
  edges: leads_to | branches_to | requires | triggers | sets_flag | unlocks | contradicts | references
  layout: x/y positions, groups, collapsed state
  source_links: page paths and stable IDs
```

Best reference codebases and product lessons:

| Source | Use For | Caution |
|---|---|---|
| React Flow / xyflow | Embedded graph editor implementation | Use as UI library only; keep NW graph JSON separate |
| Yarn Spinner VS Code extension | Dialogue authoring expectations: script + graph, graph preview, errors, references, project graph, dialogue preview | VS Code extension shape is not the app shape, but the narrative workflow is very relevant |
| Yarn Spinner Console | Compiler-style checks, extraction, graph export to DOT/Mermaid, possible future interoperability | Do not force NW to become Yarn-first unless that is an explicit export target |
| Twine / twinejs | Writer-friendly story map, passages, nonlinear story UX | GPL-3 app code is not a safe thing to copy into NW unless the whole licensing story is checked |
| Rete.js | More advanced dataflow/plugin architecture if NW later needs executable logic graphs | Heavier than needed for story authoring, and some advanced plugins have non-commercial licensing |
| Unity DialogueGraph / ClearDialogue | Concept references for choice, condition, trigger, and branch node types | Not a good web app foundation |

Initial graph MVP:

- React Flow canvas inside the Story area.
- Custom node components for beat, scene, dialogue line, choice, condition, and
  outcome.
- Edges with typed labels and validation rules.
- Right panel edits the selected node/edge.
- Node double-click opens or creates the backing page.
- Graph JSON saves under `world/story/graphs/`.
- A small compiler/checker reports unreachable nodes, missing start nodes,
  broken page links, impossible conditions, and branches without outcomes.

This makes the graph a real writing/design tool while preserving the page vault
as the readable canon layer.

## 6. Data Model

The project must be readable on disk and safe to rebuild.

Recommended project layout:

```text
MyWorld/
  world/
    scratch/
    characters/
    places/
    factions/
    story/
      arcs/
      graphs/
      dialogue/
    quests/
    scenes/
    systems/
    lore/
  assets/
    by-entity/
    library/
  production/
    briefs/
    prompts/
    jobs/
    reviews/
    tasks/
  exports/
  .nymphs-world/
    project.json
    index/
    cache/
```

Source of truth:

- `world/**/*.md`
- page frontmatter
- page sidecars where needed
- `assets/**/manifest.json`
- `production/jobs/**/*.json`
- `.nymphs-world/project.json`

Rebuildable state:

- search index
- backlinks
- graph cache
- diagnostics
- thumbnails
- module health cache

Avoid using browser localStorage for canonical world data. LocalStorage is fine
for UI preferences, panel widths, last-opened tabs, and temporary draft state.

### 6.1 Page Format

Use Markdown with YAML frontmatter:

```markdown
---
id: nw-char-rhea-001
type: character
title: Rhea Vale
status: candidate
tags: [pilot, rebel, old-city]
summary: Test pilot tied to the old-city signal.
image: assets/by-entity/nw-char-rhea-001/images/portrait-approved.png
---

# Rhea Vale

Rhea first appears in [[Opening Scene]] and has old ties to [[The Signal Yard]].

## Notes

- Wants the truth out.
- Does not trust the faction council.
```

The `id` must be stable. File names can change. Links should still be readable.

### 6.2 Asset Manifest

Each page-bound asset folder should have a manifest:

```json
{
  "entity_id": "nw-char-rhea-001",
  "assets": [
    {
      "id": "asset-rhea-portrait-001",
      "kind": "image",
      "role": "portrait",
      "status": "approved",
      "path": "images/portrait-approved.png",
      "source": {
        "type": "module_output",
        "module": "zimage",
        "raw_output": "~/NymphsData/outputs/zimage/2026-05-27/portrait.png",
        "prompt_id": "prompt-rhea-portrait-001",
        "job_id": "job-zimage-rhea-001"
      }
    }
  ]
}
```

This keeps generated work useful without making generation the product center.

### 6.3 Story Graph Record

Story graphs should be stored as project files so they can be inspected,
versioned, and rebuilt into views.

Example:

```json
{
  "id": "graph-opening-arc",
  "title": "Opening Arc",
  "kind": "story_arc",
  "source_page": "world/story/arcs/opening-arc.md",
  "nodes": [
    {
      "id": "beat-signal-found",
      "type": "beat",
      "title": "The signal is found",
      "page": "world/story/beats/signal-found.md",
      "status": "candidate",
      "x": 120,
      "y": 160
    },
    {
      "id": "scene-yard",
      "type": "scene",
      "title": "Signal Yard confrontation",
      "page": "world/scenes/signal-yard-confrontation.md",
      "status": "draft",
      "x": 420,
      "y": 160
    }
  ],
  "edges": [
    {
      "id": "edge-001",
      "from": "beat-signal-found",
      "to": "scene-yard",
      "kind": "leads_to"
    }
  ]
}
```

Dialogue graphs can use the same structure with node types such as
`dialogue_line`, `choice`, `condition`, and `outcome`.

### 6.4 Job Record

Every module job should produce a small record:

```json
{
  "id": "job-pixal3d-rhea-001",
  "module": "pixal3d",
  "entity_id": "nw-char-rhea-001",
  "input_assets": ["asset-rhea-portrait-001"],
  "status": "completed",
  "started_at": "2026-05-27T12:00:00Z",
  "completed_at": "2026-05-27T12:08:00Z",
  "outputs": [
    {
      "kind": "model",
      "path": "assets/by-entity/nw-char-rhea-001/models/rhea-test.glb"
    }
  ],
  "settings": {
    "backend": "Pixal3D",
    "resolution": 1024,
    "low_vram": true
  }
}
```

## 7. Indexing And Diagnostics

Nymphs World needs a vault spine like Chronicler:

1. Scan project folders.
2. Parse Markdown and frontmatter.
3. Discover pages, images, maps, assets, jobs, prompts, and exports.
4. Resolve wikilinks.
5. Build backlinks.
6. Build tag and relationship indexes.
7. Watch file changes.
8. Report broken links, broken media, parse errors, orphan assets, stale jobs,
   and missing approved outputs.

The index should be fast and rebuildable. It should not become the source of
truth.

Diagnostics should be a normal app area, not a developer-only feature. A user
should be able to see:

- pages with broken links
- pages missing summaries
- pages with no type
- assets not attached to any page
- jobs with missing outputs
- outputs waiting for review
- module outputs that were never imported
- pages ready for export

## 8. Module Bridge

Nymphs World should talk to modules through a generic bridge:

```text
module manifest -> status -> start if needed -> health/server_info
-> page-context payload -> job -> poll active_task -> import outputs
```

The bridge must be designed from `docs/NYMPHS_MODULE_MAKING_GUIDE.md`, not from
one module's current behavior. It should consume the standard module contract
and avoid module-specific Manager workarounds.

The bridge should understand the standard Nymphs module concepts:

- install state
- running state
- model/assets readiness
- start/stop/open/log actions
- local URL or endpoint
- declared manager UI
- declared action groups

Nymphs World should not hardcode each module deeply into the app shell. It can
ship adapters for known modules, but those adapters should consume the same
generic module model.

### 8.1 Brain

Brain should be the page-aware assistant.

Useful actions:

- summarize current page
- find contradictions in selected pages
- suggest missing questions
- draft a scene beat
- turn scratch into candidate canon
- create a prompt brief for Nymphs Image
- explain export readiness
- search project files with permission

Brain should inherit WORBI's permission lesson: AI tools that read or write the
world need visible permissions and clear activity logs.

### 8.2 Nymphs Image

Nymphs Image should be used where images belong:

- page header image
- concept reference
- location mood
- item concept
- scene frame
- part extraction
- variants for review

The basic workflow:

```text
page -> prompt brief -> generate -> review variants -> approve -> attach
```

The app should preserve prompt, settings, seed, backend, source image, output
path, and approval status.

### 8.3 TRELLIS.2 And Pixal3D

3D generation should begin from page-approved images.

The basic workflow:

```text
approved page image -> generate GLB -> preview -> accept or rerun -> attach
```

TRELLIS.2 is useful for GGUF image-to-3D, retexture, and profile-driven local
3D experiments.

Pixal3D is useful for higher-fidelity single-image 3D work, especially when the
source image is clean and approved.

Both should expose:

- backend status
- model readiness
- active task
- settings snapshot
- GLB output import
- provenance record

## 9. Implementation Slices

Build in slices that each leave the app usable.

### Slice 1: Project Vault

Goal: open and index a Nymphs World project.

Build:

- project picker
- `.nymphs-world/project.json`
- folder creation
- Markdown page scan
- image/media scan
- file watcher
- index cache
- diagnostics list

Done when:

- a project opens from disk
- pages appear in a tree
- broken links are reported
- changes on disk refresh the app
- index can be deleted and rebuilt

### Slice 2: Page Editor

Goal: make world pages pleasant to write.

Build:

- Markdown editor
- preview or split view
- frontmatter editor
- page type selector
- status selector
- tags
- wikilink autocomplete
- backlinks panel
- page templates

Done when:

- user can create and edit pages
- `[[links]]` resolve
- backlinks update
- renaming a page updates links or reports required changes

### Slice 3: Scratch And Promotion

Goal: make early thinking feel natural.

Build:

- scratch inbox
- note cards or simple scratch pages
- promote to page
- promote to task
- promote to asset request
- archive
- open question list
- decision list

Done when:

- rough notes can become structured pages without copy-paste
- unresolved questions are visible from the right panel

### Slice 4: Page Assets

Goal: attach and review media without leaving the page.

Build:

- asset panel
- import file
- attach existing file
- set page image
- asset manifest
- approval states
- compare/viewer
- orphan asset report

Done when:

- references and outputs attach to a page
- project asset manifests are written
- deleting the index does not lose asset state

### Slice 5: Brain Actions

Goal: make Brain useful inside the world vault.

Build:

- context builder for current page
- selected-page context bundle
- permissioned actions
- action log
- apply suggestion to scratch or page
- prompt brief generation for images

Done when:

- Brain can summarize, question, and draft from current project context
- user can accept output into the page or scratch

### Slice 6: Nymphs Image Action

Goal: create page-context images.

Build:

- check/start Nymphs Image
- health/server info display
- prompt drawer seeded from page context
- generate variants
- poll active task
- import selected output
- store prompt/job metadata

Done when:

- user can generate an image from a page
- approve it
- see it in that page's asset panel

### Slice 7: 3D Action

Goal: turn approved page images into imported 3D tests.

Build:

- select approved source image
- choose TRELLIS.2 or Pixal3D
- show module readiness
- submit generation
- poll active task
- import GLB
- preview model
- attach provenance

Done when:

- a page image becomes an attached GLB
- job settings and source image are preserved

### Slice 8: Story Tools

Goal: make story arcs and dialogue graphs a central authoring surface.

Build:

- story arc pages
- scene pages
- timeline view
- node-based story arc graph
- node-based dialogue graph
- beat, scene, choice, condition, and outcome node types
- page-to-node and node-to-page promotion
- branch status markers
- graph filters by character, location, faction, quest, status, and flag
- dialogue thread records
- scene participant links
- focused dialogue designer as part of the Story Graph surface
- story bible export

Done when:

- a story arc can be authored as connected nodes
- a scene can expand into a branching dialogue graph
- graph nodes can open or create their source pages
- pages, scenes, and dialogue compile into a readable export

### Slice 9: Production And Export

Goal: show what is ready and what still needs work.

Build:

- production board
- review states
- export readiness
- asset brief export
- story bible export
- engine-facing JSON export
- module job history

Done when:

- user can see candidate/canon/production-ready/exported material
- exports are traceable back to pages and assets

## 10. First Technical Decisions

These decisions should be made before coding the first slice.

1. Base app path

   Working decision: use WORBI as the base app. Preserve the existing advanced
   WORBI workspace and edit/extend it into Nymphs World. Do not start from the
   current scratch Nymphs World UI and do not start from a clean app unless
   implementation review proves WORBI cannot carry the plan.

   Refactor storage, metadata, and module integration incrementally where the
   Nymphs World plan requires it. The first rule is that WORBI should keep
   working while it becomes Nymphs World.

2. File format

   Recommended: Markdown plus YAML frontmatter for pages, JSON manifests for
   assets/jobs/project metadata.

3. Index architecture

   Recommended: Chronicler-style vault service with watcher, parser, indexer,
   writer, diagnostics, and renderer layers.

4. Canonical state

   Recommended: project files only. LocalStorage is UI preference storage.

5. Module integration

   Recommended: generic Nymph module bridge plus known adapters for Brain,
   Nymphs Image, TRELLIS.2, and Pixal3D.

6. First entity types

   Recommended: scratch, character, place, faction, story arc, quest, beat,
   scene, dialogue thread, item, system, lore.

## 11. Things To Avoid In The First Build

Avoid:

- making the landing screen a module dashboard
- making a generic relationship graph replace the world tree before pages work
- requiring every page to be fully structured before writing
- treating generated images as more important than the page they belong to
- putting canonical asset state in browser localStorage
- storing world truth in scattered global registries
- forcing all module UIs into iframes as the normal workflow
- building a large production board before the page workflow works
- adding LoRA training as a first-version requirement
- creating a database-first system that users cannot inspect or repair

## 12. Open Questions

These are the questions that still need a human call:

1. Should pages stay pure Markdown, or can some page types have sidecar JSON
   from day one?
2. How strict should the first entity type list be?
3. Should asset imports copy files into the project, reference external files,
   or support both?
4. Which 3D backend is the first supported page action: TRELLIS.2, Pixal3D, or
   both behind the same adapter?
5. What export target matters first: readable world bible, engine JSON, or
   asset bundle?

## 13. Source Notes

Sources reviewed from the grand plan:

- Nymphs Module Making Guide: `../NYMPHS_MODULE_MAKING_GUIDE.md`
- WORBI app source: https://github.com/rauty79/WORBI
- WORBI Nymph module/package repo: https://github.com/nymphnerds/worbi
- Chronicler worldbuilder reference: https://github.com/mak-kirkland/chronicler
- 3DGenStudio pipeline reference: https://github.com/visualbruno/3DGenStudio
- NymphsCore: https://github.com/nymphnerds/NymphsCore
- Nymphs registry: https://github.com/nymphnerds/nymphs-registry
- Nymphs Image / Z-Image module: https://github.com/nymphnerds/zimage
- TRELLIS.2 module: https://github.com/nymphnerds/trellis
- Pixal3D module: https://github.com/nymphnerds/Pixal3D
- Pixal3D upstream source: https://github.com/TencentARC/Pixal3D
- Brain module: https://github.com/nymphnerds/brain
- LoRA module: https://github.com/nymphnerds/lora
- Brain-Train module: https://github.com/nymphnerds/brain-train
- React Flow / xyflow: https://github.com/xyflow/xyflow
- Yarn Spinner VS Code extension: https://github.com/YarnSpinnerTool/YarnSpinner-VSCode
- Yarn Spinner Console: https://github.com/YarnSpinnerTool/YarnSpinner-Console
- Twine / twinejs: https://github.com/klembot/twinejs
- Rete.js: https://github.com/retejs/rete

The LoRA and Brain-Train sources are intentionally treated as future optional
work for this plan.
