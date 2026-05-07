# Nymph UI Shell Brief

**Generated**: 2026-05-05  
**Branch Context**: `rauty`  
**Purpose**: Define how installed and available Nymphs should be presented in the new modular `NymphsCore` Manager UI.

---

## 1. Core Idea

The Manager should stop presenting most modules as sidebar buttons.

Instead:

- the sidebar should stay minimal and core-only
- installed Nymphs should be presented primarily as **cards**
- available Nymphs should be presented as **installable cards**

This makes the product feel like:

- a modular creative platform
- a curated collection of powers/tools

instead of:

- a stack of technical pages
- an installer with extra buttons

---

## 2. Navigation Model

### Sidebar

Keep the sidebar small and stable.

Recommended sidebar items:

- `Home`
- `Core`
- `Logs`
- optional `Library`

Meaning:

- `Home` = main dashboard and Nymph presentation
- `Core` = WSL/base distro/runtime host management
- `Logs` = shared troubleshooting surface
- `Library` = optional dedicated browse/install view if Home becomes too full

Do **not** put every installed Nymph in the sidebar by default.

---

## 3. Home Screen Structure

The Home screen should become the main module surface.

Recommended sections:

### Top summary strip

Shows at a glance:

- core state
- total installed Nymphs
- total running Nymphs
- attention needed count

### `Your Nymphs`

This is the most important section.

- only installed Nymphs appear here
- each Nymph is shown as a card
- cards should feel like owned tools in the system

### `Available Nymphs`

Below installed Nymphs:

- not yet installed
- visually distinct from owned Nymphs
- focused on discovery and install

### Optional quick actions

Only if needed:

- `Start All`
- `Stop All`
- `Refresh Status`

These should stay secondary, not dominate the page.

---

## 4. Installed Nymph Cards

Installed Nymphs should be presented as rich control cards.

Each installed card should contain:

- Nymph name
- short one-line purpose
- state badge
- one primary action
- one or two small secondary actions

### Recommended content

- title: `Brain`
- subtitle: `Local coding and MCP runtime`
- badge: `Running`
- primary action: `Open`
- secondary actions:
  - `Stop`
  - `Details`

### State badge examples

- `Running`
- `Stopped`
- `Installed`
- `Needs Setup`
- `Update Available`
- `Problem`

### Primary action rules

Each card should have one obvious next action:

- not installed -> `Install`
- installed but stopped -> `Start`
- running -> `Open` or `Manage`
- broken -> `Repair`

This is important.

The cards should not feel like five equally loud buttons fighting each other.

---

## 5. Available Nymph Cards

Available Nymphs should feel more like discoverable additions.

Each available card should contain:

- Nymph name
- one-line description
- lightweight badge such as:
  - `Available`
  - `Requires Core`
  - `Depends on Brain`
- primary action:
  - `Install`

Optional secondary text:

- dependency note
- category label

These cards should feel less “live control panel” and more “choose what to add.”

---

## 6. Visual Separation

Installed and available Nymphs should not look identical.

Recommended distinction:

### Installed Nymphs

- stronger contrast
- richer status treatment
- more visually “owned”
- live-state emphasis

### Available Nymphs

- calmer presentation
- more discovery-oriented
- install-focused

This helps users instantly understand:

- what is already part of their system
- what is only an option

---

## 7. Card Interaction Model

Clicking a card should open a deeper Nymph surface.

That deeper surface can be:

- a detail page
- a slide-over panel
- a content view swap

But the important rule is:

- the card is the primary presentation layer
- the detail surface is secondary

This keeps Home clean while still allowing full controls.

---

## 8. Nymph Detail Surface

When a user opens a Nymph, the detail surface should show:

- status
- install/update state
- start/stop controls
- logs/open actions
- Nymph-specific settings

This is where the deeper technical detail belongs:

- version
- install root
- endpoints
- runtime info
- dependencies

That detail should not crowd the Home cards.

---

## 9. Core Screen

The `Core` page should not compete with Nymph cards.

It should be clearly about platform host responsibilities:

- WSL status
- distro/base install
- CUDA / shared runtime host state
- shared repair/update actions

It is the host layer, not the module gallery.

---

## 10. Logs Screen

The `Logs` page should be a shared utility surface.

Recommended functions:

- combined logs
- filter by Nymph
- filter by Core
- copy/export

This keeps troubleshooting centralized instead of scattering log surfaces everywhere.

---

## 11. Design Principle

Present Nymphs first as **tools you can use**, not as packages you must understand.

Lead with:

- what it is
- whether you have it
- whether it is running
- what to do next

Hide until needed:

- paths
- ports
- manifests
- dependency internals
- raw runtime details

---

## 12. Recommendation

The new shell should be built around this mental model:

- `Home` = your platform dashboard
- `Your Nymphs` = installed cards
- `Available Nymphs` = installable cards
- `Core` = host/runtime management
- `Logs` = shared troubleshooting

That is the clearest presentation model so far for the modular direction.
