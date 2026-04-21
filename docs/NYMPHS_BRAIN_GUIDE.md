# Nymphs-Brain Guide

`Nymphs-Brain` is the optional local LLM stack that NymphsCore Manager can install inside the managed `NymphsCore` WSL distro.

It is separate from the Blender 3D backend. You can skip it entirely if you only want Blender workflows.

When installed, Brain gives you:

- a local LM Studio-backed OpenAI-compatible LLM endpoint on `http://localhost:1234/v1`
- a local MCP gateway on `http://localhost:8100`
- Open WebUI on `http://localhost:8081`
- helper scripts under `/home/nymph/Nymphs-Brain/bin`
- a dedicated `Brain` page in NymphsCore Manager

## What Brain Is For

Brain is the local coding and tool stack for:

- Cline
- Open WebUI
- local chat and coding experiments
- MCP-powered filesystem, memory, and web-forager tools

The Blender addon does not depend on Brain.

## Install Location

The managed install lives here inside WSL:

```text
/home/nymph/Nymphs-Brain
```

Useful folders:

```text
/home/nymph/Nymphs-Brain/bin
/home/nymph/Nymphs-Brain/config
/home/nymph/Nymphs-Brain/mcp/config
/home/nymph/Nymphs-Brain/mcp/logs
```

## What Gets Installed

The current Brain stack includes:

- Linux-side LM Studio CLI/runtime wrappers
- one `Act` model role
- one optional `Plan` model role
- Open WebUI
- a local MCP proxy with:
  - `filesystem`
  - `memory`
  - `web-forager`

The normal managed endpoints are:

| Service | URL | Purpose |
|---|---|---|
| LLM | `http://localhost:1234/v1` | OpenAI-compatible local model endpoint |
| MCP | `http://localhost:8100` | local MCP gateway |
| Open WebUI | `http://localhost:8081` | browser chat UI |

## Manager Brain Page

If Brain was installed, NymphsCore Manager now exposes a dedicated `Brain` page.

Use it to:

- start or stop the Brain LLM stack
- start or stop Open WebUI
- inspect `LLM Server`, `MCP Gateway`, `Open WebUI`, and `Current Model`
- open the role-aware `Manage Models` flow
- inspect the `Brain activity` log
- update the Brain stack from the launcher

`Runtime Tools` is for the Blender backend runtimes. `Brain` is the separate Brain control page.

## Core Commands

Useful commands inside WSL:

```text
/home/nymph/Nymphs-Brain/bin/lms-start
/home/nymph/Nymphs-Brain/bin/lms-stop
/home/nymph/Nymphs-Brain/bin/lms-model
/home/nymph/Nymphs-Brain/bin/lms-get-profile
/home/nymph/Nymphs-Brain/bin/lms-set-profile
/home/nymph/Nymphs-Brain/bin/lms-update
/home/nymph/Nymphs-Brain/bin/mcp-start
/home/nymph/Nymphs-Brain/bin/mcp-stop
/home/nymph/Nymphs-Brain/bin/open-webui-start
/home/nymph/Nymphs-Brain/bin/open-webui-stop
/home/nymph/Nymphs-Brain/bin/open-webui-update
/home/nymph/Nymphs-Brain/bin/brain-status
```

From Windows PowerShell, the usual pattern is:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/brain-status"
```

## Checking Health

Use:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/brain-status"
```

Expected healthy output shape looks like:

```text
Brain install: installed
LLM server: running|stopped
Model loaded: ...
Act model: qwen/qwen2.5-coder-14b (context 65536)
Plan model: qwen3.5-9b (context 16384)
MCP proxy: running|stopped
Open WebUI: running|stopped
```

`Open WebUI` does not need to be running for Cline MCP to work.

## Model Roles: Act And Plan

Brain no longer assumes a single generic selected model.

It supports:

- `Act`: the main execution/coding model
- `Plan`: an optional lighter planning model

This makes it a better fit for tools like Cline that can use separate models for planning and action.

If `Plan` is blank, Brain loads only the `Act` model.

Profile config is stored here:

```text
/home/nymph/Nymphs-Brain/config/lms-model-profiles.env
```

## Managing Models

Use the Manager `Manage Models` button or run:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-model"
```

The current role-aware model manager lets you:

- set a downloaded model as `Act`
- set a downloaded model as `Plan`
- download a new model for `Act`
- download a new model for `Plan`
- clear `Act`
- clear `Plan`

The manager flow now updates the saved role config safely. It does not immediately unload and reload models during selection.

After changing roles, restart the Brain LLM to apply the saved configuration.

## Direct Profile Commands

Inspect the current saved roles:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-get-profile"
```

Example: set `Plan` to Qwen 3 9B and `Act` to Qwen 2.5 Coder 14B:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-set-profile plan qwen3.5-9b 16384"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-set-profile act qwen/qwen2.5-coder-14b 65536"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-get-profile"
```

Expected output shape:

```text
act: qwen/qwen2.5-coder-14b (context 65536)
plan: qwen3.5-9b (context 16384)
```

If you only want one model loaded, clear `Plan`:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-set-profile plan clear"
```

You can also clear `Act`:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-set-profile act clear"
```

Then restart the LLM:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-stop"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-start"
```

## Start, Stop, And Update

Start the LLM stack:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-start"
```

Stop the LLM stack:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-stop"
```

Start the MCP gateway:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/mcp-start"
```

Start Open WebUI:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/open-webui-start"
```

Update the Linux-side LM Studio/runtime layer:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-update"
```

Update Open WebUI:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/open-webui-update"
```

From the Manager UI, the Brain page can drive the same flows without opening a shell.

## Using Open WebUI

Open WebUI is optional, but useful if you want a browser chat UI for the local Brain stack.

Managed URL:

```text
http://localhost:8081
```

Use it when you want:

- a browser-based local chat surface
- local experimentation without VS Code
- a simple way to confirm the LLM stack is alive

## Using Cline With Brain

Use Cline with the Brain stack through the `OpenAI Compatible` provider, not the built-in `LM Studio` provider.

Cline LLM settings:

```text
API Provider: OpenAI Compatible
Base URL: http://localhost:1234/v1
OpenAI Compatible API Key: lm-studio
```

If you use separate Cline `Plan` and `Act` models:

- point Cline `Act` at the Brain `Act` model
- point Cline `Plan` at the Brain `Plan` model

If Brain `Plan` is blank, only the Brain `Act` model will be loaded.

Quick test:

```text
Reply with exactly these 5 words: local model test successful
```

Expected response:

```text
local model test successful
```

## Adding MCP Servers In Cline

Add these as `Streamable HTTP` remote servers:

### Filesystem

```text
Server Name: filesystem_mcp
Server URL: http://localhost:8100/servers/filesystem/mcp
```

### Memory

```text
Server Name: memory_mcp
Server URL: http://localhost:8100/servers/memory/mcp
```

### Web Forager

```text
Server Name: web_forager_mcp
Server URL: http://localhost:8100/servers/web-forager/mcp
```

When healthy, each one should show a green status dot in Cline.

## Installer-Generated Reference Files

Brain writes useful reference files here:

```text
/home/nymph/Nymphs-Brain/mcp/config/cline-mcp-settings.json
/home/nymph/Nymphs-Brain/mcp/config/mcp-proxy-servers.json
/home/nymph/Nymphs-Brain/config/lms-model-profiles.env
```

These are helpful when you want to compare Brain defaults with what a client such as Cline is using.

## Common Gotchas

### Brain page says the stack is installed but the services are not all running

That is normal. `Installed` means the Brain files exist. `LLM`, `MCP`, and `Open WebUI` can still be started or stopped independently.

### You changed `Act` or `Plan`, but the running model did not change

Saved profile changes are not applied until the Brain LLM is restarted.

Restart:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-stop"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-start"
```

### Cline still behaves like the old provider

Start a brand new Cline chat after changing provider or model settings. Old chats can keep older provider context.

### The built-in LM Studio provider says MCP is unsupported

That is expected. Use `OpenAI Compatible` for Brain + MCP.

### MCP tools do not connect

Check:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/mcp-start"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/brain-status"
```

## Summary

The Brain stack is now:

- a dedicated optional local LLM subsystem
- controlled from its own Manager page
- based on Linux-side LM Studio wrappers
- able to load one `Act` model and one optional `Plan` model
- usable from Open WebUI and Cline through the same local endpoints
