# Modular NymphsCore Plan

**Generated**: 2026-05-05  
**Branch Context**: `rauty`  
**Purpose**: Concise plan for restructuring `NymphsCore` into an open modular platform with installable `Nymphs`.

---

## 1. Core Direction

- `NymphsCore` is the open-source core Manager/platform.
- `Nymphs` are open Manager add-ons/modules.
- Examples of Nymphs:
  - `Brain`
  - `WORBI`
  - `Z-Image`
  - `TRELLIS.2`
- The Manager should install and control Nymphs, but only show control tabs/pages for Nymphs that are actually installed.
- The user installs the core, then adds only the Nymphs they want.

---

## 2. Product Layering

The stack should be:

- `NymphsCore`
- `Nymphs`
- external interfaces/plugins

Meaning:

- `NymphsCore` = installer, Manager UI, status, lifecycle, updates, shared runtime coordination
- `Nymphs` = installable open modules managed by `NymphsCore`
- external interfaces/plugins = Blender, Unity, or other software integrations that talk to installed Nymphs

Important product rule:

- Manager add-ons / Nymphs are not planned as paid products
- if anything is paid later, it would be a polished external interface/plugin for popular software, not the Manager-side Nymph itself

---

## 3. UI Direction

The current Manager should move toward a modular UI model:

- main/home page shows available and installed Nymphs
- only installed Nymphs get their own tabs/pages
- if a Nymph is not installed, its tab should not exist yet
- opening an installed Nymph tab should show that Nymph’s controls, status, logs, and actions

Suggested behavior:

- startup performs one shared scan of installed state and version/update state
- avoid rerunning heavy checks every time a tab is clicked
- each Nymph page can still offer manual refresh

---

## 4. Nymph Contract

Even if Nymphs are packaged differently internally, they should all look the same to `NymphsCore`.

Every Nymph should expose a common contract such as:

- `manifest`
- `install`
- `update`
- `status`
- `start`
- `stop`
- `remove`
- optional `open`
- optional `logs`
- optional `configure`

This keeps the Manager generic while letting each Nymph have very different internals.

---

## 5. Packaging Direction

Do **not** force every Nymph to use the same internal packaging.

Instead:

- standardize the Manager-facing contract
- allow multiple payload/source types underneath

Recommended packaging types:

### `script-only`

For lightweight helpers or smaller modules.

- install/control driven by scripts
- minimal runtime footprint

### `repo-based`

For open-source forks or maintained source repos.

Good fit for:

- `Brain`
- `Z-Image`
- `TRELLIS.2`

Manager behavior:

- clone/pull pinned repo state
- run that Nymph’s install/control scripts

### `archive-based`

For packaged releases or self-contained app bundles.

Good fit for:

- `WORBI` for now

Manager behavior:

- download/use bundled archive
- extract/install
- control via wrapper scripts

### `hybrid`

For cases where source and payload are best separated.

Examples:

- repo for scripts/metadata
- archive for large runtime payloads

---

## 6. Packaging Principle

`NymphsCore` should not care whether a Nymph came from:

- a git repo
- a tar/zip archive
- bundled local files

It should only care that the Nymph can answer:

- am I installed?
- how do I install?
- how do I update?
- how do I start/stop?
- what version am I?
- where are my logs/UI endpoints?

So the interface should be standardized more strongly than the payload format.

---

## 7. Current Mapping

Proposed current classification:

- `Brain` -> repo-based
- `Z-Image` -> repo-based
- `TRELLIS.2` -> repo-based
- `WORBI` -> archive-based for now

Not Nymphs themselves:

- Blender addon / interface
- Unity plugin / interface

Those belong in the external interface layer and should talk to installed Nymphs rather than being treated as the same thing as Manager-side modules.

---

## 8. First Implementation Pass

The first pass should prove the modular model without rebuilding everything at once.

Recommended goals:

1. define a Nymph manifest format
2. define the shared Manager-side module contract
3. make the Manager home page module-aware
4. show tabs/pages only for installed Nymphs
5. use one or two Nymphs as the first concrete examples

Good first candidates:

- `WORBI`
- `Brain`

Then later:

- move `Z-Image`
- move `TRELLIS.2`

---

## 9. Open Questions

These still need deciding:

- what exact fields belong in the Nymph manifest
- how dependencies between Nymphs are declared
- whether bundled/default Nymphs exist or everything is opt-in
- how update channels are represented:
  - pinned
  - release-tested
  - latest upstream
- how much install metadata should be cached at startup vs queried live

---

## 10. Recommended Next Step

The next design step should be:

- define a small manifest format for a Nymph

That will make the rest of the modular Manager design much easier to reason about, because UI, install flow, status handling, and packaging can all hang off that shared shape.
