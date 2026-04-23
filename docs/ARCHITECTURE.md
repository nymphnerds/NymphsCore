# NymphsCore Architecture Documentation

This document provides a comprehensive overview of the NymphsCore system, covering the Blender addon, backend services, the optional `Nymphs-Brain` local LLM stack, and core features.

---

## Blender Addon (Nymphs.py)

- **8,439 lines** of production Python code
- Targets **Blender 4.2+** (with blender_manifest.toml for extension installs)
- Installs via Manager app or Git URL
- Sidebar UI in 3D View with collapsible panels

---

## Backend Services

- **2 Local Blender runtime services** running in WSL with CUDA:
  - **Z-Image (Nunchaku)** - Fast image generation (port 8090)
  - **TRELLIS.2** - High-quality image → textured 3D (port 8094)
- **OpenRouter API** - Gemini vision & image gen via remote API
- WSL isolation (`-d NymphsCore -u nymph`)
- GPU monitoring via `nvidia-smi` polling

---

## Optional Nymphs-Brain Subsystem

`Nymphs-Brain` is an optional local LLM and tool stack that installs into the same managed `NymphsCore_Lite` WSL distro on the Lite test branch, but remains separate from the Blender 3D runtime family.

- Installed under `/home/nymph/Nymphs-Brain`
- Linux-side LM Studio CLI/runtime wrappers
- OpenAI-compatible local LLM endpoint on port `1234`
- local MCP gateway on port `8100`
- Open WebUI on port `8081`
- helper scripts under `/home/nymph/Nymphs-Brain/bin`
- role-aware `Act` model plus one optional `Plan` model

Primary Brain entrypoints:

- `lms-start` / `lms-stop`
- `lms-model`
- `lms-get-profile` / `lms-set-profile`
- `mcp-start` / `mcp-stop`
- `open-webui-start` / `open-webui-stop`
- `brain-status`

The Brain model profile config lives at:

```text
/home/nymph/Nymphs-Brain/config/lms-model-profiles.env
```

---

## Image Generation

- Subject presets: character, creature, building, prop, hard-surface
- Style presets: painterly fantasy, anime, grimdark, storybook inkwash, watercolor styles
- Generation profiles: turbo_fast_draft (r32), turbo_default (r128), turbo_high_detail (r256)
- Local Z-Image txt2img and experimental Nunchaku-backed img2img
- Seed control for reproducible results
- Variant batching (1-8 images)

---

## 3D Shape Pipeline

- Image-to-3D via local services
- Texture generation toggle
- TRELLIS presets: 512, 1024, 1024_cascade, 1536_cascade
- 22 fine-tuneable TRELLIS parameters (guidance strength, rescale, intervals)

---

## Character Part Extraction (Unique Feature)

- Master image → Gemini planning → separate asset breakout
- Categories: anatomy_base, hair, clothing, armor, weapon, prop, face_feature
- Auto-preserves body proportions (short, squat, chibi, bulky)
- Separate eyeball asset extraction
- Bounding-box guided cropping
- Style lock during extraction

---

## Prompt System

- Managed prompt blocks (`[Auto Subject]` / `[Auto Style]`)
- Auto-stripping for clean API payloads
- User-saveable presets (JSON in Blender config dir)
- Default preset seeding on first run
- Preset caching with TTL

---

## Threading & IPC

- Timer-based event loop (`bpy.app.timers.register`)
- Cross-thread EVENT_QUEUE for UI updates
- Daemon worker threads for all API calls
- Backend stdout capture pump for logging
- Health probe polling

---

## File Management

- Temp output dirs: `nymphs_shape_outputs/`, `nymphs_image_outputs/`
- WSL ↔ Windows path translation
- GLB export for 3D meshes
- Metadata preservation

---

## Manager App (C# WPF)

- WSL distro management
- CUDA installation automation
- One-click install scripts
- Preflight checks & verification
- Dedicated `Runtime Tools` page for Blender backend repair/model fetch/testing
- Dedicated `Brain` page for the optional local LLM, MCP, and Open WebUI stack
- Role-aware `Manage Models` flow for Brain `Act` / `Plan` assignment
- Publishes to `NymphsCoreManager.exe`

---

## Installation Workflow

- `install_all.sh` orchestrates:
  - `preflight_wsl.sh` → system deps → CUDA 13 → Z-Image → TRELLIS
  - `prefetch_models.sh` → model download
  - `verify_install.sh` → smoke tests
- Optional Brain install path adds:
  - Linux-side LM Studio runtime wrappers
  - MCP proxy services
  - Open WebUI
  - Brain helper/config scripts

---

## Design Patterns

- Guard flags prevent update recursion
- TTL transient cache (1s preset, 15s WSL distro)
- Graceful degradation on failures
- Stage/progress parsing from server stdout
