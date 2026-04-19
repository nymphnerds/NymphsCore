# Nymphs-Brain Installer, Open WebUI, and MCP Handoff

Date: 2026-04-19

This handoff summarizes the Nymphs-Brain installer investigation, the Python/system dependency fix, the recommended rebuild path, and the Open WebUI + MCP integration work.

## Summary

The Nymphs-Brain install issue is real, but it does not require manually bundling a standalone Python distribution into the tar.

The safer fix is:

1. Ensure the Manager runs a small system-dependencies-only pass before optional Nymphs-Brain install.
2. Ensure the Brain installer selects a Python interpreter that can create venvs.
3. Optionally rebuild `NymphsCore.tar` so the base distro already includes Python venv support.
4. Install Open WebUI and MCP bridge tooling inside `/home/nymph/Nymphs-Brain`.
5. Add simple Nymphs-Brain controls to the Runtime Tools page under the live log.

The Manager app must be rebuilt on Windows for the C# flow changes to ship. Rebuilding the tar is recommended, but not strictly required.

Current working tree status:

- Python/system dependency fixes are implemented.
- Open WebUI + MCP install script generation is implemented.
- Runtime Tools under-log Nymphs-Brain controls are implemented in source.
- The Windows Manager executable still needs to be rebuilt on Windows.
- `NymphsCore.tar` rebuild is still optional but recommended.

## What Was Fixed

### Brain Installer

Updated:

```text
scripts/install_nymphs_brain.sh
```

The script now:

- Detects a venv-capable Python, preferring `python3.11`, then `python3.10`, then `python3`.
- Requires Python venv support before creating the Brain environment.
- Recreates an incomplete Brain venv if `python3` or `pip` is missing inside it.
- Generates these Brain commands:
  - `lms-start`
  - `lms-stop`
  - `lms-model`
  - `nymph-chat`
  - `brain-env`
- Integrates the useful behavior from the separate `lms-model` / `lms-stop` helper scripts into the real installer flow.
- Avoids a `jq` dependency by using Python for JSON updates in `lms-model`.

The root-level scripts inspected during the investigation were:

```text
this one_install_nymphs_brain.sh
lms-model
lms-stop
```

Those files were not the best long-term place for this behavior. The useful pieces were folded into `scripts/install_nymphs_brain.sh`.

### Base Distro Creation

Updated:

```text
scripts/create_builder_distro.ps1
```

The distro creation/bootstrap now includes:

```text
python3
python3-venv
python3-pip
```

This means a rebuilt `NymphsCore.tar` should already have Python venv support available.

The base tar should include source checkouts for active managed backends,
including `Hunyuan3D-2`, `Z-Image`, and `TRELLIS.2`. It should not include the
legacy `Hunyuan3D-Part` checkout.

### System Dependency Install

Updated:

```text
scripts/install_system_deps.sh
```

The system dependency set now includes Python and venv support:

```text
python3
python3-venv
python3-pip
```

This is the fallback/repair path for existing lean distros.

### Manager Flow

Updated:

```text
apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs
apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs
```

The Manager now has a system-dependencies-only pass available before optional module install.

`InstallerWorkflowService.cs` gained:

```text
RunSystemDependenciesOnlyAsync(...)
```

That calls the existing finalize script with:

```text
-SystemOnly
```

`MainWindowViewModel.cs` now runs that pass before Nymphs-Brain install when installing Brain in module-only mode.

The user-facing log should show:

```text
Preparing system dependencies for optional modules...
```

before the Brain installer runs.

## Publish Folder Sync

The updated scripts were also copied into:

```text
apps/Nymphs3DInstaller/publish/win-x64/scripts/
```

The checked-in publish zip was updated for these script entries:

```text
apps/Nymphs3DInstaller/publish/NymphsCoreManager-win-x64.zip
```

However, the Windows executable itself still needs to be rebuilt on Windows so the C# Manager changes are included.

## Rebuild Guidance

### Required

Rebuild the Windows Manager app on Windows.

This is required because the Manager C# source changed.

### Recommended

Rebuild `NymphsCore.tar`.

This is recommended because the base distro will already include Python venv support, making Nymphs-Brain install faster and cleaner.

### Not Required

Do not manually bundle an unrelated Python installation into the tar.

The distro should install normal Ubuntu Python packages:

```text
python3
python3-venv
python3-pip
```

## Runtime Tools UI Direction

Do not add a fourth large backend card for Nymphs-Brain.

The existing three-card layout should remain:

```text
[Hunyuan 2mv] [Z-Image] [TRELLIS.2]
```

Instead, add a simple Nymphs-Brain section underneath the live log.

Target layout:

```text
Runtime Tools

[Hunyuan 2mv] [Z-Image] [TRELLIS.2]

Live log
------------------------------------------------
...

Nymphs-Brain
------------------------------------------------
Status: Installed / Server stopped / Model loaded
Model: qwen3-1.7b

Start LLM  /  Open WebUI  /  Change Model  /  Stop LLM
```

The actions can be text-link style, separated by `/`, rather than large buttons. This keeps the page lighter and avoids disrupting the existing card design.

Suggested actions:

- `Start LLM`: run `~/Nymphs-Brain/bin/lms-start`
- `Stop LLM`: run `~/Nymphs-Brain/bin/lms-stop`
- `Change Model`: open a terminal running `~/Nymphs-Brain/bin/lms-model`
- `Open WebUI`: start or open Open WebUI in the Windows browser

## Open WebUI Implementation

Use WSL-native Open WebUI, not Docker-first.

Reasoning:

- Nymphs-Brain already lives in WSL.
- LM Studio server tooling is being managed from WSL.
- MCP tooling is already installed under Nymphs-Brain.
- Docker Desktop would add another dependency and failure mode.

Implemented layout:

```text
/home/nymph/Nymphs-Brain/
  open-webui-venv/
  open-webui-data/
  bin/
    open-webui-start
    open-webui-stop
    open-webui-status
```

Open WebUI is configured to connect to the local OpenAI-compatible LM Studio endpoint:

```text
http://127.0.0.1:1234/v1
```

API key can be:

```text
lm-studio
```

or blank, depending on the final Open WebUI configuration path.

## MCP Implementation

The local Brain install already has these MCP packages:

```text
/home/nymph/Nymphs-Brain/npm-global/lib/node_modules/@modelcontextprotocol/server-filesystem
/home/nymph/Nymphs-Brain/npm-global/lib/node_modules/@modelcontextprotocol/server-memory
```

These are stdio MCP servers. Open WebUI is easier to wire up if they are bridged through `mcpo`, which exposes MCP servers as OpenAPI-compatible tool servers.

The installer now installs:

```text
mcpo
web-forager
```

`web-forager` is the likely web search/fetch MCP package mentioned during discussion. It provides web search/fetch-style tools and can be served through the same MCP bridge.

Implemented layout:

```text
/home/nymph/Nymphs-Brain/
  mcp-venv/
  mcp/
    config/
      mcpo.json
      open-webui-tool-connections.json
    data/
      memory.jsonl
    logs/
      mcpo.log
      web-forager.log
      filesystem.log
```

Generated `mcpo.json` shape:

```json
{
  "mcpServers": {
    "filesystem": {
      "command": "/home/nymph/Nymphs-Brain/npm-global/bin/mcp-server-filesystem",
      "args": [
        "/home/nymph/NymphsCore",
        "/home/nymph/Nymphs-Brain"
      ]
    },
    "memory": {
      "command": "/home/nymph/Nymphs-Brain/npm-global/bin/mcp-server-memory",
      "env": {
        "MEMORY_FILE_PATH": "/home/nymph/Nymphs-Brain/mcp/data/memory.jsonl"
      }
    },
    "web-forager": {
      "command": "/home/nymph/Nymphs-Brain/mcp-venv/bin/web-forager",
      "args": ["serve"]
    }
  }
}
```

The `mcpo` config follows the Claude Desktop-style `mcpServers` schema documented by Open WebUI/mcpo.

Generated commands:

```text
~/Nymphs-Brain/bin/mcp-start
~/Nymphs-Brain/bin/mcp-stop
~/Nymphs-Brain/bin/mcp-status
~/Nymphs-Brain/bin/open-webui-start
~/Nymphs-Brain/bin/open-webui-stop
~/Nymphs-Brain/bin/open-webui-status
~/Nymphs-Brain/bin/brain-status
```

## Network Exposure

Default goal:

```text
Windows browser -> localhost -> WSL Open WebUI
```

Avoid default LAN exposure.

Recommended defaults:

- Bind Open WebUI to localhost unless wider access is explicitly enabled.
- Bind MCP bridge to localhost by default.
- Protect MCP with an API key if it is exposed beyond localhost.
- Restrict filesystem MCP roots carefully.

The filesystem MCP can read/write files. Do not expose broad filesystem access to the network by default.

If Windows cannot reach WSL services through `localhost`, use the WSL IP from:

```bash
hostname -I
```

or add a Windows portproxy/firewall rule later.

## Implemented Manager UI Pass

Updated:

```text
scripts/install_nymphs_brain.sh
```

to add:

- `mcp-venv`
- `mcpo`
- `web-forager`
- `mcp-start`
- `mcp-stop`
- `mcp-status`
- Open WebUI venv/install
- `open-webui-start`
- `open-webui-stop`
- `open-webui-status`
- generated MCP config under `/home/nymph/Nymphs-Brain/mcp/config/`

The Manager Runtime Tools UI now has the under-log Nymphs-Brain status/action section:

```text
Nymphs-Brain
Status: Installed / LLM stopped / WebUI stopped / MCP stopped
Model: qwen3-1.7b

Start LLM  /  Open WebUI  /  Change Model  /  Stop LLM
```

Manager files touched for UI work:

```text
apps/Nymphs3DInstaller/Views/MainWindow.xaml
apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs
apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs
```

## Test Checklist

After rebuilding the Manager app on Windows:

1. Launch NymphsCore Manager.
2. Install or open the WSL distro.
3. Install Nymphs-Brain from Manager.
4. Confirm the log shows:

```text
Preparing system dependencies for optional modules...
```

5. Confirm these commands exist:

```bash
~/Nymphs-Brain/bin/lms-start
~/Nymphs-Brain/bin/lms-stop
~/Nymphs-Brain/bin/lms-model
~/Nymphs-Brain/bin/nymph-chat
~/Nymphs-Brain/bin/brain-env
```

6. Test:

```bash
~/Nymphs-Brain/bin/lms-start
~/Nymphs-Brain/bin/lms-model
~/Nymphs-Brain/bin/lms-stop
```

After the Open WebUI/MCP pass:

```bash
~/Nymphs-Brain/bin/mcp-start
~/Nymphs-Brain/bin/open-webui-start
```

Then open the WebUI from the Windows browser.

## Bottom Line

The colleague was correct that Nymphs-Brain needed a Python/system dependency fix.

The recommended solution is not to bundle a separate Python manually. The recommended solution is to:

- install normal Python system packages in the distro,
- run system dependencies before optional Brain install,
- make the Brain installer verify venv-capable Python,
- optionally rebuild the tar for a cleaner base image,
- install Open WebUI and MCP tools inside `/home/nymph/Nymphs-Brain`,
- expose simple Brain controls in the Manager Runtime Tools page.
