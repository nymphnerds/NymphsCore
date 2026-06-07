# Nymphs World: WORBI + Chronicler Core Blueprint

Date: 2026-05-27

This is a clean, new blueprint for Nymphs World.

The center of the app is **not** asset generation.

The center of the app is:

```text
WORBI + Chronicler, but better for NymphsCore
```

Nymphs World should be a local-first worldbuilding wiki, writing room, lore
vault, entity system, reference binder, and canon management workspace. Media
generation should flow **into** that workspace. It should feel like a page tool,
not the reason the app exists.

## 1. Product Thesis

Nymphs World is the place where a world lives.

The main features are:

- pages
- folders
- Markdown/rich writing
- world wiki navigation
- entities
- infoboxes
- tags
- relationships
- backlinks
- scratch notes
- references
- search
- graph/context
- canon decisions
- story/world/system organization

Media generation is an integrated helper:

```text
current page
  -> add reference
  -> generate image
  -> attach output
  -> optionally make 3D
```

It should be tucked behind page-level commands such as `Add Media`,
`Generate Reference`, or `Make 3D From This Image`.

## 2. What The App Is

Nymphs World should feel like:

- Chronicler's offline Markdown vault and world wiki
- WORBI's richer writing/editor workspace
- an entity-aware reference binder
- a local creative notebook for messy worldbuilding
- a structured world bible when ideas become canon
- a bridge into Nymphs media modules when the user wants visuals or 3D

It should not feel like:

- a generation dashboard
- a ComfyUI replacement
- a production asset tracker first
- a 3D pipeline manager first
- a module launcher with a writing panel attached

The normal user action should be "work on the world", not "run a backend".

## 3. First Correct Vertical Slice

The first demo should prove worldbuilding, not media generation.

Build this first:

```text
Create world
  -> create character page
  -> create location page
  -> link character to location
  -> see backlinks
  -> add infobox fields
  -> tag both pages
  -> add scratch note
  -> promote scratch into canon
  -> attach or generate one reference image into the page gallery
```

If that loop feels good, Nymphs World is pointed in the right direction.

The media step is last on purpose. It proves generation can enter the world, but
does not make generation the product.

## 4. Primary Inspirations

### 4.1 Chronicler

Chronicler's strongest lessons:

- local vault
- Markdown files
- no account/cloud requirement
- wikilinks
- backlinks
- tags
- templates
- infoboxes
- image embeds and galleries
- diagnostics for broken links
- open a world folder and trust that the files are yours

Nymphs World should study these patterns, not copy Chronicler code.

### 4.2 WORBI

WORBI's strongest lessons:

- richer writing/editor UI
- project/workspace feel
- information panel beside the editor
- hero image and gallery concepts
- tag management
- explicit and implicit relationships
- AI tool permissions
- file-safe workspace APIs
- Z-Image bridge
- scene/dialogue/story planning ideas
- export-oriented thinking

Nymphs World should treat WORBI as the closest starting material for user
experience and interaction shape.

### 4.3 Nymphs Modules

Nymphs Image, Pixal3D, TRELLIS, and Brain are supporting tools.

They should appear in Nymphs World as:

- page commands
- insert media actions
- context-aware helpers
- optional advanced workflows
- provenance sources

They should not dominate the navigation or first screen.

## 5. Core Objects

### 5.1 World

A world is a folder.

It contains readable files and Nymphs World metadata. It can be backed up,
versioned, searched, and inspected without the app.

### 5.2 Page

A page is the primary object.

Most things are pages:

- character
- place
- faction
- item
- creature
- quest
- scene
- system
- note
- reference board
- timeline entry

Pages may be plain notes or typed entity pages.

### 5.3 Entity

An entity is a page with a type and stable ID.

Do not build a separate entity database that hides the page. The page is the
user-facing object.

### 5.4 Infobox

An infobox is structured frontmatter shown nicely in the UI.

It should be editable without forcing the user to touch YAML by hand.

### 5.5 Link

Links connect pages.

Support both:

```text
[[Nyra]]
[[Greenwarden Forest|the forest]]
```

Backlinks should be visible from the right panel and the page footer.

### 5.6 Tag

Tags are flexible grouping, not rigid classification.

Examples:

```text
#forest
#main-cast
#merchant-route
#needs-review
#canon
```

### 5.7 Relationship

Relationships are typed links.

Examples:

```text
Nyra lives_in Greenwarden Forest
Nyra belongs_to Greenwardens
Ironroot Village trades_with Rivergate
The Moon Gate unlocks Black Marsh
```

Relationships can start as simple frontmatter or sidecar JSON, but they should
render as readable connections in the UI.

### 5.8 Scratch

Scratch is where messy thinking belongs.

It must not be treated as second-class. Rough notes, questions, fragments,
arguments, rejected names, and maybe-later ideas are part of worldbuilding.

### 5.9 Media

Media is attached to pages.

Media includes:

- pasted image
- uploaded image
- generated image
- reference board item
- map
- audio reference
- GLB/model
- future video/animation

The media object supports the world. It does not replace the page.

### 5.10 Job

A job is a record of something a module did.

Jobs should stay mostly behind the scenes. They exist so generated media can
remember where it came from.

## 6. Project Vault Layout

Recommended first layout:

```text
MyWorld.nw/
  world.nymphworld.json
  pages/
    characters/
    locations/
    factions/
    items/
    creatures/
    quests/
    scenes/
    systems/
    notes/
  scratch/
    inbox.md
    questions.md
    decisions.md
    maybe-later.md
  media/
    images/
    references/
    maps/
    models/
    audio/
  templates/
    character.md
    location.md
    faction.md
    item.md
    quest.md
    scene.md
  exports/
    world-bible/
    engine/
  .nymphworld/
    index/
    jobs/
    thumbnails/
    diagnostics/
    cache/
```

This is deliberately simple.

Media lives inside the world folder when promoted or attached. Raw backend
outputs can remain in `NymphsData`, but selected media should be copied or
registered into the world.

## 7. Page Format

Use Markdown with frontmatter.

Example:

```markdown
---
id: page.character.nyra
type: character
title: Nyra
status: draft
tags:
  - forest
  - scout
relationships:
  faction: page.faction.greenwardens
  home: page.location.greenwarden-forest
media:
  hero: media.image.20260527.0001
---

# Nyra

## Canon

Nyra is a forest scout from the Greenwardens.

## Notes

She should feel practical, fast, and observant.

## Scratch

- Maybe she used to guide traders through the old road.
- Is she trusted by the village, or only tolerated?
```

The user should be able to edit this as a normal page. The app should make the
frontmatter friendly through panels and forms.

## 8. Infobox Model

Each page type can have an infobox template.

Character:

```text
name
role
faction
home
age
status
relationships
tags
hero image
```

Location:

```text
name
region
climate
population
factions
resources
threats
connected locations
map/reference image
```

Quest:

```text
name
status
giver
location
involved characters
required items
outcomes
related scenes
```

Infoboxes should make the wiki feel alive without making page creation feel like
database entry.

## 9. Main UI

### 9.1 Shell

Recommended layout:

```text
left:     vault explorer, collections, search
center:   editor/preview/page
right:    infobox, backlinks, references, relationships, page actions
bottom:   quiet activity strip for saves/jobs/module status
```

The app should open into the world, not into a media dashboard.

### 9.2 Left Panel

The left panel should support:

- folder tree
- page type filters
- recent pages
- favorites
- tags
- scratch
- search

### 9.3 Center Panel

The center is for writing and reading.

Modes:

- edit
- preview
- split
- focus writing

### 9.4 Right Panel

The right panel is the world context panel.

Sections:

- infobox/properties
- backlinks
- outgoing links
- related pages
- references/media
- scratch for this page
- page actions
- diagnostics for this page

This is where generated media belongs: as a reference/media section on the page.

### 9.5 Page Actions

Page actions should be compact and contextual:

```text
New linked page
Add reference
Generate reference
Ask Brain
Promote scratch
Mark canon
Add relationship
Export page
```

`Generate reference` should open a drawer/modal, not take over the page.

## 10. Scratch And Canon

Worldbuilding is messy.

Nymphs World needs both:

- loose thinking
- accepted canon

Recommended states:

```text
scratch
draft
needs-review
canon
retired
contradiction
```

Scratch should support:

- quick note capture
- page-local scratch
- global inbox
- questions
- decisions
- maybe-later
- rejected ideas

Canon decisions should be explicit:

```text
decision.20260527.0001
title: Nyra belongs to the Greenwardens
status: accepted
affected_pages:
  - page.character.nyra
  - page.faction.greenwardens
```

This helps the app answer "why did we decide this?" later.

## 11. Search, Backlinks, And Graph

Search should be one of the main features.

Support:

- full-text search
- title search
- tag search
- type search
- relationship search
- media/reference search
- unresolved link search

Backlinks should be more important than a big graph at first.

Graph view can come later, but the app should index relationships early so the
right panel can say:

```text
Mentioned by
Appears in
Connected to
Uses
Contradicts
Needs review with
```

## 12. Media Generation Belongs Here

Media generation is an input method.

It should be treated like:

- upload image
- paste image
- attach file
- choose reference
- generate reference

### 12.1 Add Media Flow

From any page:

```text
Add Media
  -> Upload
  -> Paste
  -> Link existing
  -> Generate image
  -> Generate/edit from selected image
  -> Make 3D from selected image
```

The page remains the center. The generated output appears in the page's
references/media section.

### 12.2 Nymphs Image

Nymphs Image should power:

- generate portrait
- generate location reference
- generate item concept
- generate mood/reference image
- edit existing reference
- extract parts, when useful

It should not become a primary Nymphs World tab.

### 12.3 Pixal3D And TRELLIS

3D generation should be behind selected media.

Example:

```text
select approved reference image
  -> Make 3D
  -> choose simple profile
  -> run backend
  -> attach GLB to the same page
```

Advanced backend controls can exist, but they should be hidden under advanced
options or opened in the module UI.

### 12.4 Provenance

Generated media should remember:

- page it was created from
- prompt
- source image, if any
- backend/module
- settings
- output path
- accepted/rejected status

This should be visible as "details" on the media item, not as the main UI.

## 13. Brain Belongs Here

Brain should help with the world, page, and context.

Good Brain actions:

- summarize this page
- find contradictions
- list unresolved questions
- suggest relationships
- turn scratch into a page
- generate a page template
- make an image prompt from this page
- compare two versions of canon
- explain what is missing from a location or faction

Brain should ask for permission before writing canon.

Suggested permission levels:

```text
read current page
read linked pages
read whole vault
write scratch
write draft
modify canon
```

Default should be current page plus linked pages.

## 14. Implementation Plan

### Phase 0: Decide The App Spine

Goal: commit to WORBI + Chronicler as the product center.

Deliverables:

- app name and module shape
- first vault schema
- page schema
- infobox schema
- link/tag model
- source decision: fork/evolve WORBI or build a cleaner app using WORBI patterns

Acceptance:

- everyone agrees this is a worldbuilding/wiki app first
- media generation is documented as an insert/helper workflow

### Phase 1: Vault And Pages

Goal: open, create, edit, and save a world.

Build:

- create world folder
- open world folder
- page explorer
- create page
- rename page
- delete page safely
- Markdown editor
- preview
- autosave
- recent pages
- favorites

Acceptance:

- user can work in the app as a local writing/wiki tool without any AI module
  installed

### Phase 2: Links, Backlinks, Tags, Search

Goal: make it a real world wiki.

Build:

- wikilink parser
- unresolved link handling
- backlinks panel
- outgoing links panel
- tags
- tag index
- full-text search
- diagnostics for broken links

Acceptance:

- user can create connected pages and navigate the world through links and
  backlinks

### Phase 3: Infoboxes And Templates

Goal: make pages feel worldbuilding-native.

Build:

- page types
- templates
- frontmatter editor
- right-panel infobox
- type-specific fields
- relationship fields

Acceptance:

- character/location/faction/item pages feel structured without feeling like
  database forms

### Phase 4: Scratch, Decisions, Canon

Goal: support messy thinking and canon control.

Build:

- global scratch inbox
- page-local scratch
- decisions log
- promote scratch to page
- promote scratch to canon
- status field and filters
- contradiction/needs-review flags

Acceptance:

- user can capture rough ideas and later turn them into structured world pages

### Phase 5: References And Media Attachments

Goal: attach media before generating media.

Build:

- upload image/file
- paste image
- attach existing file
- page gallery
- hero image
- media details panel
- media status
- thumbnails
- missing media diagnostics

Acceptance:

- a page can have references and galleries without any generator installed

### Phase 6: Generate Media Into Pages

Goal: make generation enter the world quietly.

Build:

- `Generate Reference` action on a page
- Nymphs Image health check
- simple prompt drawer seeded from page context
- import selected output into page gallery
- provenance details

Acceptance:

- generated image appears as page media, not as a separate generation project

### Phase 7: Optional 3D From Page Media

Goal: support 3D as a secondary media action.

Build:

- select image in page gallery
- `Make 3D` action
- simple Pixal3D/TRELLIS handoff
- attach GLB/model to page
- open model preview
- provenance details

Acceptance:

- a model belongs to the page/entity that requested it

### Phase 8: Export And Reports

Goal: turn the world into useful outputs.

Build:

- world bible export
- page bundle export
- media bundle export
- broken link report
- missing media report
- canon/needs-review report

Acceptance:

- the user can export a readable world bible without caring about generation
  modules

## 15. MVP Definition

MVP is not:

```text
generate image -> generate 3D -> export asset
```

MVP is:

```text
write world -> link world -> structure world -> search world -> attach media
```

The MVP is successful when the user can use Nymphs World every day as their
main worldbuilding app even if they never press a generate button.

## 16. What To Avoid

Avoid:

- starting with an asset pipeline
- making Jobs a primary tab too early
- making Pixal3D/TRELLIS prominent in the first screen
- making the app feel like a backend launcher
- forcing every page into rigid forms
- hiding all data in a database
- making media provenance louder than the writing experience
- making Brain a permanent chat window that steals the page focus

## 17. What To Preserve From The Previous Research

Keep these ideas, but put them in the right place:

- asset manifests are useful for generated/attached media
- job records are useful for provenance
- Nymphs Image is the first generation integration
- Pixal3D is the default 3D option later
- TRELLIS is an alternate/retexture path later
- Brain is page-aware assistance
- diagnostics are essential
- exports should be readable

The difference is priority:

```text
world app first
media capability second
asset pipeline third
```

## 18. Recommended First Build

Build this:

```text
Nymphs World opens a local world folder.
The user creates pages from templates.
The user links pages with wikilinks.
The app shows backlinks and an infobox.
The user adds scratch notes and promotes one to canon.
The user attaches one image to a page.
The user optionally generates one image into the page gallery.
```

That is the correct first milestone.

Only after that should Nymphs World care about 3D, advanced jobs, export
packages, or asset production boards.

## 19. Reference Links

Primary sources and inspirations:

- WORBI app source: https://github.com/rauty79/WORBI
- WORBI Nymph module: https://github.com/nymphnerds/worbi
- Chronicler: https://github.com/mak-kirkland/chronicler
- NymphsCore: https://github.com/nymphnerds/NymphsCore
- Nymphs registry: https://github.com/nymphnerds/nymphs-registry
- Nymphs Image: https://github.com/nymphnerds/zimage
- Brain module: https://github.com/nymphnerds/brain
- Pixal3D module: https://github.com/nymphnerds/Pixal3D
- TRELLIS module: https://github.com/nymphnerds/trellis

Comparable patterns:

- Obsidian data storage: https://obsidian.md/help/data-storage
- Obsidian internal links: https://obsidian.md/help/links
- Obsidian properties: https://help.obsidian.md/properties
- Yarn Spinner nodes/options: https://docs.yarnspinner.dev/v/2.1/getting-started/writing-in-yarn/lines-nodes-and-options
- Twine passage links: https://twinery.org/reference/en/editing-stories/linking-passages.html

## 20. One Sentence

Nymphs World is a local worldbuilding wiki and writing workspace, inspired most
by WORBI and Chronicler, with Nymphs media generation quietly available when the
user wants to add generated references into that world.
