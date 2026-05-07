# WORBI Manager Integration — Technical Handoff

**Generated**: 2026-05-05  
**Branch Context**: `rauty`  
**Scope**: WORBI module review, Manager integration recommendations, and improvement notes from inspection of the packaged installer and bundled app.

---

## 1. Executive Summary

WORBI is already substantial enough to be treated as a real optional product module inside `NymphsCore`, not just a loose helper script. It currently ships as a self-contained Linux installer bundle under `NymphsCore/WORBI-installer/`, and the packaged archive contains a full local-first web application:

- React + TypeScript frontend on port `5173`
- Express backend on port `8082`
- local workspace, auth, file, graph, timeline, reminder, image, and LLM-related routes
- helper commands:
  - `worbi-start`
  - `worbi-stop`
  - `worbi-status`

From a Manager-integration point of view, WORBI fits the same broad category as `Nymphs-Brain` and `Z-Image Trainer`: an optional, self-contained tool that should be installable and operable without disturbing the core Blender/runtime stack.

My recommendation is:

- integrate WORBI into the Manager as an **optional module**
- give it its own **dedicated Manager page**
- keep it **self-contained in v1**
- do **not** tightly couple it to Brain, Z-Image, or runtime selection logic yet

That said, WORBI in its current packaged form still behaves more like a **tool module** than a polished installed daemon/service. The biggest reason is that it is started with development servers rather than a production build/service model.

---

## 2. Where WORBI Lives Right Now

Current branch contents:

- `NymphsCore/WORBI-installer/README.md`
- `NymphsCore/WORBI-installer/install.sh`
- `NymphsCore/WORBI-installer/worbi-6.2.18.tar.gz`

The installer:

- installs to `~/worbi`
- copies launcher scripts to `~/.local/bin`
- preserves some user data from previous installs
- installs Node locally into `~/.local` if missing
- extracts a bundled application archive rather than cloning a repo live

This is a good starting shape for Manager integration because it is:

- self-contained
- versioned
- deterministic
- not dependent on a separate live repo checkout

---

## 3. What The Bundled WORBI App Actually Contains

The archive contains a full application with:

- `worbi/client/`
- `worbi/server/`
- `worbi/bin/`
- workspace-level `package.json`

### Frontend

The frontend is a Vite React app with a large feature surface:

- document editing
- file explorer
- bookmarks
- tags
- graph views
- reminders
- timeline
- image generation panels
- chat / LLM helpers
- export helpers
- template-driven content generation

### Backend

The Express backend exposes:

- `/api/health`
- auth routes
- files routes
- images routes
- timeline routes
- graph routes
- LLM routes
- reminders
- settings

### Runtime model

The bundled scripts run:

- backend on `8082`
- frontend on `5173`

and manage them via PID files in:

- `~/worbi/logs/worbi-server.pid`
- `~/worbi/logs/worbi-client.pid`

Logs go to:

- `~/worbi/logs/worbi-server.log`
- `~/worbi/logs/worbi-client.log`

---

## 4. Why WORBI Fits The Manager

The current Manager already has the right high-level shape for this:

- optional-module install flow
- per-module install flags in `InstallSettings`
- dedicated service methods in `InstallerWorkflowService`
- dedicated tool pages in `MainWindowViewModel` / `MainWindow.xaml`
- existing examples:
  - `Nymphs-Brain`
  - `Z-Image Trainer`

This means WORBI can be integrated without inventing a new architectural pattern.

The cleanest fit is:

1. Add WORBI as a new optional module in the install/repair flow.
2. Add a dedicated sidebar destination such as `WORBI`.
3. Back the page with Manager-owned wrapper scripts in `Manager/scripts/`.
4. Keep WORBI’s internal application logic outside the Manager and treat the Manager as the install/control surface only.

---

## 5. Recommended Manager Integration Shape

### v1 integration target

For the first integration pass, the Manager should provide:

- `Install WORBI`
- `Repair/Reinstall WORBI`
- `Start WORBI`
- `Stop WORBI`
- `Refresh WORBI Status`
- `Open WORBI Frontend`
- `Open WORBI Backend`
- `Open WORBI Logs`

### Suggested install location

Keep the current default:

- `~/worbi`

That matches the current installer and avoids changing the bundle unnecessarily during first integration.

### Suggested Manager-owned scripts

Instead of having the C# layer call the raw packaged scripts directly, add wrapper scripts such as:

- `Manager/scripts/install_worbi.sh`
- `Manager/scripts/worbi_start.sh`
- `Manager/scripts/worbi_stop.sh`
- `Manager/scripts/worbi_status.sh`

Why this helps:

- keeps Manager logic consistent with Brain and Z-Image
- gives one stable interface even if the internal WORBI bundle changes later
- makes future migration from dev-server launch to production service easier
- gives a clean place for Manager-specific logging, validation, and path setup

### Suggested Manager UI summary

The WORBI page should show:

- install state
- backend state
- frontend state
- URLs
- install path
- log paths
- a short explanation that WORBI is a separate local worldbuilding app

---

## 6. Strong Recommendation: Treat Current WORBI As Beta-Style Module

This is the most important recommendation from inspection:

### WORBI is currently started with dev servers, not a production build/service

The bundled `worbi-start` script launches:

- `npm run dev` in the server
- `npm run dev` in the client

That is workable for internal/testing use, but it has implications:

- dev servers are noisier
- boot behavior is less polished
- startup time may vary more
- they are not the same as a stable background service model
- process supervision is lighter and more brittle than a real installed service

So the right framing inside the Manager today is:

- optional local tool module
- usable and worth integrating
- not yet the final “finished appliance” form

I would explicitly avoid overselling it in UI copy until it has a more production-like runtime path.

---

## 7. Improvement Areas Worth Calling Out

Below are the main improvement points that stood out during inspection.

### 7.1 Dev-server runtime should eventually become a production runtime

Current state:

- frontend launches through Vite dev server
- backend launches through Node dev/watch mode

Recommended future direction:

- build frontend once
- serve static build output or launch a production preview/service
- run backend with a normal production start path
- preferably move to a Manager-friendly supervised start/stop model

This is the single biggest polish upgrade available.

### 7.2 `npm install ... || true` can mask real install failures

In `install.sh`, dependency install commands are currently tolerant of failure:

- server `npm install --loglevel=error || true`
- client `npm install --loglevel=error || true`

That is risky because it can produce a “successful” install that is actually broken.

Recommendation:

- fail loudly on dependency install failure
- only suppress errors intentionally and visibly
- add clearer verification if lenient behavior is kept

### 7.3 Upgrade/backup logic wants a cleanup pass

The installer backs up existing installs and restores selected user data. The intent is good, but the logic looks fragile enough that I would want a deliberate pass before calling upgrades robust.

Recommendation:

- define exactly which user data is canonical
- back up only those paths
- restore only those paths
- avoid wildcard-style backup selection where possible
- make restore behavior deterministic and obvious in logs

### 7.4 Hardcoded user/path assumptions should be softened

Some bundled config assumes:

- `/home/nymph/...`
- `Nymphs-Brain` paths
- specific local binary locations

That is acceptable for internal use on the shared project setup, but it reduces portability.

Recommendation:

- push more paths into env/config
- let Manager supply known paths where appropriate
- avoid baking personal home-path assumptions deeper into WORBI than necessary

### 7.5 Security defaults are fine for private local use, but not “production-safe”

Observed characteristics include:

- open CORS policy
- default JWT secret fallback
- local/private-system assumptions

That is not automatically wrong for a private local app, but it should be described honestly.

Recommendation:

- keep the current relaxed local-default posture if this is explicitly a local-only tool
- avoid describing it as hardened or production-safe
- later add stronger env-driven secrets/config if broader distribution is planned

### 7.6 Status/health integration can be richer than PID-only checks

WORBI already has:

- PID files
- a backend health endpoint at `/api/health`

The Manager should use both process and health checks where possible instead of trusting only PID files.

Recommendation:

- backend status should use HTTP health when available
- frontend status can use HTTP probe plus PID
- status text should clearly differentiate:
  - installed
  - process launched
  - health responding
  - port occupied but unhealthy

### 7.7 Separate WORBI lifecycle from Brain lifecycle in v1

WORBI has LLM-provider and managed-lifecycle ideas inside it, including assumptions around local LLM and Z-Image services.

That is interesting, but I do **not** recommend deep integration in the first Manager pass.

Recommendation:

- let WORBI manage its own internal app settings first
- expose only WORBI install/start/stop/status in Manager v1
- discuss Brain/Z-Image/shared-provider integration only after the module is stable

This keeps the first integration much safer.

### 7.8 The Manager should not depend directly on the tarball’s internal structure forever

Right now the bundle contains everything, which is convenient. But if the Manager starts depending directly on archive internals, upgrades become brittle.

Recommendation:

- treat the tarball as packaged payload
- place Manager-facing contracts in wrapper scripts
- keep C# ignorant of bundle internals beyond install root, ports, and commands

---

## 8. Suggested Phase Plan

### Phase 1 — Safe integration

Goal:

- get WORBI visible and manageable in the Manager without redesigning WORBI itself

Do:

- add `InstallWorbi` setting
- add WORBI to optional modules
- add WORBI page to sidebar
- add install/start/stop/status/open actions
- add wrapper scripts
- show clear beta-style wording

Do not do yet:

- shared model orchestration with Brain
- deep coupling with Z-Image
- rewrite WORBI runtime model

### Phase 2 — Runtime hardening

Goal:

- reduce fragility and make WORBI feel like a more polished installed module

Do:

- replace dev-server launch with production-oriented launch path
- tighten install verification
- improve backup/restore logic
- improve logging and diagnostics

### Phase 3 — Cross-module intelligence

Goal:

- discuss shared lifecycle with Brain / Z-Image if still desirable

Do only if it still makes sense after Phase 2:

- shared provider defaults
- optional handoff between WORBI and Brain-managed LLM
- optional image/LLM workflow bridges

---

## 9. Recommended UI Positioning Inside The Manager

If WORBI is surfaced in the Manager now, the wording should stay grounded:

Suggested framing:

- “Optional local worldbuilding app”
- “Separate from the Blender runtime”
- “Installs and runs in the managed Linux environment”
- “Currently uses a development-style runtime path”

Avoid implying:

- it is already a polished system service
- it is required for Blender or core backend use
- it shares full lifecycle with Brain today

---

## 10. Final Recommendation

WORBI is worth integrating into the Manager now.

The right move is **not** to wait for it to be perfect, but also **not** to pretend it is already in final polished form.

Best next step:

- integrate WORBI as a self-contained optional Manager module on `rauty`
- keep the implementation modest and wrapper-driven
- present it honestly as usable but still maturing

If done this way, the Manager gains a meaningful new module without taking on unnecessary coupling risk, and WORBI gets a proper home in the product without forcing premature architectural commitments.
