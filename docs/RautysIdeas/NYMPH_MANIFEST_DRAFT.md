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

## 9. Dependencies

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

## 10. UI Block

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

## 11. Runtime Block

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

## 12. Update Policy Block

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

## 13. Example: WORBI

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

## 14. Example: Brain

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

## 15. Recommendation

For v1, keep the manifest:

- small
- mostly declarative
- script-driven

The most important thing is not fancy metadata. It is this:

- every Nymph has one manifest
- every manifest points to stable Manager-facing entrypoints
- the Manager can render UI and lifecycle actions from the same shape

That is enough to start building the modular system without overdesigning it.
