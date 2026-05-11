# Nymphs Module Making Guide

This guide is for people who want to build community modules for NymphsCore.

The goal is simple: a module author should be able to build, test, and ship a Nymph module without reading Manager source code.

## Mental Model

NymphsCore is the base shell. Modules are separate products.

The Manager owns:

- the Windows app shell
- the module registry cards
- install, update, uninstall, and delete controls
- the standard module detail page
- the standard right-side action rail
- generic action routing
- logs and lifecycle feedback

The module owns:

- its source repo or package
- its install/update/uninstall scripts
- its status/start/stop/open/logs actions
- its runtime dependencies
- its model/artifact/cache/log paths
- any custom Manager UI shown after install

Before a module is installed, the Manager should only show registry and manifest metadata. After install, the Manager may host module-owned local UI declared by the installed module manifest.

## Required Pieces

A Nymph module normally has three public pieces:

1. A registry entry in `nymphs-registry`.
2. A module repo or archive containing `nymph.json`.
3. Lifecycle entrypoint scripts declared in `nymph.json`.

For modules with custom controls, add:

4. A module-owned local Manager UI, usually `ui/manager.html`.

## Registry Entry

The registry tells the Manager that a module exists before it is installed.

The registry entry should contain enough card metadata to show a useful available-module card:

```json
{
  "id": "zimage",
  "name": "Z-Image Turbo",
  "short_name": "ZI",
  "category": "image",
  "channel": "stable",
  "packaging": "repo",
  "summary": "Local image generation backend.",
  "install_root": "$HOME/Z-Image",
  "sort_order": 20,
  "trusted": true,
  "manifest_url": "https://raw.githubusercontent.com/nymphnerds/zimage/main/nymph.json"
}
```

Registry metadata is for discovery. It is not the source of installed custom UI.

## Module Repo Layout

Recommended repo layout:

```text
nymph.json
README.md
scripts/
  install_<module>.sh
  <module>_status.sh
  <module>_start.sh
  <module>_stop.sh
  <module>_open.sh
  <module>_logs.sh
  <module>_uninstall.sh
ui/
  manager.html
```

Use lowercase module ids in filenames. Keep scripts self-contained and safe to run inside the managed `NymphsCore` WSL distro.

## Manifest

Every module must include `nymph.json` at repo root or package root.

Minimum useful shape:

```json
{
  "manifest_version": 1,
  "id": "example",
  "name": "Example Module",
  "short_name": "EX",
  "version": "0.1.0",
  "description": "A local backend managed by NymphsCore.",
  "category": "tool",
  "packaging": "repo",
  "source": {
    "type": "repo",
    "repo": "https://github.com/example/example-module.git",
    "ref": "main"
  },
  "install": {
    "root": "$HOME/example",
    "entrypoint": "scripts/install_example.sh",
    "version_marker": "$HOME/example/.nymph-module-version",
    "installed_markers": [
      "$HOME/example/.nymph-module-version"
    ]
  },
  "entrypoints": {
    "install": "scripts/install_example.sh",
    "status": "scripts/example_status.sh",
    "start": "scripts/example_start.sh",
    "stop": "scripts/example_stop.sh",
    "open": "scripts/example_open.sh",
    "logs": "scripts/example_logs.sh",
    "uninstall": "scripts/example_uninstall.sh"
  },
  "ui": {
    "sort_order": 100,
    "standard_lifecycle_rail": true
  }
}
```

Use `packaging: "repo"` when the Manager clones the module repo. Use `packaging: "archive"` when the module is installed from a packaged archive.

## Install Contract

Install scripts should be boring and recoverable.

Rules:

- Install into a staging/temp folder first.
- Install dependencies inside staging.
- Move staging into the real install root only after dependencies succeed.
- Write `.nymph-module-version` last.
- Print `installed_module_version=<version>` after success.
- If install fails or is interrupted, the install marker must not exist.
- Do not treat “folder exists” as installed.
- Do not create random backup folders like `~/module.backup.*`.

The Manager uses this rule:

```text
installed == .nymph-module-version marker exists
installed != install folder exists
installed != preserved data exists
```

## Status Contract

`status` must be fast, timeout-safe, and safe when files are missing.

Print newline-separated `key=value` pairs. Minimum recommended keys:

```text
id=example
installed=true
runtime_present=true
data_present=true
version=0.1.0
running=false
state=installed
health=ready
install_root=/home/nymph/example
logs_dir=/home/nymph/NymphsData/logs/example
last_log=/home/nymph/NymphsData/logs/example/current.log
marker=/home/nymph/example/.nymph-module-version
detail=Example module is installed.
```

For a missing install marker:

```text
id=example
installed=false
runtime_present=false
data_present=false
version=not-installed
running=false
state=available
health=missing
detail=Example module is not installed.
```

Status must not run old bin wrappers from a partial install when `.nymph-module-version` is missing.

## Action Entrypoints

Common lifecycle actions:

```text
install
status
start
stop
open
logs
uninstall
```

Useful optional actions:

```text
update
fetch_models
smoke_test
repair
doctor
```

Each action should:

- return exit code `0` on success
- return nonzero on real failure
- print useful progress
- avoid interactive prompts unless explicitly called with a confirmation flag
- write useful logs to the declared module log folder

## Uninstall And Data

Normal uninstall should remove runtime files while preserving user data.

Recommended manifest section:

```json
{
  "uninstall": {
    "supports_purge": true,
    "requires_confirmation": true,
    "dry_run_arg": "--dry-run",
    "confirm_arg": "--yes",
    "purge_arg": "--purge",
    "preserve_by_default": [
      "data",
      "config",
      "logs",
      "outputs"
    ],
    "removes_by_default": [
      "runtime source files",
      "virtual environments",
      "generated launch scripts"
    ]
  }
}
```

Normal uninstall should remove `.nymph-module-version`.

Purge/delete-data may remove only scopes declared by the module. Be conservative. A user should never lose models, outputs, datasets, or project files because a script guessed a path.

## Artifacts, Models, Cache, And Logs

Do not put user artifacts inside disposable runtime roots unless there is a clear migration plan.

Recommended shared roots:

```text
$HOME/NymphsData/models
$HOME/NymphsData/cache
$HOME/NymphsData/outputs
$HOME/NymphsData/logs
```

Recommended module roots:

```text
$HOME/NymphsData/models/<module-id>
$HOME/NymphsData/cache/<module-id>
$HOME/NymphsData/outputs/<module-id>
$HOME/NymphsData/logs/<module-id>
$HOME/NymphsData/logs/<module-id>/actions
$HOME/NymphsData/logs/<module-id>/current.log
```

Recommended manifest section:

```json
{
  "artifacts": {
    "models_root": "$HOME/NymphsData/models",
    "cache_root": "$HOME/NymphsData/cache",
    "outputs_root": "$HOME/NymphsData/outputs/example",
    "logs_root": "$HOME/NymphsData/logs/example"
  }
}
```

The module `status` output should include `logs_dir` and optionally `last_log`.

## Custom Manager UI

Custom UI belongs to the module, not the Manager.

If a module has useful controls beyond the standard lifecycle rail, put local HTML in the module repo and declare it in the installed manifest:

```json
{
  "ui": {
    "standard_lifecycle_rail": true,
    "manager_ui": {
      "type": "local_html",
      "entrypoint": "ui/manager.html",
      "title": "Example Controls"
    }
  }
}
```

Rules:

- The Manager reads `ui.manager_ui` only from the installed module folder.
- The Manager does not render custom UI from the remote registry before install.
- `entrypoint` must be a safe relative path inside the installed module root.
- The right-side Manager rail remains standard across modules.
- The UI should call module actions through the Manager action bridge, not by running shell directly.

See [NYMPH_MODULE_UI_STANDARD.md](NYMPH_MODULE_UI_STANDARD.md) for the focused UI contract.

Future Manager builds should use WebView2 for modern module frontends. The intended expansion is:

```text
local_html        simple installed HTML page
local_web_app     installed static web app
served_web_app    module-owned localhost UI
external_browser  open outside Manager when embedding is not suitable
```

This keeps the architecture the same: the Manager hosts and routes, while the module owns the web UI.

## UI Action Bridge

Module HTML can request actions with:

```text
nymphs-module-action://<action>?name=value
```

Example:

```text
nymphs-module-action://fetch_models?quant=q4_k_m
```

The Manager validates the action against the module capabilities and turns safe query pairs into CLI arguments:

```text
fetch_models --quant q4_k_m
```

The Manager does not run arbitrary shell from HTML.

## Security And Safety Rules

Module scripts run inside the managed `NymphsCore` WSL runtime, so treat them like installer code.

Rules:

- Validate user-provided paths.
- Prefer paths under `$HOME`.
- Do not delete broad folders.
- Never depend on the developer/source WSL distro.
- Do not assume any developer/source WSL distro exists.
- Do not use source-tree UNC paths as runtime paths.
- Do not silently fetch unpinned code for release channels.
- Keep long-running actions cancellable where practical.

## Versioning

Use semantic-ish module versions:

```text
0.1.0
0.1.1
0.2.0
1.0.0
```

Bump the module version when:

- install behavior changes
- entrypoint behavior changes
- the UI contract changes
- dependencies or model pins change
- data layout changes

The install script should write the same version to `.nymph-module-version`.

## Testing Checklist

Before asking for registry inclusion, test:

- clean install from the Manager
- status before install
- status during install
- status after install
- close Manager during install, then reopen
- start
- stop
- open
- logs
- custom UI button appears only after install
- custom UI action bridge works
- normal uninstall preserves declared user data
- reinstall after uninstall
- failed install leaves no `.nymph-module-version`
- partial install folder is not treated as installed
- purge/delete-data removes only declared scopes

The expected result is boring: no stuck `Checking`, no false `Installed`, no stale `Available` during install, no hidden logs, and no surprise data loss.

## Good First Module

A good first module is small:

- one install script
- one status script
- one start/stop/open/logs path
- no model downloads during install
- no custom UI until lifecycle is stable

Once lifecycle is boring, add custom UI and optional actions.
