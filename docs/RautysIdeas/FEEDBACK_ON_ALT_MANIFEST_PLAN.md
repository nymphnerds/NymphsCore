# Feedback On Alternate Manifest / Manager Plan

**Generated**: 2026-05-05  
**Branch Context**: `modular`  
**Purpose**: Concise feedback on the alternate implementation plan for modularizing the Manager.

---

## Overall Take

The plan is strong in direction, especially on:

- dynamic tabs
- a central module manager service
- dependency handling
- a cleaner always-present core shell

But it mixes strong architectural ideas with some details that are too specific too early.

The biggest correction is:

- build the **Nymph architecture first**
- do not let the UI redesign get ahead of the module contract and registry

---

## What To Keep

These parts are good and should carry forward:

- a master registry / manifest layer
- a Manager-side service for install state and dependency resolution
- always-present core tabs such as:
  - `Home`
  - `Runtime`
  - `Logs`
- dynamically created tabs/pages only for installed modules
- a shared install flow that understands prerequisites

These all fit the modular `NymphsCore` direction well.

---

## What To Change

### 1. Use `Nymphs`, not generic `addons`

If the architecture language is now:

- `NymphsCore`
- `Nymphs`
- external interfaces/plugins

then the data layer should reflect that too.

Recommendation:

- use `nymph.json` per module, or
- use a `nymphs.json` registry

Avoid building the long-term architecture around the old `addons` label if the product language has already shifted.

### 2. Do not store live `status` in the manifest

The alternate plan puts values like:

- `installed`
- `not_installed`
- `outdated`

into the manifest.

That should not live in the package definition.

Recommendation:

- manifest = mostly static definition
- runtime/install state = detected live or cached in a separate state file

Good separation:

- `nymph.json` -> what the Nymph is
- `nymph-state.json` or live detector -> what state it is currently in

### 3. Replace `install_script` with `entrypoints`

A single `install_script` field is too narrow.

Recommendation:

- use an `entrypoints` block

Example:

- `install`
- `update`
- `status`
- `start`
- `stop`
- `remove`
- optional `open`
- optional `logs`
- optional `configure`

That gives the Manager a stable contract for very different Nymph types.

### 4. Do not overcommit to the UI split before the registry exists

The proposed ViewModel/View breakdown is reasonable, but it comes a bit early.

Recommendation:

- build the Nymph registry and state detection first
- then shape the UI around the real module model

Otherwise the app risks getting restructured around assumptions that change again once manifests and lifecycle rules are real.

---

## What Feels Too Early

### Glassmorphism phase

The styling direction may be good later, but it is not the critical next step.

Right now the important work is:

- manifest design
- registry/service design
- Manager skeletonization
- built-in-to-Nymph migration

The visual redesign should follow the architecture, not lead it.

### Full installation wizard specifics

A richer install overlay may be useful, but it depends on:

- how Nymph installs are represented
- how dependency chains are modeled
- how progress and logs are surfaced

So it is better treated as a later implementation layer, not a first architectural phase.

---

## Main Architectural Correction

The alternate plan feels slightly too much like:

- redesign the Manager UI around addons

The stronger framing is:

- redefine the Manager as a modular host platform for Nymphs

That means the priority order should be:

1. define the Nymph contract
2. build the Nymph registry/state detector
3. skeletonize the Manager around installed/available Nymphs
4. generate dynamic tabs/pages from that model
5. polish visuals and flows later

---

## Recommended Merged Direction

Best combined version of both approaches:

- keep the master registry idea
- keep dependency resolution
- keep always-present core tabs
- keep dynamic tabs only for installed Nymphs
- switch architecture wording from `addons` to `Nymphs`
- keep manifest static
- move runtime state out of manifest
- replace `install_script` with a full `entrypoints` block
- treat styling as later, not phase-critical
- build the registry before the major UI rewrite

---

## Short Verdict

The alternate plan is:

- strong on UI direction
- strong on dynamic tabs
- strong on needing a central manager service
- weaker on manifest vs runtime-state separation
- too eager to lock in visual/UI structure before the Nymph contract is finished

The safest v1 path is:

- architecture first
- registry second
- UI shell third
- styling later
