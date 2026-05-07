# WORBI Git Module Packaging Task

**Created**: 2026-05-07  
**Status**: Complete
**Purpose**: Package WORBI as a NymphsCore Manager installable module hosted on GitHub

---

## 1. Objective

Package WORBI so that `NymphsCore Manager` can discover, install, and manage it as an optional module. The package will be distributed as a `tar.gz` archive hosted in a dedicated GitHub repo, registered in the Nymphs registry.

---

## 2. Repos Involved

| Repo | URL | Branch | Role |
|------|-----|--------|------|
| WORBI Module | `github.com/nymphnerds/worbi` | `main` | Contains nymph.json, wrapper scripts, and production archive |
| Nymphs Registry | `github.com/nymphnerds/nymphs-registry` | `main` | Contains nymphs.json catalog pointing to module manifests |

---

## 3. Design References

- **Package structure**: `NymphsCore/docs/RautysIdeas/WORBI_GIT_MODULE_HOSTING_HANDOFF.md`
- **Production server approach**: `NymphsCore/docs/RautysIdeas/WORBI_MANAGER_HANDOFF.md` (Section 7.1 — production runtime, not dev servers)
- **Current installer**: `/home/nymph/Nymphs-Brain/WORBI` (source project)

---

## 4. Key Constraints

- **No source code in archive** — only production-built files
- **Production server startup** — no `npm run dev`, use built frontend + production Express
- **Authorization gate** — stop and wait for user approval before any `git push`
- **main branch only** — both repos operate on `main`
- **Install root**: `~/worbi`
- **Backend port**: `8082`
- **Frontend port**: `5173`

---

## 5. Phases

### Phase 1: Gather Information

- [ ] Determine current WORBI version from source project
- [ ] Clone `github.com/nymphnerds/worbi` repo
- [ ] Clone `github.com/nymphnerds/nymphs-registry` repo
- [ ] Inspect both repo structures

### Phase 2: Build Production Package

- [ ] Build WORBI client for production (`npm run build` → `dist/`)
- [ ] Prepare server for production (Express serving static files)
- [ ] Create archive (`worbi-{version}.tar.gz`) containing only:
  - Pre-built client `dist/` output
  - Server production files (routes, config, entry point)
  - Required `node_modules` (production dependencies only)
  - Server `package.json`
  - Start/stop helper scripts
  - **Excluded**: source TypeScript files, test files, dev dependencies, `.git`, build configs

### Phase 3: Prepare WORBI Module Repo

- [ ] Create `nymph.json` at repo root (spec from GIT_MODULE_HOSTING_HANDOFF)
- [ ] Create wrapper scripts in `scripts/`:
  - `install_worbi.sh` — extract archive to `~/worbi`, install production deps, set up launch scripts
  - `worbi_status.sh` — output parseable status fields (installed, version, running, backend, frontend, health, URLs, paths)
  - `worbi_start.sh` — start production backend + frontend
  - `worbi_stop.sh` — stop both processes, clean PID files
  - `worbi_open.sh` — print/open frontend URL
  - `worbi_logs.sh` — print log paths or tail logs
- [ ] Place archive in `packages/` directory
- [ ] Add `README.md`
- [ ] Verify repo structure matches handoff spec:
  ```
  worbi/
    nymph.json
    README.md
    scripts/
      install_worbi.sh
      worbi_status.sh
      worbi_start.sh
      worbi_stop.sh
      worbi_open.sh
      worbi_logs.sh
    packages/
      worbi-{version}.tar.gz
  ```
- [ ] **Authorization checkpoint** — present plan to user, wait for push approval
- [ ] Push WORBI module repo to GitHub

### Phase 4: Prepare Registry Repo

- [ ] Create `nymphs.json` at repo root:
  ```json
  {
    "registry_version": 1,
    "updated": "2026-05-07",
    "modules": [
      {
        "id": "worbi",
        "name": "WORBI",
        "channel": "test",
        "trusted": true,
        "manifest_url": "https://raw.githubusercontent.com/nymphnerds/worbi/main/nymph.json"
      }
    ]
  }
  ```
- [ ] **Authorization checkpoint** — present plan to user, wait for push approval
- [ ] Push registry repo to GitHub

### Phase 5: Verify

- [ ] Confirm `manifest_url` resolves: `https://raw.githubusercontent.com/nymphnerds/worbi/main/nymph.json`
- [ ] Confirm registry is accessible: `https://raw.githubusercontent.com/nymphnerds/nymphs-registry/main/nymphs.json`

---

## 6. nymph.json Specification

```json
{
  "manifest_version": 1,
  "id": "worbi",
  "name": "WORBI",
  "kind": "archive",
  "category": "tool",
  "version": "TBD",
  "description": "Local worldbuilding app managed by NymphsCore.",
  "source": {
    "archive": "packages/worbi-TBD.tar.gz",
    "format": "tar.gz"
  },
  "entrypoints": {
    "install": "scripts/install_worbi.sh",
    "status": "scripts/worbi_status.sh",
    "start": "scripts/worbi_start.sh",
    "stop": "scripts/worbi_stop.sh",
    "open": "scripts/worbi_open.sh",
    "logs": "scripts/worbi_logs.sh"
  },
  "capabilities": [
    "install",
    "status",
    "start",
    "stop",
    "open",
    "logs"
  ],
  "dependencies": [],
  "ui": {
    "show_tab_when_installed": true,
    "tab_label": "WORBI",
    "page_kind": "worbi",
    "install_label": "Install WORBI",
    "sort_order": 50
  },
  "runtime": {
    "install_root": "~/worbi",
    "logs_dir": "~/worbi/logs",
    "frontend_url": "http://localhost:5173",
    "backend_url": "http://localhost:8082"
  },
  "update_policy": {
    "channel": "pinned"
  }
}
```

---

## 7. Script Contract

Scripts are called by NymphsCore Manager, not users directly. Each script must be safe to run from a fresh shell.

### install_worbi.sh
- Extract archive to `~/worbi`
- Install production dependencies (`npm install --production`)
- Create/update launch helpers
- Exit 0 on success, non-zero on failure
- Print useful progress lines

### worbi_status.sh
Output machine-readable fields:
```
installed=true|false
version={version}
running=true|false
backend=running|stopped
frontend=running|stopped
url=http://localhost:5173
frontend_url=http://localhost:5173
backend_url=http://localhost:8082
health=ok|failed|unknown
install_root=/home/nymph/worbi
logs_dir=/home/nymph/worbi/logs
detail=WORBI is running.
```

### worbi_start.sh
- Start production backend (Express on 8082)
- Serve pre-built frontend (static files on 5173)
- Write PID files to `~/worbi/logs/`
- Print URLs
- Avoid duplicate starts

### worbi_stop.sh
- Stop frontend and backend via PID files
- Clean stale PID files
- Succeed if already stopped

### worbi_open.sh
- Print or launch frontend URL

### worbi_logs.sh
- Print log paths
- Optionally tail recent lines

---

## 8. Production Build Approach

Per WORBI_MANAGER_HANDOFF.md Section 7.1:

1. **Frontend**: Run `npm run build` in client directory to produce `dist/` static files
2. **Backend**: Express server serves static `dist/` files and handles API routes
3. **No dev servers**: The archive does not include Vite dev server or TypeScript source
4. **Runtime**: Backend starts with `node` directly (not `npm run dev`), serving both API and static frontend

---

## 9. Authorization Protocol

**Before any `git push`:**
1. Stop execution
2. Present what will be pushed (branch, files, commit message)
3. Wait for explicit user approval
4. Only proceed after user says "yes" or "authorized"

---

## 10. Known Gaps & Future Work

- Registry repo may grow as more modules are added (Brain, Z-Image, etc.)
- Channel system (`stable`, `test`, `latest`) not implemented yet — using `test` for first entry
- Upgrade/backup logic in install script needs refinement (per Manager Handoff 7.3)
- Hardcoded paths should be env-driven where possible (per Manager Handoff 7.4)

---

## 11. Checklist

- [x] Phase 1: Gather Information
- [x] Phase 2: Build Production Package
- [x] Phase 3: Prepare WORBI Module Repo
- [x] Phase 4: Prepare Registry Repo
- [x] Phase 5: Verify

---

## 12. Files Changed

- `worbi/nymph.json` — created manifest (v6.2.49, port 8082)
- `worbi/scripts/install_worbi.sh` — installer script
- `worbi/scripts/worbi_status.sh` — status script
- `worbi/scripts/worbi_start.sh` — start script
- `worbi/scripts/worbi_stop.sh` — stop script
- `worbi/scripts/worbi_open.sh` — open script
- `worbi/scripts/worbi_logs.sh` — logs script
- `worbi/scripts/worbi_uninstall.sh` — uninstall script
- `worbi/packages/worbi-6.2.49.tar.gz` — production archive
- `nymphs-registry/nymphs.json` — registry catalog (WORBI + Z-Image + TRELLIS + Brain + LoRA)
