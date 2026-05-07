# Nymph Manifest Draft

**Generated**: 2026-05-05  
**Branch Context**: `rauty`  
**Purpose**: Draft a small manifest format for installable `Nymphs` managed by `NymphsCore`.

---

## 1. Design Goals

The manifest should be:

- small
- human-editable
- stable
- generic across very different Nymph types
- focused on what the Manager needs to know

The manifest should **not** try to describe every internal detail of a Nymph. It should just give `NymphsCore` enough information to:

- list the Nymph
- describe it
- know where it comes from
- know how it is packaged
- know what actions it supports
- know what dependencies it has

---

## 2. Recommended File Name

Use:

- `nymph.json`

One `nymph.json` per Nymph.

---

## 3. Minimal Shape

```json
{
  "manifest_version": 1,
  "id": "worbi",
  "name": "WORBI",
  "kind": "archive",
  "description": "Local worldbuilding app managed by NymphsCore.",
  "source": {},
  "entrypoints": {},
  "capabilities": [],
  "dependencies": []
}
```

---

## 4. Core Fields

### Required

```json
{
  "manifest_version": 1,
  "id": "worbi",
  "name": "WORBI",
  "kind": "archive",
  "description": "Local worldbuilding app managed by NymphsCore.",
  "source": {},
  "entrypoints": {}
}
```

Field meanings:

- `manifest_version`
  - version of the manifest format itself
- `id`
  - stable machine id
  - lowercase, short, no spaces
- `name`
  - display name in Manager UI
- `kind`
  - packaging/source type
  - recommended values:
    - `script`
    - `repo`
    - `archive`
    - `hybrid`
- `description`
  - short user-facing summary
- `source`
  - where the Nymph comes from
- `entrypoints`
  - how the Manager interacts with it

### Optional

```json
{
  "category": "tool",
  "version": "6.2.18",
  "install_root": "~/worbi",
  "capabilities": ["install", "status", "start", "stop", "open", "logs"],
  "dependencies": [],
  "ui": {},
  "runtime": {},
  "update_policy": {}
}
```

---

## 5. Recommended Enumerations

### `kind`

```json
"kind": "repo"
```

Recommended values:

- `script`
- `repo`
- `archive`
- `hybrid`

### `category`

Not essential, but useful for Manager grouping.

Recommended values:

- `runtime`
- `tool`
- `frontend`
- `bridge`
- `service`

---

## 6. Source Block

The `source` block should vary by `kind`.

### Repo-based

```json
"source": {
  "repo": "git@github.com:nymphnerds/Nymphs-Brain.git",
  "ref": "main"
}
```

Optional richer form:

```json
"source": {
  "repo": "git@github.com:nymphnerds/Nymphs-Brain.git",
  "ref": "main",
  "pin": null
}
```

Where:

- `repo` = git remote/url
- `ref` = branch or tag default
- `pin` = optional exact commit/tag if you want stricter installs

### Archive-based

```json
"source": {
  "archive": "WORBI-installer/worbi-6.2.18.tar.gz"
}
```

Optional richer form:

```json
"source": {
  "archive": "WORBI-installer/worbi-6.2.18.tar.gz",
  "format": "tar.gz"
}
```

### Script-only

```json
"source": {
  "path": "Manager/scripts/some_helper"
}
```

### Hybrid

```json
"source": {
  "repo": "git@github.com:nymphnerds/example.git",
  "ref": "main",
  "archive": "payloads/example-runtime.tar.gz"
}
```

---

## 7. Entrypoints Block

This is the most important part.

It tells the Manager how to interact with the Nymph.

Recommended shape:

```json
"entrypoints": {
  "install": "Manager/scripts/install_worbi.sh",
  "update": "Manager/scripts/update_worbi.sh",
  "status": "Manager/scripts/worbi_status.sh",
  "start": "Manager/scripts/worbi_start.sh",
  "stop": "Manager/scripts/worbi_stop.sh",
  "remove": "Manager/scripts/remove_worbi.sh"
}
```

Important rule:

- these should usually point to **Manager wrapper scripts**
- not directly to random internal files deep inside the payload

That keeps the Manager contract stable even if the Nymph changes internally.

Optional actions:

- `open`
- `logs`
- `configure`

Example:

```json
"entrypoints": {
  "install": "Manager/scripts/install_worbi.sh",
  "status": "Manager/scripts/worbi_status.sh",
  "start": "Manager/scripts/worbi_start.sh",
  "stop": "Manager/scripts/worbi_stop.sh",
  "open": "Manager/scripts/open_worbi.sh",
  "logs": "Manager/scripts/open_worbi_logs.sh"
}
```

---

## 8. Capabilities

Use `capabilities` for what the Manager can expose in UI.

```json
"capabilities": [
  "install",
  "update",
  "status",
  "start",
  "stop",
  "open",
  "logs"
]
```

This helps the UI stay generic.

---

## 9. Lifecycle Script Contract

Every Nymph module must treat its entrypoint scripts as a stable contract with the Manager.

This is especially important for modules that start local web apps, background servers, model workers, queues, or long-running processes.

### General Rules

- Scripts must be safe to run from a fresh shell.
- Scripts must not depend on the current working directory.
- Scripts must use absolute paths or paths derived from `$HOME` / manifest runtime fields.
- Scripts must exit `0` for expected states like "already running" or "already stopped".
- Scripts must exit non-zero only for real errors.
- Scripts must print useful progress and error lines.
- Scripts must avoid hiding errors with broad `|| true` unless the state is explicitly harmless.

### `install`

The install script should:

- install or repair the module into its normal install root
- create/update the Manager-facing wrapper scripts if the module uses installed wrappers
- preserve user data on update unless the user explicitly purges
- write or refresh an installed manifest/version marker
- print the installed version and install path
- exit non-zero if the package, dependencies, or wrapper setup failed

For archive modules, the module wrapper release and the bundled app package release may be different.
For example, a module can ship a `6.2.52` wrapper fix while still installing a `6.2.51` app archive.
In that case the installer must write an installed module marker, such as:

```text
~/module-name/.nymph-module-version
```

The status script should prefer that marker for `version=...`, so Manager update checks compare the Nymph module release.
Without this, a wrapper-only update can install successfully but still appear outdated because the app package version did not change.

### Module Repo Update Process

For a module-owned fix, do not patch the Manager unless the Manager contract itself is missing something.

Use this flow:

1. Patch the module repo scripts or package.
2. Bump `nymph.json.version`.
3. If only scripts changed, keep the existing archive and make sure `install` refreshes installed wrapper scripts.
4. If app files changed, build a new archive and update `source.archive`.
5. Push the module repo.
6. In Manager, run `Check for Updates`.
7. Use `Update Module` to rerun the module install flow inside the real managed `NymphsCore` WSL distro.

This keeps community modules independent: a new WORBI wrapper fix, LoRA launcher fix, or future Nymph backend fix should normally ship from its own repo.

### `status`

The status script should be truthful even when PID files are stale or missing.

It should check both:

- PID/process tracking
- health endpoint or equivalent runtime probe, when the module has one

If the health endpoint is alive but the PID file is missing, do **not** report simply "stopped".

Use a clear state such as:

```text
running=true
backend=running-unmanaged
health=ok
detail=Module is responding but PID tracking is missing.
```

Recommended fields:

```text
installed=true
version=1.2.3
running=true
backend=running
frontend=running
url=http://localhost:8082
frontend_url=http://localhost:8082
backend_url=http://localhost:8082
health=ok
install_root=/home/nymph/example
logs_dir=/home/nymph/example/logs
detail=Module is running.
```

Status should exit `0` even for installed/stopped/not-installed states unless the status script itself failed.

### `start`

The start script should:

- avoid duplicate starts if the module is already healthy
- remove stale PID files before starting
- start the real service from the correct working directory
- detach background processes in a way that survives `wsl.exe` returning
- write the correct service PID to the PID file
- wait briefly for health/readiness
- print the URL the Manager should open
- exit non-zero if the service exits immediately or health cannot be reached after the allowed startup window

For WSL-launched web servers, plain `nohup ... &` may not be enough. Test that the process is still alive after the `wsl.exe -d NymphsCore -- ...` command returns.

### `stop`

The stop script should:

- stop the process recorded in the PID file
- remove stale PID files
- succeed if already stopped
- if the PID file is missing but the health endpoint is alive, find and stop the matching module process
- verify the health endpoint is no longer reachable before reporting stopped, when a health endpoint exists

This prevents the Manager from saying "stopped" while `open` still works because an orphan server is alive.

### `open`

The open script should:

- print the canonical URL
- not pretend the module is running
- optionally return a clear warning if the health endpoint is not reachable

The Manager may open the URL itself after parsing the script output.

### `logs`

The logs script should:

- print recent module logs or log paths
- work even if the module is stopped
- not fail only because one optional log file is missing

---

## 10. Dependencies

Use simple ids:

```json
"dependencies": ["brain"]
```

Keep it simple in v1.

This can later support:

- hard dependencies
- optional dependencies
- version constraints

But for now, a flat array of Nymph ids is enough.

---

## 11. UI Block

Useful for Manager presentation, but keep it lightweight.

```json
"ui": {
  "show_tab_when_installed": true,
  "tab_label": "WORBI",
  "install_label": "Install WORBI"
}
```

This lets the Manager know:

- whether to create a tab/page
- what label to use

---

## 12. Runtime Block

Optional, but useful for status screens and open actions.

Example:

```json
"runtime": {
  "install_root": "~/worbi",
  "logs_dir": "~/worbi/logs",
  "frontend_url": "http://localhost:5173",
  "backend_url": "http://localhost:8082"
}
```

This is especially useful for:

- `WORBI`
- `Brain`
- anything with ports, logs, or web UIs

---

## 13. Update Policy Block

Optional, but helpful if you want release-tested vs latest behavior.

```json
"update_policy": {
  "channel": "pinned"
}
```

Recommended values:

- `pinned`
- `release-tested`
- `latest`

This does not need to be complex in v1.

---

## 14. Example: WORBI

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
    "archive": "WORBI-installer/worbi-6.2.18.tar.gz",
    "format": "tar.gz"
  },
  "entrypoints": {
    "install": "Manager/scripts/install_worbi.sh",
    "status": "Manager/scripts/worbi_status.sh",
    "start": "Manager/scripts/worbi_start.sh",
    "stop": "Manager/scripts/worbi_stop.sh",
    "open": "Manager/scripts/open_worbi.sh",
    "logs": "Manager/scripts/open_worbi_logs.sh"
  },
  "capabilities": ["install", "status", "start", "stop", "open", "logs"],
  "dependencies": [],
  "ui": {
    "show_tab_when_installed": true,
    "tab_label": "WORBI",
    "install_label": "Install WORBI"
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

## 15. Example: Brain

```json
{
  "manifest_version": 1,
  "id": "brain",
  "name": "Brain",
  "kind": "repo",
  "category": "runtime",
  "description": "Local coding and MCP runtime managed by NymphsCore.",
  "source": {
    "repo": "git@github.com:nymphnerds/Nymphs-Brain.git",
    "ref": "main"
  },
  "entrypoints": {
    "install": "Manager/scripts/install_nymphs_brain.sh",
    "status": "Manager/scripts/brain_status.sh",
    "start": "Manager/scripts/brain_start.sh",
    "stop": "Manager/scripts/brain_stop.sh",
    "open": "Manager/scripts/brain_open_webui.sh",
    "logs": "Manager/scripts/brain_open_logs.sh"
  },
  "capabilities": ["install", "status", "start", "stop", "open", "logs"],
  "dependencies": [],
  "ui": {
    "show_tab_when_installed": true,
    "tab_label": "Brain",
    "install_label": "Install Brain"
  },
  "runtime": {
    "install_root": "~/Nymphs-Brain"
  },
  "update_policy": {
    "channel": "release-tested"
  }
}
```

---

## 16. Recommendation

For v1, keep the manifest:

- small
- mostly declarative
- script-driven

The most important thing is not fancy metadata. It is this:

- every Nymph has one manifest
- every manifest points to stable Manager-facing entrypoints
- the Manager can render UI and lifecycle actions from the same shape

That is enough to start building the modular system without overdesigning it.
