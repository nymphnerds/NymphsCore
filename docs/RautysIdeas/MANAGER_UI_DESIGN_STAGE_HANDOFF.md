# Manager UI Design Stage Handoff

**Generated**: 2026-05-06  
**Branch Context**: `rauty`  
**Purpose**: Preserve the current state and clear priorities for the next stage of work, where the primary focus is **extensive UI design iteration** for the new modular `NymphsCore Manager` shell before the manifest/module contract is fully wired.

---

## 1. What Stage We Are In Now

The Manager has now crossed an important line:

- it is no longer just being discussed conceptually
- a new shell foundation has been built in code
- the next phase should focus heavily on **design refinement**, visual hierarchy, layout quality, polish, and interaction feel

Important framing:

- this next stage is **not** the manifest-first architecture phase yet
- this next stage is **not** the full module-entrypoint implementation phase yet
- this next stage is primarily about getting the **core shell UI direction right**

The reasoning is simple:

- the visual direction is now important enough to deserve concentrated attention
- the Manager will likely spend a long time being shaped visually before the full modular contract is finished
- we need a stable design handoff so that context is not lost between iterations

---

## 2. Current Product Framing

The intended product structure remains:

- `NymphsCore` = the open core platform and Manager shell
- `Nymphs` = installable open modules managed by the shell
- external interfaces/plugins = things like Blender, Unity, or other software integrations

This is still the architecture direction.

But for the **current stage**, the main emphasis is:

- design the shell as if this modular future is real
- do not wait for the final manifest system before refining the shell experience

That means the UI should already reflect:

- a small core nav
- a modular home page
- installed Nymph pages
- available-vs-installed thinking
- no permanent hardcoded product clutter for module-specific areas

---

## 3. What Was Implemented Already

A new shell-oriented Manager UI foundation now exists in the app.

The most relevant files are:

- `Manager/apps/NymphsCoreManager/Views/MainWindow.xaml`
- `Manager/apps/NymphsCoreManager/Views/MainWindow.xaml.cs`
- `Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs`
- `Manager/apps/NymphsCoreManager/ViewModels/NymphModuleViewModel.cs`
- `Manager/apps/NymphsCoreManager/ViewModels/ShellNavigationItemViewModel.cs`
- `Manager/apps/NymphsCoreManager/Models/ManagerPageKind.cs`
- `Manager/apps/NymphsCoreManager/ViewModels/RelayCommandT.cs`

What this new shell already does:

- replaces the old step-heavy visual structure with a shell-style layout
- keeps a small always-present core navigation
- treats `Brain`, `WORBI`, `Z-Image Turbo`, and `TRELLIS.2` as optional Nymphs in the UI model
- shows installed Nymphs separately from available Nymphs
- creates module navigation only for installed modules
- includes a dedicated module detail page shape
- keeps `Home`, `Logs`, and `Guide` as real core destinations
- includes a right-side utility rail for runtime summary, quick actions, recent logs, and update state
- rotates sidebar artwork from the generated nymph PNG set if that folder exists

The rotating artwork currently points at:

- `~/.codex/generated_images/019df73a-f0bf-7393-820f-569bf52f87ae`

If that folder is missing, the shell falls back to the bundled splash image.

This means the design stage is now starting from a **real coded shell**, not from zero.

---

## 4. What Has Been Decided Clearly

These decisions should now be treated as strongly preferred unless a better direction clearly emerges.

### Core shell direction

- the Manager should look and feel close to the provided mockup
- this is not meant to be a soft inspiration-only relationship
- the shell should feel premium, technical, minimal, and branded

### Navigation direction

- core left nav should stay minimal
- keep core items like:
  - `Home`
  - `Logs`
  - `Guide`
- installed Nymphs can appear as their own pages
- uninstalled Nymphs should not become permanent navigation clutter

### Modularity direction

- `Brain` should be treated as a Nymph
- `WORBI` should be treated as a Nymph
- `Z-Image Turbo` should be treated as a Nymph
- `TRELLIS.2` should be treated as a Nymph

### Core-vs-module direction

Core Manager should own:

- shared shell
- shared logs
- shared guide/help
- shared runtime/base platform view
- shared orchestration patterns

Module-specific products should own:

- their deeper control surfaces
- their detailed status
- their module-specific actions
- their module-specific runtime/configuration experience

### Branding direction

Keep:

- `NymphsCore` logo
- dark green / black-green identity
- teal technical accents
- nymph artwork presence in the shell

Do not drift into:

- generic enterprise dashboard styling
- bland flat utility UI
- purple/white default AI-app energy
- cluttered wizard feeling

---

## 5. What This Next Stage Should Prioritize

This stage should prioritize **UI/UX quality** over architecture expansion.

The main workstreams should be:

1. Improve the shell composition until it feels truly intentional and premium.
2. Refine spacing, type scale, balance, and hierarchy.
3. Improve card styling, density, and information rhythm.
4. Decide exactly how much of the mockup should be preserved literally versus adapted.
5. Refine the sidebar artwork treatment so it feels elegant, not bolted-on.
6. Improve module page layouts so they feel like real product surfaces.
7. Improve the right rail so it feels useful, not like filler.
8. Make the shell feel strong at desktop sizes and still sane at smaller window sizes.

Important:

- the goal is **not** to rush back into architecture work too early
- the goal is to spend time shaping the visual language until it feels correct

---

## 6. What Should Be Deferred For Now

These areas matter, but they are **not the main objective of the next stage**.

### Manifest and registry work

Defer:

- final `nymph.json` wiring
- registry loading
- full manifest parsing
- state/manifest separation implementation
- dynamic module loading from real manifests

### Full module lifecycle integration

Defer:

- full install/update/start/stop/remove entrypoint contract
- final wrapper-script plumbing for every Nymph
- complete dependency modeling
- full module-state caching layer

### Major architecture cleanup

Defer:

- full service-layer re-architecture
- broad codebase skeletonization beyond what the UI needs immediately
- perfection in the data model before the visual direction is stable

The key idea is:

- **design first**
- then manifest/contract work after the shell direction feels settled

---

## 7. Design Goals For The Next Stage

The UI should feel:

- premium
- technical
- restrained
- sleek
- modern
- slightly atmospheric
- modular
- intentional

The UI should not feel:

- tutorial-like
- crowded
- whimsical
- generic admin dashboard
- over-glassified
- visually noisy
- full of placeholder surfaces

The homepage in particular should feel like:

- a polished control surface
- a modular product hub
- a branded overview layer
- not a checklist wizard

---

## 8. Strong Visual Reference Direction

The provided mockup should continue to act as the primary north star.

Most important elements from that mockup:

- strong left brand rail
- large clear page heading
- technical `//` micro-language
- dark layered shell surfaces
- lime and teal status accents
- module cards as primary content objects
- compact right utility rail
- soft atmospheric artwork in the left area

The current coded shell is a **foundation**, not the final fidelity match.

The next stage should be willing to:

- rework layout ratios
- rework typography
- rework border radii
- rework card structure
- rework sidebar art placement
- rework panel density
- rework action button composition

if that is what it takes to get much closer to the intended feel.

---

## 9. Specific UI Areas That Need Design Attention

### 1. Left rail

Current shell direction is correct, but it still likely needs:

- better vertical rhythm
- better relationship between nav and artwork
- more intentional logo block treatment
- stronger active/hover state styling
- better handling when many Nymphs are installed

### 2. Home page composition

Needs likely refinement in:

- macro layout balance
- section sizing
- section priority
- how many cards belong above the fold
- how much detail belongs in overview cards

### 3. Installed Nymph cards

Needs likely refinement in:

- icon/monogram treatment
- version/status prominence
- action density
- consistency across very different Nymph types
- visual distinction from available Nymph cards

### 4. Available Nymph cards

Needs likely refinement in:

- how “installable but not installed” is communicated
- whether cards should feel quieter or more aspirational
- whether they need install CTA prominence now or later

### 5. Module detail pages

These are still early scaffolds and likely need:

- more convincing structure
- better action grouping
- better status presentation
- better use of technical micro-language
- better separation between module facts, live state, actions, and logs

### 6. Right rail

This area needs discipline.

It should only contain:

- genuinely useful summaries
- genuine shortcuts
- genuinely recent logs or quick diagnostics

If a panel does not help, it should be removed or rethought.

### 7. Typography

The next stage should take typography more seriously.

Important questions:

- what is the headline font language?
- what is the technical mono language?
- how much size contrast is needed?
- should some labels be tighter and more compact?
- should some blocks be visually calmer?

### 8. Motion and transitions

Not the top priority, but worth considering after structure stabilizes:

- artwork rotation timing
- subtle page transitions
- hover behavior
- navigation selection feel
- card response polish

---

## 10. Rules To Keep During Design Work

Even while focusing on design, do not lose these rules:

- if it does not navigate somewhere real, question it
- if it does not communicate real state, question it
- if it does not trigger a real action, question it
- if it fills space without helping, remove or redesign it

However:

- temporary scaffolding is acceptable during this design phase
- the shell may still carry some semi-placeholder content while visual decisions are being made
- just do not let those placeholders harden into permanent low-value UI

---

## 11. Relationship To The Manifest Plan

The manifest direction still matters and should quietly shape the UI.

Important architectural truths to respect even during design work:

- installed Nymphs should feel data-driven
- module pages should feel generated from a shared module model
- the shell should not regress back into hardcoded permanent runtime sections
- the UI should assume a future `manifest + state + entrypoints` world

But again:

- do not let unfinished manifest work block design iteration right now

The next stage can freely improve the shell while preserving that future direction.

---

## 12. Suggested Working Strategy For The Next Stage

Recommended order:

1. tune the shell layout until it feels visually strong
2. refine the left rail and right rail
3. refine Home card language and density
4. refine installed vs available Nymph presentation
5. refine one module page pattern deeply
6. then propagate that pattern across the rest

That is likely better than:

- jumping into manifest implementation too early
- spreading effort across architecture and detailed visual polish at the same time

This stage should probably be highly iterative.

Expect:

- multiple passes
- layout reversals
- style experimentation
- rebalancing of sections

That is normal and desirable here.

---

## 13. Open Design Questions

These are good questions to resolve during the next phase:

- How literal should the mockup fidelity be?
- Should the left rail art be large and immersive, or slightly quieter?
- Should installed module cards contain actions on Home, or mostly route into module pages?
- How much data belongs in the right rail versus the Home body?
- Should `Logs` be a utilitarian page or a more branded diagnostics surface?
- Should `Guide` remain a page, or eventually become a lighter external-doc launcher?
- How much shell translucency is useful before readability starts to suffer?
- What is the right balance between teal and lime accents?
- Should module monograms remain, or eventually become dedicated icons?

---

## 14. Build / Verification Notes

The release build script was successfully used after the new shell foundation was put in place.

Relevant build path:

- `\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1`

Successful output location:

- `Manager/apps/NymphsCoreManager/publish/win-x64/NymphsCoreManager.exe`
- `Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip`

The first build issue encountered during the shell rewrite was:

- WPF `TextBlock` does not support `LetterSpacing` in this context

That issue has already been fixed.

---

## 15. Short Summary

The current moment is:

- the old Manager shape has been broken open
- a new shell foundation now exists
- the modular future is reflected conceptually in the UI
- the next stage should spend real time on design quality

The best next focus is:

- refine the shell aggressively
- get closer to the target mockup feel
- make the UI feel premium and intentional
- postpone deeper manifest/entrypoint implementation until after the shell direction is visually convincing

This is now a **design stage handoff**, not an architecture-first handoff.
