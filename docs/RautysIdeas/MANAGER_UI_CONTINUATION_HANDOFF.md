# Manager UI Continuation Handoff

**Generated**: 2026-05-06  
**Branch Context**: `rauty`  
**Purpose**: Carry the current Manager UI redesign into a fresh session without losing the original plan, the current sidebar state, or the hard constraints the user has clarified.

---

## 1. Original Plan

The intended direction is still:

- rebuild the **UI shell** from the ground up, not just restyle the old wizard
- keep the existing orchestration/backend logic where possible
- make the Manager visually close to the provided mockup
- defer full manifest/runtime contract wiring until the shell design feels right

The product framing remains:

- `NymphsCore Manager` = shared shell, shared runtime/base checks, logs, guide, orchestration
- `Brain`, `WORBI`, `Z-Image Turbo`, `TRELLIS.2` = optional modules / Nymphs
- UI work is currently the priority, not manifest plumbing

### Current module roster to design around

These are the modules/Nymphs that should be treated as the current working set in the UI right now:

- `Z-Image Turbo`
- `Ai Toolkit`
- `TRELLIS.2`
- `Nymphs Brain`
- `WORBI`

This was the earlier working idea in plain terms:

- **new shell, same engine**
- **design first, manifest second**

---

## 2. Current Goal

The immediate focus is still **extensive UI design refinement**, especially the left sidebar and overall shell composition.

The user wants the UI **very close to the mockup**, not just “inspired by it”.

The main pain point at the moment is the **left sidebar**, especially:

- portrait placement
- portrait scale
- portrait layering behind logo/nav/footer
- nav spacing
- avoiding accidental layout breakage while adjusting one thing at a time

---

## 3. Non-Negotiable Sidebar Rules

These are now explicit and should be treated as hard rules unless the user changes them.

### Image handling

- do **not** crop the ladies
- do **not** trim them in-app
- do **not** use per-image logic
- do **not** use special-case composition tricks for one image
- assume the user will prepare **multiple portrait variants with the same canvas format**
- all sidebar portraits must be treated with the **same shared layout rules**

### Placement philosophy

- fixed placement is preferred
- the portrait should be **hard right aligned** in the sidebar
- the portrait should sit **high enough that she faces the nav links**
- the portrait should extend visually behind the footer/open-source area
- hair should be able to go behind the logo area

### Interaction / style

- selected nav item should look like the mockup: tinted/faded, not a heavy rounded button
- hover should show the more visible bounding line, not selected
- the portrait should stay fairly light/ghosted, not dominate the sidebar

---

## 4. Why The Sidebar Became Difficult

The sidebar currently has **three tightly coupled pieces**:

1. the header/logo block
2. the nav block
3. the portrait layer

When one was moved, the others often looked “wrong” even if technically unchanged.

The biggest sources of churn were:

- moving nav spacing while also moving portrait position
- changing portrait layer span/stacking while also adjusting alignment
- trying to fix image composition in code when the user intends to manage image composition in Photoshop

For the next session, keep the approach simple:

- treat the portrait as one fixed background layer
- keep logo block and nav block stable unless the user explicitly asks
- adjust **one variable at a time**

---

## 5. Current Code State

### Main files

- [MainWindow.xaml](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Views/MainWindow.xaml:1)
- [ManagerShellViewModel.cs](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs:1)
- [MANAGER_UI_DIRECTION_HANDOFF.md](/home/nymph/NymphsCore/docs/RautysIdeas/MANAGER_UI_DIRECTION_HANDOFF.md:1)
- [MANAGER_UI_DESIGN_STAGE_HANDOFF.md](/home/nymph/NymphsCore/docs/RautysIdeas/MANAGER_UI_DESIGN_STAGE_HANDOFF.md:1)

### Current sidebar XAML values

At the time of this handoff, the sidebar structure is approximately:

- sidebar root at [MainWindow.xaml:155](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Views/MainWindow.xaml:155)
- logo/header grid at [MainWindow.xaml:166](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Views/MainWindow.xaml:166)
- portrait/background layer at [MainWindow.xaml:187](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Views/MainWindow.xaml:187)
- portrait image at [MainWindow.xaml:194](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Views/MainWindow.xaml:194)
- nav stack at [MainWindow.xaml:217](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/Views/MainWindow.xaml:217)

Current key values:

- logo width: `228`
- `// Core shell` margin: `2,4,0,0`
- portrait width: `294`
- portrait height: `620`
- portrait opacity base: `0.30`
- pulse opacity: `0.18` to `0.36`
- portrait top: `-56`
- portrait right: `0`
- nav stack margin: `10,104,40,0`

Important structural changes already in place:

- portrait layer now spans the full sidebar area instead of only the middle section
- `ClipToBounds` was removed from the portrait canvas
- header/logo block has higher `Panel.ZIndex`
- header grid background is transparent so portrait overflow can exist behind it

### Current image-loading behavior

Portrait loading is controlled in [ManagerShellViewModel.cs:652](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs:652).

Important current behavior:

- the single-image override filename is still `NymphMycelium1.png`
- the app resolves the portrait folder through `ResolveSidebarArtFolder()`
- folder priority was changed so local/source `AppAssets/SidebarPortraits` is preferred before the publish folder

Relevant code:

- [LoadSidebarArtwork()](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs:652)
- [ResolveSidebarArtFolder()](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs:682)

---

## 6. Current Asset Paths

Primary editable portrait folder:

- [SidebarPortraits](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/AppAssets/SidebarPortraits)

Current test portrait:

- [NymphMycelium1.png](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/AppAssets/SidebarPortraits/NymphMycelium1.png:1)

Current logo:

- [NymphsCore_logo.png](/home/nymph/NymphsCore/Manager/apps/NymphsCoreManager/AppAssets/NymphsCore_logo.png:1)

---

## 7. What The User Is Frustrated By

This matters for the next session.

The user is specifically frustrated by:

- one fix breaking another part of the sidebar
- overcomplicated image logic
- any mention of crop/trim/special-case handling
- changing alpha when they asked for placement
- changing size when they asked for position
- changing nav/header spacing when they asked for portrait placement

So the next session should be very disciplined:

- say exactly what one thing is being changed
- change only that one thing
- avoid theory unless it directly explains a bug

---

## 8. What Needs Doing Next

The next session should **not** jump to architecture or manifest work yet.

### Immediate next UI tasks

1. Stabilize the left sidebar without broad restructuring.
2. Fine-tune only:
   - portrait `Canvas.Top`
   - portrait `Canvas.Right`
   - nav stack top margin
3. Confirm the face alignment relative to `Home / Logs / Guide`.
4. Confirm there is no unwanted gap above the footer split.
5. Confirm hair can visually live behind the logo area without destroying spacing.

### What not to do next

- do not reintroduce crop/trim logic
- do not redesign the whole sidebar again
- do not change the right rail or main content unless the user asks
- do not move back into manifest/module-contract work yet

---

## 9. Suggested Next-Step Method

Use this exact sequence next time:

1. Read this handoff.
2. Inspect the current sidebar values in `MainWindow.xaml`.
3. Ask the user for one concrete visual correction if needed, or act on the latest screenshot.
4. Change exactly one of:
   - portrait top
   - portrait right
   - nav top margin
5. Stop and let the user rebuild/check.

Do **not** combine unrelated sidebar changes in one step unless absolutely necessary.

---

## 10. Build / Run Notes

The user is rebuilding from Windows with:

```powershell
powershell -ExecutionPolicy Bypass -File "\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1"
```

Common issue:

- build fails if `NymphsCoreManager.exe` is still running / locked

Typical fix:

```powershell
taskkill /IM NymphsCoreManager.exe /F
```

Then rebuild again.

---

## 11. Summary

The project is still on the right path:

- the shell direction is correct
- the mockup-aligned visual language is correct
- the current blocker is no longer architecture, it is **literal visual fidelity**

---

## 12. Newer Context Since This Handoff

The focus has shifted away from “sidebar-only” refinement.

The current highest-friction area is now the **Home page composition and component styling**, especially:

- top action bar styling
- overview strip usefulness and wording
- runtime monitor behavior
- module card width/height/proportion
- clipping in the right-side mini action button area
- over-round buttons and generic-looking controls

The user is explicitly comparing the current UI against the mockup and sees the current implementation as still too “generic WPF dashboard” instead of a literal match.

This should be treated as a **mockup-clone exercise**, not a loose interpretation exercise.

---

## 13. New Hard Rules Clarified By The User

These are now explicit and should be respected in future passes.

### Home / main content

- the Home page should be **compact**
- the user should **not** need to enlarge the window massively to see the page
- when the window is smaller, wheel scrolling is acceptable
- when the window is at the intended size, the page should feel like **one composed screen**
- the Home page should feel **very close to the mockup**, not just inspired by it

### System overview

- `Partial` is a misleading label in the runtime overview tile
- `Core Services` as vague wording is not good
- `Modules` in the overview is only worth keeping if it earns its place
- the overview is only useful if it behaves like a **real monitor strip**
- the user liked the idea of:
  - `Runtime`
  - `System Checks`
  - `Modules`
- `System Load` is better in the top-right runtime monitor, not duplicated in the center strip

### Runtime monitor

- the **top-right runtime card** is the preferred place for live monitoring
- the runtime card should show useful operational data while installs/builds are running
- good monitor fields include:
  - distro name
  - kernel
  - uptime
  - CPU
  - RAM
  - disk usage
  - last refreshed time
- disk usage is especially important
- if WSL is working, the card should not show `Offline`

### Navigation / page behavior

- `Home` in the left sidebar should always get the user back to the main page
- the user reported being “stuck” on the system checks page
- `System Checks` in the overview should open the actual checks page

### Buttons / controls

- the current buttons still feel too rounded / too generic
- the top action row (`Check for Updates` + gear) should look much more like the mockup
- module action buttons should not feel like bulky rounded 90s-era controls

### Module cards

- cards are still too big for the reduced window size
- cards are still too squashed width-wise
- cards are still clipping, especially on the **right side**
- the user called out that the right-side mini action area is what is clipped
- installed and available module cards should feel much more like the mockup cards:
  - wider
  - cleaner
  - flatter
  - less text-heavy
  - not awkwardly compressed

### Overview strip widths

- the three overview sections should be visually **equal widths**

---

## 14. Current Known Unresolved Problems

At the time of this updated handoff, these are the main unresolved issues that still need to be addressed cleanly:

1. **Top action bar still not mockup-faithful enough**
   - the `Check for Updates` button and gear button are closer than before, but the user still does not feel they match the mockup

2. **Module cards still need a deep pass**
   - they are clipped
   - they are too narrow
   - they are too tall in the wrong way
   - they still do not resemble the mockup card language closely enough

3. **Right-side card action clipping**
   - this was repeatedly misunderstood as bottom clipping
   - the user explicitly clarified the clipping is on the **right side**
   - the mini action area / ellipsis area must fit cleanly without cutoff

4. **Runtime monitor still untrusted by the user**
   - the user reported it showing `Offline` even though WSL should be available
   - the user also reported missing kernel / uptime / monitor info
   - monitor work is present in code, but the user does not yet trust the result

5. **System Checks interaction may still be failing in practice**
   - code has been added for it
   - the user reported that clicking it did not open the page
   - this needs validation

6. **Home-page density still not right**
   - too much space consumed by cards
   - not compact enough for the chosen 25%-smaller window target
   - still reads like a generic dashboard rather than the mockup

---

## 15. Important Current Intent For The Next Session

The next session should assume the following:

- the user is now evaluating the UI mostly on **mockup fidelity**
- “close enough” is not enough
- the right answer is often to be more literal, not more abstract
- if a component looks generic, it probably needs to be rebuilt, not tweaked

In practical terms:

- top action controls should be rebuilt more literally if needed
- overview strip should be kept only if it is genuinely useful
- runtime monitor should be trustworthy
- module cards need a real proportion pass, not tiny incremental nudges

---

## 16. Recommended Next-Step Priorities

If work resumes in a fresh session, do the next steps in this order:

1. Validate / fix the runtime monitor so the top-right card shows real data.
2. Validate / fix `System Checks` page opening and `Home` returning to main page.
3. Do a **deep pass** on installed/available module cards:
   - widen them
   - fix right-side clipping
   - reduce vertical bloat
   - make actions flatter and more integrated
4. Make the three center overview sections equal-width.
5. If still needed, make the top action bar even more literal to the mockup.

Avoid mixing those with new sidebar experimentation unless the user asks.
- modular product framing is correct

The main issue right now is **sidebar stability during fine tuning**, not overall direction.

The next session should continue with:

- small, isolated sidebar placement changes
- no clever image handling
- no architecture detour
- no broad restyling pass
