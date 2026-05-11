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

## Z-Image Example

`nymphnerds/zimage` declares `ui.manager_ui.entrypoint` as `ui/manager.html`. That file lives in the module repo and is copied into the installed module root by the module installer.

The Manager only hosts that local file after Z-Image is installed.
