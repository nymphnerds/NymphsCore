# Nymphs-Brain Guide

`Nymphs-Brain` is the optional local LLM stack that NymphsCore Manager can install inside the managed `NymphsCore` WSL distro.

It is separate from the Blender 3D backend. You can skip it entirely if you only want Blender workflows.

When installed, Brain gives you:

- a local llama-server OpenAI-compatible LLM endpoint on `http://localhost:8000/v1`
- a local MCP gateway on `http://localhost:8100`
- an OpenAPI tool bridge on `http://localhost:8099`
- Open WebUI on `http://localhost:8081`
- helper scripts under `/home/nymph/Nymphs-Brain/bin`
- a dedicated `Brain` page in NymphsCore Manager

## What Brain Is For

Brain is the local coding and tool stack for:

- Cline
- Open WebUI
- local chat and coding experiments
- MCP-powered filesystem, memory, context7, web-forager, and optional remote LLM tools

The Blender addon does not depend on Brain.

## Install Location

The managed install lives here inside WSL:

```text
/home/nymph/Nymphs-Brain
```

Useful folders:

```text
/home/nymph/Nymphs-Brain/bin
/home/nymph/Nymphs-Brain/models
/home/nymph/Nymphs-Brain/mcp/config
/home/nymph/Nymphs-Brain/mcp/logs
/home/nymph/Nymphs-Brain/secrets
```

## What Gets Installed

The current Brain stack is a hybrid setup:

- LM Studio CLI is used for local model download and management
- `llama-server` serves the selected GGUF model on port `8000`
- Open WebUI provides a browser chat surface on port `8081`
- MCP proxy exposes tools on port `8100`
- `mcpo` exposes OpenAPI tool servers on port `8099`
- optional `llm-wrapper` delegates explicit tool calls to an OpenRouter model

The normal managed endpoints are:

| Service | URL | Purpose |
|---|---|---|
| LLM | `http://localhost:8000/v1` | OpenAI-compatible local model endpoint served by llama-server |
| MCP | `http://localhost:8100` | local MCP gateway |
| mcpo | `http://localhost:8099` | OpenAPI bridge for Open WebUI tools |
| Open WebUI | `http://localhost:8081` | browser chat UI |

## Manager Brain Page

If Brain was installed, NymphsCore Manager exposes a dedicated `Brain` page.

Use it to:

- start or stop the Brain stack
- start or stop Open WebUI
- inspect `LLM Server`, `MCP Gateway`, `Open WebUI`, local model, remote model,
  and OpenRouter key status
- enter an optional OpenRouter key for `llm-wrapper`
- open `Manage Models` for the local GGUF model, context length, and optional remote wrapper model
- inspect the Brain activity log
- update or repair the Brain module through the universal Manager lifecycle rail

The Brain module action row is state-aware. `Start Brain` hides while Brain is
running, `Stop Brain` hides while Brain is stopped, and the WebView2 Open WebUI
page swaps `Open WebUI` for a single `Close WebUI` action. `Browser` remains the
external browser fallback.

The left sidebar `// BRAIN MONITOR` shows live service telemetry plus the
configured model choices:

```text
LLM: Running
TPS: Waiting
Context: 32,768
Local: qwen/qwen3.5-9b
Remote: deepseek/deepseek-chat
```

`Runtime Tools` is for the Blender backend runtimes. `Brain` is the separate Brain control page.

## OpenRouter Key And Optional Wrapper

`llm-wrapper` is optional. The rest of the Brain stack works without it.

If you want remote OpenRouter-backed delegation inside Brain:

1. open the `Brain` page in NymphsCore Manager
2. paste an OpenRouter API key into the one-line `OpenRouter key` field
3. click `Apply Key`
4. use `Manage Models` to pick the remote `llm-wrapper` model
5. start Brain, or use the universal Update/Repair controls if the module files need refreshing

If no key is configured, Brain omits `llm-wrapper` from MCP, mcpo, Open WebUI, and generated Cline settings while keeping the rest of the stack running.

The saved remote wrapper config lives here:

```text
/home/nymph/Nymphs-Brain/secrets/llm-wrapper.env
```

## Core Commands

Useful commands inside WSL:

```text
/home/nymph/Nymphs-Brain/bin/lms-start
/home/nymph/Nymphs-Brain/bin/lms-stop
/home/nymph/Nymphs-Brain/bin/lms-status
/home/nymph/Nymphs-Brain/bin/lms-model
/home/nymph/Nymphs-Brain/bin/lms-update
/home/nymph/Nymphs-Brain/bin/mcp-start
/home/nymph/Nymphs-Brain/bin/mcp-stop
/home/nymph/Nymphs-Brain/bin/mcp-status
/home/nymph/Nymphs-Brain/bin/open-webui-start
/home/nymph/Nymphs-Brain/bin/open-webui-stop
/home/nymph/Nymphs-Brain/bin/open-webui-status
/home/nymph/Nymphs-Brain/bin/open-webui-update
/home/nymph/Nymphs-Brain/bin/brain-status
/home/nymph/Nymphs-Brain/bin/nymph-chat
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
llama-server: running on port 8000|stopped
Model loaded: ...
Remote llm-wrapper model: deepseek/deepseek-chat
MCP proxy: running|stopped
Open WebUI: running|stopped
```

`Open WebUI` does not need to be running for Cline MCP to work.

## Managing Models

Use the Manager `Manage Models` button or run:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-model"
```

The current model manager lets you:

- set a downloaded local model
- download a new local model through LM Studio CLI
- choose the local context length
- choose the remote OpenRouter model used by `llm-wrapper`
- enter a custom remote OpenRouter model id
- clear the local model
- remove downloaded local models

The manager flow updates:

- `/home/nymph/Nymphs-Brain/bin/lms-start` for the selected local model and context length
- `/home/nymph/Nymphs-Brain/nymph-agent.py` for the selected model id
- `/home/nymph/Nymphs-Brain/secrets/llm-wrapper.env` for the optional remote wrapper model

It does not immediately unload and reload models during selection. After changing the local model or context length, restart the Brain LLM to apply the saved configuration.

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-stop"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-start"
```

## Start, Stop, And Update

Start the local LLM server:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-start"
```

Stop the local LLM server:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-stop"
```

Check the local LLM server:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-status"
```

Start the MCP gateway:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/mcp-start"
```

Start Open WebUI:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/open-webui-start"
```

Update the Linux-side runtime layer:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-update"
```

The modular Manager uses the universal module Update/Repair controls to refresh
Brain module files from the trusted registry. This replaces the old hardcoded
`Update Stack` button.

Update Open WebUI:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/open-webui-update"
```

From the Manager UI, the Brain page can drive the same flows without opening a shell.

## Using Open WebUI

Open WebUI is optional, but useful if you want a browser chat UI for the local Brain stack.

The Brain installer starts both the MCP gateway and the `mcpo` OpenAPI bridge, then seeds the Brain tool connections into Open WebUI automatically when you run `open-webui-start`.

If an OpenRouter key has been applied, the seeded tool list includes `llm-wrapper`. If no key is present, Open WebUI is still seeded automatically, just without that one optional tool.

Managed URL:

```text
http://localhost:8081
```

If you want to verify the optional wrapper directly:

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
Base URL: http://localhost:8000/v1
OpenAI Compatible API Key: nymphs-brain
```

If you use separate Cline planning and acting models:

- point the local Cline role at the Brain model served on port `8000`
- keep the other role on an external provider if you want more than one active model
- use the optional `llm-wrapper` MCP tool for explicit remote delegation from the local Brain workflow

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

## Installer-Generated Reference Files

Brain writes useful reference files here:

```text
/home/nymph/Nymphs-Brain/mcp/config/cline-mcp-settings.json
/home/nymph/Nymphs-Brain/mcp/config/mcp-proxy-servers.json
/home/nymph/Nymphs-Brain/mcp/config/open-webui-tool-servers.json
/home/nymph/Nymphs-Brain/bin/lms-start
/home/nymph/Nymphs-Brain/secrets/llm-wrapper.env
```

These are helpful when you want to compare Brain defaults with what a client such as Cline is using.

## Common Gotchas

### Brain page says the stack is installed but the services are not all running

That is normal. `Installed` means the Brain files exist. `LLM`, `MCP`, and `Open WebUI` can still be started or stopped independently.

### You changed the local model, but the running model did not change

Saved model changes are not applied until the Brain LLM is restarted.

Restart:

```powershell
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-stop"
wsl -d NymphsCore --user nymph -- bash -lc "/home/nymph/Nymphs-Brain/bin/lms-start"
```

### The Brain page shows a partial running state

This can happen if only Open WebUI or MCP is running, or if llama-server is running but the loaded model report is delayed. Use the primary `Stop Brain` button to stop all active Brain services from the Manager.

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
- based on LM Studio CLI for model management and llama-server for inference
- configured around one local GGUF model plus an optional remote `llm-wrapper` model
- usable from Open WebUI and Cline through the same local endpoints
