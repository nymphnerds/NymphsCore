# Nymph Module UI Standard

This is the Manager/module boundary for custom module UI.

## Rule

The Manager owns the shell, registry cards, install/update/uninstall controls, right rail, logs, and generic action routing.

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

## WebView2 Roadmap

The Manager should move from WPF `WebBrowser` to Microsoft Edge WebView2 for module UI hosting.

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

For static local apps, use WebView2 virtual host mapping instead of raw `file://` when possible:

```text
https://<module-id>.nymphs.invalid/index.html
```

mapped to:

```text
<installed module root>/ui/dist
```

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

## Z-Image Example

`nymphnerds/zimage` declares `ui.manager_ui.entrypoint` as `ui/manager.html`. That file lives in the module repo and is copied into the installed module root by the module installer.

The Manager only hosts that local file after Z-Image is installed.
