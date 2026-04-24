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
- one primary local `Plan` model role
- one optional local `Act` model role
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

- start or stop the Brain stack
- start or stop Open WebUI
- inspect `LLM Server`, `MCP Gateway`, `Open WebUI`, and `Current Model`
- enter an optional OpenRouter key for `llm-wrapper`
- open the role-aware `Manage Models` flow for local and remote model choices
- inspect the `Brain activity` log
- update the Brain stack from the launcher

The primary Brain button is an all-stop safety control when any Brain service is running. If LLM, MCP, or WebUI is active, it shows `Stop Brain` and stops the active pieces it can manage.

`Runtime Tools` is for the Blender backend runtimes. `Brain` is the separate Brain control page.

## OpenRouter Key And Optional Wrapper

`llm-wrapper` is optional.

If you want remote OpenRouter-backed delegation inside Brain:

1. open the `Brain` page in NymphsCore Manager
2. paste an OpenRouter API key into the one-line `OpenRouter key` field
3. click `Apply Key`
4. use `Manage Models` to pick the remote `llm-wrapper` model
5. start Brain or run `Update Stack`

If no key is configured, Brain omits `llm-wrapper` from MCP, mcpo, Open WebUI, and generated Cline settings while keeping the rest of the stack running.

## Core Commands

Useful commands inside WSL:

```text
/home/nymph/Nymphs-Brain/bin/lms-start
/home/nymph/Nymphs-Brain/bin/lms-stop
/home/nymph/Nymphs-Brain/bin/lms-model
/home/nymph/Nymphs-Brain/bin/lms-get-profile
/home/nymph/Nymphs-Brain/bin/lms-set-profile
/home/nymph/Nymphs-Brain/bin/brain-refresh
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
Plan model: qwen3.5-9b (context 16384)
Act model: none (context none)
MCP proxy: running|stopped
Open WebUI: running|stopped
```

`Open WebUI` does not need to be running for Cline MCP to work.

If the optional wrapper is enabled, `brain-status` also shows:

```text
Remote llm-wrapper model: openai/gpt-4o-mini
```

## Model Roles: Act And Plan

Brain no longer assumes a single generic selected model, and the Lite branch is now plan-first.

It supports:

- `Plan`: the primary local planning model
- `Act`: an optional local execution/coding model

This makes it a better fit for workflows where Cline or another client uses a local planning model but keeps action/execution on an external provider.

If `Act` is blank, Brain loads only the local `Plan` model. If both are configured, Brain loads `Plan` first, then `Act`.

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

- set a downloaded model as `Plan`
- set a downloaded model as `Act`
- download a new model for `Plan`
- download a new model for `Act`
- choose the remote OpenRouter model used by `llm-wrapper`
- enter a custom remote OpenRouter model id
- clear `Plan`
- clear `Act`
- clear the remote `llm-wrapper` model override
- remove downloaded LM Studio model folders

The manager flow now updates the saved role config safely for:

- local `Plan`
- local `Act`
- remote `llm-wrapper`

It does not immediately unload and reload models during selection.

After changing roles, restart the Brain LLM to apply the saved configuration.

Removing a model unloads it if possible, deletes the matching folder under `~/.lmstudio/models`, and clears the saved Plan/Act profile if that profile pointed at the removed model. LM Studio's CLI does not currently expose an `rm` command, so Brain handles removal through the local model folder.

## Direct Profile Commands

Inspect the current saved roles:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-get-profile"
```

Example: set `Plan` to Qwen 3.5 9B and leave `Act` external:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-set-profile plan qwen3.5-9b 16384"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-set-profile act clear"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-get-profile"
```

Expected output shape:

```text
plan: qwen3.5-9b (context 16384)
act: none (context none)
remote llm-wrapper: openai/gpt-4o-mini
```

If you want a separate local Act model too:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-set-profile act qwen/qwen2.5-coder-14b 65536"
```

You can also clear `Act`:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-set-profile act clear"
```

You can inspect or set the remote `llm-wrapper` model directly too:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-get-profile remote"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-set-profile remote openai/gpt-4o-mini"
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

Refresh the installed Brain wrapper scripts without changing model profiles:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/brain-refresh"
```

The Manager Brain page `Update Stack` action runs `brain-refresh` before LM Studio/Open WebUI updates, so repaired installs do not stay stuck on older plan/act wrapper logic.

Older Brain installs may not have `brain-refresh` yet. In that case, the Manager falls back to running the packaged Brain installer script once to seed the new wrapper while preserving saved Plan/Act profiles.

Update Open WebUI:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/open-webui-update"
```

From the Manager UI, the Brain page can drive the same flows without opening a shell.

## Optional OpenRouter Key For `llm-wrapper`

`llm-wrapper` is optional. The rest of the Brain stack does not depend on it.

From the Manager Brain page you can:

- enter an OpenRouter key in the `OpenRouter key` field
- click `Apply Key` to write `/home/nymph/Nymphs-Brain/secrets/llm-wrapper.env`
- then use `Start Brain` or `Update Stack` to regenerate the Brain MCP config with `llm-wrapper` enabled

If no OpenRouter key is present:

- Brain still starts normally
- `filesystem`, `memory`, `web-forager`, and `context7` remain available
- `llm-wrapper` is omitted from the generated MCP, mcpo, Cline, and Open WebUI config
- the bundled cached runtime is still written into the Brain install tree, but it stays unused until a key is present

Reference file:

```text
/home/nymph/Nymphs-Brain/secrets/llm-wrapper.env
/home/nymph/Nymphs-Brain/local-tools/remote_llm_mcp/cached_llm_mcp_server.py
```

## Using Open WebUI

Open WebUI is optional, but useful if you want a browser chat UI for the local Brain stack.

The Brain installer now starts both the MCP gateway and the `mcpo` OpenAPI bridge, then seeds the Brain tool connections into Open WebUI automatically when you run `open-webui-start`.

If an OpenRouter key has been applied, the seeded tool list includes `llm-wrapper`. If no key is present, Open WebUI is still seeded automatically, just without that one optional tool.

Reference files:

```text
/home/nymph/Nymphs-Brain/mcp/config/open-webui-mcp-servers.md
/home/nymph/Nymphs-Brain/mcp/config/open-webui-tool-servers.json
```

Managed URL:

```text
http://localhost:8081
```

Use it when you want:

- a browser-based local chat surface
- local experimentation without VS Code
- a simple way to confirm the LLM stack is alive

If you want to verify the optional wrapper directly instead of relying on chat-model tool selection:

```bash
curl -s http://127.0.0.1:8099/llm-wrapper/llm_call \
  -H 'Content-Type: application/json' \
  -d '{"prompt":"Reply with exactly DIRECT_WRAPPER_TEST_OK and nothing else."}'
```

Run the exact same request twice to check cache `MISS` then cache `HIT`.

## Using Cline With Brain

Use Cline with the Brain stack through the `OpenAI Compatible` provider, not the built-in `LM Studio` provider.

Cline LLM settings:

```text
API Provider: OpenAI Compatible
Base URL: http://localhost:1234/v1
OpenAI Compatible API Key: lm-studio
```

If you use separate Cline `Plan` and `Act` models:

- point Cline `Plan` at the Brain `Plan` model
- point Cline `Act` at the Brain `Act` model, or keep Act on an external provider

If Brain `Act` is blank, only the Brain `Plan` model will be loaded locally.

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

### Context7

```text
Server Name: context7_mcp
Server URL: http://localhost:8100/servers/context7/mcp
```

### LLM Wrapper

```text
Server Name: llm_wrapper_mcp
Server URL: http://localhost:8100/servers/llm-wrapper/mcp
```

This server is only present when an OpenRouter key has been configured.

When healthy, each enabled one should show a green status dot in Cline.

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

### The Brain page shows a partial running state

This can happen if only Open WebUI or MCP is running, or if LM Studio is running but the loaded model report is delayed. Use the primary `Stop Brain` button to stop all active Brain services from the Manager.

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
- plan-first, with a local `Plan` model and optional local `Act` model
- usable from Open WebUI and Cline through the same local endpoints
