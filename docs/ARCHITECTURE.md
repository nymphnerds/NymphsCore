# NymphsCore Architecture Documentation

This document provides a comprehensive overview of the NymphsCore system, covering the Blender addon, backend services, and core features.

---

## Blender Addon (Nymphs.py)

- **8,439 lines** of production Python code
- Targets **Blender 4.2+** (with blender_manifest.toml for extension installs)
- Installs via Manager app or Git URL
- Sidebar UI in 3D View with collapsible panels

---

## Backend Services

- **3 Local AI Services** running in WSL with CUDA:
  - **Hunyuan 3D-2 (2mv)** - Image → 3D mesh (port 8080)
  - **Z-Image (Nunchaku)** - Fast image generation (port 8090)
  - **TRELLIS.2** - High-quality image → textured 3D (port 8094)
- **OpenRouter API** - Gemini vision & image gen via remote API
- WSL isolation (`-d NymphsCore -u nymph`)
- GPU monitoring via `nvidia-smi` polling

---

## Image Generation

- Subject presets: character, creature, building, prop, hard-surface
- Style presets: painterly fantasy, anime, grimdark, storybook inkwash, watercolor styles
- Generation profiles: turbo_fast_draft (r32), turbo_default (r128), turbo_high_detail (r256)
- 4-View MV generation (front/left/right/back)
- Seed control for reproducible results
- Variant batching (1-8 images)

---

## 3D Shape Pipeline

- Image-to-3D via local services
- Multi-view input support
- Text-to-3D option
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
- Publishes to `NymphsCoreManager.exe`

---

## Installation Workflow

- `install_all.sh` orchestrates:
  - `preflight_wsl.sh` → system deps → CUDA 13 → Hunyuan → Z-Image → TRELLIS
  - `prefetch_models.sh` → model download
  - `verify_install.sh` → smoke tests

---

## Design Patterns

- Guard flags prevent update recursion
- TTL transient cache (1s preset, 15s WSL distro)
- Graceful degradation on failures
- Stage/progress parsing from server stdout
