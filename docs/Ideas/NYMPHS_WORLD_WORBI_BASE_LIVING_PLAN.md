# Nymphs World WORBI Base Working Brief

Date: 2026-05-28
WORBI source snapshot: `/home/nymph/NymphsModules/worbi-source` at `9ded6d6`
Purpose: concise implementation brief for turning WORBI into Nymphs World.

## Decision

Use the current WORBI UI as the Nymphs World base.

Do not start with a redesign. Do not rebuild the prototype UI. Bring the current WORBI interface into the Nymphs World module path, then adapt only where the product, project model, media bridge, or Nymphs module standard requires it.

Chronicler remains a reference for vault behavior, Markdown/frontmatter lessons, diagnostics, backlinks, and map interaction patterns. It is not the codebase base.

## Non-Negotiables

- Read `/home/nymph/NymphsCore/docs/NYMPHS_MODULE_MAKING_GUIDE.md` before implementation.
- Use current modules as references, especially WORBI's wrapper and lifecycle.
- Use the project-local WORBI skills in `/home/nymph/NymphsModules/nymphs-world/.agents/skills` when working on the WORBI-derived app. Start with `worbi-project`, `worbi-conventions`, `windows-caveats`, and `worbi-rules`; add `worbi-api`, `worbi-package`, or `worbi-tests` as needed.
- Treat those skills and rules as guidance, not source truth. Some skill text targets older WORBI versions, so current source, the NymphsCore module guide, direct user instructions, and this brief win when there is a conflict.
- Do not invent Manager-specific shortcuts for Nymphs World.
- Keep Manager behavior generic.
- Keep production outputs framed as production draft assets.
- Prefer deterministic conversion/spec generation first; use Brain for drafting, enrichment, review, repair, and specialist assistance.
- Keep project data under `$HOME/NymphsData/nymphs-world`.
- Treat old Nymphs World prototype work as reference material, not the UI foundation.

## Source Snapshot

The pulled WORBI source currently contains the right app shape:

- React app shell in `client/src/App.tsx`
- current WORBI UI structure with explorer, activity bar, editor, side panels, tabs, settings, export, AI, and media panels
- Express backend in `server/src/index.js`
- local auth/workspace model in `server/src/services/authService.js`
- file operations and profiles in `server/src/services/fileService.js`
- template creation and Story Bible export in `server/src/services/templateService.js`
- TipTap editor in `client/src/components/DocumentEditor.tsx`
- Brain-like chat/tool flow in `client/src/hooks/useLLM.ts`, `server/src/services/llmService.js`, and `server/src/services/toolService.js`
- image generation bridge in `client/src/components/ImageGeneratorPanel.tsx` and `server/src/routes/imageGeneration.js`
- deterministic GameReady export in `client/src/utils/gameExportConverter.ts`

The module wrapper lives separately at:

```text
/home/nymph/NymphsModules/worbi/nymph.json
```

That wrapper is the best local reference for Nymphs World install/status/start/stop/open/logs/uninstall behavior.

## Latest WORBI Pull

Pulled on 2026-05-28:

```text
9ded6d6 feat: enhance game export with converter utilities and improved template detection
```

Important changes from the previous reviewed commit `4649e9c`:

- `GameExportModal.tsx` now scans the workspace, finds `Build: Yes` documents, writes GameReady `.txt` files, detects existing exports, and offers ZIP download.
- `gameExportConverter.ts` converts WORBI HTML into deterministic GameReady output for NPCs, player characters, quests, main story, locations, items, and factions.
- `zipFiles.ts` creates local ZIP downloads.
- `templateService.js` injects `<!-- Build: Yes -->` and `<!-- Template: ... -->` metadata when missing.
- `fileService.js` changed the game profile folders to `MainStory`, `Quests`, `PlayerCharacters`, `NPCs`, `Locations`, and `Items`.

Implication: Nymphs World should not make every production draft asset depend on a live LLM. Use deterministic converters/specs where possible, then use Brain around them.

## Current Source Notes

These are source-grounded notes from the `9ded6d6` pass.

- `node --check server/src/routes/imageGeneration.js` passes.
- This source checkout has no `node_modules`, so a full build/test was not run.
- The README still mentions a `World` default folder, but source now uses `Locations` and `Items`.
- `authService.js` still uses per-user WORBI workspaces under `server/src/data/users/<username>/workspace`.
- New users choose a profile; `ProfileSelectorModal.tsx` creates game/work folders after login.
- `ImageGeneratorPanel.tsx` and `client/src/services/api.ts` still call `/api/llm/image-generation/start` and `/stop`.
- `server/src/routes/imageGeneration.js` exposes status, progress, test, and generate, but no start/stop endpoints.
- `toolService.js` needs audit before broad production use. Some tag/location tool calls do not pass `username`, and some handlers treat `{ success, added }` style return values as arrays.
- Tags currently use a global registry at `~/.wbu/tags.json`; locations are per-user workspace JSON.
- Metadata is scattered across sidecar files and workspace JSON. Nymphs World should consolidate project-local metadata over time.
- Port rule: WORBI production uses `8082`; Nymphs World production uses adjacent port `8083`. WORBI dev uses backend `8082` plus Vite `5173`; Nymphs World dev should use backend `8083` plus Vite `5174`. Do not use `8084`; it is reserved for a colleague-owned remote Git server admin surface.

## Port Discipline

Do not assign ports casually. Check current module manifests and scripts before adding or moving a service.

Checked module manifests/scripts in `/home/nymph/NymphsModules` plus the current Pixal3D checkout at `/home/nymph/Pixal3D`: Brain, Brain Train, LoRA, Nymphs World, Pixal3D, TRELLIS.2, WORBI, and Z-Image.

Known local module ports:

- Brain API/Open WebUI/MCPO family: `8000`, `8081`, `8099`, `8100`
- WORBI production/backend: `8082`
- WORBI Vite dev frontend: `5173`
- Nymphs World production/backend: `8083`
- Nymphs World Vite dev frontend: `5174`
- Z-Image: `8090`
- TRELLIS.2: `8095`
- Pixal3D: `8097`
- LoRA/AI Toolkit surfaces: `8675`, `7861`

Reserved external/admin ports:

- `8084`: colleague-owned remote Git server admin surface. Never use for Nymphs World.

Provider/default ports that may appear in settings but are not NymphsCore module-owned include `1234` for LM Studio, `11434` for Ollama, `8080` for llama.cpp/LocalAI-style OpenAI-compatible endpoints, `5000` for TextGen WebUI, and `1337` for Jan.

Future Nymphs World auxiliary services should avoid the crowded `808x` and `809x` bands. Prefer starting at `7000+` for new internal services unless the team formally reserves a different range first, while still avoiding already-owned ports such as LoRA's `7861` and `8675`. Update this section before assigning any new port.

## Handoff Docs Audit

WORBI has multiple handoff and legacy docs. Treat them as clue lists, not contracts.

Useful handoff material:

- `docs/handoff/WORBI_PRODUCTION_HANDOFF.md` is useful for the intended product surface and agent-facing capabilities.
- `docs/handoff/WORBI_NYMPHSCORE_INTEGRATION_RECOMMENDATIONS.md` is useful for NymphsCore packaging, lifecycle, status, install, update, backup, and path-portability checks.
- `docs/handoff/LLM_TOOLS_EXPANSION_HANDOFF.md` is useful as a tool-audit checklist.
- `docs/handoff/LLM_TOOL_PERMISSIONS_UI_HANDOFF.md` is useful for intended permission categories.
- `docs/handoff/UNIT_TESTING_HANDOFF.md` is useful for test strategy and Vitest patterns.

Known stale or suspect areas:

- Some handoffs name WORBI `v6.2.51`, while the current source package is `6.3.18` and the installed wrapper manifest is `6.3.24`.
- Some handoffs use older default folders such as `World`, `Characters`, `Lore`, and `Notes`; current source profile folders are `MainStory`, `Quests`, `PlayerCharacters`, `NPCs`, `Locations`, and `Items`.
- Some export notes describe AI-powered GameReady output; current source now has deterministic conversion in `gameExportConverter.ts`.
- Managed LLM lifecycle and GPU handoff claims are not proven in the active source. Current image generation UI still calls start/stop endpoints that the server route does not expose.
- LLM tool completion and test-count claims should not be trusted until dependencies are installed and tests are run locally.
- `WBUI_HANDOFF.md` and `docs/legacy/*` are early vision archaeology only.

Rule: a handoff doc can create a verification task, but it cannot establish an implementation fact. Current source, `/home/nymph/NymphsModules/worbi/nymph.json`, and `/home/nymph/NymphsCore/docs/NYMPHS_MODULE_MAKING_GUIDE.md` win.

## Product Target

Nymphs World is WORBI evolved into a NymphsCore worldbuilding and production draft asset workspace.

It should support:

- world projects
- rich pages
- templates
- links and references
- characters, NPCs, locations, items, factions, quests, timelines, scenes, and dialogue
- maps and biomes
- Brain-assisted writing and review
- production draft asset briefs
- image and 3D media generation through Nymphs modules
- deterministic exports where exact output matters

The first milestone is not a new design exercise. The first milestone is current WORBI running as Nymphs World, with NymphsCore-safe project storage and module lifecycle.

## Future Direction: Remote NymphsCore Frontend

If NymphsCore can be installed as-is on a remote Linux server, the WORBI-derived Nymphs World UI could become the browser frontend for that backend instead of depending on the Windows desktop app.

This is not the first implementation slice, but it should influence architecture now:

- keep the UI browser-native and avoid Windows-only assumptions
- keep module discovery, status, logs, start, stop, and open actions behind backend APIs
- design project storage so local and remote roots can both be represented cleanly
- make Brain and media module access capability-based rather than hardcoded to local processes
- require authentication, HTTPS, user/project isolation, and safe remote file boundaries before exposing this mode
- keep Manager compatibility while allowing the same UI shell to talk to a remote NymphsCore backend later

Target shape: Nymphs World starts as a local Nymphs module, then can evolve into the web workspace for a server-hosted NymphsCore install.

## Data Model Direction

Use a Chronicler-like vault shape, adapted to WORBI's current rich editor.
The folder is the world. The app should index it, assist it, and generate into
it, but the user should still be able to browse and back up the project as
normal files.

Use project-local data rooted here:

```text
$HOME/NymphsData/nymphs-world/projects/<world-id>/
```

Current WORBI state to migrate away from:

- authenticated users have a single `users/<username>/workspace` root
- game profile folders are `MainStory`, `Quests`, `PlayerCharacters`, `NPCs`,
  `Locations`, and `Items`
- templates create `.html` files
- generated image assets currently live under a user asset folder

Target project shape:

```text
<world-id>/
  nymphs-world.json
  MainStory/
  Quests/
  PlayerCharacters/
  NPCs/
  Locations/
  Factions/
  Items/
  Scenes/
  Lore/
  Maps/
  Biomes/
  Data/
  Assets/
    references/
    generated/
      images/
      models/
    maps/
    audio/
  Production/
    briefs/
    jobs/
    exports/
  _system/
    templates/
  .nymphs-world/
```

Chronicler reference points from source:

- `src-tauri/src/world.rs` keeps one active vault as the backend source of truth.
- `src-tauri/src/indexer.rs` indexes directories, Markdown pages, images, maps,
  tags, wikilinks, backlinks, broken links, broken images, and parse errors.
- `src-tauri/src/parser.rs` treats frontmatter, links, images, and inserts as
  first-class page metadata.
- `src/lib/config.ts` keeps user-editable templates under `_system/templates`.
- `src/lib/mapModels.ts` stores interactive maps as config files with layers,
  pins, regions, links to pages, and optional nested maps.

Nymphs World should learn from that shape without copying Chronicler code.

Keep WORBI's rich HTML editor format for the first implementation. Do not force
a Markdown migration before the app is stable. The first version can use `.html`
pages plus WORBI metadata comments, but the folder and manifest design should be
Markdown-friendly so a later Markdown export or migration is possible.

Project metadata boundaries:

- `nymphs-world.json` is the stable project manifest.
- `_system/templates` is for user-visible page templates.
- `.nymphs-world` is for app metadata, caches, indexes, diagnostics, graph
  caches, media indexes, and migration records.
- `Production/briefs` holds readable production draft asset briefs.
- `Production/jobs` holds job/provenance records for generated media.
- `Assets` holds actual source and generated media files.

Minimal project manifest draft:

```json
{
  "schema": "nymphs-world.project.v1",
  "id": "example-world",
  "title": "Example World",
  "format": "worbi-html-v1",
  "features": {
    "brain": true,
    "media": true,
    "maps": true,
    "biomes": true
  }
}
```

Minimal `.nymphs-world` internal shape:

```text
.nymphs-world/
  index.json
  backlinks.json
  diagnostics.json
  media-index.json
  jobs.json
  graph-cache.json
  migrations.json
```

These files should be treated as rebuildable or repairable where possible.
Readable project content belongs in the public folders, not hidden app caches.

## Structure-First Gate

Before expanding media generation, settle the vault shape above.

The next implementation gate should prove:

1. Nymphs World opens one active world project rather than a generic WORBI user workspace.
2. The left explorer shows the project folders and files from the active world root.
3. Page creation writes to project folders, including `Factions`, `Scenes`, `Lore`, `Maps`, and `Biomes`.
4. Assets are stored under the project, not in a loose user-only asset bucket.
5. A project-local index can report pages, images, backlinks, broken links, broken images, and diagnostics.
6. Media Studio can save a production draft asset brief before any backend render happens.
7. Page galleries and hero images reference project-relative assets.

This gate comes before more backend media routes. Media generation needs a
stable place to write briefs, generated files, provenance, and page attachments.

## Keep From WORBI

- current UI structure
- app shell
- activity bar
- explorer
- tabs
- TipTap editor
- split main/info document model
- templates and `Build` metadata
- deterministic GameReady export direction
- tags, locations, timeline, dialogue, graph, reminders
- AI chat and tool permission concept
- image generation panel structure
- settings and maintenance surfaces
- module wrapper pattern

## Adapt For Nymphs World

- Replace WORBI user workspace roots with Nymphs World project roots.
- Add world/project create, open, list, and select.
- Rebrand only where necessary for a Nymphs World module.
- Make Brain the default local AI route while preserving provider configurability.
- Replace app-local image start/stop assumptions with Nymphs module lifecycle awareness.
- Store generated asset provenance beside project assets.
- Move toward project-local metadata instead of global or scattered metadata.
- Keep the current WORBI UI, but adjust wording and flows where they conflict with Nymphs World.

## Brain Integration

WORBI already has a strong Brain-shaped architecture:

- provider catalog
- chat history
- document context
- tool permissions
- file tools
- search tools
- graph tools
- tag/location tools
- reminders
- fallback when tools are unavailable

Nymphs World should:

- default to Brain where installed and healthy
- keep provider settings for advanced users
- pack page, project, backlinks, assets, and selected context deliberately
- keep destructive tools disabled by default
- audit every tool signature before enabling it in production flows
- record AI provenance for generated production draft assets

## LLM Provider Strategy

Do not collapse all AI access into one path. Nymphs World should keep three
separate lanes:

1. Local stack / Brain-first.
   Use Nymphs Brain as the preferred local route when it is installed and
   healthy. Keep support for local OpenAI-compatible servers such as llama.cpp,
   LM Studio, Ollama, LocalAI-style endpoints, TextGen WebUI where compatible,
   and other local module-owned services. Treat WORBI's existing LLM endpoints
   as unproven until they have fake-provider tests and at least one live smoke
   pass.

2. OpenRouter and API providers.
   Keep OpenRouter-style provider settings as an optional advanced route for
   users who choose to bring API keys. This is separate from ChatGPT
   subscription login and should be labelled clearly so users understand they
   are using an API provider.

3. Codex subscription / app-server.
   This is the route for users who want WORBI/Nymphs World AI features to use
   their signed-in Codex/ChatGPT plan instead of an OpenRouter/API key. The
   best official fit is Codex app-server, because it is intended for deep
   product integrations with authentication, conversation history, approvals,
   and streamed agent events. This lane should be tested separately from
   WORBI's current OpenAI-compatible LLM endpoints.

Provider UI implication:

- show Local/Brain, API Providers, and Codex Sign In as distinct concepts
- never imply that ChatGPT Plus/Pro can be pasted in as an API key
- make Codex sign-in use Codex's managed ChatGPT/device-code flow, not scraped
  browser cookies or user-provided session tokens
- keep provider health checks and model list tests visible
- store generated-output provenance with the provider lane used

## Codex Subscription Lane

This is the primary path for the user request "LLM features on WORBI use a
Codex subscription."

This lane is for creative worldbuilding features, not just coding tasks:
story drafting, character bios, faction writing, quest beats, lore expansion,
editor continuations, map and biome briefs, prompt refinement, page repair,
summaries, and production draft asset planning should be able to use this
provider when the user signs in.

Official source-grounded shape:

- Codex CLI can authenticate with either a ChatGPT account or an API key.
- ChatGPT Plus, Pro, Business, Edu, and Enterprise plans include Codex access.
- Codex app-server is the deep-integration protocol for embedding Codex inside
  another product, with authentication, conversation history, approvals, and
  streamed agent events.
- Codex app-server exposes account/login methods for `chatgpt` browser login
  and `chatgptDeviceCode` login. It reports auth mode and plan type, and can
  read ChatGPT rate limits.

Nymphs World implication:

- Add a "Codex Sign In" provider lane in settings.
- On sign-in, start Codex app-server locally through the module backend.
- Prefer device-code login first if browser callbacks are brittle under WSL or
  Windows app hosting.
- Store only module-safe auth state needed to reconnect. Do not ask users to
  paste ChatGPT cookies, browser sessions, or private tokens.
- Route WORBI chat, specialist prompts, document generation, repair, and
  context-aware planning through a Codex adapter when this lane is active.
- Preserve WORBI's creative-writing intent. The Codex lane should receive a
  worldbuilding prompt contract, selected page/project context, and explicit
  output shape; it should not default to code-oriented behavior just because
  the transport is Codex app-server.
- Keep OpenRouter/API providers and local Brain providers available as separate
  lanes.
- Before production, build a small adapter proof: start app-server, read auth
  state, complete login, start a thread, send selected document context, stream
  response events, and handle rate-limit/account errors cleanly.

Implemented first slice in `nymphs-world`:

- added `docs/handoff/NYMPHS_WORLD_WORBI_HANDOFF.md`
- added backend Codex status/login-start routes under `/api/codex`
- added `codexService` to report Codex CLI, ChatGPT login, app-server daemon
  readiness, and explicit warnings
- added "Codex Sign In" as a distinct provider lane in Settings
- guarded WORBI creative LLM routes so Codex does not silently fall through to
  OpenAI-compatible URL handling before the app-server adapter is proven
- added unit tests for Codex status behavior

Implemented second slice:

- added a minimal stdio JSON-RPC app-server client for Codex
- added `/api/codex/probe` for account/model probing
- routed Codex provider model listing through `model/list`
- routed plain WORBI/Nymphs World creative chat, completion, and document
  generation through a guarded Codex creative-turn adapter
- kept image transcription, file tools, and streaming UI out of scope until they
  have their own proof pass

Implemented third slice:

- Settings now probes Codex account/model state and exposes available Codex
  models in the provider UI
- `/api/settings/test` validates Codex through account/model probing, not only
  CLI presence
- installed runtime at `http://127.0.0.1:8083` reports `codex_ready=true`
- installed runtime live probe sees ChatGPT account type, plan type `prolite`,
  six models, and default model `gpt-5.5`
- installed runtime creative-turn smoke test returned the expected text through
  the Codex adapter
- browser sign-in is now wired through Codex app-server
  `account/login/start`; the UI opens the returned `authUrl` and polls the
  retained login session for `account/login/completed`

Implemented fourth slice:

- Settings now has separate `Browser Sign In` and `Device Code` actions for the
  Codex provider lane
- browser sign-in no longer uses frontend `window.open`; the backend opens the
  returned auth URL through the OS default browser so WebView-hosted Manager
  sessions do not create tabless popup windows
- pending sign-in cards keep `Open` and `Copy Link` fallback actions visible
- device-code sign-in displays the returned user code, copy action, and
  verification-page action for cases where Google/ChatGPT password flow is
  awkward under WSL or Windows app hosting
- live device-code start/cancel smoke test returned `chatgptDeviceCode`, a
  login id, a verification URL, a user code, and cancelled cleanly

Guardrail decision: Codex creative turns use read-only sandboxing, no network,
`approvalPolicy=never`, web search disabled, and explicit instructions to avoid
shell/filesystem/tool behavior. This is intentionally a text-generation bridge,
not the full Codex coding-agent surface.

Current Codex dependency state: the official standalone Codex CLI is installed
at `/home/nymph/.local/bin/codex`, reports `codex-cli 0.134.0`, and
`codex login status` reports ChatGPT login. `codex app-server daemon start` and
`codex app-server daemon version` were verified outside the sandboxed Codex
agent. Inside the sandboxed agent, app-server socket probes can fail with an
operation-permitted error; that is a sandbox limitation, not a module runtime
signal.

Story and character generation may now route through Codex for guarded text
drafting. Keep file writes, image transcription, media generation, and tool
calling on separate proof passes.

Open questions before production hardening:

- Which features should use Codex directly versus Brain/local stack first?
- How should approvals appear in the WORBI UI without turning the workspace into
  a coding-agent console?
- How should streaming deltas appear in the WORBI chat/editor panels?

Official references:

- `https://developers.openai.com/codex/cli`
- `https://developers.openai.com/codex/app-server`

## ChatGPT Apps SDK Track

This is a later companion-surface path, not the primary path for local WORBI AI
features using a Codex subscription.

There is a real ChatGPT app path, but it sits beside the local stack,
OpenRouter/API provider paths, and Codex app-server lane. It does not replace
them, and it is not the same as adding an OpenRouter-style backend provider
inside the local app.

Official direction:

- OpenAI Apps SDK lets developers build apps that run inside ChatGPT.
- Apps SDK is built on MCP, so Nymphs World can expose tools and interactive UI
  widgets from its own backend.
- ChatGPT users are already signed in to ChatGPT; OpenAI lists app availability
  for logged-in users on Free, Go, Plus, and Pro plans in supported regions.
- Apps can connect to a developer backend so existing customers can sign in or
  access premium features.

Product implication:

- Local Nymphs World remains the WORBI-derived desktop/browser workspace.
- A future Nymphs World ChatGPT App should be a companion surface that runs
  inside ChatGPT and talks to a Nymphs World MCP/App SDK backend.
- In that mode, ChatGPT supplies the conversation/model host and user
  subscription context; Nymphs World supplies project tools, vault data,
  widgets, exports, and production draft asset workflows.
- Do not present this as "local app uses ChatGPT subscription as a generic API
  backend." That is not the same supported product shape.

Initial ChatGPT App slice:

1. Create a Nymphs World MCP/App SDK server surface beside the local module API.
2. Expose read-only tools first: list projects, search pages, open page
   summaries, list assets, list diagnostics.
3. Add safe write tools only after auth and consent are clear: create page,
   save brief, attach generated asset, export bundle.
4. Add widgets for project explorer, page preview, map/biome specs, asset grid,
   and production draft asset review.
5. If user-specific vault access is needed, use OAuth from ChatGPT into Nymphs
   World rather than trying to reuse ChatGPT cookies or browser sessions.

Official references:

- `https://help.openai.com/en/articles/12515353-build-with-the-apps-sdk`
- `https://developers.openai.com/apps-sdk/quickstart`
- `https://developers.openai.com/apps-sdk/build/auth`
- `https://openai.com/index/introducing-apps-in-chatgpt/`
- `https://developers.openai.com/api/docs/actions/authentication`

## Media Bridge

Do not hardwire Nymphs World to one media server.

Nymphs World should discover and use module status/actions for:

- Nymphs Image or Z-Image for image generation
- TRELLIS.2 for 3D asset generation
- Pixal3D where relevant
- later media modules through the same pattern

Generated output should be saved under the selected world project with:

- source page or tool
- prompt/spec
- model/module
- seed/settings if available
- generated file path
- review status

## UI-First Media Layout Direction

Do not let backend modules define the main workflow. The UI should start from
the production draft asset the user wants to make, then route to Brain, Nymphs
Image/Z-Image, TRELLIS.2, Pixal3D, LoRA, or future modules as capabilities.

Current WORBI UI landmarks to keep:

- left activity bar: opens workspace tools such as Explorer, Images, Graph,
  Timeline, Dialogue, and Reminders
- left panel: tool controls and compact asset browsing
- center: active document, canvas, preview, or opened production draft asset
- right panel: Brain/chat/context assistance
- document information panel: page-specific hero image, gallery, tags, scenes,
  notes, and references
- Settings: connection and advanced configuration only, not the main creation
  workflow

Preferred media shape:

- turn the current `ImageGeneratorPanel` direction into a broader Media Studio
  panel over time, not a pile of separate backend panels
- expose tool modes by asset intent: Image, Map, Biome, Character Visual,
  Item/Icon, Scene/Environment, 3D Asset, and Transcript
- keep backend status compact: connected/running, module, model/profile,
  device, queue state, and output root
- keep backend start/stop/test controls in a small Backends or Runtime area,
  with advanced settings remaining in Settings
- keep generation forms as structured briefs: source page, asset type, prompt,
  constraints, references, size/format, module target, and review status
- show generated outputs as an asset grid/list with an inspector, provenance,
  attach-to-page actions, and open-in-editor/open-on-canvas actions
- keep page-specific media attachment in the document information panel, so a
  page can choose hero/gallery assets without becoming the generation console
- keep Brain on the right as a contextual assistant for improving briefs,
  filling missing fields, creating specialist prompts, and reviewing outputs

First UI exploration target:

- keep the existing left Images activity as the entry point
- rename the concept in planning to Media Studio, but avoid broad code renaming
  until the layout is proven
- add internal tabs or segmented modes before adding more backend routes:
  `Brief`, `Generate`, `Assets`, `Backends`, and `History`
- make Backends a compact status/control strip, not the dominant page
- make Biome Maker and Map Maker start as structured brief/spec workflows,
  where the first saved output is a production draft asset brief even before a
  rendered image or map exists

Backend implication:

- backend endpoints should serve capability discovery, job status, and asset
  records for the UI shape above
- avoid one-off backend endpoints that only fit a single panel button
- do not add another media backend until the Media Studio layout has a place for
  capability, status, queue, output, and provenance

## Specialist Tools

Specialist tools should be structured workflows, not just raw prompt boxes.

Shared rules:

- use careful versioned prompt templates
- collect structured inputs
- validate required fields
- preview before writing files
- save prompt/spec/provenance
- link results back to the source page or project
- allow Brain to ask clarifying questions when required information is missing

### Biome Maker

Best first prototype because it can produce text/spec output before requiring a map UI.

Inputs:

- climate band
- terrain
- altitude
- moisture
- seasonality
- nearby biomes
- resource profile
- settlement pressure
- danger level
- visual style

Outputs:

- biome page
- biome JSON/spec
- palette and visual notes
- map overlay hints
- asset prompt pack
- linked region candidates

### Map Generator

Build after Biome Maker or in parallel once the media bridge is stable.

Modes:

- region map
- settlement map
- route/travel map
- political map
- resource map
- biome/climate overlay

Outputs:

- map image asset
- `.nwmap.json` config
- prompt/spec/provenance file
- optional linked pages for pins, regions, landmarks, and nested maps

Chronicler references:

- `src/lib/mapModels.ts`
- `src/lib/mapStore.ts`
- `src/lib/mapActions.ts`

Use Chronicler for lessons on pins, layers, regions, map caching, and page/map links. Do not port it blindly.

### Asset Brief Generator

Generates production draft asset briefs for image or 3D modules.

Each brief should include:

- source page
- asset type
- style constraints
- must-include details
- must-avoid details
- technical output target
- module target
- saved result links

## Next Implementation Sprint

The next sprint is structure-first. Do not expand the Media Studio beyond
brief/spec planning until project roots and project-local assets are settled.

1. Re-pull and re-check the latest WORBI source before touching code.
2. Keep the Nymphs module wrapper aligned with the module guide.
3. Add project create/open/list/select under `$HOME/NymphsData/nymphs-world/projects`.
4. Replace direct `users/<username>/workspace` assumptions with an active world root resolver.
5. Create the target project folder shape, including `_system/templates`, `Assets`, `Production`, and `.nymphs-world`.
6. Preserve WORBI editor, tabs, templates, and deterministic export while changing only the storage root.
7. Move image upload/generated-output paths from user assets into the active project `Assets` tree.
8. Add a project-local index/diagnostics service inspired by Chronicler, starting with pages, images, backlinks, broken links, and broken images.
9. Add Media Studio brief records under `Production/briefs` and job/provenance records under `Production/jobs`.
10. Only then prototype Biome Maker as the first specialist workflow.

Next slice success means:

- Manager shows Nymphs World correctly under the right registry mode.
- Installed module remains visible offline.
- Start/status/open/logs/stop work.
- A user can create/open a world project.
- A user can create and save a page using current WORBI UI.
- Project folders and assets are written under the active world, not a generic user workspace.
- A basic project diagnostics panel can report broken links and broken images.
- GameReady-style deterministic export still works where applicable.
- Biome Maker can create a reviewed production draft spec inside the project.

## Implementation Progress

2026-05-28 first media bridge slice:

- Nymphs World source was synced into the installed copy at `$HOME/Nymphs-World`.
- The WORBI-derived server is running at `http://127.0.0.1:8083`.
- `/server_info` and `/api/health` verify the Nymphs World runtime.
- The Z-Image/Nymphs Image bridge now exposes Start and Stop endpoints for local module scripts instead of leaving the UI with missing routes.
- Generated Z-Image outputs are copied into the Nymphs World user assets root under `$HOME/NymphsData/nymphs-world/users`, not the old app-local WORBI data path.
- Generated image previews use the existing signed image proxy route so browser `<img>` previews can load without custom auth headers.

2026-05-28 WORBI function hardening slice:

- Added shared server-side path safety helpers for user workspace, asset, and metadata paths.
- File and image helpers now reject traversal through both `../` and Windows-style `..\` input before resolving paths.
- Rename and upload helpers now reject path separators in leaf filenames.
- Tag and location metadata paths now use the same safe path boundary checks as file operations.
- AI tool dispatcher calls for tag/location file lookups now pass the authenticated username.
- AI tag/location add/remove tools now handle service return objects correctly instead of treating them like arrays.
- The installed runtime was synced and restarted after the patch; health is still `ok` on `8083`.

2026-05-28 project vault root slice:

- Added a Nymphs World project service with active project selection, project creation, project listing, manifest creation, and the target vault folder structure.
- The old WORBI `users/<username>/workspace` resolver now points at the active project root under `$HOME/NymphsData/nymphs-world/projects/<world-id>`.
- The old user image asset resolver now points at `Assets/generated/images` inside the active project.
- Existing legacy user workspace and image folders are copied into the default project non-destructively when a user first resolves an active project.
- Added `/api/projects`, `/api/projects/active`, and project creation/selection routes.
- Existing WORBI file APIs still drive the explorer/editor, but they now read and write from the active Nymphs World vault.
- Live smoke test verified `babyjaws-world` at `/home/nymph/NymphsData/nymphs-world/projects/babyjaws-world`, the expected top-level folder structure, hidden project manifest, and healthy runtime on `8083`.

## Explicit Deferrals

Do not do these in the first slice:

- redesign the WORBI UI
- migrate the whole app to Markdown
- clone Chronicler's full map editor
- hardcode one media backend
- enable destructive AI tools broadly
- build a huge new schema before the project root and editor path work

## Revalidation Checklist

When WORBI source changes again:

```bash
git -C /home/nymph/NymphsModules/worbi-source pull
git -C /home/nymph/NymphsModules/worbi-source rev-parse --short HEAD
git -C /home/nymph/NymphsModules/worbi-source status --short --branch
```

Then re-check:

- `package.json`
- `docs/handoff/WORBI_PRODUCTION_HANDOFF.md`
- `docs/handoff/WORBI_NYMPHSCORE_INTEGRATION_RECOMMENDATIONS.md`
- `docs/handoff/LLM_TOOLS_EXPANSION_HANDOFF.md`
- `docs/handoff/LLM_TOOL_PERMISSIONS_UI_HANDOFF.md`
- `docs/handoff/UNIT_TESTING_HANDOFF.md`
- `client/src/App.tsx`
- `client/src/components/DocumentEditor.tsx`
- `client/src/components/GameExportModal.tsx`
- `client/src/utils/gameExportConverter.ts`
- `client/src/components/ImageGeneratorPanel.tsx`
- `client/src/hooks/useLLM.ts`
- `server/src/index.js`
- `server/src/services/authService.js`
- `server/src/services/fileService.js`
- `server/src/services/templateService.js`
- `server/src/services/llmService.js`
- `server/src/services/toolService.js`
- `server/src/routes/imageGeneration.js`
- `/home/nymph/NymphsModules/worbi/nymph.json`
- `/home/nymph/NymphsCore/docs/NYMPHS_MODULE_MAKING_GUIDE.md`

Update this brief before implementation if those files change the assumptions above.
