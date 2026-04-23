
## Why NymphsCore Changes the Game

NymphsCore is the **first AI-powered game asset pipeline built directly into Blender**. While other solutions require you to learn complex node-based workflows, juggle multiple applications, and manually configure servers—NymphsCore delivers professional-grade AI image-to-3D generation with a single click from the Blender sidebar you already know.

Stop switching between apps. Stop configuring servers. Stop memorizing node graphs. **Just create.**

---

## What Makes NymphsCore Different

### Built for Game Asset Artists
Unlike generic AI tools, NymphsCore is purpose-built for game development. Every feature—from the character part extraction system to the game-art-optimized style presets—exists to help you ship assets faster. No VFX jargon, no 3D printing workflows, just game-ready output.

### One-Click Blender Integration
The entire system lives in Blender's sidebar. Generate images, create 3D meshes, extract character parts, apply textures—all without leaving your project. No context switching. No secondary applications. No disruption to your creative flow.

### Zero Configuration Backend
The Manager app handles everything: WSL setup, CUDA installation, Python environments, model downloads. You download one zip, run one installer, and have a fully operational AI backend. Other solutions expect you to be a systems administrator.

### Optional Local Brain Stack
NymphsCore also includes an optional local AI assistant stack for coding and tool workflows.

- `Nymphs-Brain` installs inside the managed WSL distro
- local OpenAI-compatible LLM endpoint on `http://localhost:1234/v1`
- Open WebUI on `http://localhost:8081`
- local MCP gateway on `http://localhost:8100`
- role-aware `Act` and optional `Plan` model setup
- dedicated `Brain` page in Manager for start, stop, update, logs, and model management

---

## Key Features

### Character Part Extraction (Patent-Worthy Innovation)
Master image → AI planning → separate game-ready assets in one workflow.

- **AI-Powered Planning**: Gemini vision analyzes your character and plans optimal extraction
- **Body-Aware Extraction**: Preserves proportions (short, squat, chibi, bulky, elderly) from silhouette
- **Asset Categories**: anatomy_base, hair, clothing, armor, weapon, prop, face_feature
- **Eyeball Assets**: Separate reusable spherical eyeballs with iris/pupil detail
- **Style Lock**: Generated parts match your source art style automatically
- **Bounding Box Guidance**: Crop precision from AI-calculated regions

No other tool does this. This is your secret weapon.

### 3D Pipeline

**TRELLIS.2** — Premium single-image to 3D
- Single image → textured mesh in one pass
- 1024/1536 cascade for high detail
- 22 fine-tuneable parameters for experts
- Native texture and retexture support

### Local Image Generation (Z-Image / Nunchaku)
Generate concept art and references locally—no external API calls.

- Turbo fast draft (rank 32) for quick iteration
- Balanced default (rank 128) for production
- High quality (rank 256) for final output
- local `Image to Image` from a picked guide image
- 1024×1024 up to 1536×1536 resolution
- Seed control for reproducible variants

### Art Style Presets
Purpose-built for game art production.

- **Painterly Fantasy** — Elegant linework, soft watercolor shading
- **Clean Anime** — Crisp linework, cel-shaded rendering
- **Grimdark Realism** — Grounded materials, moody natural palette
- **Storybook Inkwash** — Visible ink contour, loose brush details
- **Japanese Watercolor Woodblock** — Ukiyo-e inspired, flat layered washes
- **Minimalist Chinese Watercolor** — Sparse brushwork, xieyi-inspired simplicity

Plus 8 subject presets: character, creature, building, prop, hard-surface, and more.

### Texture Generation & Retexturing
Apply textures to existing meshes—AI understands your geometry.

- Mesh → textured mesh in one request
- Works on imported GLB/GLTF files
- Multiple texture resolution options
- Seamless integration with shape workflow

### Prompt Builder System
Structured prompts that actually work.

- Subject + Style preset combination
- Auto-managed prompt blocks (no accidental corruption)
- User-saveable custom presets (JSON in Blender config)
- Auto-stripping for clean API payloads
- Preset caching with TTL for performance

### Local Privacy
Your assets never leave your machine.

- All generation runs locally via WSL
- No external API calls for image/3D generation
- OpenRouter only for optional Gemini vision features
- Full data sovereignty for commercial projects

### Manager App
The installation experience that respects your time.

- One-click WSL distro creation
- Automated CUDA 13.0 installation
- Python environment setup
- Model prefetch (72GB of AI models downloaded automatically)
- Smoke tests verify everything works
- Runtime Tools for repair and model fetch
- Dedicated `Brain` page for the optional local LLM, MCP, and Open WebUI stack

### Nymphs-Brain
Optional local LLM and MCP workflows without leaving the managed NymphsCore system.

- local coding/chat model access through an OpenAI-compatible endpoint
- Open WebUI for browser-based local chat
- MCP servers for filesystem, memory, and web-forager tools
- separate `Act` and optional `Plan` model roles
- designed for local clients such as Cline without requiring a separate manual stack setup

---

## The NymphsCore Advantage

| Feature | NymphsCore | Traditional Workflows |
|---------|------------|----------------------|
| Setup time | 30 minutes | 4+ hours of manual config |
| Context switching | None (Blender sidebar) | Separate app + window |
| Character part extraction | AI-guided, automatic | Manual in Photoshop |
| 3D output | One-click from image | Import to DCC, manual modeling |
| Texture generation | AI understands geometry | Substance Painter manual |
| Local coding stack | Optional Brain with LLM + MCP + WebUI | Usually separate tools and manual setup |
| Style consistency | Auto-preserved from source | Manual matching |
| Local processing | Full privacy | Cloud dependency |

---

## Comparison to Node-Based Workflows

Node-based tools like ComfyUI offer tremendous flexibility—but that flexibility comes at a cost:

**Node-based limitations:**
- Steep learning curve (hundreds of nodes to understand)
- No Blender integration (separate window, separate workflow)
- Manual server configuration
- Generic outputs not optimized for game assets
- Time spent building workflows instead of creating

**NymphsCore advantages:**
- Zero learning curve (click buttons, get results)
- Native Blender integration
- Zero-configuration backend (Manager handles everything)
- Game asset optimization (character parts, props, creatures)
- Time spent creating, not configuring

The node graph is the tool. NymphsCore is the solution.

---

## Technical Highlights

- **8,439 lines** of production Python
- **Blender 4.2+** native with extension support
- **WSL 2** isolation for clean system integration
- **CUDA 13.0** for GPU-accelerated generation
- **Threading model** with timer-based event loop for UI responsiveness
- **Guard flags** prevent update recursion
- **TTL transient cache** for performance
- **Graceful degradation** on failures
- **GLB export** for immediate game engine compatibility

---

## Who Is NymphsCore For?

**Perfect for:**
- Game asset artists building characters, creatures, props
- Indie developers needing quick concept-to-3D workflows
- Studios wanting local AI processing for confidentiality
- Artists who want Blender-native tools without context switching

**Less ideal for:**
- Researchers needing custom model architectures
- Studios with existing ComfyUI workflows they love
- Users without NVIDIA GPUs (CUDA required)

---

## Get Started

1. Download [NymphsCoreManager](https://github.com/nymphnerds/NymphsCore/raw/main/Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip)
2. Run the installer (one-click setup)
3. Open Blender, enable the addon
4. Start creating

**System requirements:** Windows 10/11, NVIDIA GPU, 120-150GB free disk space

---

*Last updated: April 2026*
