# WORBI Git Module Hosting Handoff

**Generated**: 2026-05-07  
**Purpose**: Help the WORBI developer host WORBI as a test installable Nymph module for `NymphsCore Manager`.

---

## Goal

WORBI should be hosted online in a way that lets `NymphsCore Manager` discover it as an available module.

The Manager should eventually be able to:

1. Fetch a trusted Nymph registry.
2. See that `WORBI` exists.
3. Read WORBI's `nymph.json`.
4. Show WORBI as `Available` in the Manager.
5. Install/start/open/stop WORBI through stable wrapper scripts.

This test does **not** require the full Manager registry system to be finished yet. The goal is to prepare WORBI's repo in the right shape now.

---

## Recommended GitHub Setup

### Plain-English Explanation

There are two different things:

```text
1. WORBI repo
2. Nymphs registry repo
```

The **WORBI repo** is where the actual WORBI module lives.

It contains:

- WORBI package files
- install/start/stop/status scripts
- `nymph.json`, which describes WORBI to the Manager

The **Nymphs registry repo** is just a catalog.

It does **not** contain WORBI itself.

It only tells the Manager:

```text
WORBI is an official/trusted module.
You can find WORBI's module description over here:
github.com/nymphnerds/worbi/nymph.json
```

Think of it like this:

```text
nymphs-registry = the shop catalog
worbi repo       = the actual product on the shelf
```

The Manager should read the catalog first, then follow the catalog link to the WORBI repo.

This is better than making the Manager search all of GitHub or the internet randomly.

It also means that when a new backend/module is released later, `NymphsCore` only needs to update the catalog, and users will see the new module as available in the Manager.

Best long-term shape:

```text
github.com/nymphnerds/worbi
github.com/nymphnerds/nymphs-registry
```

Use one clean repo per Nymph module. For WORBI, that repo should simply be called `worbi`.

The Manager should not need to know WORBI's private/internal project layout. It only needs the WORBI repo's `nymph.json` and stable wrapper scripts.

If using a shared Git account for a quick test, that is okay temporarily, but a GitHub organization is cleaner:

- no shared password
- friend can be added as maintainer
- repos can be transferred later
- Manager can trust a stable organization namespace

---

## WORBI Repo Shape

Recommended minimal repo layout:

```text
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
    worbi-6.2.18.tar.gz
```

For the first test, WORBI can still be archive-based. The important part is that the Manager-facing contract is stable.

---

## Minimal `nymph.json`

Place this at the root of the WORBI repo:

```json
{
  "manifest_version": 1,
  "id": "worbi",
  "name": "WORBI",
  "kind": "archive",
  "category": "tool",
  "version": "6.2.18",
  "description": "Local worldbuilding app managed by NymphsCore.",
  "source": {
    "archive": "packages/worbi-6.2.18.tar.gz",
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

## Script Contract

The Manager should call the wrapper scripts, not internal app scripts directly.

Each script should be safe to run from a fresh shell.

### `scripts/install_worbi.sh`

Expected behavior:

- install or repair WORBI into `~/worbi`
- extract/copy the packaged app
- install required dependencies
- create/update helper launch scripts if needed
- exit `0` on success
- exit non-zero on failure
- print useful progress lines

### `scripts/worbi_status.sh`

Expected behavior:

- print machine-readable-ish status lines
- check install folder
- check backend PID and/or health endpoint
- check frontend PID and/or HTTP response
- exit `0` even if WORBI is not installed, unless the status script itself fails

Good output shape:

```text
installed=true
backend=running
frontend=running
frontend_url=http://localhost:5173
backend_url=http://localhost:8082
detail=WORBI is running.
```

### `scripts/worbi_start.sh`

Expected behavior:

- start backend and frontend
- write/update PID files
- print URLs
- avoid starting duplicate copies when already running

### `scripts/worbi_stop.sh`

Expected behavior:

- stop frontend and backend
- clean stale PID files
- succeed if already stopped

### `scripts/worbi_open.sh`

Expected behavior:

- print or launch the frontend URL
- Manager may later open the URL itself on Windows

### `scripts/worbi_logs.sh`

Expected behavior:

- print log paths or tail recent log lines
- do not require the app to be running

---

## WORBI Manager Page Notes

Each Nymph can have a completely different Manager page.

WORBI does **not** need to fit the Brain, Z-Image, AI Toolkit, or TRELLIS page shape.

For the first version, WORBI should be treated as a simple **launcher/status module**.

The Manager does not need deep WORBI controls yet. WORBI has its own internal app UI for worldbuilding, files, graph, reminders, chat, image generation, and settings.

The Manager v1 page only needs to:

- show whether WORBI is installed
- show whether WORBI is running
- start WORBI
- open WORBI
- stop WORBI if needed
- show logs when something fails

So WORBI should give the Manager enough information to build a useful launcher/status page with:

- install state
- backend running/stopped state
- frontend running/stopped state
- frontend URL
- backend URL
- backend health result
- WORBI version
- log folder path
- simple actions:
  - install/repair
  - start
  - stop
  - open app
  - view logs

The `worbi_status.sh` script is the most important part for this page. It should print stable fields the Manager can parse.

Recommended richer status output:

```text
installed=true
version=6.2.18
running=true
backend=running
frontend=running
url=http://localhost:5173
frontend_url=http://localhost:5173
backend_url=http://localhost:8082
health=ok
install_root=/home/nymph/worbi
logs_dir=/home/nymph/worbi/logs
detail=WORBI is running.
```

If a field does not exist yet, print an empty or `unknown` value rather than changing the shape every release.

Example:

```text
health=unknown
```

---

## Registry Repo Shape

The Manager should not search the whole internet. It should fetch a trusted registry.

Suggested registry repo:

```text
nymphs-registry/
  nymphs.json
```

Example `nymphs.json`:

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

Later, this same registry can list:

```text
Brain
Z-Image Turbo
TRELLIS.2
AI Toolkit
WORBI
future backends
```

---

## Versioning Recommendation

For the first test:

- keep `version` in `nymph.json`
- update the archive filename when the package changes
- tag releases in Git when possible

Example tags:

```text
v6.2.18
v6.2.19
```

Later, the registry can support channels:

```text
stable
test
latest
```

---

## What Not To Do

Avoid these in the first test:

- do not make the Manager inspect WORBI internals deeply
- do not require Brain/Z-Image integration for WORBI to install
- do not try to design deep Manager controls before the workflow is clear
- do not put live installed/running state inside `nymph.json`
- do not rely on random internet search
- do not hardcode personal machine paths beyond the current managed Linux user assumption

The repo should define what WORBI is. The Manager should detect live state separately.

---

## First Test Checklist

Before wiring into Manager, verify from a clean-ish WSL shell:

```bash
bash scripts/install_worbi.sh
bash scripts/worbi_status.sh
bash scripts/worbi_start.sh
bash scripts/worbi_status.sh
bash scripts/worbi_stop.sh
bash scripts/worbi_status.sh
```

Expected result:

- install succeeds
- status can detect installed/stopped/running states
- start exposes frontend on `5173`
- backend health responds on `8082`
- stop cleans up processes

---

## Short Version

Host WORBI like this:

```text
WORBI repo:
  nymph.json
  scripts/*
  packages/worbi-version.tar.gz

Registry repo:
  nymphs.json -> points to WORBI nymph.json
```

Then `NymphsCore Manager` can eventually fetch the registry, show WORBI as available, and use it as a clean launcher/status module.
