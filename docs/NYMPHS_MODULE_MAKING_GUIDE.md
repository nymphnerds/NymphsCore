# Nymphs Module Making Guide

This is the source-of-truth community module standard for NymphsCore.

This guide is for people who want to build community modules for NymphsCore.

The goal is simple: a module author should be able to build, test, and ship a Nymph module without reading Manager source code.

## How This Standard Evolves

This standard is defined by testing real modules, one at a time.

Do not treat planning notes as the contract. When a module test proves a rule,
update this guide first, then update the Manager, module repo, registry entry,
or UI standard as needed.

Current proof order:

1. Base Runtime: proves the managed WSL shell, install location, runtime logs,
   Windows WSL readiness, and shared runtime identity.
2. WORBI: proves the light module lifecycle: install, status, start, stop,
   logs, open, uninstall, marker handling, custom UI, and preserved user data.
3. Z-Image: proves heavyweight GPU/runtime behavior: model downloads,
   backend health, long actions, cache preservation, and Blender-facing image
   generation.
4. TRELLIS: proves 3D asset output, artifact metadata, and Blender/Unity
   consumption.
5. Brain: proves long-running services, secrets, model management, and chat/API
   style backends.

Testing loop:

```text
test module -> discover rule -> update this guide -> update implementation -> retest
```

Current heavyweight proof state:

- Z-Image proves native compact model fetch for image generation weights.
- TRELLIS proves native compact model fetch for multi-part 3D GGUF bundles,
  support checkpoints, and auxiliary models.
- Both modules keep model fetch module-owned through `ui.manager_action_groups`.
- Both modules keep generated outputs, logs, config, and reusable model caches
  under `$HOME/NymphsData` instead of inside disposable runtime source roots.

Supporting docs:

- `NYMPH_MODULE_UI_STANDARD.md`: focused contract for installed module UI.
- `Ideas/CURRENT_NYMPH_MODULE_REPO_DEEP_DIVE.md`: migration notes for current
  module repos.
- `Ideas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md`: session handoff and current
  proof-state notes.
- `Ideas/NYMPH_MANIFEST_DRAFT.md`: older manifest design sketch; useful
  background, but this guide wins when there is a conflict.

## Mental Model

NymphsCore is the base shell. Modules are separate products.

The Manager owns:

- the Windows app shell
- the module registry cards
- install, update, uninstall, and delete controls
- the standard module detail page
- the universal right-side lifecycle rail
- generic action routing
- logs and lifecycle feedback

The module owns:

- its source repo or package
- its install/update/uninstall scripts
- its status/start/stop/open/logs actions
- its runtime dependencies
- its model/artifact/cache/log paths
- any custom Manager UI shown after install
- the installed module action buttons shown in the module details pane

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

Think of the registry as the shop shelf. It tells users that your module exists
before they install it.

Put simple public info in the registry:

```text
what the module is called
what it does
what it needs
where its nymph.json file is
where it should appear in the Manager list
```

Do not put your installed workflow buttons in the registry. Buttons belong in
your module's own `nymph.json`, because your module can change its tools without
needing the catalog to know every detail.

## Publishing Checklist

When you release a module update, use this order:

1. Push the module repo first.
2. Check that the raw `nymph.json` URL opens in a browser.
3. Update the registry only if the catalog card changed.
4. Push the registry if you changed it.
5. Test from the Manager using the pushed module repo and pushed registry.

Never update the registry to advertise a module change that only exists on your
local machine. If the Manager cannot download it from GitHub yet, it is not
published yet.

When a module author is actively pushing updates, fetch the module repo before
editing, keep changes small, and avoid rewriting their unpublished local work.

## What Goes Where

The module detail page has two kinds of controls.

### Manager-Owned Controls

The right side of the page is owned by the Manager. These buttons are the same
for every module:

```text
Guide
Install
Update
Repair
Model Cache
Directory
Uninstall
```

These are install/admin buttons. You do not define these in your module.

### Module-Owned Controls

The action buttons inside the details pane are yours. Your module decides:

```text
which buttons appear
what they are called
what order they appear in
which script each button runs
what the Manager should do with the script output
```

The Manager renders these buttons and safely runs the entrypoint you declare.
The Manager should not hardcode a universal button set for every module.

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

For modules with model downloads, include the fetch action explicitly:

```text
scripts/
  <module>_fetch_models.sh
```

The fetch script should accept clear module-owned arguments such as `--model`,
`--weight`, or `--quant`, and it should persist selected runtime presets under:

```text
$HOME/NymphsData/config/<module-id>/
```

## Process Shutdown Standard

Closing the Manager must cancel active module lifecycle work for every module.

Module install, update, repair, uninstall, fetch, and smoke-test scripts must
keep their child work inside the process tree launched by the Manager. Do not
use `nohup`, `disown`, detached `setsid`, or untracked background workers for
these lifecycle jobs.

If a lifecycle script starts child processes, trap `TERM` and `INT`, then stop
those children before exiting. The Manager will cancel active module lifecycle
process trees when the app closes; module scripts must not escape that contract.

Long-running `start` scripts are the exception only when they intentionally
start a managed backend service. In that case the module must write enough PID
or ownership state for its `stop` script to terminate the backend cleanly.

## Manifest

Every module must include `nymph.json` at repo root or package root.

Minimum useful shape for a normal module:

```json
{
  "manifest_version": 1,
  "id": "example",
  "name": "Example Module",
  "short_name": "EX",
  "version": "0.1.0",
  "description": "A local tool managed by NymphsCore.",
  "category": "tool",
  "packaging": "repo",
  "source": {
    "type": "repo",
    "repo": "https://github.com/example/example-module.git",
    "ref": "main"
  },
  "install": {
    "root": "$HOME/Example",
    "entrypoint": "scripts/install_example.sh",
    "version_marker": "$HOME/Example/.nymph-module-version",
    "installed_markers": [
      "$HOME/Example/.nymph-module-version"
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
    "manager_actions": [
      {
        "id": "start",
        "label": "Start",
        "entrypoint": "start",
        "result": "show_output"
      },
      {
        "id": "stop",
        "label": "Stop",
        "entrypoint": "stop",
        "result": "show_output"
      },
      {
        "id": "logs",
        "label": "Logs",
        "entrypoint": "logs",
        "result": "show_output"
      }
    ]
  },
  "uninstall": {
    "supports_purge": true,
    "requires_confirmation": true,
    "dry_run_arg": "--dry-run",
    "confirm_arg": "--yes",
    "purge_arg": "--purge",
    "preserve_by_default": [
      "outputs",
      "logs"
    ],
    "removes_by_default": [
      "runtime source files",
      "generated module scripts"
    ]
  }
}
```

Use this shape for simple tools such as WORBI-style modules. They still need
the install marker, lifecycle entrypoints, and safe uninstall metadata, but they
do not need model fetch controls, Hugging Face tokens, smoke tests, or model
cache paths unless the module actually uses them.

Model-fetch backend shape:

Use this larger shape for AI backends like Z-Image Turbo or TRELLIS where
installing the backend and downloading model files are separate steps.

```json
{
  "manifest_version": 1,
  "id": "example-backend",
  "name": "Example Backend",
  "short_name": "EX",
  "version": "0.1.0",
  "description": "A local AI backend managed by NymphsCore.",
  "category": "image",
  "packaging": "repo",
  "source": {
    "type": "repo",
    "repo": "https://github.com/example/example-backend.git",
    "ref": "main"
  },
  "install": {
    "root": "$HOME/ExampleBackend",
    "entrypoint": "scripts/install_example_backend.sh",
    "version_marker": "$HOME/ExampleBackend/.nymph-module-version",
    "installed_markers": [
      "$HOME/ExampleBackend/.nymph-module-version"
    ]
  },
  "runtime": {
    "host": "127.0.0.1",
    "port": 8090,
    "health_url": "http://127.0.0.1:8090/health",
    "server_info_url": "http://127.0.0.1:8090/server_info"
  },
  "artifacts": {
    "models_root": "$HOME/NymphsData/models",
    "cache_root": "$HOME/NymphsData/cache",
    "outputs_root": "$HOME/NymphsData/outputs/example-backend",
    "logs_root": "$HOME/NymphsData/logs/example-backend",
    "huggingface_cache": "$HOME/NymphsData/cache/huggingface"
  },
  "entrypoints": {
    "install": "scripts/install_example_backend.sh",
    "status": "scripts/example_backend_status.sh",
    "start": "scripts/example_backend_start.sh",
    "stop": "scripts/example_backend_stop.sh",
    "open": "scripts/example_backend_open.sh",
    "logs": "scripts/example_backend_logs.sh",
    "fetch_models": "scripts/example_backend_fetch_models.sh",
    "smoke_test": "scripts/example_backend_smoke_test.sh",
    "uninstall": "scripts/example_backend_uninstall.sh"
  },
  "ui": {
    "sort_order": 100,
    "manager_action_groups": [
      {
        "id": "model_fetch",
        "title": "Model Fetch",
        "layout": "compact",
        "entrypoint": "fetch_models",
        "result": "show_logs",
        "visibility": "installed",
        "description": "Install sets up the backend only. Fetch Models downloads the actual AI model files. Explain what will be downloaded, how large it may be, and which option a new user should choose.",
        "links": [
          {
            "label": "Base model",
            "url": "https://huggingface.co/example/base-model"
          },
          {
            "label": "Quantized weights",
            "url": "https://huggingface.co/example/quantized-weights"
          }
        ],
        "fields": [
          {
            "name": "model_choice",
            "type": "select",
            "label": "Download",
            "arg": "--model",
            "default": "recommended",
            "options": [
              {
                "label": "Recommended",
                "value": "recommended",
                "description": "Best first choice for most users"
              },
              {
                "label": "All models",
                "value": "all",
                "description": "Download every supported model option"
              }
            ]
          },
          {
            "name": "hf_token",
            "type": "secret",
            "label": "Hugging Face token",
            "secret_id": "huggingface.token",
            "env": "NYMPHS_HF_TOKEN",
            "optional": true
          }
        ],
        "submit": {
          "label": "Fetch Models"
        }
      }
    ],
    "manager_actions": [
      {
        "id": "smoke_test",
        "label": "Smoke Test",
        "entrypoint": "smoke_test",
        "result": "show_output"
      },
      {
        "id": "start",
        "label": "Start",
        "entrypoint": "start",
        "result": "show_output"
      },
      {
        "id": "stop",
        "label": "Stop",
        "entrypoint": "stop",
        "result": "show_output"
      },
      {
        "id": "logs",
        "label": "Logs",
        "entrypoint": "logs",
        "result": "show_output"
      }
    ]
  },
  "uninstall": {
    "supports_purge": true,
    "requires_confirmation": true,
    "dry_run_arg": "--dry-run",
    "confirm_arg": "--yes",
    "purge_arg": "--purge",
    "preserve_by_default": [
      "outputs",
      "logs",
      "model cache"
    ],
    "removes_by_default": [
      "runtime source files",
      "virtual environment",
      "generated module scripts"
    ]
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

Manager interpretation rule:

- The install marker is the source of truth for whether a module is installed.
- A `status` script must agree with the marker.
- If the marker exists but `status` fails, times out, or reports `installed=false`, the Manager should keep the module in the installed group and surface a status warning/detail. It should not demote the module to available, and it should not show a scary top-level install state.
- The proper fix for a marker/status mismatch is in the module status script or manifest path, not a Manager hardcode.

## Native Install Options

Use `install.fields` only for choices that must be known before the install
script runs, such as TRELLIS FlashAttention build limits or GPU architecture.
Do not hardcode module-specific install choices into the Manager.

Install fields follow the same compact field shape as native action-group
fields:

```json
{
  "install": {
    "title": "FLASH ATTENTION OPTIONS",
    "root": "$HOME/TRELLIS.2",
    "entrypoint": "scripts/install_trellis.sh",
    "fields": [
      {
        "name": "flash_attn_cuda_archs",
        "type": "select",
        "label": "GPU",
        "env": "TRELLIS_FLASH_ATTN_CUDA_ARCHS",
        "default": "auto",
        "options": [
          {
            "label": "Auto-detect",
            "value": "auto",
            "description": "Read the NVIDIA compute capability"
          },
          {
            "label": "SM80 / RTX 30+40",
            "value": "sm80",
            "description": "Compile FlashAttention for Ampere/Ada targets"
          }
        ]
      }
    ]
  }
}
```

Rules:

- The Manager renders these fields before install and passes selected values to
  the install script through the declared `env` names.
- The install script owns validation and must fail clearly if a value is
  unsupported.
- Defaults should be safe. For expensive native builds, prefer a conservative
  default and explain faster/riskier choices in the details pane.
- The Manager may show the selected install options in the details pane while
  the lifecycle action runs, but the module script still owns the real work.

## Installed Detection Standard

The working Manager detection path is deliberately two-stage.

Stage 1 is a fast marker scan:

```text
registry/manifest roster -> resolve install root -> check <install root>/.nymph-module-version
```

If the marker exists, the module is shown as installed immediately. This keeps
Home responsive and avoids waiting for every backend to prove runtime health.

On Windows, the Manager must perform this startup marker scan from the Windows
side through the UNC view of the managed runtime distro:

```text
\\wsl.localhost\NymphsCore\home\nymph\<module>\.nymph-module-version
```

Do not implement startup install truth as a WSL bash probe, module `status`
call, model-cache scan, smoke test, or backend health check. The Manager may be
launched from the development/source distro, such as `NymphsCore_Lite`, while
installed modules live in the managed runtime distro, `NymphsCore`. Direct
Windows-side marker reads avoid that distro-boundary mistake and avoid WSL wake
races.

Stage 2 is the module `status` action:

```text
status -> key=value snapshot -> runtime health/detail
```

Status is the recovery and verification path. The Manager still runs status in
the background because the fast marker scan can fail when WSL is slow to wake,
when a manifest path is wrong, or when the distro service has just recovered.

The rule is:

```text
fast marker scan may promote Available -> Installed
status may promote Available -> Installed
status may add health/detail warnings
status must not demote marker-installed modules to Available
```

If the fast marker scan times out, a deferred marker-only retry is allowed. That
retry must still read only install markers and repair-candidate filesystem
signals. It must not become a deep status pass.

Do not remove the status recovery pass just to make startup feel faster. Make
the shell responsive first, then refresh status in the background.

For now, the canonical marker path is:

```text
<resolved install root>/.nymph-module-version
```

`install.root` is preferred, then `runtime.install_root`, then registry
`install_root`, then the Manager's module-id fallback. Future manifest fields
such as `install.version_marker` or `install.installed_markers` may be added
only after at least one proof module ships them and the Manager tests that path.

## Startup Performance Standard

Startup should be boring and quick:

- Load registry/manifest cards first.
- Prime installed state from Windows-side marker reads against the real managed
  `NymphsCore` runtime distro.
- Show the Home grid as soon as the roster is ready.
- Run Windows/system checks with short timeouts.
- Run module status in the background after the shell is usable.
- Keep per-module status bounded and safe.
- Do not run heavyweight health checks, model scans, downloads, or smoke tests
  as part of startup status.

The bottom status line should distinguish these phases:

```text
Manager shell loaded.
Refreshing live status...
Manager shell refreshed.
```

Z-Image/WORBI proof rule:

```text
startup install truth == Windows-side marker read from the real managed runtime distro
startup install truth != module status
startup install truth != model cache scan
startup install truth != smoke test
```

This is now part of the standard because the Manager may be launched from the
developer/source distro path, such as `NymphsCore_Lite`, while installed modules
live in the managed runtime distro, `NymphsCore`. The fast startup checker must
target the real runtime distro and read `.nymph-module-version` through the
Windows UNC view. Do not replace this with WSL bash startup probing unless the
Windows-side path is unavailable and the fallback is clearly bounded.

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

Installed action execution standard:

- After install, lifecycle and utility actions should run the installed
  module-owned script directly from the installed module root.
- The Manager may use the registry/cache manifest to discover actions, but it
  must not let a stale cache override an installed script that exists locally.
- Conventional installed scripts such as
  `scripts/<module_id>_<action>.sh` should be treated as the authoritative
  action implementation for installed modules.
- The module owns the script behavior and exit code. The Manager owns only
  rendering, routing, and progress capture.

## Smoke Test Standard

`smoke_test` is a lightweight health validation action.

For backend modules, a smoke test should usually:

```text
start the backend if it is not already running
wait for a health/config endpoint
print concise evidence, such as /server_info
stop the backend if the smoke test started it
exit 0 only when the health/config check passed
exit nonzero on real failure
```

Smoke tests should not silently run a full generation unless the module clearly
labels that as a heavier validation. A backend can pass smoke test with
`loaded_model_id=null` if the test only proves that the server starts and
answers `/server_info`. Model load or generation can be a separate action later.

The Manager UI must report the result plainly:

```text
SMOKE TEST PASSED
SMOKE TEST FAILED
```

Do not use vague success labels such as `finished` for tests. The user should not
have to decode raw JSON to know whether the test passed.

## Installed Module Buttons

Installed module buttons appear in the module details pane after install.

Use these when your module has actions like:

```text
Start
Stop
Browser
Logs
Fetch Models
Open Project
Backup
Reindex
Train
Export
```

Declare them in `ui.manager_actions`:

```json
{
  "ui": {
    "manager_actions": [
      {
        "id": "start",
        "label": "Start",
        "entrypoint": "start",
        "result": "open_in_manager"
      },
      {
        "id": "browser",
        "label": "Browser",
        "entrypoint": "start",
        "result": "open_external_browser"
      },
      {
        "id": "logs",
        "label": "Logs",
        "entrypoint": "logs",
        "result": "open_notepad"
      }
    ]
  }
}
```

Fields:

- `id`: stable action id, such as `start` or `browser`.
- `label`: button text shown to users.
- `entrypoint`: the key from your `entrypoints` block to run.
- `result`: what the Manager should do after the script succeeds.

Supported result modes:

```text
show_output
open_in_manager
open_external_browser
open_notepad
```

Your module owns the behavior. The Manager is only the renderer, launcher, and
safe host bridge.

Module actions should print structured hints when they want the Manager to open
something:

```text
url=http://localhost:8082
log_file=/home/nymph/worbi/logs/worbi-server.log
server_log=/home/nymph/worbi/logs/worbi-server.log
message=Action finished.
```

Simple rule:

```text
If you want a button, declare it in ui.manager_actions.
If you want the button to do something, point it at an entrypoint script.
```

## Native Action Groups

Use `ui.manager_action_groups` when a module needs compact native controls
instead of a single button.

This is the standard for model fetch panels such as Z-Image Turbo and TRELLIS.
It is also suitable for small module-owned setup forms, such as selecting a
runtime profile or optional model pack.

Use action groups when you need:

```text
select/dropdown fields
checkboxes
saved secret inputs
source links
one submit action
compact controls that still match the Manager style
```

Do not build a WebView2/local HTML page just to choose model files. Keep simple
model download choices native, compact, and module-owned.

Example:

```json
{
  "ui": {
    "manager_action_groups": [
      {
        "id": "model_fetch",
        "title": "Model Fetch",
        "layout": "compact",
        "entrypoint": "fetch_models",
        "result": "show_logs",
        "visibility": "installed",
        "description": "Fetch the backend model files. Install only sets up the runtime.",
        "links": [
          {
            "label": "Base model",
            "url": "https://huggingface.co/example/base"
          },
          {
            "label": "Quantized weights",
            "url": "https://huggingface.co/example/weights"
          }
        ],
        "fields": [
          {
            "name": "model",
            "type": "select",
            "label": "Download",
            "arg": "--model",
            "default": "small",
            "options": [
              {
                "label": "Small",
                "value": "small",
                "description": "Lower VRAM"
              },
              {
                "label": "All weights",
                "value": "all",
                "description": "Downloads every selectable preset"
              }
            ]
          },
          {
            "name": "hf_token",
            "type": "secret",
            "label": "Hugging Face token",
            "secret_id": "huggingface.token",
            "env": "NYMPHS3D_HF_TOKEN",
            "optional": true
          }
        ],
        "submit": {
          "label": "Fetch Models"
        }
      }
    ]
  }
}
```

Field rules:

- `label` must be understandable to a beginner. Use `Hugging Face token`, not
  `HF`.
- `secret` fields are saved by the Manager once and injected into the module
  action through the declared environment variable. Do not print secrets.
- `links` should be real links to source model pages, not button-looking UI.
- Long downloads should print progress and keep the Manager responsive.
- The module owns download validation, selected-model persistence, and cache
  layout. The Manager only renders controls and routes the declared action.

The compact Z-Image proof established this behavior:

```text
Install = set up backend/runtime only
Fetch Models = download the real AI model files
Base model downloads can be large and must be explained in the guide text
All weights is optional and should be clearly labelled as larger
Blender/addon model choice can consume the fetched models later
```

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
- The Manager shell remains standard across modules. The current WebView2 host keeps the sidebar and shows a full-width, thin standard Back bar above the hosted module UI.
- The UI should call module actions through the Manager action bridge, not by running shell directly.

See [NYMPH_MODULE_UI_STANDARD.md](NYMPH_MODULE_UI_STANDARD.md) for the focused UI contract.

Current local Manager builds use WebView2 for modern module frontends. The intended expansion is:

```text
local_html        simple installed HTML page
local_web_app     installed static web app
served_web_app    module-owned localhost UI
external_browser  open outside Manager when embedding is not suitable
```

This keeps the architecture the same: the Manager hosts and routes, while the module owns the web UI.

Module authors should keep local HTML lightweight and self-contained. Avoid remote CDN dependencies for Manager-hosted controls. Any expensive backend checks, model scans, or downloads should be triggered through explicit module actions, not during HTML page load.

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

The module owns the web UI label and behavior:

- Set `ui.manager_ui.title` to the user-facing action label, such as `Fetch Models`.
- Add every callable HTML action to `entrypoints`, such as `fetch_models`.
- Keep the HTML lightweight; trigger model downloads, backend starts, scans, and other expensive work through explicit `nymphs-module-action://` links.
- Long actions should print useful progress lines. The Manager switches to the standard Logs page and streams stdout, stderr, and carriage-return progress there.
- The Manager may refresh the trusted module repo cache to find a newly declared action, but the action still belongs to the module manifest and script.

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
