# Brain Module Native Manager Plan

## Important: Next Session Read First

Read these first before implementing:

```text
/home/nymph/NymphsCore/docs/Ideas/BRAIN_MODULE_NATIVE_MANAGER_PLAN.md
/home/nymph/NymphsCore/docs/NYMPHS_MODULE_MAKING_GUIDE.md
/home/nymph/NymphsCore/docs/NYMPH_MODULE_UI_STANDARD.md
/home/nymph/NymphsCore/docs/NYMPHS_BRAIN_GUIDE.md
```

Goal: move the old hardcoded Nymphs-Brain Manager page into the modular Manager
system without losing the working Brain UX.

Status as of 2026-05-14: implemented and tested in the modular Manager shell.
Brain is a module-owned native Manager page; Open WebUI remains the WebView2 or
external browser surface; Manage Models remains the installed terminal flow.

This plan uses the current module standard proven by WORBI, Z-Image, and
TRELLIS. Brain should become an installable module with module-owned scripts and
native Manager-rendered controls. Open WebUI remains the browser/WebView2 app
surface. The existing interactive model manager remains a terminal script.

## Current Decisions

- Brain must not reintroduce a hardcoded Manager page.
- Brain must not use local HTML for its Manager controls.
- Open WebUI should use the WORBI-style `local_url` path.
- Manage Models should keep the old interactive `lms-model` terminal flow.
- The left sidebar Brain monitor already exists, so the Brain module page should
  not duplicate the old monitor panel.
- Universal Manager lifecycle controls own install/update/repair/uninstall.
- Brain should not expose `Update Stack` as a module action.
- The left sidebar Brain monitor shows configured `Local` and `Remote` model
  labels separately.
- The OpenRouter group uses the compact secret-only action-group shape:

```text
// OpenRouter
   OpenRouter key: [masked key field] [// Apply Key] [// Remove Key]
```

## Proven Patterns To Reuse

### WORBI

WORBI proves the browser-app pattern:

```json
"manager_ui": {
  "type": "local_url",
  "title": "WORBI",
  "url": "http://localhost:8082",
  "requires_running": true,
  "start_action": "start"
}
```

WORBI also proves:

- `open_in_manager` for WebView2.
- `open_external_browser` for browser fallback.
- simple module action buttons for start/stop/browser/logs.
- deeper app UI belongs inside the app, not the Manager action row.

### Z-Image And TRELLIS

Z-Image and TRELLIS prove:

- module-owned scripts do the real work.
- installed module scripts win over stale registry/cache scripts.
- long actions use `show_logs`.
- validation actions use `show_output`.
- module model controls can be native when options are compact and static.

Brain's model manager is not static, so it should not be rebuilt as a partial
native picker yet.

### Current Manager Shell

The current Manager owns the universal right-side lifecycle rail:

```text
Guide
Install
Update
Repair
Model Cache
Directory
Uninstall
```

`Update` and `Repair` rerun the trusted registry install/update flow through:

```text
Manager/scripts/install_nymph_module_from_registry.sh
```

Therefore Brain-specific actions should not include `Update Stack`.

## Target Brain Page Shape

Brain should feel like the old Brain page, minus the duplicated monitor panel
and minus universal lifecycle actions.

Details should show:

```text
LLM:            running/stopped/unknown
MCP:            running/stopped/unknown
WebUI:          running/stopped/unknown
Local model:    selected/loaded model
Remote model:   llm-wrapper model
OpenRouter key: Saved / Not set
```

Module-owned actions should be:

```text
Start Brain
Stop Brain
Open WebUI
Browser
Manage Models
Apply Key
Logs
```

If Manager later supports state-aware action labels, `Start Brain` and
`Stop Brain` can collapse into one toggle like the old page. Until then, a
separate stop action is acceptable.

## Brain Module Manifest Shape

Expected `entrypoints`:

```json
{
  "entrypoints": {
    "install": "scripts/install_brain.sh",
    "status": "scripts/brain_status.sh",
    "start": "scripts/brain_start.sh",
    "stop": "scripts/brain_stop.sh",
    "webui": "scripts/brain_webui.sh",
    "open": "scripts/brain_open.sh",
    "manage_models": "scripts/brain_manage_models.sh",
    "apply_key": "scripts/brain_apply_key.sh",
    "logs": "scripts/brain_logs.sh",
    "uninstall": "scripts/brain_uninstall.sh"
  }
}
```

Expected Open WebUI declaration:

```json
{
  "ui": {
    "manager_ui": {
      "type": "local_url",
      "title": "Open WebUI",
      "url": "http://localhost:8081",
      "requires_running": true,
      "start_action": "webui"
    }
  }
}
```

Expected module actions:

```json
{
  "ui": {
    "manager_actions": [
      {
        "id": "start",
        "label": "Start Brain",
        "entrypoint": "start",
        "result": "show_output"
      },
      {
        "id": "stop",
        "label": "Stop Brain",
        "entrypoint": "stop",
        "result": "show_output"
      },
      {
        "id": "webui",
        "label": "Open WebUI",
        "entrypoint": "webui",
        "result": "open_in_manager"
      },
      {
        "id": "browser",
        "label": "Browser",
        "entrypoint": "webui",
        "result": "open_external_browser"
      },
      {
        "id": "manage_models",
        "label": "Manage Models",
        "entrypoint": "manage_models",
        "result": "open_terminal"
      },
      {
        "id": "logs",
        "label": "Logs",
        "entrypoint": "logs",
        "result": "show_output"
      }
    ]
  }
}
```

OpenRouter key handling likely belongs in a compact native action group because
it needs a secret field plus one submit button.

## OpenRouter Key Action Group

Brain should declare a module-owned action group similar to model fetch secret
fields, but with an OpenRouter secret:

```json
{
  "id": "openrouter_key",
  "title": "OpenRouter",
  "layout": "compact",
  "entrypoint": "apply_key",
  "result": "show_output",
  "visibility": "installed",
  "description": "Optional. Enables the Brain llm-wrapper tool bridge.",
  "fields": [
    {
      "name": "openrouter_api_key",
      "type": "secret",
      "label": "OpenRouter key",
      "secret_id": "openrouter.api_key",
      "env": "OPENROUTER_API_KEY",
      "optional": true
    }
  ],
  "submit": {
    "label": "Apply Key"
  }
}
```

This requires generic secret support in the Manager. Current Manager secret
handling is Hugging Face-specific.

## Script Responsibilities

### `brain_status.sh`

Should report module status and Brain-specific facts as key/value lines.

Recommended output:

```text
id=brain
installed=true
runtime_present=true
data_present=true
version=0.1.0
running=true
state=running
health=ok
llm_running=true
mcp_running=true
open_webui_running=true
local_model=...
remote_model=...
openrouter_key=saved
url=http://localhost:8081
install_root=/home/nymph/Nymphs-Brain
logs_dir=/home/nymph/Nymphs-Brain/logs
detail=Brain is running.
```

The generic Manager can use `installed`, `running`, `state`, `health`,
`version`, `url`, `install_root`, `logs_dir`, and `detail` now. The Brain-specific
fields are available for a richer native details layout later.

### `brain_start.sh`

Should start the Brain services used by the old `Start Brain` button:

```text
~/Nymphs-Brain/bin/lms-start
~/Nymphs-Brain/bin/mcp-start
```

If no local model is configured, it should fail clearly or start only the safe
pieces and tell the user to run Manage Models. The exact behavior should match
the old page expectations.

### `brain_stop.sh`

Should stop all active Brain services:

```text
open-webui-stop
mcp-stop
lms-stop
```

It should succeed when services are already stopped.

### `brain_webui.sh`

Should start Open WebUI if needed and print:

```text
http://localhost:8081
```

This allows both `open_in_manager` and `open_external_browser` to work.

### `brain_manage_models.sh`

Should preserve the old working model manager flow by running:

```text
~/Nymphs-Brain/bin/lms-model
```

The Manager should launch this through a new generic `open_terminal` result.

This keeps:

- remote model search/download through LM Studio CLI
- downloaded model selection
- context selection
- remote llm-wrapper model selection
- local model clearing/removal
- vision model mmproj handling

### `brain_apply_key.sh`

Should receive:

```text
OPENROUTER_API_KEY
```

and write:

```text
~/Nymphs-Brain/secrets/llm-wrapper.env
```

It should preserve an existing `REMOTE_LLM_MODEL` if present.

### `brain_logs.sh`

Should print useful recent logs and paths, including:

```text
~/Nymphs-Brain/logs/lms.log
~/Nymphs-Brain/open-webui-data/logs/open-webui.log
~/Nymphs-Brain/mcp/logs/mcp-proxy.log
```

## Manager Changes Needed

### Generic `open_terminal` Result

Add a result mode:

```text
open_terminal
```

Behavior:

- only runs installed module-declared entrypoints
- opens Windows Terminal with `wsl.exe -d NymphsCore --user nymph -- <script>`
- falls back to `cmd.exe start ... wsl.exe ...` if `wt.exe` is unavailable
- title should be `<Module Name> - <Action Label>`
- based on the old hardcoded `OpenNymphsBrainModelManager` implementation
- should be generic, not Brain-specific

Interactive terminal actions are intentionally user-owned after launch. They do
not need the same cancellation behavior as Manager-owned install/update/fetch
jobs.

### Generic Secret Support

Generalize module action group secret handling:

- respect any safe `secret_id`
- respect any safe declared `env`
- save/reload OpenRouter separately from Hugging Face
- never print secret values to logs

Minimum needed for Brain:

```text
secret_id=openrouter.api_key
env=OPENROUTER_API_KEY
```

## What Not To Build

- No hardcoded Brain page in Manager.
- No local HTML Brain controls.
- No duplicated Brain monitor panel in the module page.
- No Brain-owned `Update Stack` button.
- No partial native replacement for `lms-model`.
- No dynamic model-search UI until the old model manager is safely modular.

## Implementation Order

1. Update Brain manifest to current module standard.
2. Add Brain wrapper scripts for `webui`, `manage_models`, and `apply_key`.
3. Keep install writing installed `nymph.json` and `.nymph-module-version`.
4. Add Manager `open_terminal` result mode.
5. Generalize Manager secret field support for OpenRouter.
6. Test Brain install/update/repair through the universal right rail.
7. Test installed Brain actions: start, stop, Open WebUI, Browser, Manage Models,
   Apply Key, Logs.
8. Only after the above works, consider a richer native service details layout
   for Brain-specific status fields.
