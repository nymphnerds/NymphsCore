# Nymphs World Grand Plan

Generated: 2026-05-27  
Status: Draft for review and refinement  
Short name: NW

## Purpose

Nymphs World is the planned worldbuilding and pre-production module for NymphsCore.

NymphsCore is our two-person game development pipeline. NW is the first major creative production stage: the place where a game world is born, structured, expanded, visualized, and prepared for the rest of the toolchain.

The core idea:

> NW is where a game world becomes a production-ready project.

NW should store the world, organize the assets, preserve creative context, and connect each piece of lore to the NymphsCore tools that can help make it real.

It should eventually replace WORBI as the main worldbuilding module. WORBI is currently the starting material and reference point. If WORBI proves suitable, NW can grow from it. If not, WORBI remains a reference and migration source.

Just as importantly:

> NW is a scratch pad for a small team building a world together.

It should not demand that every thought is clean, final, categorized, or production-ready. A two-person game team needs somewhere to throw rough ideas, argue with the world, collect references, make decisions, and gradually promote scraps into canon.

## Starter Framework: Smallest Expandable NW

This is the stripped-down first structure.

If the full plan feels huge, start here.

The first version of NW should help a small team answer five questions:

```text
1. What is in our world?
2. What are we still thinking about?
3. What connects to what?
4. What assets do we have?
5. What should we make next?
```

Everything else can grow later.

### The Smallest Mental Model

NW starts as:

```text
Scratch pad
  -> world pages
    -> links
      -> assets
        -> next actions
```

Or, even simpler:

> Capture ideas, turn the good ones into pages, connect the pages, attach assets, and track what is missing.

This is enough to start building a world.

### The Five Starting Buckets

The first draft only needs five top-level creative buckets:

```text
1. Scratch
2. World
3. Story
4. Systems
5. Assets
```

What each bucket means:

- **Scratch:** loose notes, ideas, questions, references, decisions, maybe-later thoughts.
- **World:** characters, places, factions, cultures, creatures, items, lore.
- **Story:** arcs, quests, scenes, dialogue, branches, consequences.
- **Systems:** rough gameplay concepts, stats, abilities, rules, flags, progression, encounter ideas.
- **Assets:** images, models, audio, animation, references, generated outputs, approved assets.

This gives the team enough structure to avoid chaos without demanding a full database on day one.

### Minimal Project Folder

The first project folder can be much smaller than the full future layout:

```text
NymphsWorlds/
  ProjectName/
    scratch/
      inbox.md
      decisions.md
      questions.md
      daily/
    world/
      characters/
      places/
      factions/
      lore/
      items/
      creatures/
    story/
      arcs/
      quests/
      scenes/
      dialogue/
    systems/
      concepts/
      stats.md
      abilities.md
      flags.md
      progression.md
    assets/
      by-entity/
      library/
      incoming/
      approved/
    .nymphs-world/
      project.json
      index.json
```

Expansion path later:

```text
places/
  -> geography/
    -> continents/
    -> regions/
    -> settlements/
    -> biomes/
    -> terrain/
    -> routes/
    -> dungeons/

systems/
  -> combat/
  -> economy/
  -> crafting/
  -> reputation/
  -> encounters/
  -> loot/

assets/
  -> production jobs
  -> generated outputs
  -> exports
```

The starter structure should not block the larger structure. It should simply hide most of it until needed.

### Minimal Page Types

Start with only these page types:

```text
scratch-note
concept
character
place
faction
item
story-arc
quest
scene
system
asset
```

That is enough.

Do not start with dozens of specialized templates. Specialized pages can emerge later.

Recommended first templates:

```text
Character:
  Summary
  Role
  Personality
  Relationships
  Story Use
  Visual Direction
  Assets
  Open Questions

Place:
  Summary
  Geography
  People/Factions
  Story Use
  Mood/Visual Direction
  Assets
  Open Questions

Story Arc:
  Premise
  Beats
  Choices/Branches
  Characters
  Places
  Consequences
  Open Questions

System/Concept:
  Raw Idea
  Why It Might Be Cool
  Player Experience
  Possible Rules
  Connected World Pieces
  Open Questions
  Next Step
```

### Minimal Metadata

Every page only needs a tiny amount of metadata at first:

```yaml
---
id: character.nyra
type: character
status: rough
tags: []
---
```

Useful statuses:

```text
scratch
rough
candidate
canon
needs-assets
production-ready
archived
```

Do not over-design metadata early. Add fields only when the team repeatedly needs them.

### Minimal UI

The first UI should have three stable zones:

```text
Left:   bucket/tree/search
Middle: selected page
Right:  context/actions/assets
```

The first rail only needs:

```text
Scratch
World
Story
Systems
Assets
Search
```

The middle should be calm:

- page title
- status
- tags
- editor/viewer
- backlinks/mentions

The right panel should answer:

- what is this?
- what is it linked to?
- what assets does it have?
- what questions are open?
- what can we do next?

No global production board is required at first.

No giant dashboard is required at first.

No always-open AI chat is required at first.

### Minimal Workflows

The first version should support six workflows.

#### 1. Capture

```text
Write rough idea -> save to scratch inbox
```

Example:

```text
"Village elders remember broken promises."
```

#### 2. Promote

```text
Scratch note -> concept/page
```

Example:

```text
scratch/inbox.md
  -> systems/concepts/village-memory.md
```

#### 3. Connect

```text
Page -> links to other pages
```

Example:

```text
Village Memory links to Emberwell, Ashfall, Elder Miyako, reputation.
```

#### 4. Attach

```text
Page -> asset folder -> image/reference/model/audio
```

Example:

```text
world/characters/nyra.md
assets/by-entity/characters/nyra/references/
```

#### 5. Decide

```text
Open question -> decision note -> page update
```

Example:

```text
Question: Is village memory per village or per faction?
Decision: Per settlement first, faction layer later.
```

#### 6. Make Next

```text
Page -> next action
```

Examples:

```text
Generate portrait
Find references
Write quest beat
Define rough stat
Make map sketch
Ask Brain to summarize contradictions
```

If these six workflows feel good, NW has a strong foundation.

### Minimal Asset Pattern

Every important page can have one asset folder.

```text
assets/by-entity/<type>/<slug>/
  references/
  generated/
  approved/
  notes.md
```

That is enough for draft one.

Later this can expand into:

```text
concepts/
portraits/
models/
textures/
audio/
animation/
exports/
jobs/
```

The important first rule:

> Assets should belong to a page before they belong to a backend.

### Minimal Graph

The first graph does not need to be a huge visual node canvas.

Start with a simple relationship index:

```text
backlinks
outgoing links
linked assets
linked story pages
linked system pages
open questions
```

A visual graph can come later.

The first useful graph is the right panel saying:

```text
Nyra is linked to:
  Ashfall
  Ember Reversal
  Ashen Crown story arc
  Dusk Blade
  portrait_v001.png
```

### Minimal Brain Use

Brain should be helpful but optional in the first draft.

Starter actions:

- summarize this page
- ask questions about this idea
- turn scratch note into a concept page
- find contradictions
- suggest links to existing pages
- draft a first version of a character/place/quest/system page
- make an image prompt from this page

Brain should not be required to use NW.

### Minimal Module Use

For draft one, only one module action is required:

```text
Generate or import one image and attach it to a page.
```

That proves the page-to-asset loop.

Everything else can come later:

- Pixal3D/TRELLIS mesh generation
- LoRA datasets
- audio
- video
- animation
- export packages

### The First Real Goal

The first real goal is not "build the whole pipeline."

The first real goal is:

```text
A team can create a small world bible with:
  5 characters
  5 places
  2 factions
  1 story arc
  3 quests/scenes
  3 rough gameplay concepts
  linked references/assets
  open questions and decisions
```

If NW can make that feel good, the larger system is worth building.

### What To Avoid In Draft One

Avoid:

- too many page types
- too much metadata
- mandatory schemas for rough thoughts
- giant dashboards
- always-visible production tools
- visual graph as the primary UI
- requiring module backends for basic worldbuilding
- treating generated assets as more important than the world pages

Draft one should feel like:

> A calm world scratchpad that can grow teeth when needed.

## Reference Apps Studied

## Reference Repository Links

Primary references from this planning session:

- WORBI app source: https://github.com/rauty79/WORBI
- WORBI Nymph module/package repo: https://github.com/nymphnerds/worbi
- Chronicler worldbuilder reference: https://github.com/mak-kirkland/chronicler
- 3DGenStudio pipeline reference: https://github.com/visualbruno/3DGenStudio

Related NymphsCore module references:

- NymphsCore: https://github.com/nymphnerds/NymphsCore
- Nymphs registry: https://github.com/nymphnerds/nymphs-registry
- Nymphs Image / Z-Image module: https://github.com/nymphnerds/zimage
- TRELLIS.2 module: https://github.com/nymphnerds/trellis
- Pixal3D module: https://github.com/nymphnerds/Pixal3D
- Pixal3D upstream source: https://github.com/TencentARC/Pixal3D
- Brain module: https://github.com/nymphnerds/brain
- LoRA module: https://github.com/nymphnerds/lora
- Brain-Train module: https://github.com/nymphnerds/brain-train

### WORBI

Source checkout:

```text
/home/nymph/NymphsModules/worbi-source
```

Current module/package repo:

```text
/home/nymph/NymphsModules/worbi
```

WORBI already has several foundations NW can learn from or reuse:

- local worldbuilding workspace
- React/Express app structure
- file explorer and rich editor
- game-oriented templates
- tags and locations
- wiki links
- relationship graph
- timeline
- scenes and dialogue tools
- LLM assistant/tool permissions
- Z-Image integration
- story bible and game export thinking
- Godot/NymphsAIFramework integration notes

WORBI is currently more "files plus metadata" than "game-world entity database." NW needs to move toward an entity-first vault where pages, assets, relationships, jobs, and exports are all connected.

### Chronicler

Reference:

```text
https://github.com/mak-kirkland/chronicler
```

Important license note: Chronicler is source-available, not open-source. It can be studied for product patterns, but code should not be copied or reused.

Useful concepts:

- local vault
- plain Markdown files
- YAML frontmatter
- wikilinks
- backlinks
- page inserts/transclusion
- infoboxes
- templates
- image galleries and carousels
- tag index
- broken link/image diagnostics
- user ownership of project files

Chronicler answers the question:

> Where does the world live?

NW should use this philosophy: user-owned, readable, portable world data.

### 3DGenStudio

Reference:

```text
https://github.com/visualbruno/3DGenStudio
```

Useful concepts:

- project-based asset production workspace
- Kanban board for asset stages
- node graph for asset relationships
- unified asset library
- image -> edit -> mesh -> texture -> export flow
- parent-child asset lineage
- per-card actions
- workflow/job status
- local asset storage

3DGenStudio answers the question:

> How does an asset move through production?

NW should adapt this pattern for worldbuilding and game pre-production, where an entity page can produce images, models, audio, animation references, cutscene plans, engine exports, and tasks.

### NymphsCore Modules

Relevant modules:

- Brain
- Nymphs Image / Z-Image
- TRELLIS.2
- Pixal3D
- LoRA
- Brain-Train
- future audio generation
- future video generation
- future animation generation
- future Blender/engine helpers

Useful existing NymphsCore patterns:

- module-owned UI
- manifest-declared entrypoints
- install/status/start/stop/open/logs contract
- `manager_ui` local URL or local HTML
- WebView2 host in Manager
- module action messages
- artifact roots for outputs, logs, models, config
- shared secrets such as OpenRouter and Hugging Face tokens

NW should not duplicate every module UI. It should host or surface module frontends contextually when needed.

Future module categories should be expected from the beginning:

- audio generation for music, ambience, sound effects, dialogue scratch tracks, and voice references
- video generation for mood shots, concept trailers, animatics, scene previews, and cutscene reference
- animation generation for character motion, creature motion, combat moves, emotes, camera moves, and retargetable clips

The NW asset model should treat these future backends the same way it treats image and 3D backends: contextual action, job record, output capture, entity attachment, review, approval, and export.

## Big Product Definition

NW is three things at once:

1. A local-first world vault.
2. A page-bound asset workspace.
3. A NymphsCore module orchestration layer.

In plain language:

> NW stores the world. NymphsCore backends make pieces of the world real. NW keeps those pieces attached to the world they came from.

## Second-Pass Architecture Proposal

The tighter version of NW is not "WORBI plus many buttons." It is a world database with a live vault index, a production asset graph, and contextual module bridges.

The core architecture should be:

```text
Project Folder
  -> Vault Scanner / Watcher
    -> World Index
      -> Unified Graph
        -> Entity Pages, Asset Panels, Maps, Boards, Module Jobs, Exports
```

This is the biggest lesson from studying Chronicler and WORBI together:

- Chronicler has the right local-first vault/indexer philosophy.
- WORBI has useful worldbuilding tools, editor concepts, AI tooling, graph/timeline/dialogue features, and game export intent.
- NW should combine those ideas under one project-local data model instead of letting tags, scenes, locations, graph data, prompts, images, and browser state become separate islands.

### The NW Spine

NW should have one central project service, possibly called `WorldRuntime`, `ProjectIndexService`, or `NymphsWorldCore`.

It should:

- open/create the project folder
- scan `world/`, `assets/`, `production/`, and `exports/`
- parse Markdown/frontmatter/entity manifests
- discover images, maps, models, audio, animation, cutscene files, prompts, jobs, and exports
- resolve wikilinks and stable entity IDs
- build backlinks, tag indexes, media indexes, geography indexes, and production indexes
- watch the filesystem for changes
- rebuild only what changed when possible
- emit "index updated" events to the UI
- detect broken links, missing assets, orphan files, stale job references, and missing approved outputs

This makes NW resilient. A user can edit files outside the app, drop assets into folders, or move work between machines, and NW can re-index the world instead of losing track.

### Source Of Truth

The preferred storage model:

```text
Readable source of truth:
  world/*.md
  assets/by-entity/**/manifest.json
  production/jobs/**/*.json
  production/prompts/**/*.json
  .nymphs-world/project.json

Rebuildable cache:
  .nymphs-world/index.json
  .nymphs-world/graph.json
  .nymphs-world/search-cache.*
```

Markdown/frontmatter pages should hold human-authored lore and important entity metadata. JSON manifests should hold asset/job/export metadata that is awkward to maintain in prose. Any SQLite/database layer, if added later, should be treated as a cache or acceleration layer unless there is a very clear reason to make it canonical.

NW should avoid the storage fragmentation visible in WORBI:

- global user tag files as canonical project state
- hidden per-feature registries scattered through the workspace
- browser localStorage as canonical asset state
- graph data separate from files, scenes, tags, images, and relationships

Those patterns are useful as prototypes, but NW should make project-local files and a unified index the truth.

### Stable IDs

Every meaningful thing needs a stable ID that survives renames and folder moves.

Examples:

```text
character.nyra
settlement.city.ashfall
settlement.village.emberwell
biome.glasswood-marsh
terrain.northern-ridge
route.old-cinder-road
weapon.dusk-blade
scene.first-meeting
asset.20260527.0001
job.pixal3d.20260527.0001
```

Pages, assets, jobs, and exports should reference IDs first and paths second. Paths are where files live today. IDs are what they mean.

### Unified Graph

NW should have one graph that merges several kinds of relationships:

- wikilinks from pages
- explicit page relationships
- family/social/faction relationships
- geography containment, such as city inside region
- geography adjacency, such as route connects village to city
- scene participation, such as character appears in scene
- quest usage, such as quest uses location and item
- asset ownership, such as portrait belongs to character
- asset lineage, such as image produced mesh
- module job lineage, such as Pixal3D job produced model from approved image
- export lineage, such as approved model exported to engine package

The graph should be browsable visually, but more importantly it should power diagnostics and production decisions. For example:

- show all assets needed for a city
- show all scenes affected if a character design changes
- show all regions without maps
- show all approved images that do not yet have a mesh
- show every generated model whose source prompt is missing

### The Golden Loop

NW's main user loop should be simple and repeatable:

```text
Create entity
  -> write lore and production brief
  -> attach references
  -> ask Brain for expansion or consistency checks
  -> generate images
  -> review/approve one image
  -> send approved image to Pixal3D or TRELLIS
  -> review/approve model
  -> add audio/animation/cutscene references when relevant
  -> export approved assets
```

Everything in NW should support this loop without forcing the user to remember where the output went or which backend made it.

### Module Job Contract

NW should treat every backend action as a job with context and provenance.

Example:

```json
{
  "job_id": "job.pixal3d.20260527.0001",
  "module_id": "pixal3d",
  "action": "image_to_3d",
  "entity_id": "weapon.dusk-blade",
  "source_assets": ["asset.20260527.0009"],
  "prompt_id": "prompt.weapon.dusk-blade.mesh.v001",
  "status": "completed",
  "module_output_root": "$HOME/NymphsData/outputs/pixal3d/2026-05-27/dusk-blade",
  "imported_outputs": ["asset.20260527.0014"],
  "created_at": "2026-05-27T12:00:00Z",
  "completed_at": "2026-05-27T12:12:00Z"
}
```

Module output folders should be treated as raw backend output. NW should import, attach, or register the chosen outputs into the project asset structure so the world remains portable.

### Contextual Module Bridge

NymphsCore already has useful module primitives:

- `nymph.json` manifests
- module runtime ports
- `manager_ui.local_url`
- start/stop/status/open/logs actions
- manager-hosted WebView UIs
- module action messaging
- artifact roots under `NymphsData/outputs/<module>`

NW should build on this instead of hardcoding every backend. The near-term bridge can launch existing module UIs and import outputs. The long-term bridge can add a clearer NW-aware action contract:

```text
nw.generate_image
nw.image_to_3d
nw.train_lora
nw.ask_brain
nw.generate_audio
nw.generate_video
nw.generate_animation
nw.export_to_engine
```

Each action should accept an entity context package and return output metadata that NW can attach to the graph.

### WORBI Reuse And Refactor Notes

Current recommendation:

> Use WORBI as the starting base for the prototype and narrative systems, then steadily replace the storage spine with the NW vault/index/graph model.

WORBI should not be discarded. It already solves many hard product problems that NW would otherwise have to rebuild from zero.

The valuable parts are mostly feature and workflow gold. The risky parts are mostly storage and architecture debt.

#### WORBI Gold To Preserve

- **Scene Conversation Designer:** idle chatter, ambient NPC banter, interactive dialogue, branching choices, target nodes/threads, choice outcomes, conditions, node positions, pan/zoom canvas, participant filtering.
- **Timeline and scene system:** scenes as time plus place, grouped by era, assignable to files. NW should evolve this into story arcs, scene boards, geography-aware scenes, and production tasks.
- **Game export flow:** template detection, `GameReady/` outputs, rich document to game-format conversion, prompt templates, review-before-save workflow.
- **Main story and quest arc templates:** chapters, steps, player choices, alignment labels, consequences, NPC involvement, and "creative license" directives for engine/runtime LLM behavior.
- **AI tool permissions:** readable toggles around whether the assistant can read, write, search, edit, use graph/tags/locations/files/images. NW needs this, but project-aware and tied to entity context.
- **Relationship graph thinking:** documents as nodes, edges from tags, wikilinks, and explicit relationships. NW should broaden this into the unified entity/geography/narrative/asset/job graph.
- **Rich editor and wikilinks:** TipTap editor, tabs, image embedding, document outline, bookmarks, wiki link autocomplete/popovers, export helpers.
- **Side panels:** tags, locations, timeline, dialogue, graph, images, outline, reminders. NW can keep the idea of calm contextual side panels instead of overwhelming the main page.
- **Information panel concept:** hero image, page notes, attached image/gallery context. NW should keep the right-side entity context idea while moving canonical asset state into project files.
- **Image generation integration:** already shows how generation can live inside the worldbuilder. NW should generalize this to Nymphs Image, Pixal3D, TRELLIS, LoRA, audio, video, animation, and export helpers.
- **Maintenance and diagnostics:** orphaned images, duplicate images, broken links, corrupt documents, stale metadata, empty directories, stale accounts. NW needs this early because generated assets can become chaotic quickly.
- **Story bible / compile thinking:** gathering world files into a coherent bible/export is core to NW's role in the pipeline.
- **Workspace/profile isolation:** NW should become project-first, but WORBI's isolation patterns are still useful as implementation reference.

#### WORBI Pieces To Refactor

- move canonical tags/locations/scenes/conversations from hidden/global registries into project-local pages/manifests/indexes
- make geography entities first-class instead of location labels only
- make scenes, participants, conversations, dialogue, and cutscene planning graph-connected entities
- move canonical page asset state out of browser localStorage
- unify graph data with page links, tags, assets, jobs, narrative branches, and geography
- replace feature-specific sidecar sprawl with a small number of predictable project metadata files
- make Nymphs module integration generic rather than Z-Image-specific
- move from path-only entity references toward stable entity IDs
- make generated files/imported assets portable inside the NW project structure

#### Suggested Base Strategy

The best path is not pure scratch and not pure WORBI extension forever.

Recommended path:

```text
Fork WORBI
  -> rename/re-skin into Nymphs World prototype
  -> keep editor, explorer, timeline, dialogue, graph, AI, export, and generation flows
  -> introduce NW project folder structure
  -> introduce stable IDs and project-local manifests
  -> build the vault indexer/graph cache alongside current services
  -> migrate each WORBI metadata island into the NW data model
  -> generalize module integration beyond Z-Image
```

In short:

> WORBI-first, NW-architecture underneath.

## Product Pillars

### 1. Local World Vault

NW stores each project as a local folder that the user owns.

The vault should be:

- readable
- backup-friendly
- git-syncable later if desired
- portable across machines
- understandable without NW running

The world should not be trapped only inside an opaque database.

### 1.5 Team Scratch Pad

NW should support messy creative work before it becomes structured world data.

For a small team, the scratch pad is not a side feature. It is where the world starts.

Useful scratch areas:

- inbox
- daily notes
- loose ideas
- mood notes
- design questions
- unresolved decisions
- meeting notes
- reference dumps
- sketches and screenshots
- prompt experiments
- discarded ideas
- canon candidates
- "maybe later" ideas

The scratch pad should make it easy to capture:

- a one-line idea
- a pasted image/reference
- a rough quest premise
- a character name
- a scene fragment
- a map note
- a mechanic thought
- a question for the other teammate
- a decision made during discussion
- a module output worth reviewing later

Scratch notes should be promotable.

Example:

```text
Scratch note
  -> canon lore note
  -> character/location/quest/entity page
  -> production task
  -> asset request
  -> story arc beat
  -> rejected/archive note
```

This keeps NW from becoming too formal too early.

Recommended scratch folder:

```text
world/_scratch/
  inbox.md
  decisions.md
  questions.md
  references.md
  discarded.md
  daily/
    2026-05-27.md
```

Useful scratch metadata:

- owner
- status: raw, reviewed, promoted, archived
- linked entity
- linked asset
- tags
- created date
- decision date

Important rule:

> Not every note needs to become an entity.

The UI should make rough work feel welcome. A small team should be able to use NW during a live brainstorming session without stopping every two minutes to satisfy schema fields.

### 1.6 Slow Development Space

NW should be a place where the world and systems develop slowly.

A small team should not have to decide every mechanic, stat, quest branch, faction rule, item schema, or export format up front. The app should support gradual refinement.

Recommended maturity states:

```text
scratch
  -> rough
  -> discussed
  -> candidate
  -> canon
  -> production-ready
  -> exported
```

This applies to:

- lore
- characters
- geography
- factions
- story arcs
- quests
- dialogue branches
- RPG stats
- abilities
- items
- encounters
- asset briefs
- style guides
- export schemas

Each idea should be able to start as plain text.

Example:

```text
"Maybe old ruins should increase magic instability."
```

Later it can become:

```text
world/systems/magic-instability.md
world/biomes/ruined-wilds.md
world/encounters/unstable-ruins-ambush.md
world/abilities/volatile-spark.md
```

The UI should make this progression easy:

- capture first
- tag lightly
- link when useful
- promote when ready
- structure only when structure helps
- export only when the idea is stable enough

Brain can help in this slow-development space by:

- summarizing loose notes
- extracting possible entities
- finding contradictions
- turning rough ideas into candidate system pages
- proposing questions for the team
- listing what needs a decision before production

Important rule:

> NW should never punish unfinished thinking.

The system should welcome uncertainty as a normal part of game development.

### 1.7 General Gameplay Systems Incubator

Gameplay systems should be able to emerge naturally inside NW.

NW should not assume every project has the same RPG rules, combat model, stats, inventory, magic, dialogue checks, factions, crafting, survival mechanics, or progression. It should provide a flexible place to grow those systems from rough notes into something more concrete.

The first version should support "general gameplay system" pages before it supports rigid system editors.

Examples:

```text
world/systems/combat.md
world/systems/progression.md
world/systems/dialogue-checks.md
world/systems/reputation.md
world/systems/exploration.md
world/systems/crafting.md
world/systems/economy.md
world/systems/magic.md
world/systems/stealth.md
world/systems/survival.md
world/systems/settlement-growth.md
```

Each system page can start as plain writing:

- what the system is meant to feel like
- what player behavior it rewards
- what fantasy it supports
- what world/lore it connects to
- what content depends on it
- open questions
- possible rules
- possible data tables
- balance notes
- export needs

Then, only when useful, NW can add structured blocks:

- stat definitions
- formulas
- tables
- flags
- progression tiers
- unlocks
- conditions
- outcomes
- references to quests/items/entities
- export fields

Recommended maturity path:

```text
general idea
  -> rough system note
  -> candidate rule
  -> prototype table
  -> linked content
  -> tested/balanced
  -> exportable schema
```

Important rule:

> Gameplay systems should begin as design thinking, not as mandatory forms.

This lets NW work for different kinds of games. An RPG can grow stats, factions, quests, and dialogue checks. A survival game can grow hunger, weather, crafting, and shelter rules. A narrative game can grow choices, flags, relationship states, and scene gates.

#### Gameplay System Conceptualization Pattern

NW should help the team conceptualize a gameplay system from nothing.

A system concept should start with human questions:

- What player experience is this system for?
- What fantasy does it support?
- What problem does it solve?
- What choices does it create?
- What does the player gain, lose, risk, or unlock?
- What world/lore does it connect to?
- What other systems does it touch?
- What content does it need?
- What assets does it need?
- What must be tracked by the game?
- What might be exportable later?

Recommended system concept page:

```markdown
---
id: system.magic-instability
type: gameplay-system
status: rough
owner: nymph
tags: [magic, exploration, risk-reward]
---

# Magic Instability

## Intent

## Player Fantasy

## Core Loop

## Rules In Plain English

## Open Questions

## Connected Lore

## Connected Entities

## Possible Stats / Flags

## Possible UI Needs

## Possible Assets

## Balance Notes

## Export Notes
```

The first useful output of conceptualizing a system is not a perfect spec. It is a clear idea of what the system is trying to do.

Example system concept:

```text
Magic Instability

Intent:
  Make ruined magical places feel dangerous and tempting.

Player fantasy:
  The player risks unstable magic to gain rare power or information.

Core loop:
  Enter unstable area
  -> detect instability level
  -> choose to stabilize, harvest, avoid, or exploit it
  -> receive reward or consequence

Possible rules:
  High instability increases rare resource chance.
  High instability also increases mutation/ambush/miscast risk.
  Certain companions/factions react differently to exploiting it.

Open questions:
  Is instability visible on the map?
  Can it permanently change a location?
  Is it tied to weather, time, or story progression?
```

After that, NW can help split the concept into pieces:

```text
System page
  -> stats/flags
  -> affected locations/biomes
  -> encounter ideas
  -> items/resources
  -> abilities/perks
  -> faction reactions
  -> visual/audio asset needs
  -> export schema candidates
```

Useful Brain actions for system conceptualization:

- "Ask me questions to define this system"
- "Turn this rough idea into a system concept page"
- "List content this system would require"
- "Find existing lore that could connect to this system"
- "Suggest possible stats, flags, and outcomes"
- "Find contradictions with existing systems"
- "Make a first balance table"
- "Make an MVP version of this system"

Recommended UI:

- a plain editor first
- a small right-panel "System Shape" summary
- optional structured blocks for stats/tables/flags
- a "Promote to System" action from scratch notes
- a "Split into linked pages" action when the idea grows
- no mandatory form fields until the team chooses to structure it

Important rule:

> A gameplay system is allowed to be a paragraph before it becomes data.

#### Inventing Any Gameplay Or World Concept

NW should make it easy to invent anything from a rough idea.

The concept might later become a stat, ability, faction rule, traversal mechanic, quest structure, relationship system, crafting material, economy rule, enemy behavior, settlement system, map feature, magic law, ritual, social custom, environmental hazard, UI idea, or something that does not have a category yet.

The point is not to know what kind of thing it is immediately.

The point is to capture it, think with it, connect it, and let its shape emerge.

The team should be able to start with:

```text
"Maybe Nyra has an ability where taking fire damage charges her next attack."
"Maybe old ruins should make magic unstable."
"Maybe villages remember whether you helped them."
"Maybe weapons made from glasswood hate being used underground."
"Maybe companions interrupt dialogue if they know the speaker."
"Maybe rain changes which monsters come out."
```

Then NW helps turn that into:

- a loose concept page
- possible categories
- related lore
- possible rules
- possible player choices
- possible system dependencies
- possible assets
- open questions
- risks and contradictions
- linked characters, locations, quests, items, factions, encounters, or systems
- exportable gameplay data later, only if it becomes stable enough

General concept template:

```markdown
---
id: concept.magic-instability
type: concept
status: rough
owner: nymph
tags: [magic, ruins, risk-reward]
---

# Magic Instability

## Raw Idea
What did we think of?

## Why It Might Be Cool
What feeling, fantasy, or gameplay problem does it support?

## Possible Category
Is this lore, mechanic, system, location rule, enemy behavior, quest pattern, item property, UI idea, or unknown?

## Player Experience
What does the player notice, choose, risk, gain, or lose?

## World/Lore Connection

## Possible Rules

## Possible Content It Needs

## Possible Assets It Needs

## Connected Entities

## Open Questions

## Contradictions / Risks

## Next Step
```

Later, if the concept becomes clearer, NW can help split it into typed pages.

Example:

```text
concept.magic-instability
  -> system.magic-instability
  -> stat.instability
  -> biome.ruined-wilds
  -> encounter.unstable-ruins-ambush
  -> item.glasswood-stabilizer
  -> ability.volatile-spark
  -> vfx.magic-distortion
  -> audio.instability-hum
```

Concepts should support typed blocks only when useful.

Examples of optional blocks:

```text
Rule block
Stat block
Flag block
Quest branch block
Encounter block
Loot block
Dialogue condition block
Asset brief block
Export sketch block
```

Typed concept examples:

```markdown
---
id: relationship.village-memory
type: concept
status: candidate
tags: [settlements, reputation, consequences]
---

# Village Memory

## Meaning
Villages remember whether the player helped, ignored, exploited, or harmed them.

## Possible Category
Reputation / settlement state / quest consequence system

## Player Experience
Returning to a village feels different because of past actions.

## Possible Rules
- hidden village trust value
- public gratitude/fear/resentment states
- vendors, guards, gossip, and quests react to state

## Connected Entities
- settlements
- vendors
- guards
- rumor system
- faction reputation

## Open Questions
- Is this per settlement or per faction?
- Can trust recover?
- Does Brain generate reactive flavor text?
```

Useful concept invention prompts:

- "What is the raw idea?"
- "Why is it cool?"
- "What feeling should it create?"
- "What choices does it create?"
- "What world/lore does it connect to?"
- "What content would make this shine?"
- "What characters, places, quests, enemies, items, factions, or systems might touch it?"
- "What VFX, sound, animation, icon, or UI does it need?"
- "Does it need numbers yet, or is it still just a feeling?"
- "What would be the tiny MVP version?"
- "What would make this too complicated?"
- "What should we decide next?"

The right panel for a concept page could show:

- status
- possible category
- linked characters/locations/items/factions/quests/systems/assets
- open questions
- decision history
- maturity state
- required assets
- possible export readiness
- "promote to entity/system" action
- "split into linked pages" action
- "ask Brain to shape this" action

Example maturity path:

```text
raw thought
  -> concept page
  -> linked notes/entities
  -> candidate category
  -> optional typed blocks
  -> rough rules/content/assets
  -> playtest/design notes
  -> promoted system/entity/data
  -> export-ready data, if needed
```

If the concept does become a stat, ability, item, faction rule, or system later, NW can offer a typed template then. But the first capture should stay general.

Specific typed paths can still exist:

```text
concept
  -> stat
  -> ability
  -> perk
  -> status effect
  -> encounter type
  -> faction rule
  -> settlement mechanic
  -> traversal rule
  -> economy rule
  -> world flag
  -> asset brief
```

Important rule:

> NW should help the team discover what kind of thing an idea is.

It should not force the team to pick a category before the idea has a shape.

Older narrow examples, like inventing a stat or ability, are still valid. They are just special cases of the broader concept workflow:

```text
rough concept
  -> maybe a stat
  -> maybe an ability
  -> maybe a world rule
  -> maybe just lore
  -> maybe nothing yet
```

When a concept becomes gameplay data, then NW can add details such as:

- value range
- trigger
- effect
- scaling
- requirements
- counters/limits
- balance notes
- export readiness

Important rule:

> NW should help the team discover the idea, not force them to know the final mechanic before they write it down.

### 2. Entity-First Worldbuilding

NW should treat important game-world pieces as first-class entities:

- characters
- locations
- factions
- quests
- story arcs
- quest arcs
- scenes
- dialogue threads
- narrative branches
- timelines
- maps
- regions
- biomes
- terrain zones
- continents
- countries/kingdoms
- provinces
- cities
- towns
- villages
- landmarks
- roads/routes
- dungeons/interiors
- lore notes
- items
- weapons
- creatures
- vehicles
- abilities
- cultures
- religions
- organizations
- events

Each entity should have:

- a world page
- structured metadata
- relationships
- tags
- assets
- prompts
- production jobs
- approved/current outputs
- export status

### 3. Narrative Depth

NW should not only store production assets. It should hold the creative truth of the world.

Examples:

- backstory
- biography/bio summary
- family
- relationships
- motivations
- personality
- secrets/spoilers
- history
- culture
- biome/ecology context
- terrain/environment context
- dialogue style
- scene appearances
- quest involvement
- timeline events
- relationship to the player
- relationship to factions and locations

#### Story Arcs And Branching

WORBI already has important narrative work that NW should preserve and upgrade:

- main story templates with chapters, steps, choices, and "creative license" prompts
- quest arc templates with numbered beats and choice consequences
- scene records that bind time, place, and participants
- dialogue files generated per scene
- a Scene Conversation Designer with visual conversation nodes
- idle chatter, ambient NPC banter, and interactive player dialogue threads
- choice branches that can point to target nodes or other threads
- choice outcomes such as stat changes, item grants, perk unlocks, and alignment shifts
- choice conditions such as quests, stats, items, and flags

NW should make these narrative structures first-class instead of treating them as separate files or hidden registries.

Recommended narrative hierarchy:

```text
Story Arc
  Chapter / Act
    Quest Arc
      Beat
        Scene
          Dialogue Thread
            Node
              Choice
                Condition
                Outcome
```

This should remain simple in the normal UI. Most pages should only show quiet narrative links in the side panel. Dedicated narrative views can appear when needed:

- story arc outline
- quest/beat list
- scene board
- branching narrative graph
- dialogue/conversation graph
- timeline view

Important states:

- draft
- canon
- optional
- alternate route
- cut
- deprecated
- exported

The strongest NW version is not one giant graph for everything all the time. It is a calm Chronicler-like page interface with deeper graph tools available for story arcs, quests, scenes, and dialogue when the user opens them.

For a character, NW should answer:

> Who are they, how are they connected, what do they look/sound/move like, what assets exist for them, and what still needs making?

For a location, NW should answer:

> What happened here, who lives here, what it looks/sounds/feels like, what scenes and quests use it, and what production assets exist for it?

For a map, region, biome, or terrain zone, NW should answer:

> What area does it cover, what terrain and ecology define it, what locations/quests/scenes exist inside it, what traversal or gameplay constraints matter, and what maps, heightmaps, blockouts, textures, ambience, VFX, and engine exports exist for it?

#### RPG-Specific Worldbuilding

If NW is used for an RPG, it should understand more than lore and assets. It should also model the pieces that make a world playable.

Useful RPG entity/system types:

- player character
- companion
- NPC
- enemy
- boss
- faction
- reputation track
- quest
- quest stage
- objective
- dialogue branch
- choice
- consequence
- world flag
- skill/stat check
- item
- weapon
- armor
- spell/ability
- perk
- class/archetype
- encounter
- loot table
- vendor
- crafting recipe
- dungeon
- settlement service
- codex entry
- bestiary entry

RPG pages should answer game-design questions as well as lore questions.

For a quest, NW should answer:

> What starts it, what stages exist, what choices can change it, what NPCs/locations/items are involved, what rewards exist, what flags are set, and what endings are possible?

For a companion, NW should answer:

> How do they join, what is their approval/reputation logic, what scenes and banter do they have, what equipment/abilities/assets exist for them, and what personal quest arc belongs to them?

For a faction, NW should answer:

> What does this faction want, who belongs to it, what territories does it control, what reputation states exist, what quests affect it, and what changes in the world when its state changes?

For an encounter, NW should answer:

> Where does it happen, what enemies/NPCs appear, what triggers it, what tactical constraints matter, what loot/rewards exist, and what assets are needed?

Important RPG mechanics to track:

- quest state: unavailable, available, active, completed, failed, hidden, repeatable
- world flags: boolean or enum state set by choices/events
- reputation: faction, companion, settlement, or morality track
- skill/stat checks: requirements, pass/fail branches, alternate solutions
- inventory effects: items granted, removed, required, equipped, crafted, sold
- progression: XP, perks, unlocks, class/archetype changes, ability upgrades
- consequences: immediate result, delayed result, world state change, NPC relationship change
- gating: lock/unlock locations, routes, dungeons, vendors, dialogue, quests, endings
- encounter tuning: level range, difficulty, enemy groups, patrols, boss phases, rewards

This connects directly to WORBI's existing branching dialogue system. WORBI already supports choices with outcomes such as stat changes, item grants, perk unlocks, alignment shifts, and conditions such as quests, stats, items, and flags. NW should promote those ideas from dialogue-only data into a broader RPG state model.

Recommended RPG state model:

```text
Choice
  -> Conditions
      stat >= value
      has item
      quest stage reached
      world flag set
      faction reputation threshold
  -> Outcomes
      set flag
      change quest stage
      grant/remove item
      grant XP/perk/ability
      change reputation
      unlock location/route/dialogue/vendor
      trigger scene/encounter
```

RPG-specific UI should stay contextual:

- quest pages show stages, objectives, branches, rewards, flags, and involved entities
- companion pages show approval, banter, personal quest, outfit/model/voice/animation assets
- faction pages show members, reputation states, territories, quests, and conflicts
- item/weapon pages show stats, crafting, vendors, loot tables, models, textures, and icons
- dungeon/encounter pages show rooms, enemies, loot, objectives, triggers, and map/blockout assets

Possible RPG-first views:

- quest board
- companion board
- faction/reputation board
- encounter planner
- loot/item database
- codex/bestiary
- world state/flags inspector
- skill check and consequence graph

These should not replace the calm entity page. They should be task views opened from RPG entities when needed.

#### Stats And Gameplay Systems

NW should also act as a design scratch pad for RPG systems.

This does not mean NW needs to become a full engine editor on day one. It means the team should be able to define, discuss, revise, and eventually export the systems that make the world playable.

Core system categories:

- attributes/stats
- derived stats
- skills
- abilities
- perks/talents
- classes/archetypes
- status effects
- damage types
- resistances/weaknesses
- item stats
- weapon/armor stats
- enemy stat blocks
- companion progression
- XP/leveling rules
- economy/currency
- crafting rules
- faction reputation
- morality/alignment
- world flags
- difficulty tiers
- encounter tuning
- loot tables
- vendor inventories

Useful stat examples:

```text
Primary attributes:
  strength
  agility
  endurance
  intelligence
  charisma
  luck

Derived stats:
  health
  stamina
  mana/focus
  carry weight
  movement speed
  crit chance
  detection radius
  dialogue influence
```

These are starter examples, not fixed rules. NW should let a project rename, remove, merge, or invent any stat.

Generic RPG stat vocabulary:

```text
Primary attributes:
  strength        physical power, melee damage, carrying, intimidation
  dexterity       precision, ranged accuracy, lockpicking, finesse weapons
  agility         dodging, speed, stealth, initiative, acrobatics
  endurance       health, stamina, poison/bleed resistance, survival
  intelligence    knowledge, crafting, magic, hacking, investigation
  wisdom          intuition, awareness, survival, spirit, judgment
  perception      spotting traps, ranged awareness, secrets, ambush detection
  charisma        persuasion, leadership, trade, intimidation, companions
  willpower       fear resistance, concentration, mental defense, magic control
  luck            crit chance, rare loot, unusual outcomes, random saves

Combat stats:
  health
  stamina
  mana/focus/energy
  attack power
  spell power
  defense/armor
  accuracy
  evasion
  block chance
  parry chance
  crit chance
  crit damage
  attack speed
  movement speed
  initiative

Resistances:
  fire
  frost
  lightning
  poison
  bleed
  disease
  shadow/dark
  holy/light
  psychic/fear
  physical
  magic

Social/exploration stats:
  persuasion
  intimidation
  deception
  insight
  stealth
  lockpicking
  survival
  tracking
  crafting
  medicine
  trade/barter
  reputation
```

Generic skill examples:

```text
Combat:
  one-handed
  two-handed
  archery
  firearms
  shields
  unarmed
  thrown weapons
  light armor
  heavy armor

Magic/special:
  fire magic
  frost magic
  healing
  illusion
  summoning
  ritual
  spirit
  alchemy
  technomancy

Utility:
  stealth
  lockpicking
  trap handling
  crafting
  cooking
  medicine
  survival
  navigation
  investigation
  animal handling

Social:
  persuasion
  intimidation
  deception
  leadership
  performance
  bargaining
```

Generic ability types:

```text
Active:
  player presses a button to use it

Passive:
  always on or automatically modifies behavior

Triggered:
  activates when a condition happens

Toggle:
  stays active until turned off or resource runs out

Ultimate:
  high-impact ability with rare use or long cooldown

Reaction:
  responds to an enemy action, damage event, dialogue moment, or world state

Traversal:
  changes movement or access, such as dash, climb, swim, glide, blink

Social:
  unlocks dialogue, negotiation, intimidation, charm, disguise, or reputation options

Crafting/utility:
  creates, repairs, upgrades, harvests, scans, identifies, or transforms things
```

Generic ability fields:

```text
name
internal_id
type
short description
player-facing description
trigger
target: self/enemy/ally/area/object/location
range
area
resource cost
cooldown
duration
effect
scaling stat
requirements
upgrade path
counters/limits
status effects applied
animation need
VFX need
audio need
icon need
balance notes
export notes
```

Example generic abilities:

```text
Shield Bash
  Type: Active
  Cost: 20 stamina
  Effect: Deals low damage, interrupts casting, may stun weak enemies.
  Scaling: strength + shield skill

Second Wind
  Type: Triggered Passive
  Trigger: Health drops below 25%
  Effect: Restore stamina and briefly increase defense.
  Limit: Once per encounter.

Silver Tongue
  Type: Passive/Social
  Effect: Unlocks extra persuasion choices with merchants and nobles.
  Scaling: charisma or persuasion.

Shadow Step
  Type: Active/Traversal
  Cost: focus
  Effect: Blink a short distance to a visible shadow.
  Limits: Cannot cross sealed doors or warded barriers.

Hunter's Mark
  Type: Active
  Effect: Mark an enemy, revealing tracks and increasing damage against them.
  Scaling: perception + tracking.

Field Repair
  Type: Utility
  Effect: Repair damaged gear outside town using scrap.
  Requirements: crafting kit.
```

Generic status effects:

```text
burning
frozen
shocked
poisoned
bleeding
stunned
silenced
feared
charmed
invisible
revealed
slowed
hasted
weakened
shielded
regenerating
exhausted
cursed
blessed
marked
```

Generic progression concepts:

```text
level
XP
skill rank
perk point
ability tier
class/archetype
mastery
reputation rank
companion approval
faction standing
world state tier
unlock condition
```

System pages should support both rough design notes and structured tables.

Example:

```text
world/systems/stats.md
world/systems/skills.md
world/systems/classes.md
world/systems/abilities.md
world/systems/items.md
world/systems/economy.md
world/systems/crafting.md
world/systems/reputation.md
world/systems/difficulty.md
world/systems/encounter-tuning.md
world/systems/world-flags.md
```

System pages should answer:

> What does this system do, why does it exist, what numbers does it use, what content depends on it, and what still needs balancing?

For a stat, NW should track:

- display name
- internal ID
- description
- min/max/default values
- derived formulas
- affected mechanics
- dialogue/quest checks that use it
- items/abilities/enemies that modify it
- balance notes

For an ability/perk, NW should track:

- requirements
- cost
- cooldown
- effects
- scaling
- animation/VFX/audio needs
- unlock source
- affected stats/flags
- related classes/archetypes

For a loot table, NW should track:

- source encounter/vendor/container
- possible drops
- rarity/weight
- level range
- quest/state conditions
- economy value
- balance notes

Recommended system workflow:

```text
Rough system idea
  -> scratch note
  -> system page
  -> table/schema
  -> linked content
  -> balance pass
  -> export-ready data
```

Stats and systems should connect into the graph:

- dialogue choices can require stats
- quests can set flags
- factions can change reputation
- enemies can use stat blocks
- items can modify stats
- perks can unlock dialogue/options
- encounters can use difficulty tiers
- exports can validate missing IDs or broken references

This is where Brain can help a lot:

- propose stat systems
- check balance consistency
- find impossible requirements
- summarize dependencies
- generate test encounters
- flag unused stats or orphan abilities
- explain how a choice affects downstream systems

Important UI rule:

> Systems should be editable like notes first and tables second.

For a two-person team, it should be easy to write "maybe charisma should affect shop prices" before anyone has to define the exact formula.

### 4. Asset Workspace On Every Page

Every NW page is both a lore document and an asset workspace.

Assets should be viewable, editable, replaceable, regenerated, compared, promoted, and deprecated from the page they belong to.

Examples:

- character page shows portraits, concept art, outfit variants, voice references, animation clips, LoRA datasets, models, textures, and approved exports
- weapon page shows concepts, orthographics, material references, model previews, texture maps, and engine export status
- location page shows maps, mood boards, environment concepts, blockouts, ambience, set dressing references, and lighting notes
- map/biome/terrain page shows regional maps, heightmaps, splat maps, biome rules, traversal notes, climate/ecology notes, blockouts, terrain materials, ambience, VFX, and engine export status
- scene page shows storyboard, animatic, cutscene shots, dialogue audio, camera blocking, VFX notes, and export tasks

NW should have one universal interface, not a patchwork of iframed module screens.

The original module UIs should be copied/adapted into NW.

This means the existing Nymphs Image, Pixal3D, TRELLIS, LoRA, and future module UIs are not merely inspiration. They are source material for NW surfaces. The work is to lift the proven UI flows into NW, then adapt them to NW's shell, state model, and directory contract.

The backend paths should also be copied and trusted:

- Nymphs Image already proves how to list outputs, preview them, group them, move them, delete them, and keep metadata nearby.
- Pixal3D already proves how to warm the backend, prepare source images, generate models, poll progress, export GLB, open outputs, and clean up runtime state.
- TRELLIS already proves the alternate image-to-3D/retexture path.
- LoRA already proves the dataset/job/output path.

NW should use those proven routes, endpoints, output folders, and lifecycle behaviors, but present them through a single NW design language.

This means:

- copy/adapt original module UI code where it saves time and avoids breaking known workflows
- keep one NW shell, navigation model, job strip, context panel, and asset inspector
- avoid embedding whole mismatched apps as the normal experience
- preserve existing backend routes so the Blender addon and module workflows keep working
- add NW metadata beside outputs instead of replacing the module output format

The preview/review experience should become native to NW:

- one NW image viewer
- one NW model viewer
- one NW asset strip
- one NW approval/review pattern
- one NW job strip
- one NW action drawer

The original module UIs can still be opened for advanced work, debugging, or comparison, but the day-to-day NW experience should feel like one coherent app.

In other words:

```text
Copied/adapted module UI pieces = proven workflow controls inside NW
Module backend path = proven generation and output machinery
NW universal shell = one worldbuilding workspace
```

### 5. Geography And World Map

Geography should be a major NW pillar.

NW should model the world spatially, not only narratively. The user should be able to describe, organize, and produce assets for the physical world at several scales:

- world
- continent
- country/kingdom
- province/region
- biome
- terrain zone
- city
- town
- village
- district
- landmark
- road/route
- dungeon/interior
- building
- room

Geography pages should support:

- parent/child nesting, such as continent -> kingdom -> province -> city -> district -> building
- adjacency, such as roads, borders, connected regions, gates, caves, paths, rivers, passes
- containment, such as a village inside a region or a shop inside a city district
- traversal notes, such as blocked routes, unlock conditions, fast travel, danger level, terrain difficulty
- climate, weather, seasons, light, ecology, resources, population, culture, economy, factions, conflicts
- gameplay notes, such as encounter tables, quest hubs, NPC density, resource nodes, secrets, and points of interest

Geography should connect directly to production:

- world maps
- regional maps
- city maps
- dungeon maps
- terrain heightmaps
- splat maps and terrain masks
- biome color palettes
- landmark concepts
- building/interior blockouts
- set dressing references
- ambience and soundscapes
- weather/VFX references
- engine export packages

Example geography hierarchy:

```text
World
  Continent
    Kingdom
      Province / Region
        Biome
        City
          District
            Building
              Room
        Village
        Landmark
        Dungeon
        Road / Route
```

### 6. Contextual Module Surfaces

Existing NymphsCore modules already have frontends. NW should reuse them.

NW should make module UIs available in context:

- from a character page, open image generation with character context
- from a weapon page, open image/mesh workflows with weapon context
- from a location page, open map/concept generation tools
- from a scene page, open Brain with scene/lore context
- from a character/style page, open LoRA dataset/training flow
- from an approved concept image, open Pixal3D or TRELLIS for image-to-3D

Principle:

> Original module UIs should be copied/adapted into NW where they are part of the single workflow. NW provides context, routing, storage, output capture, and the canonical project directory contract.

### 7. Production Pipeline Views

NW should support more than page browsing.

Useful views:

- world explorer
- entity page
- relationship graph
- asset library
- production board
- module job history
- timeline
- map view
- scene/cutscene board
- export center
- diagnostics

The production board can borrow the idea from 3DGenStudio:

```text
Idea -> Brief -> Reference -> Concept -> Model -> Texture -> Animation -> Review -> Approved -> Exported
```

Not every entity uses every stage. NW should let stages be flexible by entity type.

## Proposed Project Folder Structure

Top-level shape:

```text
NymphsWorlds/
  ProjectName/
    world/
    assets/
    production/
    exports/
    .nymphs-world/
```

### World Folder

```text
world/
  characters/
  locations/
  factions/
  quests/
  story-arcs/
  quest-arcs/
  scenes/
  dialogue/
  timelines/
  maps/
  regions/
  biomes/
  terrain/
  continents/
  countries/
  provinces/
  settlements/
    cities/
    towns/
    villages/
  landmarks/
  routes/
  dungeons/
  interiors/
  lore/
  items/
  weapons/
  creatures/
  vehicles/
  abilities/
  cultures/
  events/
```

World pages should probably be Markdown or Markdown-like documents with structured metadata.

Example:

```text
world/characters/nyra.md
world/weapons/dusk-blade.md
world/story-arcs/ashen-crown.md
world/quest-arcs/village-defense.md
world/locations/ashfall-market.md
world/settlements/cities/ashfall.md
world/settlements/villages/emberwell.md
world/routes/old-cinder-road.md
world/landmarks/black-spire.md
world/dungeons/hollow-mine.md
world/biomes/glasswood-marsh.md
world/terrain/northern-ridge.md
world/scenes/first-meeting.md
world/dialogue/first-meeting-conversation.md
```

### Asset Folder

The asset structure should support both:

1. human-friendly entity folders
2. type-based library browsing

Recommended layout:

```text
assets/
  by-entity/
  _library/
  incoming/
  generated/
  approved/
  deprecated/
```

#### Entity Asset Folders

```text
assets/by-entity/
  characters/
  locations/
  factions/
  quests/
  scenes/
  items/
  weapons/
  creatures/
  maps/
  regions/
  biomes/
  terrain/
  settlements/
    cities/
    towns/
    villages/
  landmarks/
  routes/
  dungeons/
  interiors/
```

Example character:

```text
assets/by-entity/characters/nyra/
  README.md
  manifest.json
  references/
  concepts/
  portraits/
  turnarounds/
  lora-dataset/
  models/
  textures/
  materials/
  animation/
  audio/
  cutscenes/
  exports/
```

Example weapon:

```text
assets/by-entity/weapons/dusk-blade/
  README.md
  manifest.json
  references/
  concepts/
  orthographics/
  scale-reference/
  material-reference/
  models/
  textures/
  materials/
  animation/
  audio/
  vfx/
  exports/
```

Example location:

```text
assets/by-entity/locations/ashfall-market/
  README.md
  manifest.json
  references/
  concepts/
  maps/
  heightmaps/
  layout/
  blockouts/
  environment-concepts/
  set-dressing/
  models/
  textures/
  materials/
  ambience/
  music/
  vfx/
  lighting/
  exports/
```

Example city:

```text
assets/by-entity/settlements/cities/ashfall/
  README.md
  manifest.json
  references/
  concepts/
  maps/
  districts/
  buildings/
  street-level/
  landmarks/
  signage/
  population/
  faction-presence/
  ambience/
  music/
  lighting/
  weather/
  set-dressing/
  blockouts/
  exports/
```

Example village:

```text
assets/by-entity/settlements/villages/emberwell/
  README.md
  manifest.json
  references/
  concepts/
  maps/
  buildings/
  residents/
  farms/
  resources/
  ambience/
  set-dressing/
  blockouts/
  exports/
```

Example route:

```text
assets/by-entity/routes/old-cinder-road/
  README.md
  manifest.json
  references/
  route-maps/
  concepts/
  terrain-notes/
  landmarks/
  encounters/
  travel-stages/
  ambience/
  weather/
  exports/
```

Example dungeon/interior:

```text
assets/by-entity/dungeons/hollow-mine/
  README.md
  manifest.json
  references/
  floorplans/
  room-layouts/
  encounter-areas/
  props/
  lighting/
  ambience/
  vfx/
  blockouts/
  exports/
```

Example biome:

```text
assets/by-entity/biomes/glasswood-marsh/
  README.md
  manifest.json
  references/
  concepts/
  maps/
  ecology/
  flora/
  fauna/
  terrain-materials/
  textures/
  ambience/
  weather/
  vfx/
  exports/
```

Example terrain zone:

```text
assets/by-entity/terrain/northern-ridge/
  README.md
  manifest.json
  references/
  concepts/
  heightmaps/
  splatmaps/
  masks/
  blockouts/
  terrain-materials/
  rocks-cliffs/
  foliage/
  lighting/
  exports/
```

Example scene/cutscene:

```text
assets/by-entity/scenes/first-meeting/
  README.md
  manifest.json
  storyboard/
  animatic/
  shots/
  camera-blocking/
  dialogue-audio/
  music-cues/
  sfx/
  vfx/
  sequence/
  exports/
```

#### Global Asset Library

```text
assets/_library/
  images/
  models/
  textures/
  materials/
  animation/
  audio/
  cutscenes/
  ui/
  vfx/
  terrain/
  biomes/
  geography/
  settlements/
  documents/
  workflows/
  brushes/
  references/
```

This is for reusable/shared assets not owned by one entity.

#### Full Asset Type Coverage

```text
assets/_library/images/
  concepts/
  portraits/
  maps/
  references/
  mood/
  storyboards/

assets/_library/models/
  source/
  generated/
  edited/
  retopo/
  optimized/
  approved/

assets/_library/textures/
  albedo/
  normal/
  roughness/
  metallic/
  masks/
  height/

assets/_library/materials/
  shader-presets/
  pbr/
  stylized/

assets/_library/animation/
  characters/
  creatures/
  weapons/
  props/
  cameras/
  mocap/
  retargeted/
  approved/

assets/_library/cutscenes/
  storyboards/
  animatics/
  shots/
  cameras/
  sequences/
  renders/
  exports/

assets/_library/audio/
  music/
  ambience/
  sfx/
  dialogue/
  voice/
  temp/
  approved/

assets/_library/ui/
  icons/
  hud/
  menus/
  fonts/
  cursors/

assets/_library/vfx/
  particles/
  shaders/
  decals/
  materials/

assets/_library/terrain/
  heightmaps/
  splatmaps/
  masks/
  terrain-materials/
  erosion/
  blockouts/

assets/_library/biomes/
  ecology-references/
  flora/
  fauna/
  weather/
  ambience/
  color-palettes/

assets/_library/geography/
  world-maps/
  regional-maps/
  city-maps/
  dungeon-maps/
  roads-routes/
  borders/
  landmarks/
  points-of-interest/

assets/_library/settlements/
  architecture/
  street-references/
  building-types/
  interiors/
  signage/
  crowd-population/
```

### Production Folder

```text
production/
  briefs/
  prompts/
  jobs/
  module-runs/
  reviews/
  tasks/
  notes/
```

Examples:

```text
production/briefs/characters/nyra_visual_brief.md
production/prompts/characters/nyra_portrait_prompt.json
production/jobs/zimage/job_2026-05-27_001.json
production/module-runs/pixal3d/nyra_mesh_attempt_001.json
production/module-runs/trellis/nyra_mesh_attempt_001.json
```

### Export Folder

```text
exports/
  blender/
  godot/
  unity/
  unreal/
  web/
  packages/
```

NW should eventually export both:

- content/data exports
- approved asset exports

Examples:

```text
exports/godot/world_data/
exports/godot/assets/
exports/blender/references/
exports/blender/models/
```

### NW Metadata Folder

```text
.nymphs-world/
  project.json
  index.json
  graph.json
  assets.json
  jobs.json
  modules.json
  diagnostics.json
  templates.json
  schema/
```

This folder is NW-managed. It can cache indexes and state, but the source of truth should still be readable project files whenever practical.

## Entity Manifest Concept

Each entity asset folder can have a small manifest:

```json
{
  "entity_id": "character.nyra",
  "entity_type": "character",
  "display_name": "Nyra",
  "linked_world_page": "world/characters/nyra.md",
  "approved": {
    "portrait": "portraits/approved/nyra_portrait_v003.png",
    "model": "models/approved/nyra.glb",
    "voice": "audio/voice/approved/nyra_voice_ref.wav"
  }
}
```

NW should generate and maintain this.

## Asset Metadata Model

Every asset should have metadata. This can live in the central asset index and/or sidecar files.

Useful fields:

```json
{
  "asset_id": "asset.20260527.0001",
  "entity_id": "character.nyra",
  "type": "image",
  "role": "portrait",
  "status": "approved",
  "path": "assets/by-entity/characters/nyra/portraits/approved/nyra_portrait_v003.png",
  "created_by": "zimage",
  "source_job": "job.zimage.20260527.0001",
  "source_prompt": "production/prompts/characters/nyra_portrait_prompt.json",
  "parent_asset_id": "asset.20260527.0000",
  "tags": ["portrait", "main-character", "approved"],
  "notes": "Current approved portrait for Nyra."
}
```

Important concepts:

- type: image, model, texture, audio, animation, cutscene, document, workflow
- role: reference, concept, portrait, map, mesh, voice, storyboard, approved export
- world role: biome, terrain zone, region, traversal area, encounter area, quest area
- geography role: continent, country, province, city, town, village, district, landmark, route, dungeon, interior
- status: incoming, draft, generated, review, approved, deprecated, exported
- entity: the thing this asset belongs to
- source job: what produced it
- parent asset: lineage tracking

## Module Integration Model

NW should integrate NymphsCore modules through a formal module/tool contract.

### Brain

Possible uses:

- worldbuilding assistant
- consistency checks
- character expansion
- lore summarization
- quest/scene drafting
- prompt generation
- relationship reasoning
- dialogue style drafting
- export/package explanation

### Nymphs Image

Possible uses:

- character portraits
- location concepts
- item/weapon concepts
- map sketches
- settlement maps
- city/village concepts
- dungeon/floorplan concepts
- biome concepts
- terrain concepts
- terrain texture/reference generation
- style frames
- image edits
- part extraction
- reference cleanup

### TRELLIS.2

Possible uses:

- image-to-3D from approved concept/reference
- weapon/object mesh generation
- creature/prop/location pieces
- generated mesh preview and export

### Pixal3D

Possible uses:

- primary high-fidelity image-to-3D generation path
- textured 3D assets from approved NW concept/reference images
- character, creature, prop, weapon, landmark, and set-dressing meshes
- PBR-textured model outputs for downstream Blender/engine review
- generation profiles for local GPU/VRAM constraints
- compare Pixal3D and TRELLIS outputs when both are useful

NW should treat Pixal3D as a first-class 3D module, not an optional footnote. If Pixal3D is the best available mesh generator in the local stack, entity pages should prefer it for "Make 3D Asset" while keeping TRELLIS available as an alternate backend.

### LoRA

Possible uses:

- character LoRA datasets
- style LoRAs
- object/weapon consistency
- outfit consistency
- curated captions from entity context

### Brain-Train

Possible uses:

- dataset building from NW docs
- adapters for project-specific coding/world rules
- game/project knowledge adapters

### Future Blender/Engine Helpers

Possible uses:

- open approved assets in Blender
- generate blockout tasks
- send models/textures to engine folders
- create placeholder scenes
- validate export packages

## Contextual Module UI Pattern

NW should not replace all module UIs.

Instead:

- NW opens a module UI from an entity page.
- NW passes context when possible.
- The module performs specialized work.
- Outputs are saved back into the NW project.
- NW attaches the result to the right entity.

Example flow:

```text
Character page -> Generate Portrait -> Nymphs Image
Nymphs Image output -> assets/by-entity/characters/nyra/portraits/generated/
NW prompts user to approve, compare, retry, or send to LoRA/Pixal3D/TRELLIS
```

Example flow:

```text
Weapon page -> Generate Concept -> Nymphs Image
Approved concept -> Generate Mesh -> Pixal3D or TRELLIS.2
3D module output -> models/generated/
NW previews model and tracks export state
```

## Generative Backend And Module UI Integration

The generative modules should be treated as specialist studios that NW can call into, not as UI work that NW needs to rebuild from scratch.

This is important because the existing module UIs already solve hard, practical problems:

- model selection
- model fetching
- warmup
- CUDA/VRAM pressure
- source image preparation
- generation profiles
- progress polling
- output viewing
- restart/stop/kill behavior
- logs and troubleshooting

NW should keep those UIs alive and respect them.

### Hard Rule: Copy/Adapt Module UIs Into NW

NW should not become a visual collage of Pixal3D, TRELLIS, Nymphs Image, Easy LoRA, Brain/Open WebUI, or future audio/video/animation UIs.

NW should have its own universal interface.

The original module UIs should be copied/adapted into that universal interface when they represent proven workflow paths.

Copy/adapt:

- Nymphs Image generation controls where NW needs image generation
- Nymphs Image preview/strip/output organizer where NW needs image review
- Pixal3D source prep, warmup, progress, generation, and model viewer flows
- TRELLIS alternate image-to-3D/retexture controls where useful
- Easy LoRA dataset/job flow where NW needs training
- future audio/video/animation UIs when those modules arrive

Also copy and trust the backend path:

- proven endpoints
- startup scripts
- health checks
- warmup flows
- progress polling
- output folders
- metadata sidecars
- runtime stop/kill behavior
- model asset checks
- logs and troubleshooting routes

The adaptation target is important:

```text
Original module UI flow
  -> copied into NW source
  -> restyled into NW shell
  -> rewired to NW project/entity/asset state
  -> still calls proven backend endpoints
  -> still preserves raw module output paths
```

The original standalone module UIs can remain available for debugging and comparison, but the main workflow should live inside NW.

NW owns:

- world context
- entity identity
- source assets
- prompts and briefs
- job records
- output import
- asset approval state
- provenance
- links back to world pages
- the universal viewer/review interface

Each copied/adapted module surface keeps:

- specialist controls
- backend startup details
- model-specific settings
- generation parameters
- warmup and cleanup
- local progress display
- module-specific output browsing
- module-specific failure handling

This keeps NW coherent while preserving the hard-earned module machinery underneath it.

### On-Demand Backend Loading

NW should not start every backend at launch.

The first integration layer should be lazy:

```text
User chooses an action
  -> NW checks the module manifest
  -> NW probes health_url
  -> if stopped, NW runs the module start action
  -> NW polls health_url/server_info_url
  -> NW opens the module frontend_url
  -> module does the specialist work
  -> NW imports selected outputs
  -> NW attaches outputs to the correct entity
  -> NW optionally stops the backend when safe
```

This keeps the machine calmer, reduces VRAM conflicts, and avoids turning NW into a giant always-running module launcher.

Recommended visible states:

```text
Not installed
Needs model assets
Stopped
Starting
Warming
Ready
Running
Output ready
Importing
Imported
Failed
```

The right panel can show these as small status chips. A bottom job strip can show active work across modules.

### Manifest-Driven Module Contract

Most current modules already expose enough metadata for NW to discover and use them.

NW should read each `nymph.json` and rely on:

- module id
- name
- category/kind
- installed markers
- runtime `health_url`
- runtime `server_info_url`
- runtime `frontend_url`
- manager UI URL or local HTML
- start/open/stop actions
- outputs root
- logs root
- config root
- notes/capabilities

This means NW can start with a generic module wrapper instead of hardcoding every backend.

Minimum module card:

```text
Module name
Status
Open UI
Start
Stop/Kill, if supported
Open Outputs
Open Logs
Last imported assets
```

Minimum entity action:

```text
Action label
Target module id
Required source asset type
Suggested output role
Destination folder
Context payload
```

### Current Backend Map

**Brain**

Use Brain for page-aware thinking and writing work:

- expand a character
- summarize lore
- check consistency
- draft dialogue
- write quest beats
- convert notes into structured fields
- generate prompts for image/3D/audio/video modules
- explain what is missing from an entity

NW can use Brain in two modes:

- small contextual actions from the NW page
- full Open WebUI when the user wants the whole chat workspace

**Nymphs Image**

Use Nymphs Image for 2D concept work:

- portraits
- character sheets
- creature concepts
- item and weapon concepts
- settlement views
- biome and terrain references
- map sketches
- dungeon or floorplan concepts
- visual style frames
- image edits
- reference cleanup
- parts planning and extraction
- captions through Brain vision support

Useful existing surfaces:

- `/nymph` for the full image UI
- `/health` for backend readiness
- `/server_info` for model/runtime details
- `/active_task` for progress
- `/generate` for direct image jobs
- `/api/outputs` for recent outputs
- `/api/parts/plan` and `/api/parts/extract` for parts workflows
- `/api/vision/caption` and `/api/vision/parts-plan` for Brain-assisted image understanding

NW should use the existing UI for most work and only use direct API calls for simple "quick generate" actions.

**Pixal3D**

Use Pixal3D as the primary image-to-3D path.

Pixal3D should be the default "Make 3D Asset" backend for approved reference images when it is installed and ready.

Useful existing surfaces:

- `/nymph` for the Pixal3D Nymph UI
- `/health` for readiness
- `/server_info` for runtime and warmup state
- `/app_config` for active profile/config
- `/warmup_status` for warmup
- `/api/warmup` for explicit warmup
- `/api/preprocess` for source image preparation
- `/api/generate_3d` for generation
- `/progress` for session progress
- `/api/extract_glb_api` for GLB export
- `/api/free_pipeline_api` for cleanup
- `/api/stop_runtime` for stop/kill behavior
- `/outputs` for generated files

NW should not duplicate the Pixal3D controls. The Pixal3D UI already handles warmup, source prep, generation profiles, progress, GLB export, cleanup, and kill behavior.

**TRELLIS.2**

Use TRELLIS.2 as an alternate image-to-3D/retexture backend.

It is useful for:

- comparing outputs against Pixal3D
- generating alternate mesh candidates
- retexturing experiments
- cases where TRELLIS produces a better form for a specific asset

Useful existing surfaces:

- `/nymph` for the TRELLIS UI
- `/health` for readiness
- `/server_info` for model readiness and quant info
- `/active_task` for progress
- `/generate` for GLB generation/retexture requests

NW should expose TRELLIS as "Try alternate 3D backend" rather than hiding it behind Pixal3D.

**LoRA**

Use LoRA as the consistency training bridge.

Useful workflows:

- collect approved images from an entity
- prepare a dataset folder
- caption the dataset with Brain
- open Easy LoRA
- create/start/stop training jobs
- attach finished `.safetensors` files back to the entity
- make the LoRA visible in Nymphs Image generation

NW should not rebuild Easy LoRA. It should prepare the dataset, hand off to the LoRA UI, then import the finished adapter as a tracked world asset.

**Future Audio, Video, And Animation**

Future generators should follow the same pattern:

```text
Entity page
  -> choose action
  -> start backend only when needed
  -> open module-owned UI
  -> generate/review inside module UI
  -> import selected outputs into NW
  -> attach outputs to entity/story/system
```

Likely future uses:

- character voice references
- creature sounds
- ambient biome loops
- city ambience
- weapon sounds
- dialogue scratch audio
- cutscene animatics
- motion/animation clips
- spell effects
- location flythroughs

### Entity Page Action Examples

Character page:

```text
Ask Brain
Generate portrait
Generate outfit sheet
Extract reusable parts
Prepare LoRA dataset
Open Easy LoRA
Make 3D from approved image
Attach voice reference, future
Attach animation, future
```

Place page:

```text
Ask Brain
Generate location concept
Generate map sketch
Generate biome reference
Generate terrain reference
Attach ambience, future
Attach flythrough/cutscene, future
```

Item or weapon page:

```text
Generate concept
Generate icon/reference
Make 3D asset with Pixal3D
Try TRELLIS alternate
Attach material references
Attach sound, future
```

Story page:

```text
Ask Brain for beats
Generate storyboard frame
Generate key art
Attach dialogue scratch audio, future
Attach cutscene animatic, future
```

Systems page:

```text
Ask Brain to formalize mechanic
Link mechanic to characters/items/quests
Generate UI reference, if needed
Generate example icons, if needed
Attach prototype notes
```

### Output Import And Provenance

Module output roots should stay intact.

NW should import or reference selected outputs into the world project, then track provenance.

Important correction:

> NW has its own directory contract.

The module output directories are not the final shape of the NW project. They are raw/staging output locations. NW should adapt copied module UI pieces so the user sees and works in the NW project structure.

This is part of what makes NW unique.

NW is not just a launcher for image, model, LoRA, audio, video, and animation tools. It is the world-production system that decides:

- what entity an asset belongs to
- what role the asset plays
- whether it is a draft, review candidate, approved reference, or export
- what story/world/system page it supports
- what generated it
- what it can be sent to next
- what Blender/engine package it should eventually become

Recommended pattern:

```text
NymphsData/outputs/<module>/
  raw module outputs remain here

NymphsWorlds/<project>/assets/by-entity/<entity-type>/<slug>/
  selected/imported outputs live here
```

This creates three clear zones:

```text
1. Module raw outputs
   NymphsData/outputs/<module>/
   Used by the backend and original module UI.

2. NW canonical project assets
   NymphsWorlds/<project>/assets/...
   Used by NW pages, review, approval, lineage, and team organization.

3. Engine/Blender export staging
   NymphsWorlds/<project>/exports/...
   Used for Blender addon, engine import, packaged references, and final handoff.
```

Copied/adapted UI code should point at the NW contract where possible:

- image strips browse NW entity assets, not only raw image outputs
- model viewers open NW imported models, not only raw Pixal3D/TRELLIS outputs
- approval controls write NW metadata
- send-to-module actions create NW job records
- export buttons write to NW export folders
- raw module paths remain stored as provenance

The backend can still generate into its expected module output folder. NW then imports/promotes the selected result into the NW asset contract.

This avoids breaking backend assumptions while keeping the single UI workflow clean.

Each imported asset should have metadata:

```text
asset_id
entity_id
entity_type
asset_type
asset_role
status
source_module
source_module_version
source_output_path
source_job_id
source_prompt
source_settings
source_assets
created_at
imported_at
approved_by
parent_asset_id
notes
```

This lets the team answer:

- where did this come from?
- what prompt/settings made it?
- what entity does it belong to?
- is it approved or just a candidate?
- what asset did it derive from?
- can it be regenerated?

### Context Handoff Payload

Even if module UIs do not consume the payload on day one, NW should create a consistent handoff record for every module action.

Example:

```json
{
  "handoff_id": "nw-job-2026-05-27-001",
  "project_id": "project-name",
  "entity_id": "character-nyra",
  "entity_type": "character",
  "action": "make_3d_asset",
  "target_module": "pixal3d",
  "source_assets": [
    "assets/by-entity/characters/nyra/portraits/approved/nyra-front.png"
  ],
  "brief": "Create a clean game-ready 3D character reference mesh from the approved portrait.",
  "suggested_prompt": "Nyra, young forest scout, practical leather gear, readable silhouette, game asset reference",
  "output_role": "mesh",
  "destination": "assets/by-entity/characters/nyra/models/generated/",
  "return_to": "world/characters/nyra.md"
}
```

Later, module UIs can accept a handoff path or query parameter. For draft one, NW can still use this record to track the job and import results.

### Backend Integration Modes

NW can support three levels of backend integration.

The guiding idea:

```text
Copy proven backend paths.
Copy/adapt proven viewer components where useful.
Open the correct full module UI when the specialist workspace is needed.
Do not make NW feel like pasted-together module screens.
```

**Level 1: NW-Native Quick Action**

NW starts the backend on demand and exposes a small, consistent action drawer.

Use this for common cases:

```text
Generate one portrait
Caption this image
Make one GLB from this image
Prepare this LoRA dataset
```

This should be the normal NW experience.

**Level 2: NW-Native Advanced Panel**

NW still draws the UI, but exposes more backend-specific options.

Use this when a workflow needs a few extra controls but should still feel like NW:

```text
Choose image model/profile
Choose Pixal3D profile
Choose source preprocessing mode
Choose TRELLIS alternate
Choose LoRA preset/job settings
```

**Level 3: Open The Correct Full Module UI**

NW starts the backend and opens the correct full module UI as an advanced escape hatch.

Use this for troubleshooting, deep settings, or workflows NW has not wrapped yet.

The full module UI should be available from the related NW action, entity, asset, or job.

Examples:

```text
Image asset -> Open in Nymphs Image
Approved image -> Open in Pixal3D
Mesh candidate -> Open in Pixal3D or TRELLIS
Character image set -> Open in Easy LoRA
Lore page -> Open in Brain/Open WebUI
```

This makes the specialist workspace reachable without forcing it to be the default NW surface.

Recommended integration priority:

```text
NW-native quick action
  -> NW-native advanced panel
    -> correct full module UI when needed
```

### First Integration Milestones

The smallest useful sequence:

```text
1. Read installed module manifests.
2. Show module status from health/server_info.
3. Add NW-native quick actions on entity pages.
4. Start backends on demand.
5. Generate/import one image through Nymphs Image backend paths.
6. Attach imported image to an entity.
7. Send approved image to Pixal3D through NW controls.
8. Import and preview the generated GLB in NW.
9. Prepare one LoRA dataset from approved entity images.
10. Add TRELLIS as alternate 3D generation.
11. Keep full module UIs available as advanced escape hatches.
```

This sequence proves the whole NW production loop without replacing any existing module UI.

## Simplest First UI Draft

The first usable NW UI should be much simpler than the full vision.

The baseline should feel closer to Chronicler than to a production dashboard:

```text
Left Rail      Left Panel          Main Page                     Right Panel
---------      ----------          ---------                     -----------
World          World tree          Lore/editor/view              Entity info
Search         Search results      Simple page tabs              Assets
Map            Filters             Markdown/rich text            Links
Story          Templates           Optional preview              Actions
Assets
```

The mental model:

> Pick a thing on the left. Work on its page in the middle. See its context and next actions on the right.

Everything else should be progressive disclosure.

### First Screen

When NW opens a project, show the world itself, not a marketing-style dashboard.

Recommended first screen:

- left rail with 5 main icons: World, Search, Map, Story, Assets
- left panel with the world tree
- center page showing the selected entity or a simple project home page
- right panel showing selected entity metadata, assets, relationships, and actions
- small bottom job strip only when module jobs are running

No huge dashboard is needed for the first draft. A small project home page can show recent pages, missing links, assets needing review, and active jobs.

### Main Views For Draft One

Keep the first UI to five views:

- **World:** entity tree and pages
- **Search:** full project search
- **Map:** geography tree first, visual map later
- **Story:** arcs, quests, scenes, and dialogue
- **Assets:** attached/generated/approved assets

Production board, graph view, exports, diagnostics, and module management can exist as secondary panels or later views.

### Entity Page Pattern

Every entity page should use the same simple layout:

```text
Header: Name, type, status, quick actions
Tabs: Overview | Details | Assets | Story | Production
Body: editor or structured view
Right: infobox, links, assets, actions
```

For draft one, the tabs can stay minimal:

- **Overview:** readable page/editor
- **Assets:** attached images/models/audio/etc.
- **Links:** relationships, backlinks, scenes, geography, tags
- **Actions:** Brain, generate image, make 3D, import asset

The app should not show all possible systems at once. A character page should feel like a character page. A city page should feel like a city page. A story arc page should feel like a story arc page.

### Simple UI Rules

- Chronicler-like calm page browsing first.
- WORBI-like narrative tools only when a story/scene/dialogue page needs them.
- Module backends appear through NW-native controls first.
- Correct full module UIs remain available from contextual actions.
- Proven viewer pieces can be copied/adapted into NW if they fit the universal UI.
- The right panel answers: what is this, what is connected to it, what assets exist, what can I do next?
- The bottom job strip answers: what is running, what finished, what failed?
- Graphs and boards should be optional modes, not the default work surface.

### Universal NW UI Shape

The NW UI should feel like one calm worldbuilding app.

The first draft should have this structure:

```text
App Shell
  Left Rail
  Project/Entity Panel
  Main Work Surface
  Context Panel
  Bottom Job Strip
```

The shell should stay stable. Switching from a character to a city to an asset should not make the app feel like it changed products.

#### Left Rail

Permanent top-level modes:

```text
World
Map
Story
Systems
Assets
Search
```

Each mode changes the left panel, not the whole app personality.

#### Project/Entity Panel

This is the navigation and filtering column.

In World mode it shows:

- world tree
- entity folders
- tags
- status filters
- recently edited pages
- missing/needs-review filters

In Map mode it shows:

- geography tree
- regions
- settlements
- biomes
- terrains
- routes
- dungeons/interiors

In Story mode it shows:

- arcs
- quests
- scenes
- dialogue threads
- branch groups
- unresolved choices

In Assets mode it shows:

- asset folders
- asset types
- review state
- source module
- attached/unattached filters
- approved/exported filters

#### Main Work Surface

The main surface changes by selected object, but should always use familiar NW patterns.

Entity page:

```text
Title/status bar
Page tabs
Editor/viewer
Inline linked assets
Notes and structured fields
```

Asset page:

```text
Preview viewer
Asset metadata
Lineage
Attached entity
Review/approval controls
Send-to-module actions
```

Story page:

```text
Readable outline
Node/branch view when needed
Scene list
Dialogue/choice metadata
Linked characters/places/systems
```

Map/geography page:

```text
Map or placeholder canvas
Place details
Connected routes
Contained settlements/biomes/terrain
Attached map/reference assets
```

#### Context Panel

The right panel is the "what can I do with this?" area.

It should contain:

- infobox metadata
- backlinks/relationships
- attached assets
- missing asset prompts
- quick actions
- module/backend status
- recent jobs for the selected entity
- export readiness

The right panel should be contextual, not noisy. If the user is on a character page, it should not show terrain tools unless a linked action needs them.

#### Universal Asset Viewer

NW should have one asset viewer pattern for all media.

Recommended shape:

```text
Main preview
  Image / model / audio / animation / storyboard

Asset strip
  thumbnails or file cards
  grouped by role/status/folder/date

Inspector
  metadata
  source module
  lineage
  notes
  approval state

Actions
  approve
  compare
  replace
  deprecate
  send to image
  send to Pixal3D
  send to TRELLIS
  prepare LoRA dataset
  export
```

For images, use the proven Nymphs Image backend/output concepts, but draw them as NW:

- big preview
- strip organizer
- recent/date/folder grouping
- select/move/import/delete/approve
- send selected image onward

For models, use the proven Pixal3D viewer/backend concepts, but draw them as NW:

- interactive GLB/model viewer
- camera controls
- auto-rotate toggle
- lighting/exposure controls
- open original
- inspect source image lineage
- compare mesh candidates

#### Universal Action Drawer

Generation should start from a consistent NW drawer.

Example:

```text
Action: Generate Character Portrait
Entity: Nyra
Backend: Nymphs Image
Input: page context + style notes
Output role: portrait/concept
Destination: character asset folder
Controls: simple NW prompt/seed/profile fields
Advanced: open full module UI
```

The drawer should hide backend complexity until needed.

Default controls:

- short brief/prompt
- output role
- source asset, if needed
- backend choice
- profile/preset
- generate button
- advanced/open module UI link

After generation:

- show output in the universal viewer
- keep source output path
- import selected result
- attach to entity
- mark as draft/review/approved

#### Bottom Job Strip

The bottom strip only appears when useful.

It should show:

- backend starting
- model warming
- generation running
- LoRA training status
- import complete
- failed jobs
- quick open logs/action buttons

It should be small and practical, not a dashboard.

#### Visual Tone

NW should look like a quiet creative production notebook:

- readable
- compact
- page-first
- asset-aware
- calm colors
- clear status chips
- restrained panels
- no decorative dashboard clutter
- no giant landing page

The UI should make a two-person team feel like they are steadily building a world, not operating a server farm.

### Draft One Feature Boundary

The first UI draft only needs to prove:

```text
Create entity
  -> edit page
  -> link to other entities
  -> attach asset
  -> start the image backend on demand
  -> generate/import one image through NW-native controls
  -> mark asset approved
  -> see it from the entity page
```

If that loop feels clean, the larger NW system has a good foundation.

## Chronicler UI Vs WORBI UI

This comparison matters because NW should not blindly copy either app.

The best short version:

```text
Chronicler = better base feeling
WORBI      = better feature mass
NW         = Chronicler calm shell + WORBI systems + NymphsCore module actions
```

### Chronicler UI Character

Chronicler feels like a dedicated world vault.

Its strongest design qualities:

- page-first
- calm
- readable
- local-first
- low visual noise
- world wiki mental model
- Markdown/frontmatter as the center
- folders, tags, wikilinks, backlinks, and media all support the page
- editor/preview/split modes are understandable
- metadata and infoboxes feel like part of the page, not a separate app
- maps and galleries exist as worldbuilding aids rather than dominant surfaces
- diagnostics are practical: broken links, broken images, YAML errors

Chronicler's main emotional effect:

> "I am safely browsing and editing my world."

That is valuable. NW should keep this feeling as the default mode.

Chronicler is also strong because it respects the user's files. The UI feels like a window into a vault, not like the vault only exists inside the app.

For NW, this suggests:

- open directly into the world tree and selected page
- keep writing/browsing visually quiet
- make Markdown/frontmatter/project files feel trustworthy
- avoid showing every production system all the time
- make backlinks, tags, assets, maps, and diagnostics feel like natural page supports

### WORBI UI Character

WORBI feels more like a creative IDE.

Its strongest design qualities:

- VS Code-like activity bar
- tabbed rich editor
- file explorer with favorites/recent files
- AI sidebar
- tag/location/timeline/dialogue/image/graph/reminder panels
- right-side information panel
- rich document editing
- graph modal
- story bible and game export flows
- image generation inside the worldbuilding tool
- scene conversation designer for branching dialogue

WORBI's main emotional effect:

> "I am actively building, generating, exporting, and wiring game content."

That is also valuable. NW needs this power, especially for a two-person game pipeline.

But WORBI's risk is that many tools compete for attention. It can feel like the user is inside an editor, an AI chat, an image generator, a timeline manager, a graph tool, and a game exporter all at once.

For NW, this suggests:

- keep WORBI's feature ideas
- avoid making every feature a permanent primary surface
- make tools contextual to the current entity/page
- reduce the number of always-visible side buttons in the first draft
- move AI, graph, generation, dialogue, and export into page-relevant actions

### Direct Comparison

| Area | Chronicler | WORBI | NW Direction |
| --- | --- | --- | --- |
| First impression | Calm vault/wiki | Busy creative IDE | Calm vault with hidden production depth |
| Main object | Page | File/document plus tools | Entity page |
| Data feel | User-owned Markdown vault | Workspace files plus side metadata | Project-owned vault with stable IDs and manifests |
| Navigation | Sidebar tree/tags/gallery | Activity bar plus explorer/search/panels | Small rail plus context-aware panel |
| Writing | Markdown editor/preview | Rich editor with tabs | Page editor that can support Markdown/rich editing |
| Metadata | Frontmatter/infobox | Tags/locations/scenes in side systems | Frontmatter + project-local manifests + generated index |
| Links | Wikilinks/backlinks central | Wikilinks plus graph/tag relationships | Unified entity graph from links, metadata, geography, narrative, assets |
| Narrative | General world pages | Scenes, dialogue, conversation graph, story/quest export | WORBI narrative system made first-class and calmer |
| Assets | Images/media/gallery support | Image insertion/generation/info panel | Entity asset panel with generation, approval, lineage |
| AI | Not the main point | Always available assistant/sidebar | Contextual Brain actions and optional chat |
| Production | Light | Stronger export/generation direction | Production actions inside page, boards later |
| Risk | Too note-like for game production | Too many tools visible at once | Progressive disclosure |

### What NW Should Borrow From Chronicler

Borrow the feeling and page discipline:

- readable world tree
- page as the default workspace
- Markdown/frontmatter compatibility
- backlinks and broken-link diagnostics
- media gallery as support, not clutter
- clean editor/preview modes
- infobox-like metadata that belongs to the page
- calm theme and restrained visual density
- file ownership and portability

Chronicler should guide how NW feels during normal browsing and writing.

### What NW Should Borrow From WORBI

Borrow the systems and production intent:

- rich editor and tabbed work surface
- AI assistant/tool permissions
- scene/timeline/dialogue systems
- branching conversation designer
- story/quest/game export templates
- relationship graph thinking
- tags/locations/side panels
- image generation workflow
- right-side information panel
- maintenance diagnostics
- story bible compile/export flow

WORBI should guide what NW can do once the user needs more than page browsing.

### What NW Should Avoid From Chronicler

Avoid becoming only a beautiful note/wiki app.

NW cannot stop at:

- pages
- tags
- backlinks
- image galleries
- maps

It must also know about production assets, module jobs, approved outputs, entity state, export readiness, and game pipeline needs.

### What NW Should Avoid From WORBI

Avoid permanent tool overload.

The first NW UI should not show:

- too many activity icons
- always-open AI chat
- image generator as a global primary panel
- graph, timeline, dialogue, reminders, tags, locations, assets, and export all fighting for the same left panel
- hidden metadata files as the user's only source of truth
- localStorage as canonical page/asset state

WORBI's power is useful, but NW needs more hierarchy.

### Recommended NW UI Philosophy

NW should have two modes of feeling:

```text
Default state:
  Quiet world vault.

When the user asks for power:
  Focused production tool.
```

In normal use, NW should feel like:

```text
Chronicler:
  tree -> page -> backlinks/assets/context
```

When working on a specific task, NW can temporarily feel like:

```text
WORBI:
  scene graph, module UI, AI assistant, export wizard, production board
```

The transition should be contextual. The user should not have to think, "Which app mode am I in?" They should think, "I am on this character page, and I can generate a portrait," or "I am on this scene page, and I can open the dialogue graph."

### Concrete First Draft UI Direction

Use this hierarchy:

```text
Primary shell:
  World tree
  Page editor/viewer
  Entity context panel

Secondary tools:
  Search
  Map/geography
  Story/narrative
  Asset library

Task overlays:
  Brain prompt/chat
  Image generation
  Pixal3D/TRELLIS handoff
  Dialogue graph
  Relationship graph
  Export wizard
```

The core screen should stay stable. Heavy tools should open as:

- right-panel action drawers
- focused modals
- full-page task views
- hosted module surfaces

They should close cleanly back to the entity page.

### Entity Page As The Unifying Screen

The entity page is where Chronicler and WORBI meet.

For a character page:

- Chronicler side: biography, tags, wikilinks, backlinks, infobox
- WORBI side: scenes, dialogue, relationships, AI prompts, export status
- NW side: portraits, models, voice refs, animations, LoRA datasets, module jobs

For a city page:

- Chronicler side: lore, districts, landmarks, factions, history
- WORBI side: scene appearances, quest arcs, timeline events
- NW side: maps, concepts, ambience, set dressing, terrain/engine exports

For a story arc page:

- Chronicler side: readable outline and notes
- WORBI side: beats, choices, branches, consequences, game-ready export
- NW side: linked scene assets, cutscene refs, dialogue audio, storyboard tasks

This is the main UI thesis:

> The page stays calm, but the page knows how to produce things.

### Practical Design Rule

If a feature is used constantly, it belongs in the shell.

Examples:

- world tree
- search
- page editor/viewer
- entity context panel
- asset attachments

If a feature is powerful but occasional, it belongs behind a contextual action.

Examples:

- dialogue graph
- relationship graph
- image generation
- mesh generation
- LoRA training
- story export
- diagnostics
- production board

This keeps draft one understandable while still leaving room for the giant system.

## Key UI Areas

### Project Dashboard

Shows:

- current world/project
- recent entities
- active module jobs
- assets needing review
- broken links/assets
- production progress

### World Explorer

Shows:

- entity tree
- folders
- tags
- filters
- templates

### Entity Page

The most important screen.

Suggested layout:

- main lore editor
- infobox/metadata panel
- relationships panel
- assets panel
- production/actions panel
- job history
- approved/current outputs

### Asset Panel

Per page:

- image gallery
- model viewer
- big preview surface for the selected asset
- horizontal strip organizer for related assets
- recent/date/folder filters for generated outputs
- audio player
- animation preview where possible
- cutscene/storyboard viewer
- version comparison
- approve/deprecate controls
- compare/select/promote controls
- send-to-module actions

The image side should be NW-native, but it should use the proven Nymphs Image backend/output pattern: a large readable preview, a strip of thumbnails, folder/recent grouping, selection controls, and actions for moving/importing/approving assets.

The model side should be NW-native, but it should use the proven Pixal3D backend/output pattern: an interactive model viewer with camera controls, auto-rotate, clear output labels, open-file actions, and enough lighting/shadow control to inspect the asset before it is approved.

### Relationship Graph

Should show:

- pages/entities
- wikilinks
- tags
- explicit relationships
- asset dependencies
- quest/scene involvement
- family/faction/location links

### Production Board

Board stages can be configurable.

Possible default stages:

```text
Idea
Brief
Reference
Concept
Asset Generation
Review
Approved
Exported
```

Asset-specific stages:

```text
Image
Image Edit
Mesh
Mesh Edit
Texture
Animation
Review
Export
```

### Asset Library

Global searchable asset view:

- images
- models
- textures
- audio
- animations
- cutscenes
- workflows
- documents

Filters:

- entity
- type
- role
- status
- module/source
- approved/current
- date
- tag

### Diagnostics

Borrowing from Chronicler:

- broken wikilinks
- missing assets
- missing approved outputs
- orphan assets
- duplicate assets
- parse errors
- stale module job records
- export mismatch

## Data Philosophy

NW should prefer:

- readable world pages
- stable entity IDs
- stable folder layout
- small JSON manifests/indexes
- sidecar metadata where useful
- cached indexes for speed
- rebuildable search/graph/cache data
- project-local metadata over global user metadata

Avoid:

- hiding everything in one opaque database
- scattering assets randomly by backend output folder
- making modules guess where files belong
- losing prompt/job history
- storing canonical project data in browser localStorage
- keeping separate feature registries that disagree with each other
- letting module output folders become the only place an asset exists

## Possible Page Format

Markdown with frontmatter is a strong candidate.

Example:

```markdown
---
id: character.nyra
type: character
title: Nyra
tags: [main-character, fire, exile]
status: active
faction: [[Ashen Circle]]
home: [[Ashfall Market]]
approved_portrait: assets/by-entity/characters/nyra/portraits/approved/nyra_portrait_v003.png
approved_model: assets/by-entity/characters/nyra/models/approved/nyra.glb
---

# Nyra

## Summary

## Backstory

## Family

## Personality

## Relationships

## Timeline

## Visual Direction

## Dialogue Style

## Production Notes
```

## Suggested MVP

The MVP should prove NW as the world database and asset workspace. It does not need every module fully wired on day one.

### MVP 1: Project Vault

- create/open NW project
- create folder structure
- create entity pages from templates
- readable world docs
- metadata/index cache
- vault scanner/indexer
- filesystem watcher
- broken link/orphan asset diagnostics

### MVP 2: Entity Pages

- character/location/item/weapon/scene templates
- story arc/quest arc/dialogue thread templates
- map/region/biome/terrain templates
- city/town/village/landmark/route/dungeon templates
- infobox panel
- tags
- wikilinks
- backlinks
- relationships
- stable IDs
- graph edges for geography, scenes, factions, families, quests, and assets
- WORBI-style narrative branch data for choices, conditions, and outcomes

### MVP 2.5: Narrative Branching

- story arc outline page
- quest arc beat list
- scene participant list
- simple conversation graph per scene
- idle chatter, ambient banter, and interactive dialogue thread types
- choice nodes with target node/thread links
- choice outcomes and conditions stored as project-local metadata

### MVP 3: Asset Attachments

- attach images/models/audio/docs to entity
- entity asset folders
- asset metadata
- gallery/viewer
- approve/deprecate
- orphan asset detection
- raw output import from module artifact folders
- asset lineage and source job tracking

### MVP 4: Nymphs Image Action

- from entity page: generate concept/portrait/reference
- save output into correct entity folder
- store prompt/job metadata
- show result in page asset panel
- keep the original module output path for provenance

### MVP 5: 3D Mesh Action

- from approved image: send to Pixal3D first, with TRELLIS as an alternate backend
- save model output into entity folder
- show model preview if feasible
- track status/export
- record image-to-mesh lineage

### MVP 6: Production Board

- show entity/asset cards by status
- move assets through review/approved/exported
- surface missing work
- filter by entity type, geography area, module, job status, and missing asset type

## Longer-Term Roadmap

### Stage 1: NW Foundation

- choose base: WORBI fork/refactor vs new app
- project/vault structure
- entity schema
- asset schema
- module registry awareness
- unified index/graph/cache layer
- migration path from WORBI-style metadata to NW project metadata

### Stage 2: Worldbuilding Depth

- templates for all entity types
- family/relationship tools
- faction/culture views
- scene/quest builders
- story arc and quest arc builders
- branching dialogue/conversation designer
- timeline and map support
- terrain, region, and biome support
- geography hierarchy and settlement support
- spoilers/secrets

### Stage 3: Asset Production

- image generation integration
- image edit/part extraction
- mesh generation integration
- texture/material tracking
- audio references
- animation references
- cutscene/storyboard support
- future audio generation integration
- future video generation integration
- future animation generation integration

### Stage 4: Pipeline Orchestration

- module job queue
- job history
- backend start-on-demand
- module status cards
- retry/compare/promote flows
- production board
- graph-based pipeline view
- NW-aware module action contract
- raw output import/approval/export loop

### Stage 5: Training/Consistency

- LoRA dataset creation from entity assets
- caption management
- Brain project knowledge packs
- style guides
- consistency checks

### Stage 6: Engine/Blender Export

- approved asset export packages
- Godot export
- Blender reference folders
- Unity/Unreal export layouts
- scene/quest/dialogue structured exports
- validation reports

## Open Questions

### Base App

Should NW:

1. fork/evolve WORBI,
2. start new and import WORBI ideas,
3. use WORBI only as reference?

### File Format

Should world pages be:

1. Markdown with frontmatter,
2. HTML documents like current WORBI,
3. dual format with Markdown source and rich editor rendering?

### Storage

Should metadata be:

1. frontmatter plus `.nymphs-world` indexes,
2. sidecar JSON files,
3. SQLite plus readable exports,
4. hybrid?

Recommended current bias:

> Markdown/frontmatter plus project-local JSON manifests should be canonical. `.nymphs-world` indexes should be rebuildable cache. SQLite can be added later for performance if needed.

### Module Context Passing

What is the cleanest way for NW to pass entity context to module UIs?

Options:

- URL query params
- local JSON handoff files
- WebView/message bridge
- module API endpoint
- shared project/job folder

Recommended current bias:

> Start with shared context/job JSON files and imported outputs, because that matches local module boundaries. Add WebView/API actions where modules expose cleaner hooks.

### Scope Control

Which entity types are MVP?

Likely first:

- character
- location
- region
- biome
- terrain zone
- city/town/village
- route/landmark/dungeon
- item/weapon
- scene
- lore note

Later:

- factions
- creatures
- maps
- terrain and biomes
- cutscenes
- animation
- audio
- full engine export

## Risks

### Scope Explosion

NW can become enormous. It needs an MVP that proves the core loop:

```text
Entity -> Context -> Asset generation -> Attached output -> Review -> Approved asset
```

### Format Lock-In

If the page format is wrong, migration will be painful. Choose a readable format early.

### Module Coupling

NW should not depend on fragile internals of each backend. Prefer declared module contracts and stable output handoff paths.

### Asset Chaos

Without a strong folder and metadata structure, generated outputs will become impossible to manage.

### Index Drift

If pages, sidecars, graph cache, and production jobs all store overlapping truth, NW can drift out of sync. The app needs a clear rule: project files are canonical, indexes are rebuildable, and diagnostics explain conflicts.

### License Boundaries

Chronicler can inspire product behavior but not code reuse.

## Design Principles

- The user owns the world.
- The world is readable on disk.
- Every asset knows what entity it belongs to.
- Every generated output remembers its prompt and source job.
- Original module UIs should be copied/adapted into NW when they are part of the single workflow.
- NW adds context, storage, review, production flow, and the canonical directory contract.
- Lore and production must live together.
- Pages are not just notes; they are workspaces.
- Assets are not just files; they are production objects.
- Approved assets should be obvious.
- Orphaned/broken assets should be discoverable.
- Export should use approved assets and structured world data.

## Best Short Description

> Nymphs World is a local-first worldbuilding and pre-production studio for NymphsCore. It stores a game world as readable project data, attaches images/models/audio/animation/cutscene assets to the pages they belong to, and orchestrates NymphsCore modules to turn lore into production-ready game assets.

## Next Recommended Planning Docs

After this draft is reviewed, split it into:

```text
NYMPHS_WORLD_PRODUCT_SPEC.md
NYMPHS_WORLD_ARCHITECTURE_PLAN.md
NYMPHS_WORLD_VAULT_AND_ASSET_SCHEMA.md
NYMPHS_WORLD_MODULE_INTEGRATION_CONTRACT.md
NYMPHS_WORLD_MVP_ROADMAP.md
```

This file should remain the big-picture map.
