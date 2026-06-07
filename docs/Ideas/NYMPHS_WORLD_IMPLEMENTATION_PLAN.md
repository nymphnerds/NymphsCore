# Nymphs World Implementation Plan

Date: 2026-05-27
Status: Researched implementation draft

Nymphs World is the worldbuilding module for NymphsCore.

It is the place where a game world starts as rough notes, becomes structured
pages, grows relationships, gains references and media, and eventually exports
into the rest of the game pipeline.

This plan is meant to be buildable. It keeps the big vision from the grand plan,
but reduces it into a clearer product shape and implementation path.

## 1. Product Shape

Nymphs World should feel like a local writer's room for a game world.

The core loop:

```text
capture idea
  -> shape it into a page
  -> connect it to the world
  -> decide what is canon
  -> add references or media
  -> prepare it for story, systems, or export
```

The app should support both messy thinking and structured world data. A small
team should be able to throw rough thoughts into a scratch area, then gradually
promote the good ones into characters, places, factions, quests, scenes, items,
systems, and reference boards.

Nymphs World should be useful before any generation or export features are
connected. The first strong version is a calm, capable world vault. The wider
NymphsCore modules then become actions inside that vault.

## 2. Design Anchors

### 2.1 The World Lives In A Project Folder

The user should own the project files.

Use readable Markdown pages for the main world content. Use project-local JSON
sidecars for data that is awkward to keep in prose. Use generated indexes as
caches that can be rebuilt.

### 2.2 The Page Is The Main Object

A page can be a character, place, faction, item, quest, scene, system concept,
scratch note, or reference board.

The user should mostly feel like they are working on pages. More advanced
systems should hang from pages, not replace them.

### 2.3 Scratch Is Part Of The Product

Worldbuilding starts messy.

The scratch area should support:

- inbox notes
- daily notes
- loose references
- questions
- rejected ideas
- maybe-later ideas
- decisions

Promotion from scratch into a page is a first-class workflow.

### 2.4 Context Should Be Visible

When a page is open, the app should show:

- what it is
- what state it is in
- what it links to
- what links back to it
- what tags and relationships it has
- what questions or decisions are attached
- what media or references belong to it
- what actions are available next

### 2.5 Modules Are Page Actions

Nymphs Image, Brain, Pixal3D, TRELLIS, and future modules should be available
from relevant context.

Examples:

```text
Character page -> Generate reference image
Faction page   -> Ask Brain for contradictions
Scene page     -> Open dialogue designer
Approved image -> Make 3D reference
World project  -> Export world bible
```

The user should feel that the result returns to the world, not that they have
left the worldbuilding app.

## 3. Research Summary

### 3.1 Chronicler Lessons

Chronicler is strongest as a local vault and world wiki.

Useful ideas:

- local Markdown vault
- user-owned files
- folder tree
- frontmatter-based infoboxes
- graphical infobox editing
- wikilinks and backlinks
- tag index
- templates
- image embeds, galleries, and carousels
- page inserts/transclusion
- editor/preview/split view
- broken link, broken image, and parse diagnostics
- file watcher and index rebuild
- link updates on rename

Nymphs World should use these ideas as product guidance. Chronicler's license
means the source should be studied, not reused.

### 3.2 WORBI Lessons

WORBI is strongest as a writer's-room workspace for game content.

Useful ideas:

- rich editor with tabs
- two-column writing layout
- file explorer, recents, starred files, and search
- right-side information panel
- hero image and gallery per document
- document notes
- tags and tag manager
- implicit relationships from shared tags
- relationship graph direction
- wikilinks and backlinks direction
- scene assignment and timeline direction
- scene conversation designer
- dialogue data with speakers, choices, conditions, and outcomes
- AI chat with tool permissions
- local image generation bridge
- story bible and game export direction
- Godot integration thinking

Nymphs World should carry these strengths forward with cleaner storage and a
more page-centered structure.

### 3.3 NymphsCore Module Lessons

The surrounding modules are strongest when they do one job well and return an
output.

Nymphs World should provide:

- the page context
- the prompt or input material
- the destination folder
- the attachment to the page
- the metadata/provenance
- the export context

The module handles its own specialist work.

## 4. First Version Experience

### 4.1 Main Layout

Use three stable zones:

```text
Left:   world tree, scratch, tags, search
Middle: page editor and preview
Right:  page context panel
```

The first screen should open into the world. A simple project home page can show
recent pages and reports, but the main product is the workspace.

### 4.2 Left Side: World Explorer

The explorer should include:

- folder tree
- scratch area
- page type icons
- recent pages
- starred pages
- tag filters
- search entry
- reports entry

The folder structure should stay visible and understandable.

### 4.3 Middle: Page Workspace

The page workspace should include:

- title
- status/canon control
- editor mode
- preview mode
- split mode
- autosave
- wikilink autocomplete
- media embed autocomplete
- heading outline
- template insertion

Markdown should be the durable storage format. A rich editor is welcome if it
preserves the source cleanly.

### 4.4 Right Side: Context Panel

Recommended sections:

```text
Infobox
Status
Tags
Links
Backlinks
Relationships
Questions
Decisions
Scenes
Media
Actions
```

This panel is where the app becomes more than a text editor. It gives each page
its world context.

## 5. Core Workflows

### 5.1 Create A World

```text
New project
  -> choose folder/name
  -> create starter structure
  -> open project home
```

Starter folders:

```text
scratch/
world/
story/
systems/
media/
exports/
.nymphs-world/
```

### 5.2 Capture And Promote

```text
write note in scratch
  -> select useful fragment
  -> promote to page
  -> choose page type
  -> keep backlink to original scratch note
```

This should be one of the smoothest flows in the app.

### 5.3 Create And Connect Pages

```text
create character page
create place page
write [[Place Name]] inside character page
see backlink on place page
add tags and relationship
```

This proves the world is becoming connected.

### 5.4 Record A Decision

```text
open question
  -> choose answer
  -> record decision
  -> update page status/canon field
```

Decisions should be searchable and linked back to the pages they affect.

### 5.5 Add Media To A Page

```text
open page
  -> Attach Media
  -> import, paste, browse, or generate
  -> save into project media folder
  -> attach to hero slot or gallery
```

Media should belong to page context. The same flow can later handle images,
audio, models, and other reference material.

### 5.6 Use Brain From Context

```text
open page
  -> Ask Brain
  -> choose action
  -> review result
  -> insert, create page, or discard
```

Useful first actions:

- summarize this page
- suggest links
- find contradictions
- ask worldbuilding questions
- turn scratch into a page draft
- make a reference prompt
- compile a section for export

### 5.7 Work On A Scene

```text
create scene page
  -> add place, era/date, participants
  -> link quest/story arc
  -> open dialogue designer if needed
  -> add idle, ambient, or interactive dialogue
```

Scene tools should be attached to scene pages rather than living as a separate
unrelated editor.

### 5.8 Export

```text
select pages or whole project
  -> choose export target
  -> validate links/media/status
  -> generate world bible or game data
```

The first export target should be a readable world bible. Game-focused exports
can follow once the page and relationship data is solid.

## 6. Project Data

### 6.1 Folder Layout

```text
NymphsWorlds/
  ProjectName/
    world.nymphworld.json
    scratch/
      inbox.md
      questions.md
      decisions.md
      daily/
    world/
      characters/
      places/
      factions/
      items/
      creatures/
      lore/
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
    media/
      library/
      by-page/
      incoming/
      approved/
    exports/
      world-bible/
      godot/
      generic/
    .nymphs-world/
      index/
      sidecars/
      thumbnails/
      diagnostics/
      cache/
```

### 6.2 Project Manifest

`world.nymphworld.json`:

```json
{
  "schema": "nymphworld.project.v1",
  "id": "world.20260527.0001",
  "name": "Untitled World",
  "created_at": "2026-05-27T00:00:00Z",
  "updated_at": "2026-05-27T00:00:00Z",
  "default_page_format": "markdown",
  "default_export_targets": ["world-bible", "godot"]
}
```

Keep the manifest small. It should identify the project and basic settings.

### 6.3 Page Format

Pages are Markdown with YAML frontmatter:

```markdown
---
id: page.character.nyra
type: character
title: Nyra
status: rough
canon: candidate
tags: [main-cast, forest]
hero: media/by-page/page.character.nyra/hero.png
---

# Nyra

Nyra is...
```

Baseline frontmatter:

```text
id
type
title
status
canon
tags
hero
summary
```

Templates can add type-specific fields.

### 6.4 Sidecars

Sidecars store page-linked data that does not fit nicely in Markdown.

Example:

```text
.nymphs-world/sidecars/page.character.nyra.json
```

Use sidecars for:

- right-panel notes
- gallery ordering
- media provenance
- explicit relationships
- open questions
- decisions
- scene assignments
- dialogue graph positions

Sidecars should reference page IDs.

### 6.5 Indexes

Indexes are generated from the project.

Generate:

```text
pages index
tags index
links index
backlinks index
media index
relationships index
diagnostics
```

Diagnostics:

- broken wikilinks
- missing media
- invalid frontmatter
- duplicate page IDs
- orphan sidecars
- orphan media
- unindexed files

## 7. Page Templates

Start with a small template set.

### Character

```text
Summary
Role
Personality
Relationships
Story Use
Visual Notes
Open Questions
```

### Place

```text
Summary
Geography
People/Factions
History
Story Use
Visual Notes
Open Questions
```

### Faction

```text
Summary
Goals
Leadership
Members
Territory
Allies and Enemies
Story Use
Open Questions
```

### Quest

```text
Premise
Hook
Objectives
Scenes
Characters
Choices
Consequences
Rewards
Open Questions
```

### Scene

```text
Summary
Place
Era/Date
Participants
Purpose
Beats
Dialogue Notes
Consequences
Open Questions
```

### System Concept

```text
Raw Idea
Player Experience
Rules Sketch
World Connections
Story Connections
Open Questions
Next Step
```

## 8. Media Model

Media should be stored inside the project and attached to pages.

Example media record:

```json
{
  "media_id": "media.20260527.0001",
  "page_id": "page.character.nyra",
  "kind": "image",
  "role": "reference",
  "path": "media/by-page/page.character.nyra/references/nyra-ref-001.png",
  "source": {
    "type": "generated",
    "module": "nymphs-image",
    "job_id": "job.zimage.20260527.0001",
    "prompt": "Nyra, forest scout...",
    "original_output_path": "/module/output/path.png"
  }
}
```

For imported media, `source.type` can be `imported`, `pasted`, or `external`.

The UI should show the media simply:

- hero
- gallery
- role
- status
- details if opened

## 9. Brain And Module Bridges

### 9.1 Brain Context

When Brain is called from a page, send a context bundle:

```text
current page
frontmatter
summary
outgoing links
backlinks
tags
open questions
related pages
selected text if any
```

This lets Brain act like a world-aware assistant rather than a generic chat box.

### 9.2 Media Module Bridge

For Nymphs Image:

```text
page context
  -> prompt draft
  -> user edits
  -> generate
  -> choose result
  -> import into project
  -> attach to page
```

For future 3D:

```text
selected page media
  -> send to Pixal3D or TRELLIS
  -> import selected model
  -> attach to page
```

The bridge should use module status/contract data rather than hardcoded internal
paths wherever possible.

## 10. Implementation Roadmap

### Phase 1: Project And Index

Build:

- create/open project
- starter folders
- project manifest
- safe file operations
- Markdown page read/write
- frontmatter parser
- page tree
- page index
- tag index
- wikilink parser
- backlink index
- file watcher
- diagnostics

Acceptance:

```text
Create two pages, link them, rename one, and see backlinks/diagnostics update.
```

### Phase 2: Editor And Templates

Build:

- editor/preview/split modes
- autosave
- templates
- wikilink autocomplete
- media embed autocomplete
- infobox display
- graphical infobox editor
- tags editing
- page status/canon editing

Acceptance:

```text
Create character/place pages from templates, fill their infoboxes, tag them,
and link them together.
```

### Phase 3: Scratch And Decisions

Build:

- scratch inbox
- questions view
- decisions view
- promote note to page
- link scratch to created page
- status/canon workflow
- open questions in context panel
- decisions in context panel

Acceptance:

```text
Capture a rough note, promote it, record a decision, and keep the trail.
```

### Phase 4: Context Panel

Build:

- right panel sections
- infobox section
- tags
- outgoing links
- backlinks
- relationships
- notes
- scene assignments
- media summary
- page actions

Acceptance:

```text
Selecting a page shows its identity, connections, notes, media, and next actions.
```

### Phase 5: Page Media

Build:

- attach media to page
- paste/import image
- browse media library
- set hero
- gallery ordering
- media sidecars
- broken media diagnostics

Acceptance:

```text
Attach references to a page, reopen the project, and see the same media context.
```

### Phase 6: Generate Reference Into Page

Build:

- page action: Generate Reference
- prompt seeded from page context
- Nymphs Image status check
- generation request
- selected output import
- gallery attach
- provenance record

Acceptance:

```text
Generate one image from a page and have it land back in that page's gallery.
```

### Phase 7: Story, Scene, Dialogue

Build:

- story arc template
- quest template
- scene template
- timeline view over scene/event pages
- scene participant assignments
- dialogue designer launched from scene pages
- basic idle/ambient/interactive thread storage

Acceptance:

```text
Create a quest scene, link participants and place, sketch dialogue, and see the
links from related pages.
```

### Phase 8: Brain And Export

Build:

- page-aware Brain action menu
- context bundle builder
- world bible export
- Godot-oriented structured export
- reports for unresolved questions, missing media, broken links, and non-canon pages

Acceptance:

```text
Export a readable world bible and starter game data from the same project.
```

## 11. Suggested Service Boundaries

Backend/services:

```text
projectService
vaultService
pageService
frontmatterService
indexService
templateService
scratchService
relationshipService
mediaService
brainContextService
moduleBridgeService
exportService
diagnosticsService
```

Frontend areas:

```text
WorldShell
WorldExplorer
PageWorkspace
PageEditor
PagePreview
ContextPanel
InfoboxPanel
ScratchPanel
RelationshipPanel
MediaPanel
StoryPanel
ReportsPanel
ExportPanel
```

Keep these boundaries early. The app will become large quickly.

## 12. First Build Slice

Build this first:

```text
open/create project
  -> world tree
  -> Markdown page editor
  -> frontmatter infobox
  -> wikilinks
  -> backlinks
  -> tags
  -> scratch promotion
  -> right context panel
```

Then add:

```text
page media
  -> attach/import
  -> set hero
  -> gallery
  -> generate reference into current page
```

Then add:

```text
story tools
  -> scene pages
  -> timeline view
  -> dialogue designer
  -> export
```

## 13. Later Expansion

After the first loop works, expand into:

- visual relationship graph
- production board
- 3D model actions
- richer export profiles
- game state/flags inspector
- dialogue outcome validation
- asset approval/export workflow
- map views
- family/faction trees
- advanced Brain workflows

These are natural growth paths. They do not need to block the first useful app.

## 14. Sources Reviewed

Primary source links:

- WORBI app source: https://github.com/rauty79/WORBI
- Chronicler repository: https://github.com/mak-kirkland/chronicler
- Chronicler help guide: https://github.com/mak-kirkland/chronicler/blob/master/HELP.md
- WORBI Nymphs module: https://github.com/nymphnerds/worbi
- Nymphs Image module: https://github.com/nymphnerds/zimage
- Brain module: https://github.com/nymphnerds/brain
- Pixal3D module: https://github.com/nymphnerds/Pixal3D
- TRELLIS module: https://github.com/nymphnerds/trellis

Local source checkouts:

```text
/home/nymph/NymphsModules/worbi-source
/tmp/nymphs-world-research-chronicler
```

Local files reviewed:

```text
WORBI:
  README.md
  docs/INFORMATION_PANEL_SYSTEM.md
  docs/TAG_RELATIONSHIP_SYSTEM.md
  docs/AI_TOOLS_INTEGRATION.md
  docs/legacy/CROSS_DOCUMENT_WIKILINKS.md
  docs/legacy/DOCUMENT_TEMPLATES.md
  docs/NEW_DIALOGUE_SYSTEM.md
  docs/RELATIONSHIP_GRAPH.md
  docs/APP_REFACTOR_PLAN.md
  docs/WORBI_GODOT_INTEGRATION.md
  client/src/components/InformationPanel.tsx
  client/src/components/ImageExplorer.tsx
  client/src/components/HeroImagePicker.tsx
  server/src/services/fileService.js
  server/src/services/tagService.js
  server/src/services/sceneService.js
  server/src/routes/imageGeneration.js
  server/src/routes/graph.js

Chronicler:
  README.md
  HELP.md
  src-tauri/src/world.rs
  src-tauri/src/indexer.rs
  src-tauri/src/parser.rs
  src-tauri/src/renderer.rs
  src-tauri/src/writer.rs
  src-tauri/src/commands.rs
  src/lib/infobox.ts
  src/lib/components/views/FileView.svelte
  src/lib/components/views/Editor.svelte
  src/lib/components/views/BacklinksPanel.svelte
```
