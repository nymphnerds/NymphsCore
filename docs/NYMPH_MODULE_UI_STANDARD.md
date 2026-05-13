# Nymph Module UI Standard

This is the Manager/module boundary for custom module UI.

## Rule

The Manager owns the shell, registry cards, install/update/uninstall controls, standard module detail page, standard Back navigation, logs, and generic action routing.

Installed modules own any custom frontend.

The Manager must not hardcode custom UI for Brain, Z-Image, LoRA, TRELLIS, WORBI, or future modules. Before a module is installed, the Manager can only show registry and manifest metadata plus standard lifecycle controls.

## Manifest Contract

An installed module may expose a local Manager UI from its installed `nymph.json`:

```json
{
  "ui": {
    "manager_ui": {
      "type": "local_html",
      "entrypoint": "ui/manager.html",
      "title": "Module Controls"
    }
  }
}
```

Rules:

- `type` must be `local_html`.
- `entrypoint` must be a safe relative path inside the installed module root.
- The Manager reads this only from the installed module folder, never from a remote registry preview.
- If the installed file is absent or invalid, the Manager hides the custom UI button.

## WebView2 Host

The Manager hosts installed module UI with Microsoft Edge WebView2.

Current local host behavior:

- The Manager keeps the left sidebar/shell.
- The hosted module UI page has a standard full-width, thin `Back` bar owned by the Manager.
- The module HTML owns only the content inside the hosted WebView2 surface.
- The module manifest owns the custom UI button label through `ui.manager_ui.title`; the Manager displays that module-owned title instead of hardcoding module-specific labels.
- The Manager warms the real WebView2 module UI host during app startup, but it must not overlay or block the Home page.
- The Manager must not add module-specific frontend code for Z-Image, Brain, LoRA, TRELLIS, WORBI, or future modules.

## Fast Load Rules

These rules came from the Z-Image module UI performance proof. Keep them intact unless a replacement is timed and visually confirmed.

For `local_html`:

- Copy the installed module UI into the Manager's local module UI cache under `%LOCALAPPDATA%\NymphsCore\ModuleUiCache`.
- Refresh that cache when the installed module UI source changes. A stale cache makes module UI fixes look broken even when the module repo is correct.
- Preserve a newer cache over an older installed source. Do not refresh only because byte length differs; that can let an old installed module UI overwrite a fast, corrected cache and reintroduce stale controls.
- Load the cached HTML with WebView2 `NavigateToString`, not `file://`.
- Allow WebView2's internal `data:` navigation. `NavigateToString` becomes a `data:` navigation internally; blocking `data:` makes the page appear stuck on a blank surface.
- Keep `nymphs-module-action://` interception for module actions.
- Keep `file:`, `about:`, and `data:` allowed in the WebView2 navigation filter. Cancel other schemes unless the Manager explicitly supports them.

For the WebView2 host:

- Use an explicit local user-data folder: `%LOCALAPPDATA%\NymphsCore\WebView2`.
- Do not let WebView2 create `NymphsCoreManager.exe.WebView2` beside the EXE, especially when the EXE is launched from a WSL/UNC path.
- Prewarm the actual visible module UI `WebView2` control, not a separate throwaway hidden browser.
- When opening a module UI, switch the shell to the module UI page before setting `ModuleUiSource`.
- Queue module UI navigation at high dispatcher priority. Do not queue the first real navigation at idle/background priority; it can sit behind shell/status refresh work for many seconds.
- Skip repeated navigation only when the cached module UI source path, last-write time, and file size are unchanged.
- Background status refresh must not reopen or reload the current module UI page.

Timing logs belong in `%LOCALAPPDATA%\NymphsCore\manager-app.log` with the `module-ui-host` prefix. When debugging load performance, compare:

```text
module UI opened
navigate_request
navigate_to_string_ms
navigation_complete
```

The expected healthy behavior is that `module UI opened` and `navigate_request` happen in the same second, and `navigate_to_string_ms` is near zero for small `local_html` pages. If a UI fix does not appear, compare the logged `bytes=` value with the cached file under `%LOCALAPPDATA%\NymphsCore\ModuleUiCache`; a smaller or older byte count usually means stale installed HTML has overwritten the cache.

Current supported type:

```text
local_html
```

Planned types:

```text
local_web_app
served_web_app
external_browser
```

`local_web_app` is for installed static web apps such as React/Vite/Svelte/plain HTML builds:

```json
{
  "ui": {
    "manager_ui": {
      "type": "local_web_app",
      "entrypoint": "ui/dist/index.html",
      "title": "Module Dashboard"
    }
  }
}
```

`served_web_app` is for modules that already expose a browser UI:

```json
{
  "ui": {
    "manager_ui": {
      "type": "served_web_app",
      "start_action": "start_ui",
      "stop_action": "stop_ui",
      "url": "http://127.0.0.1:7860",
      "title": "Training UI"
    }
  }
}
```

For future static local apps, WebView2 virtual host mapping may be used instead of raw `file://` when it is measured and proven locally:

```text
https://<module-id>.nymphs.invalid/index.html
```

mapped to:

```text
<installed module root>/ui/dist
```

Do not switch an already working `local_html` host to a new loading mechanism without timing logs and local visual confirmation. First priority is boring, reliable load behavior.

## Action Bridge

Module HTML can request module actions through:

```text
nymphs-module-action://<action>?name=value
```

The Manager validates the action against the installed module capabilities and runs the module-owned entrypoint. Query pairs become safe CLI arguments:

```text
nymphs-module-action://fetch_models?quant=q4_k_m
```

becomes:

```text
fetch_models --quant q4_k_m
```

The Manager does not run arbitrary shell from HTML.

If an older installed manifest is missing a requested action, the Manager may refresh the trusted registry repo cache and resolve the action from the current module manifest before reporting it unavailable. This keeps action routing generic while letting modules add buttons such as model fetchers without Manager code changes.

When a module UI action starts, the Manager switches to the standard Logs page and streams stdout, stderr, and carriage-return progress there. Module UI pages should therefore trigger long jobs with `nymphs-module-action://` and let the module script print useful progress instead of trying to run downloads during page load.

## Native Action Groups

Some module-owned UI does not need WebView2. If a module only needs compact
native controls, use `ui.manager_action_groups` in the module manifest.

Action groups are still module-owned:

- the module declares the controls
- the module owns the script that runs
- the module owns validation, downloads, cache paths, and persisted presets
- the Manager only renders the controls and routes the declared action

Use native action groups for:

```text
model fetch controls
small setup forms
runtime/profile selectors
token entry
source links
one submit action
```

Do not build a WebView2/local HTML page just to choose model files. The Z-Image
Turbo proof showed that model fetch can stay compact, Manager-styled, and
module-owned without custom Manager code.

### Model Fetch UI

Model fetch controls should live on the installed module detail page, below the
standard `// DETAILS` pane and above `// MODULE ACTIONS`.

Do not create a second details card, a separate browser button, or a new
module-specific Manager page for simple model downloads.

The `// DETAILS` pane should explain what the user is choosing. The fetch
controls should stay compact and action-oriented.

Recommended layout:

```text
// DETAILS
<module status or guide>
clear beginner guide
source links

// Model Fetch
Hugging Face token: [masked token field........] [remove token]
Download:           [model/weight selector....] [// Fetch Models]

// MODULE ACTIONS
// Smoke Test   // Start   // Stop   // Logs
```

Standard rules:

- Explain that installing a backend is not the same as downloading model
  weights.
- If every fetch downloads a required base model first, say that clearly in the
  guide text. Do not let a user think the smallest weight is the whole download.
- If `All weights` exists, label it as optional and larger. Use it only when the
  downstream tool, such as the Blender addon, can switch between models later.
- Let the user choose the model or quantized weight manually. Do not silently
  auto-pick from GPU detection.
- Use real, beginner-readable labels. Use `Hugging Face token`, not `HF`.
- Show source model pages as links in the details guide, not button-looking
  controls floating near the submit button.
- Keep the token row separate from the model fetch row.
- Mask saved tokens across the width of the token field, not with a tiny token
  indicator that looks like only a few characters were saved.
- The selector should show the useful end of long model filenames, such as
  `int4_r32`, while the value sent to the script can still be the full filename.
- Long downloads should use `result: "show_logs"` and print progress lines.
- Short validation actions, such as `Smoke Test`, should use `result:
  "show_output"` and leave a clear pass/fail result in the details pane.
- A successful smoke test must say `SMOKE TEST PASSED` or equivalent. `finished`
  is too vague for a test result.
- The details pane should preserve the latest action result long enough for the
  user to read it. Background status refresh and delayed manifest refresh must
  not immediately overwrite a just-completed action result.

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
        "description": "Install sets up the backend only. Fetch Models downloads the actual AI model files.\nEvery fetch downloads the required base model first.\nThen it downloads your selected quantized weight. Choose All weights only if another tool can switch between them later.",
        "links": [
          {
            "label": "Base model",
            "url": "https://huggingface.co/example/base-model"
          },
          {
            "label": "Quantized weights",
            "url": "https://huggingface.co/example/weights"
          }
        ],
        "fields": [
          {
            "name": "quantized_weight",
            "type": "select",
            "label": "Download",
            "arg": "--model",
            "default": "small",
            "options": [
              {
                "label": "small",
                "value": "small-model-file.safetensors",
                "description": "Smallest download and lowest VRAM"
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
    ],
    "manager_actions": [
      {
        "id": "smoke_test",
        "label": "Smoke Test",
        "entrypoint": "smoke_test",
        "result": "show_output"
      }
    ]
  }
}
```

### Model Fetch Script Output

The module-owned fetch script should print human-readable progress. The Manager
does not know the module's model sizes, Hugging Face repos, cache shape, or file
names beyond what the manifest declares.

Useful lines include:

```text
model_fetch_plan=1 required base model, then selected weight
MODEL FETCH STARTED: step=1/2 required base model repo=...
MODEL FETCH STATUS: downloading=required large base model files
MODEL FETCH STATUS: huggingface_cache_total=...
MODEL FETCH COMPLETE: step=1/2 required base model repo=...
MODEL FETCH STARTED: step=2/2 selected Blender weight repo=...
MODEL FETCH COMPLETE: step=2/2 selected Blender weight repo=...
```

The module should persist the selected generation preset in a module-owned config
file under a shared data root, for example:

```text
$HOME/NymphsData/config/<module-id>/
```

The Manager should not infer model readiness by scanning large model caches at
startup. Startup installed state comes from the module install marker. Model
readiness belongs in module-owned status/fetch scripts and user-triggered
actions.

### Smoke Test Result UI

Smoke tests are validation actions, so the UI must make pass/fail obvious.

The Manager should render successful smoke tests like:

```text
<Module Name>: SMOKE TEST PASSED
SUCCESS: backend started, answered /server_info, and stopped cleanly.
```

Do not show only `finished`. A user should not have to read JSON or logs to know
whether a test passed.

Module-owned smoke test scripts should print concise raw evidence after the
success line, such as the server URL and `/server_info` response. It is valid
for `/server_info` to report `loaded_model_id=null` if the smoke test only checks
that the backend starts and answers health/config endpoints. A generation test
is a separate, heavier validation.

## Shared Secrets

Module UI pages and action groups must not print secrets into logs or bake them
into installed HTML.

The standard shared Hugging Face token path is:

```text
%LOCALAPPDATA%\NymphsCore\shared-secrets.json
```

Modules should request this with a declared secret field:

```json
{
  "name": "hf_token",
  "type": "secret",
  "label": "Hugging Face token",
  "secret_id": "huggingface.token",
  "env": "NYMPHS3D_HF_TOKEN",
  "optional": true
}
```

The Manager saves the token once and passes it into the module action
environment as `NYMPHS3D_HF_TOKEN`. The token itself must not be logged.

## Z-Image Example

`nymphnerds/zimage` is the current proof for compact native model fetch controls.
It does not need a custom WebView2 page for model selection.

The installed Z-Image manifest declares:

- a `Model Fetch` action group
- a persistent `Hugging Face token` secret field
- a `Download` selector for Nunchaku-compatible Z-Image Turbo weights
- source links for the base model and quantized weights
- simple module actions for `Smoke Test`, `Start`, `Stop`, and `Logs`

The Z-Image Fetch Models action currently offers all published compatible
weights: INT4 r32/r128/r256 and FP4 r32/r128. `All weights` is available so the
Blender addon can switch between presets later. These are generation weights,
not LoRA training precision.
