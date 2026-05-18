# Blender MCP + Nymphs-Brain Architecture Handoff

**Repo target:** `Manager/docs/blender_mcp_brain_architecture_handoff.md`  
**Branch analyzed:** `codex/brain-webui-mcp-manager`  
**Analysis date:** April 19, 2026

---

## Executive Summary

Yes — **Nymphs-Brain could realistically operate a Blender MCP to control the Blender-side process**.

The current branch already has the right high-level shape for this:

- `NymphsCore Manager` owns the managed WSL backend runtime and installs `TRELLIS.2`, `Hunyuan 2mv`, and `Z-Image / Nunchaku` as dedicated local runtimes.
- `Nymphs-Brain` is an optional local LLM layer installed under `/home/nymph/Nymphs-Brain`.
- The current Brain work already installs MCP-related tooling, generates Brain-side MCP config, and is explicitly moving toward **one Brain-managed MCP stack** using **Streamable HTTP** endpoints shared by clients like Cline and Open WebUI.
- LM Studio can host MCP servers, and Open WebUI can connect to MCP servers over **MCP (Streamable HTTP)**.

That means a future design where:

**Brain plans** → **Blender MCP executes scene/addon actions** → **Manager-owned runtimes do the heavy model work**

is not only plausible, but is probably the cleanest long-term architecture for the project.

---

## Direct Answer

**Could the Brain operate a Blender MCP to control the process?**

**Yes.**

**Should it replace the Manager’s ownership of TRELLIS.2, Z-Image, model installs, CUDA env setup, or runtime repair?**

**No.**

The best boundary is:

- **Nymphs-Brain / LM Studio / Open WebUI** = natural-language planning and tool orchestration
- **Blender MCP** = Blender scene control and addon/operator execution
- **Manager / WSL runtime stack** = installation, health checks, model fetching, backend startup, repairs, and low-level runtime ownership

---

## Current Branch Signals That This Fits

### 1. Manager already owns the heavy local runtimes

The Manager README describes `NymphsCore Manager` as the Windows setup and repair app for the local backend runtime used by the Blender addon. It imports and maintains a dedicated WSL distro and prepares:

- `TRELLIS.2`
- `Hunyuan 2mv`
- `Z-Image / Nunchaku`
- CUDA / Python environments / helper scripts / runtime checks

This strongly suggests the Manager is intended to remain the **runtime owner**, not just a launcher.  
Source: [Manager README](https://github.com/nymphnerds/NymphsCore/tree/codex/brain-webui-mcp-manager/Manager)

### 2. Runtime Tools already separate runtime readiness from user workflow

`runtime_tools_status.sh` checks `Z-Image` and `TRELLIS.2` independently, including whether the runtime environment exists and whether required model files are present. That means the current system already has a clean seam between:

- backend environment ownership
- user-visible orchestration
- runtime availability checks

This is exactly the kind of boundary a Brain + Blender MCP layer can sit on top of.  
Source: [runtime_tools_status.sh](https://github.com/nymphnerds/NymphsCore/blob/codex/brain-webui-mcp-manager/Manager/scripts/runtime_tools_status.sh)

### 3. Nymphs-Brain already installs MCP tooling

The current `install_nymphs_brain.sh` installs or configures:

- LM Studio CLI
- MCP helper packages like `@modelcontextprotocol/server-filesystem` and `@modelcontextprotocol/server-memory`
- a Brain-side MCP config
- helper scripts like `mcp-start`, `mcp-stop`, and `mcp-status`
- Open WebUI wiring that starts MCP before starting the WebUI

That means the Brain stack is already being shaped as a **tool-using orchestration layer**, not only as a local chat box.  
Source: [install_nymphs_brain.sh](https://github.com/nymphnerds/NymphsCore/blob/codex/brain-webui-mcp-manager/Manager/scripts/install_nymphs_brain.sh)

### 4. The repo handoff already moves toward one Brain-managed MCP stack

The Brain handoff doc explicitly says the newer preferred MCP shape is:

- one Brain-managed MCP stack
- one shared client transport
- Streamable HTTP for both Cline and Open WebUI

and gives local endpoint examples like:

- `http://localhost:8100/servers/filesystem/mcp`
- `http://localhost:8100/servers/memory/mcp`
- `http://localhost:8100/servers/web-forager/mcp`

This is important because a future Blender MCP fits naturally into the same design, for example:

- `http://localhost:8100/servers/blender/mcp`

Source: [nymphs_brain_installer_webui_mcp_handoff.md](https://github.com/nymphnerds/NymphsCore/blob/codex/brain-webui-mcp-manager/Manager/docs/nymphs_brain_installer_webui_mcp_handoff.md)

---

## External Tooling Reality Check

### LM Studio

LM Studio supports MCP servers and can expose them to models. Its docs describe LM Studio as an MCP host and support both local and remote MCP servers via `mcp.json`. Its API docs also support MCP-enabled model requests.  
Sources:

- [LM Studio MCP docs](https://lmstudio.ai/docs/app/mcp)
- [LM Studio MCP via API](https://lmstudio.ai/docs/developer/core/mcp)

### Open WebUI

Open WebUI supports **native MCP using Streamable HTTP only**. That aligns well with your Brain handoff direction, which is already moving toward Streamable HTTP endpoints instead of fragmented local-only transport assumptions.  
Source: [Open WebUI MCP docs](https://docs.openwebui.com/features/extensibility/mcp/)

### Blender MCP ecosystem

There is already precedent for Blender MCP servers that let an LLM control Blender actions, inspect scenes, manipulate objects/materials, and bridge Blender through an addon + MCP server pattern. That confirms the overall pattern is viable and not hypothetical.  
Examples:

- [ahujasid/blender-mcp](https://github.com/ahujasid/blender-mcp)
- [poly-mcp/Blender-MCP-Server](https://github.com/poly-mcp/Blender-MCP-Server)

These projects should be treated as **reference patterns**, not necessarily as production dependencies.

---

## Recommended Architecture

```text
User
  ↓
Nymphs-Brain (LM Studio and/or Open WebUI)
  ↓
Brain-managed MCP gateway (Streamable HTTP)
  ↓
Blender MCP server
  ↓
Blender addon / Nymphs operators / scene actions
  ↓
Manager-owned backend services
  ├─ TRELLIS.2 jobs
  ├─ Z-Image jobs
  └─ Hunyuan 2mv jobs
```

### Control boundary

**Brain should decide what to do.**

Examples:

- create scene shell
- import reference images
- run addon operator
- send asset to backend
- apply generated textures
- export result

**Blender MCP should execute Blender-facing actions.**

Examples:

- add object
- modify transforms
- assign materials
- invoke registered Blender operators
- inspect current scene state
- trigger export

**Manager/backend services should still own the heavy runtime operations.**

Examples:

- install or repair `TRELLIS.2`
- install or repair `Z-Image`
- check runtime readiness
- fetch missing models
- start/stop backend servers
- manage CUDA/Python/WSL dependencies

---

## Why This Boundary Is Better Than Letting Blender MCP Own Everything

### Good reasons to use Blender MCP

A Blender MCP is a strong fit for:

- scene assembly
- object creation and selection
- material application
- viewport/scene inspection
- export actions
- calling Blender addon operators
- interactive workflow steering from a local LLM

### Bad reasons to overload Blender MCP

A Blender MCP is the wrong place to own:

- CUDA dependency management
- WSL repair logic
- Python environment provisioning for ML backends
- large model download/prefetch strategy
- runtime smoke-test logic
- backend health checks and version compatibility logic

Those belong in the Manager because the Manager already does them today.

---

## What “Operate the Process” Should Mean

In this architecture, “the Brain operates the process” should mean:

1. understand the user’s goal
2. choose a safe sequence of tools
3. call Blender tools to prepare the scene or collect inputs
4. call backend-facing tools to trigger model jobs
5. receive results back into Blender
6. continue scene refinement
7. export final deliverables

That gives the Brain real workflow control **without** moving low-level system ownership into a chat-driven agent layer.

---

## MVP Recommendation

### Phase 1: Safe Blender MCP only

Build a **narrow** Blender MCP first.

Recommended initial tools:

- `get_scene_summary`
- `list_objects`
- `select_object`
- `create_primitive`
- `set_transform`
- `assign_material`
- `import_image`
- `export_glb`
- `run_nymphs_operator`
- `save_blend`

At this stage, keep backend calls minimal. Let Brain prove it can safely control Blender.

### Phase 2: Backend orchestration hooks

Add MCP tools or backend proxy tools such as:

- `check_runtime_status`
- `run_trellis_job`
- `run_zimage_job`
- `fetch_missing_models`
- `attach_generated_asset_to_scene`

These should call **existing Manager/backend entry points** rather than bypass them.

### Phase 3: End-to-end workflow agent

Then Brain can execute higher-level workflows like:

- “Take this concept image and block out a scene.”
- “Run TRELLIS on this reference and import the mesh.”
- “Generate a matching texture pass in Z-Image and apply it.”
- “Export a GLB and save the Blender file.”

---

## Security and Safety Recommendations

This part matters.

LM Studio explicitly warns that MCP servers can access local files, run code, and use the network. Your own Brain handoff also warns to keep services on localhost and to restrict filesystem roots carefully.

### Strong recommendation

Do **not** expose a broad “execute arbitrary Blender Python” tool in v1.

Even though some community Blender MCP servers support that, it is too broad for a productized local workflow unless you layer strong trust controls around it.

### Prefer explicit tools instead

Examples:

- `import_reference_image`
- `run_nymphs_extract_parts`
- `apply_material_pack`
- `export_active_scene`
- `send_selected_mesh_to_backend`

### Network defaults

- bind Blender MCP to localhost by default
- bind the Brain MCP gateway to localhost by default
- do not expose broad filesystem roots
- require explicit opt-in for LAN exposure
- keep any credentialed tools off by default

---

## Transport Recommendation

Use **Streamable HTTP** end to end where practical.

Why:

- Open WebUI natively supports MCP via Streamable HTTP
- the Brain handoff already points toward one shared Streamable HTTP transport
- it reduces transport fragmentation across clients
- it matches the repo’s newer preferred MCP direction

Recommended local endpoint shape:

- `http://localhost:8100/servers/blender/mcp`

If Blender-side transport is initially easier as stdio or a socket bridge, that is acceptable internally, but expose it through the same Brain-managed MCP gateway so the clients still see one consistent transport model.

---

## Implementation Options

### Option A — Wrap an external Blender MCP project

Use an existing Blender MCP implementation as a starting point, then narrow/rename its tools to match NymphsCore semantics.

**Pros**

- faster prototype
- existing patterns for addon + server communication
- useful for proving Brain-driven orchestration

**Cons**

- tool surface may be too broad
- security posture may not match your needs
- may expose generic Blender actions instead of Nymphs-focused workflow actions

### Option B — Build a Nymphs-specific Blender MCP

Create a small Blender addon + MCP server pair specifically for NymphsCore.

**Pros**

- clean tool naming
- easier to keep safe and narrow
- better alignment with Manager/Brain terminology
- easier to map to existing addon operators and backend workflow steps

**Cons**

- slower initial build
- more maintenance owned by the project

### Recommendation

Prototype with **Option A-inspired architecture**, but productize with **Option B-style narrow tools**.

---

## Suggested Tool Surface for a Nymphs-Specific Blender MCP

### Scene inspection

- `get_scene_summary`
- `get_selected_objects`
- `get_object_hierarchy`
- `get_material_summary`
- `capture_viewport_snapshot`

### Scene editing

- `create_primitive`
- `delete_object`
- `duplicate_object`
- `set_transform`
- `frame_camera_on_selection`

### Asset IO

- `import_glb`
- `import_fbx`
- `import_reference_image`
- `export_glb`
- `export_fbx`
- `save_blend`

### Nymphs addon actions

- `run_nymphs_operator`
- `send_reference_to_trellis`
- `apply_generated_texture_set`
- `attach_generated_mesh`
- `run_retexture_workflow`

### Backend coordination

- `check_backend_status`
- `start_backend_if_needed`
- `submit_backend_job`
- `get_backend_job_status`

The last group can be direct MCP tools, or thin wrappers that call Manager-owned scripts/services.

---

## Recommended File / Repo Touchpoints

### Existing files already relevant

- `Manager/scripts/install_nymphs_brain.sh`
- `Manager/scripts/runtime_tools_status.sh`
- `Manager/docs/nymphs_brain_installer_webui_mcp_handoff.md`
- `Manager/apps/Nymphs3DInstaller/ViewModels/MainWindowViewModel.cs`
- `Manager/apps/Nymphs3DInstaller/Services/InstallerWorkflowService.cs`
- `Manager/apps/Nymphs3DInstaller/Views/MainWindow.xaml`

### Likely new additions

Under `Manager/`:

- `scripts/install_blender_mcp.sh`
- `scripts/blender_mcp_status.sh`
- `scripts/blender_mcp_start.sh`
- `scripts/blender_mcp_stop.sh`
- `docs/blender_mcp_brain_architecture_handoff.md`

Possible Brain-side layout:

```text
/home/nymph/Nymphs-Brain/
  mcp/
    config/
      mcp-proxy-servers.json
    logs/
      blender-mcp.log
```

Potential Blender-side project layout in repo:

```text
BlenderAddon/
  nymphs_blender_mcp/
    addon.py
    server_bridge.py
```

Use the exact existing addon folder structure already present in the repo if one exists; the shape above is only a suggested target.

---

## UI / Product Recommendation

In Manager Runtime Tools, a future Brain section could include:

- `Start Brain`
- `Open WebUI`
- `Start Blender MCP`
- `Stop Blender MCP`
- `Blender MCP Status`
- `Test Blender Connection`

Do **not** merge Blender MCP controls into the TRELLIS/Z-Image runtime status blocks. Those should remain separate because they represent a different responsibility layer.

---

## Decision Matrix

| Question | Answer |
|---|---|
| Can Nymphs-Brain operate a Blender MCP? | Yes |
| Does the current branch already point in that direction? | Yes |
| Should Blender MCP replace Manager as runtime owner for TRELLIS.2/Z-Image? | No |
| Should Brain be the high-level planner? | Yes |
| Should Blender MCP be narrow and explicit in v1? | Yes |
| Should transport standardize on Streamable HTTP? | Yes |
| Is this more realistic than trying to make LM Studio install image/3D runtimes directly? | Yes |

---

## Final Recommendation

Build toward this architecture:

**Nymphs-Brain as the planner**  
**Blender MCP as the DCC execution layer**  
**Manager as the backend/runtime owner**

That gives you:

- a natural-language control layer
- a safe and inspectable tool layer for Blender
- a stable owner for the fragile runtime/install/model logic
- a path to unify LM Studio, Open WebUI, Cline, and future local agents around one Brain-managed MCP surface

This is a better long-term fit than asking LM Studio alone to own `TRELLIS.2` and `Z-Image`, and it is more aligned with the branch’s current architecture direction.

---

## Source Notes

Primary sources used for this handoff:

1. NymphsCore Manager branch and README  
   <https://github.com/nymphnerds/NymphsCore/tree/codex/brain-webui-mcp-manager/Manager>

2. Brain installer script  
   <https://github.com/nymphnerds/NymphsCore/blob/codex/brain-webui-mcp-manager/Manager/scripts/install_nymphs_brain.sh>

3. Runtime Tools status script  
   <https://github.com/nymphnerds/NymphsCore/blob/codex/brain-webui-mcp-manager/Manager/scripts/runtime_tools_status.sh>

4. Brain installer / WebUI / MCP handoff  
   <https://github.com/nymphnerds/NymphsCore/blob/codex/brain-webui-mcp-manager/Manager/docs/nymphs_brain_installer_webui_mcp_handoff.md>

5. LM Studio MCP docs  
   <https://lmstudio.ai/docs/app/mcp>

6. LM Studio MCP via API  
   <https://lmstudio.ai/docs/developer/core/mcp>

7. Open WebUI MCP docs  
   <https://docs.openwebui.com/features/extensibility/mcp/>

8. Blender MCP reference patterns  
   <https://github.com/ahujasid/blender-mcp>  
   <https://github.com/poly-mcp/Blender-MCP-Server>

