# Manager Skeletonization Plan

**Generated**: 2026-05-05  
**Branch Context**: `modular`  
**Purpose**: Plan the next step of stripping `NymphsCore` down to its true core so it is ready to host installable `Nymphs`, including `Brain`, `Z-Image`, and `TRELLIS.2`.

---

## 1. Goal

The next step is not to keep adding features into the current Manager shape.

The next step is to reduce the Manager to its true responsibilities, so it becomes a modular host for Nymphs.

That means:

- core Manager stays small and generic
- current built-ins stop being special cases
- `Brain`, `Z-Image`, and `TRELLIS.2` become first-class Nymphs
- future Nymphs like `WORBI` plug into the same system

---

## 2. What Core Should Still Own

After skeletonization, `NymphsCore` should still own:

- base install flow
- WSL/base distro management
- shared status shell
- module/Nymph discovery
- manifest loading
- lifecycle orchestration
- install/update/start/stop/status dispatch
- dynamic UI generation for installed Nymphs
- shared logs and troubleshooting surfaces

This is the real “platform” layer.

---

## 3. What Should Stop Being Core

These should move out of the “hardcoded Manager internals” category and become Nymphs:

- `Brain`
- `Z-Image`
- `TRELLIS.2`
- `WORBI`

General rule:

- if it has its own install path, runtime, ports, lifecycle, page, or logs, it probably should not be hardcoded into the core forever

---

## 4. Key Principle

Do not start by splitting everything into external repos immediately.

First:

- convert current built-ins into **internal Nymphs**
- prove the Manager can host them through the same contract

Then later:

- refine packaging
- split repos where useful
- add more external Nymphs

This keeps the refactor honest without turning it into chaos.

---

## 5. Target Manager Shape

The Manager should move toward this structure:

### Core surfaces

- `Home`
- `Installed Nymphs`
- `Available Nymphs`
- `Logs / Troubleshooting`

### Dynamic Nymph surfaces

- each installed Nymph gets its own tab/page
- if a Nymph is not installed, it does not get a tab yet
- installing a Nymph adds its control surface to the Manager

### Nymph page responsibilities

Each Nymph page should own:

- install/update state
- runtime state
- start/stop actions
- status refresh
- logs/open actions
- any Nymph-specific controls

---

## 6. First Refactor Objective

Replace hardcoded feature sections with a Nymph-driven model.

That means moving away from baked-in assumptions like:

- `Brain` page is special
- `Z-Image Trainer` page is special
- `TRELLIS` support is just part of Runtime Tools

Instead:

- the Manager reads manifests
- discovers installed Nymphs
- builds its control surfaces from those manifests

---

## 7. Migration Strategy

### Phase 1 — Define the core boundary

Decide exactly what belongs to:

- platform/core
- Nymphs
- external interfaces/plugins

This is mostly an architecture pass.

### Phase 2 — Add Nymph registry + manifest loading

Build:

- manifest discovery
- Nymph model objects in the Manager
- a registry/service that knows what is installed and what is available

### Phase 3 — Build manifest-driven UI shell

Refactor UI so:

- installed Nymphs appear dynamically
- the main page becomes a Nymph-aware control surface
- tabs/pages come from installed manifests rather than hardcoded sections

### Phase 4 — Convert built-ins into internal Nymphs

First candidates:

- `Brain`
- `Z-Image`
- `TRELLIS.2`

This is the proof that the architecture is real.

### Phase 5 — Add newer Nymphs cleanly

After the core works:

- add `WORBI`
- add future Nymphs
- refine packaging per Nymph type

---

## 8. Recommended Order Of Conversion

Suggested order:

1. `Brain`
2. `Z-Image`
3. `TRELLIS.2`
4. `WORBI`

Why:

- `Brain` already behaves like a module with a strong identity
- `Z-Image` and `TRELLIS.2` are the real test of whether major runtimes can stop being “special”
- `WORBI` should land on the new architecture instead of forcing the old Manager shape to grow further

---

## 9. Risks To Avoid

### Do not do a blind rewrite

The danger is deleting structure faster than the replacement architecture is ready.

Better approach:

- keep current functionality working
- introduce Nymph infrastructure in parallel
- migrate sections one by one

### Do not overdesign manifests before the shell exists

The manifest should stay small and practical.

### Do not let Runtime Tools remain a dumping ground forever

Some shared runtime checks may remain core, but the Manager should avoid keeping every major subsystem trapped in one permanent “tools” page.

### Do not split packaging too early

Packaging can stay local/internal while the architecture proves itself.

---

## 10. Success Criteria

This skeletonization pass is successful when:

- the Manager still handles base install and shared environment setup
- installed Nymphs are discovered through manifests
- tabs/pages are created only for installed Nymphs
- `Brain`, `Z-Image`, and `TRELLIS.2` no longer feel like hardcoded exceptions
- the Manager can accept a new Nymph without needing a custom architectural rewrite

---

## 11. Recommended Immediate Next Step

The immediate next step should be:

- create the Nymph registry inside the Manager

Reason:

- once the Manager can discover and model Nymphs internally, both the UI refactor and the built-in-to-Nymph migration become much easier to stage safely

That is the clean bridge between today’s Manager and the modular architecture you actually want.
