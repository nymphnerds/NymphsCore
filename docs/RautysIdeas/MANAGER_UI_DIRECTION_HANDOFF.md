# Manager UI Direction Handoff

## What The System Is

`NymphsCore` is the overall modular platform/ecosystem.

The Manager is one part of that system.

The Manager is the base shell/control app inside the wider `NymphsCore` system.

The system has these layers:

- `NymphsCore` = the overall platform/ecosystem
- `Manager` = the base shell/control app
- modules/add-ons = installable toolsets and runtimes
- external interfaces/plugins = things like Blender or Unity integrations that connect into the system

The Manager should handle:

- base runtime / WSL setup
- shared install and update flow
- shared status, logs, and troubleshooting
- discovery and control of installed modules/add-ons

Examples of modules/add-ons discussed so far:

- `Brain`
- `WORBI`
- `Z-Image`
- `TRELLIS.2`

Important product rule:

- `NymphsCore` stays open source
- Manager-side modules/add-ons are not the thing intended to be sold
- if anything is paid later, it would be polished external interfaces/plugins for popular software, not the Manager-side module/add-on itself

## What The Modular System Means

The modular system means:

- users install the base platform first
- users add only the modules/add-ons they actually want
- installed modules become available to control from the Manager
- uninstalled modules stay out of the way

So the Manager should behave like a modular shell, not like a permanently overstuffed app.

## What The Core Manager Should Own

The core Manager should own:

- base install flow
- base runtime / distro handling
- shared status shell
- manifest loading
- installed-module discovery
- install / update / start / stop / status orchestration
- shared logs and troubleshooting
- UI shell that exposes installed modules cleanly

## What Should Be Modules

These should be treated as modules/add-ons rather than permanent hardcoded core sections:

- `Brain`
- `WORBI`
- `Z-Image`
- `TRELLIS.2`

General rule:

- if it has its own install path, runtime, lifecycle, controls, or logs, it probably should not live as a permanent special-case inside core

## Packaging Direction

Modules/add-ons do not all have to be packaged the same way internally.

What should be standardized is the Manager-facing contract, not the payload format.

Expected packaging shapes:

- `repo-based`
- `archive-based`
- `script-only`
- `hybrid`

Likely current fit:

- `Brain` -> repo-based
- `Z-Image` -> repo-based
- `TRELLIS.2` -> repo-based
- `WORBI` -> archive-based for now

The Manager should care about:

- what the module is
- whether it is installed
- how to install/update/start/stop it
- what version it is
- where its logs or UI live

It should not care too much about whether the payload came from a repo, archive, or bundled files.

## UI Goal

Redesign the Manager as a bare, clean shell for this modular system.

The shell should feel:

- light
- clear
- technical
- premium
- purposeful

Main rule:

- if something has no purpose, lose it

## What Must Be Kept

From the current Manager:

- the `NymphsCore` logo
- the soft Nymph splash/watermark in the sidebar/background
- the dark green base palette
- teal highlight text/accent language

These are part of the identity and should carry forward.

## Navigation Direction

The preferred shell direction is:

- very minimal left sidebar
- sidebar only for base/core items
- `Home` should exist
- `Logs` should exist
- `Guide` can exist
- anything else should justify itself

The sidebar should not be full of permanent module buttons.

Installed modules should appear as their own tabs/pages only when installed.

## Home Page Direction

Home should stay light.

Its jobs are:

- show what is installed
- show what is available to add
- show a quick runtime/server glance
- offer real actions to install/manage modules

It should not become a wall of explanation.

## Installed Module Direction

Installed modules should:

- appear only when installed
- get their own control surfaces
- hold their deeper controls, status, logs, and module-specific actions there

That keeps Home lighter and keeps detailed management where it belongs.

## Visual Direction

The desired look is:

- slick
- technical
- premium
- current
- subtle

Not:

- cute
- noisy
- over-decorated
- crowded

Glass/translucency is fine if used lightly on shell surfaces, but it should not overwhelm dense technical areas.

## Technical-But-Stylish Language

Small mono labels and restrained teal micro-language were liked.

Good uses:

- section tags
- small technical labels
- status pills when they communicate something real

Examples of the tone:

- `// Installed`
- `// Available`
- `// Features`

These should be used carefully and only where they help.

## Inspiration References

### Current Manager

Keep:

- logo
- splash art
- dark green identity
- teal accents

Move away from:

- step-heavy feeling
- wizard energy
- old hardcoded structure

### NymphsCore homepage

Use as inspiration for:

- color language
- typography mood
- technical mono styling
- premium dark/teal identity

### AMD Adrenalin examples

Use as inspiration for:

- polished technical shell
- premium application feeling
- control-center energy

### NVIDIA app home/library examples

Use as inspiration for:

- minimal nav
- installed/library thinking
- clean technical shell

### NVIDIA graphics/settings example

Use as inspiration for:

- installed-module management view
- left list + right detail control pattern

This is better inspiration for a detailed installed-module page than for the main Home page.

## Hard Rules

- if it does not navigate somewhere real, remove it
- if it does not show real state, remove it
- if it does not trigger a real action, remove it
- if it fills space without helping the user, remove it

## Summary

The intended Manager is:

- a branded modular shell
- a platform for installing and controlling modules/add-ons
- visually minimal but premium
- light on Home
- deeper in installed module pages

The key idea is simple:

- `NymphsCore` is the overall platform
- the Manager is the shell/control app inside it
- modules/add-ons plug into that system
- the Manager should present that clearly and elegantly

## Current Build State

The shell is now being designed against a fixed canvas.

Locked shell size:

- `1020 x 620`
- do not go back to freeform resize-based layout decisions

Current global layout:

- left sidebar stays
- right rail is gone
- main area is the only content surface

Current nav direction:

- `Home`
- `Logs`
- `Guide`

These are real destinations and should stay minimal and purposeful.

## Locked Decisions

These decisions are already made and should not be re-litigated every pass:

- `Home` is the shell landing page
- `Logs` is a dedicated logs page
- `Guide` is a dedicated guide/wiki page
- the right-side quick-actions/recent-logs rail is removed
- Home module cards should be simple and compact
- clicking an installed Home module card should open that module page
- Home cards should not carry body text or dead button rows
- Home cards should not use `WrapPanel`-style loose responsive behavior
- use measured fixed layout, not repeated margin nudging

## Home Direction Now

`System Overview` is intentionally reduced.

Only `System Checks` is needed there.

Do not bring back:

- extra overview cards for runtime/modules
- oversized overview trays
- filler content

Home should mainly show:

- `System Checks`
- installed modules
- available modules

## Home Card Direction Now

Home cards were overcomplicated in earlier passes and must stay simple now.

Very important width rule:

- `System Checks` card width and Home module card widths should follow the same horizontal rhythm
- only the height should differ
- overview cards should be shorter, not narrower or wider

Current issue at handoff time:

- the Home layout was left in a broken/inconsistent state
- one of the mistakes made was letting overview and module cards drift out of consistent width rhythm
- that should be corrected before doing more styling work

Home installed cards should contain only:

- monogram
- module name
- small `// type` line
- status line

Home available cards should contain only:

- monogram
- module name
- small `// category` line
- availability line

Do not reintroduce:

- body/description text
- inline `Open` buttons
- `...` buttons
- large action rows

Installed cards are full-card click targets.

## Spacing / Layout Rule

The fixed shell means the Home rows should be laid out with explicit math.

Use:

- fixed card widths
- fixed card heights
- fixed horizontal gutters
- shared left alignment reference

Avoid:

- `WrapPanel` drift
- balancing by eye with random negative margins
- changing one row independently from the others

If one row is aligned to the `System Checks` card, the other Home rows should use the same left start.

## Anchor Rule

The main problem in later passes was losing the content anchor.

Important:

- the page needs one clear left rail for the main content
- `Home`, the subtitle, the `//` section labels, and the Home card rows should all feel intentionally related to that rail
- widening the app shell without re-anchoring the content makes the page feel like it is floating in empty space
- do not solve Home layout problems by increasing window width alone

Practical rule:

- anchor the content first
- then fit the 3-card row inside that anchored lane

## Home Row Fit Rule

The repeated failure mode was simple:

- the 3-card installed row was too wide for its lane
- the third card pressed into the right edge
- single-card rows could look fine while the 3-card row was still broken

So future passes should verify the 3-card row first, not the single-card rows.

The row should satisfy all of these at once:

- first card has a deliberate left gap from the content edge
- third card has a matching right gap
- middle gaps are consistent
- the row does not clip on the right
- card widths stay consistent across `System Checks`, installed modules, and available modules

## Header Alignment Rule

The `Home` title and its subtitle were tuned visually against the sidebar logo block.

Important:

- `Home` itself is close and should not be freely nudged around anymore
- if alignment work continues, move the subtitle carefully against the sidebar `// GameDev Ai Pipelines` line
- do not keep changing the internal spacing between `Home` and its subtitle unless absolutely necessary

## Logs Direction Now

Logs are now one unified log surface.

That page should stay:

- full-width
- simple
- readable

Do not split it back into multiple large log boxes.

## Module Page Direction Now

The new `rauty` Home/cards/navigation shell must **not** replace the module pages already built on `main`.

Important:

- the Home cards are navigation and discovery
- the module page content is module-specific
- the generic `Module Facts / Live Detail / Manager Contract` page is only a temporary fallback/debug page
- do not flatten every module into one shared manifest inspector

The pages on `main` represent real product work and should be carried forward:

- `Brain` page: LLM, MCP, WebUI, local/remote model, OpenRouter key, live monitor, model manager, update stack, activity log
- `Z-Image Trainer / AI Toolkit` page: datasets, LoRAs, captions, presets, adapter version, training settings, queue/job controls, progress, live log
- `Runtime Tools` page: Z-Image and TRELLIS backend readiness, Hugging Face token, TRELLIS GGUF quant, runtime code mode, fetch/test actions, live log

Direction for the modular shell:

- keep the new Home/cards/sidebar work
- preserve the real page content from `main`
- split Runtime Tools into proper module-owned pages where it makes sense:
  - Z-Image page
  - TRELLIS page
- make `AI Toolkit` its own trainer page using the existing Z-Image Trainer work
- make `Brain` use the existing Brain page work
- make `WORBI` a custom page later, not a generic facts page

Shared layout is allowed, but the content area must be owned by the module.

Good mental model:

```text
Home/card shell = which Nymph exists and whether it is installed
Module page     = the actual tool/workbench for that Nymph
Manifest        = how Manager discovers and wires the Nymph
```

## Guide Direction Now

Guide is meant to evolve into a real wiki/reference surface.

Longer term direction:

- bring in the existing repo guide/wiki content
- use the Manager guide page as the in-app documentation surface

## Working Style Warning

This UI got worse whenever changes were made by repeated tiny nudges without a stable reference.

For future passes:

- measure first
- change one layout system at a time
- prefer rebuilding a small section cleanly over stacking margin hacks
- keep the shell simple
- if the shell width changes, re-check the content anchor before touching card math
