# Nymphs Module Making Guide

This is the source-of-truth community module standard for NymphsCore.

This guide is for people who want to build community modules for NymphsCore.

The goal is simple: a module author should be able to build, test, and ship a Nymph module without reading Manager source code.

## How This Standard Evolves

This standard is defined by testing real modules, one at a time.

Do not treat planning notes as the contract. When a module test proves a rule,
update this guide first, then update the Manager, module repo, or registry
entry as needed.

Current proof order:

1. Base Runtime: proves the managed WSL shell, install location, runtime logs,
   Windows WSL readiness, and shared runtime identity.
2. WORBI: proves the light module lifecycle: install, status, start, stop,
   logs, open, uninstall, marker handling, custom UI, and preserved user data.
3. Z-Image: proves heavyweight GPU/runtime behavior: model downloads,
   backend health, long actions, cache preservation, and Blender-facing image
   generation.
4. TRELLIS: proves 3D asset output, artifact metadata, and Blender/Unity
   consumption.
5. Brain: proves long-running services, secrets, model management, and chat/API
   style backends.

Testing loop:

```text
test module -> discover rule -> update this guide -> update implementation -> retest
```

Current heavyweight proof state:

- Z-Image proves native compact model fetch for image generation weights.
- TRELLIS proves native compact model fetch for multi-part 3D GGUF bundles,
  support checkpoints, and auxiliary models.
- Pixal3D proves that gated model-access guidance belongs in Details, Model
  Fetch, or a temporary `NEXT STEP` prompt, not in unrelated install option
  headings.
- Brain and Pixal3D prove that module installers must verify real venv
  creation with pip, not only `import venv`, before assuming Python venv support
  is usable on a fresh or repaired runtime.
- Base Runtime proves that the native CUDA 13 toolkit is shared system
  infrastructure. It installs CUDA at `/usr/local/cuda-13.0` and exposes
  standard CUDA environment through `/etc/profile.d/nymphscore-cuda.sh`.
- Brain proves that native CUDA builds should use the Base Runtime toolkit for
  llama.cpp/ggml CUDA compilation, while Open WebUI can use CPU PyTorch when it
  does not need GPU tensors.
- Nymphs Image proves that GPU PyTorch modules may still download PyTorch's
  bundled `nvidia-*` CUDA wheels. Those wheels are Python package dependencies,
  not a replacement for the Base Runtime native CUDA toolkit.
- Both modules keep model fetch module-owned through `ui.manager_action_groups`.
- Both modules keep generated outputs, logs, config, and reusable model caches
  under `$HOME/NymphsData` instead of inside disposable runtime source roots.

Supporting docs:

- `Ideas/CURRENT_NYMPH_MODULE_REPO_DEEP_DIVE.md`: migration notes for current
  module repos.
- `Ideas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md`: session handoff and current
  proof-state notes.
- `Ideas/NYMPH_MANIFEST_DRAFT.md`: older manifest design sketch; useful
  background, but this guide wins when there is a conflict.

## Mental Model

NymphsCore is the base shell. Modules are separate products.

The Manager owns:

- the Windows app shell
- the module registry cards
- install, update, uninstall, and delete controls
- the standard module detail page
- the universal right-side lifecycle rail
- generic action routing
- logs and lifecycle feedback

The module owns:

- its source repo or package
- its install/update/uninstall scripts
- its status/start/stop/open/logs actions
- its runtime dependencies
- its model/artifact/cache/log paths
- any custom Manager UI shown after install
- the installed module action buttons shown in the module details pane

Before a module is installed, the Manager should only show registry and manifest metadata. After install, the Manager may host module-owned local UI declared by the installed module manifest.

## Future Module Agent Rule

Use this rule as the prompt/checklist for any LLM or agent working on Nymphs
modules:

```text
Work on modules holistically, not one card at a time.

Before changing a module, inspect the whole module system:
- the target module's nymph.json and lifecycle scripts
- every installed/current module status script
- the Manager's generic module-state interpretation
- the shared registry/display fields
- this module standard

Preserve the shared contract first:
- startup card placement comes from cheap install markers, not deep scans
- installed/available grouping must stay fast and stable
- module status scripts own live health only after install
- normal stopped/configured-later states are not "needs attention"
- model or asset downloads use explicit states, not generic failure wording
- Manager code must stay generic; do not hardcode one module's UI or buttons
- registry fields may change card classification; packaging is install mechanics

When one module needs a new state, update the standard and check every module
against it in the same pass. If any module drifts, fix that module too.
Do not ship a module-specific workaround that makes Brain, LoRA, WORBI,
Z-Image, TRELLIS, Pixal3D, or brain-train worse.

After changes, run all module status scripts locally and record the state table:
id, installed, running, state, health, models/assets readiness, and detail.
Then build the Manager if Manager interpretation changed.
```

The goal is boring reliability. A module card should answer only:

```text
Available
Installed
Model download needed
Needs assets
Running
Repair needed
```

Use `Needs attention` only when the installed runtime is genuinely broken or a
required module dependency prevents normal use. Do not use it for a stopped
service, an optional key, an unselected model, preserved data after uninstall,
or a normal first-run model/asset download.

## Module Status Contract

Every module `status` action must be fast, side-effect-light, and line based:

```text
id=<module-id>
installed=true|false
runtime_present=true|false
data_present=true|false
version=<version|not-installed|unknown>
running=true|false
state=<state>
health=<health>
detail=<one useful sentence>
```

Standard states:

```text
available              not installed
installed              installed and ready enough to open/manage
running                a module-owned service/UI/worker is running
model_download_needed  installed, but required model files are missing
needs_assets           installed trainer/runtime, but training assets are missing
needs_brain            installed, but the Brain module dependency is missing
repair_needed          files exist but the standard install marker is missing
needs_attention        installed runtime is broken or cannot perform core work
```

Standard health values:

```text
unavailable            not installed or intentionally unavailable
ok                     installed state is healthy, including stopped services
unknown                installed health was not checked yet
model-download-needed  required model files are missing
assets-needed          required training assets are missing
repair-needed          install marker/source root needs repair
degraded               installed runtime has a real missing/broken piece
unreachable            process exists but expected health endpoint failed
status-warning         Manager-generated status script failure fallback
status-timeout         Manager-generated status timeout fallback
```

Rules:

- If `installed=false`, use `state=available` and `health=unavailable`.
- If a module is installed but stopped, use `state=installed`,
  `running=false`, and `health=ok` or `unknown`.
- If a service is running and healthy, use `state=running`, `running=true`, and
  `health=ok`.
- If models are missing, use `state=model_download_needed`,
  `health=model-download-needed`, and `models_ready=false`.
- If training assets are missing, use `state=needs_assets`,
  `health=assets-needed`, and `assets_ready=false`.
- If optional configuration is missing but the module can still be managed, keep
  `state=installed` and expose a specific field such as
  `model_configured=false` or `openrouter_key=not_set`.
- `needs_attention` is reserved for broken installs, missing runtime files,
  failed wrappers, missing required adapters, or impossible core operation.
- A status script must not download models, install packages, build code, start
  long services, or perform deep remote checks. Put those in explicit actions.
- Slow or deep probes must be opt-in through a module-specific environment flag,
  not the default Manager refresh path.

## Module Port Planning Standard

Ports are part of the module contract. Do not assign or move a port just
because it is free on one development machine.

Before adding or changing a module port:

- inspect active module manifests, start/status scripts, and known local admin
  services
- update `nymph.json.runtime`, script defaults, Manager-facing docs, and this
  guide together
- keep ports configurable with environment overrides where the module owns a
  service
- make status output expose the active URL, port, and health endpoint when a
  service exists
- avoid `808x` and `809x` for new auxiliary services unless the team has
  intentionally reserved that slot

Known current reservations:

```text
5173  WORBI Vite dev frontend
5174  Nymphs World Vite dev frontend
7861  LoRA / AI Toolkit Gradio surface
8000  Brain LLM API
8081  Brain Open WebUI
8082  WORBI production/backend
8083  Nymphs World production/backend
8084  colleague-owned remote Git server admin surface; do not use
8090  Z-Image
8095  TRELLIS.2
8097  Pixal3D
8099  Brain MCPO OpenAPI
8100  Brain MCP gateway
8675  LoRA / AI Toolkit UI
```

Provider defaults may appear in settings but are not NymphsCore-owned module
ports: `1234` for LM Studio, `11434` for Ollama, `8080` for
llama.cpp/LocalAI-style OpenAI-compatible endpoints, `5000` for TextGen WebUI,
and `1337` for Jan.

For future module services and Nymphs World auxiliary workers, prefer starting
at `7000+` after checking this guide and active manifests. If there is a
conflict, update this guide first, then update the module repo and registry.

## Base Runtime Dependency Floor

The managed WSL base runtime must provide the small command-line floor needed to
fetch, clone, unpack, and run module installers:

```text
bash
ca-certificates
curl
git
python3
python3-venv
sudo
tar
unzip
wget
```

The Manager's base setup scripts install this floor, and the registry module
installer must also check it before running a module's install entrypoint. If an
older or repaired distro is missing one of these tools, module install should
self-heal with apt when `sudo` and `apt-get` are available, not fail inside a
module-specific script with a mystery missing-tool error.

Python venv checks must prove `ensurepip` works. Do not treat `import venv` as
enough. Before creating a real module venv, either rely on a known-good Python
from the completed Base Runtime floor or create a temporary probe venv and run
that venv's Python with `-m pip --version`. If the probe fails and apt is
available, install the matching `python3-venv`/`python3-pip` or pinned
`python3.X-venv`/`python3.X-dev` packages first, then retry.

Base Runtime also owns the shared native build and CUDA floor used by GPU
modules:

```text
build-essential
cmake
pkg-config
CUDA 13 native toolkit at /usr/local/cuda-13.0
CUDA profile at /etc/profile.d/nymphscore-cuda.sh
```

Modules must use the Base Runtime CUDA toolkit for native CUDA builds. Do not
install a private native CUDA toolkit inside a module root or venv. Native CUDA
build scripts should respect the standard environment keys written by Base
Runtime:

```text
CUDA_HOME
CUDA_PATH
CUDAToolkit_ROOT
CUDACXX
CUDA_INCLUDE_DIRS
CUDA_LIBRARY_DIR
PATH
LD_LIBRARY_PATH
LIBRARY_PATH
CMAKE_PREFIX_PATH
```

Modules still own product-specific dependencies such as Python versions beyond
the base floor, Node runtimes, llama.cpp source builds, model managers, Gradio
apps, AI Toolkit, or 3D/image runtime libraries. A module install script may
install those dependencies itself, but it should print clear progress and fail
with the exact package/tool that needs attention.

PyTorch CUDA wheels are a separate Python packaging concern. A module that
needs GPU PyTorch may install PyTorch's `nvidia-*` wheel dependencies even
though Base Runtime already installed native CUDA. A module that does not need
GPU PyTorch should prefer CPU PyTorch to avoid unnecessary multi-GB downloads.

## Optional Provider Dependencies

Some modules expose optional provider lanes, such as OpenRouter API keys, local
Brain services, or Codex Sign In through the official standalone Codex CLI.
These must be represented as optional dependencies, not hidden core install
requirements, unless the module has no useful non-provider mode.

Rules:

- Missing optional providers must not block install, update, start, or basic UI
  access.
- Status scripts may expose clear keys such as `codex_cli`,
  `codex_logged_in`, `codex_app_server`, and `codex_ready`.
- Do not set `state=needs_attention` only because an optional provider is
  missing. Reserve `needs_attention` for broken core runtime behavior.
- Remote installers, account login, and token/key setup must be explicit user
  actions or clearly printed next steps. Do not scrape browser cookies or ask
  users to paste private session tokens.

## Python Venv Install Standard

Any module that creates a Python virtual environment must treat Python and pip
as one readiness unit. A venv directory, or even `bin/python`, is not enough.

Installer rules:

- Before creating a venv, verify the chosen Python can import both `venv` and
  `ensurepip`. If apt is available, install the matching `python*-venv` and
  `python*-pip` packages when that tooling is missing.
- If a venv already exists but its Python is missing, its pip executable is
  missing, or `python -m pip --version` fails, remove and recreate that venv.
- After creating a venv, run `python -m pip --version` before installing
  dependencies. If pip is still unavailable, fail with a clear repair message.
- Never write `.nymph-module-version` until the venv, required scripts, and
  core runtime pieces for that install have all completed successfully.

This keeps failed installs retryable. A user should be able to press Install or
Repair again after an interrupted dependency install without manually deleting a
half-created venv.

### Shared Runtime Venvs

If two modules intentionally share a heavyweight runtime venv, both installers
must be able to create and repair that same venv. Do not make one module a
hidden prerequisite for the other just because they share CUDA/native packages.

Current standard example:

```text
$HOME/TRELLIS.2/.venv
```

`TRELLIS.2` and `Pixal3D` both use that shared runtime. Installing either module
first must leave the venv usable by both modules. TRELLIS model weights are not
part of Pixal3D readiness and must not be required for Pixal3D install, Gradio,
API start, or smoke testing.

Keep the user flow simple:

- `Install` or `Repair` on the module the user is looking at should prepare its
  required runtime.
- Do not add Manager-side special cases just to explain a missing shared venv.
- The module status may mention the shared venv path, but the next action should
  be a normal `Install` or `Repair` on the current module.

## Required Pieces

A Nymph module normally has three public pieces:

1. A registry entry in `nymphs-registry`.
2. A module repo or archive containing `nymph.json`.
3. Lifecycle entrypoint scripts declared in `nymph.json`.

For modules with custom controls, add:

4. A module-owned local Manager UI, usually `ui/manager.html`.

## Registry Entry

The registry tells the Manager that a module exists before it is installed.

The registry entry should contain enough card metadata to show a useful available-module card:

```json
{
  "id": "zimage",
  "name": "Z-Image Turbo",
  "short_name": "ZI",
  "category": "image",
  "kind": "image",
  "channel": "stable",
  "packaging": "repo",
  "summary": "Local image generation backend.",
  "install_root": "$HOME/Z-Image",
  "sort_order": 20,
  "trusted": true,
  "manifest_url": "https://raw.githubusercontent.com/nymphnerds/zimage/main/nymph.json"
}
```

Think of the registry as the shop shelf. It tells users that your module exists
before they install it.

Put simple public info in the registry:

```text
what the module is called
what it does
what it needs
what product kind it should display as
where its nymph.json file is
where it should appear in the Manager list
```

Use `category` and `kind` for user-facing classification. For example,
Z-Image is `category: "image"` and `kind: "image"`, so cards can show
`// image`.

Use `packaging` only for installation mechanics. `packaging: "repo"` means the
Manager clones a Git repository. `packaging: "archive"` means the Manager
downloads and unpacks a packaged build. Do not use `packaging` as the card
subtitle or product type.

The registry may override the display classification of a module without a
Manager code change. The Manager prefers registry `category` and `kind` for
catalog/display text, and reads `packaging` separately for install facts and
installer behavior.

### Public vs Developer Registry

Use `nymphs.json` for modules that should be visible to normal users.

Use `nymphs-dev.json` for modules that are still private, experimental, or being
actively shaped. The Manager only reads `nymphs-dev.json` when Developer Mode is
enabled.

The visibility rules are:

- Public registry modules are visible in normal mode and Developer Mode.
- Developer registry modules are visible only in Developer Mode.
- Installed public modules should still show from local metadata when the
  Manager is offline or the public registry fetch fails.
- Installed developer-only modules must not leak into normal mode just because
  their local marker exists. They should reappear only when Developer Mode is
  enabled and the developer registry or local dev-mode metadata allows it.

For a module to survive offline detection, it must still install the standard
local files:

```text
<install root>/nymph.json
<install root>/.nymph-module-version
```

Do not move an in-progress module from `nymphs-dev.json` to `nymphs.json` until
it is ready for normal users to discover, install, update, and repair.

## Install Root Contract

The module manifest is the source of truth for the installed module root:

```json
{
  "install": {
    "root": "$HOME/Z-Image"
  }
}
```

Use `install.root` whenever the runtime folder needs a stable product name,
capitalization, or legacy-compatible location. Examples:

```text
$HOME/Nymphs-Brain
$HOME/Z-Image
$HOME/TRELLIS.2
$HOME/Pixal3D
$HOME/LoRA
```

The Manager resolves `$HOME`, `~/...`, absolute paths, and relative paths. If a
module omits `install.root`, the Manager falls back to the historical module id
location such as `$HOME/<module-id>`.

Installed-state detection, installed UI lookup, installed action routing,
version checks, update checks, terminal actions, and uninstall must all use the
manifest root first. Legacy hardcoded roots and lowercase `$HOME/<module-id>`
paths are only compatibility fallbacks for old installs.

Every install, repair, and update script must write:

```text
<install.root>/nymph.json
<install.root>/.nymph-module-version
```

The Manager startup card state depends on this marker. It must remain a cheap
marker probe, not a module `status` script call, so installed cards appear
quickly even when heavyweight modules or WSL services are slow.

Installed state and health have separate owners:

- Startup marker probes may set only coarse install state such as `Installed`,
  `Available`, or `Repair needed`.
- Module-owned `status` output owns live health such as `Model download needed`,
  `Ready`, `Running`, `Needs assets`, and status-warning details.
- Marker retry and repair-candidate probes must never downgrade or flatten a
  richer installed status that already came from the module-owned `status`
  script.
- Installed manifest/control refresh may update buttons, fields, links, and
  hosted UI metadata only. It must not overwrite the current health label,
  status brush, detail text, or model-readiness state.

Do not put your installed workflow buttons in the registry. Buttons belong in
your module's own `nymph.json`, because your module can change its tools without
needing the catalog to know every detail.

Install confirmations should stay concise for ordinary modules. The Manager may
show the module name and a short one-paragraph description, but it must not dump
the full `overview.body`, requirements, and links into every install popup.

Install option titles must describe only install-time choices. Do not put model
fetch, token, gated-access, or next-step instructions in an install form heading
beside runtime/build options. Keep those instructions in Details, Model Fetch,
action confirmation text, or a temporary module-owned `NEXT STEP` prompt.

Use install or action confirmation terms only for modules with real legal,
license, gated-access, or safety restrictions. Put those terms behind explicit
phrases such as `License and access notice:` or `Before installing or fetching
model files, review the upstream terms:` so the Manager can distinguish a real
acknowledgement from ordinary product overview text.

## Publishing Checklist

When you release a module update, use this order:

1. Push the module repo first.
2. Check that the raw `nymph.json` URL opens in a browser.
3. Update the registry only if the catalog card changed.
4. Push the registry if you changed it.
5. Test from the Manager using the pushed module repo and pushed registry.

Never update the registry to advertise a module change that only exists on your
local machine. If the Manager cannot download it from GitHub yet, it is not
published yet.

When a module author is actively pushing updates, fetch the module repo before
editing, keep changes small, and avoid rewriting their unpublished local work.

## What Goes Where

The module detail page has two kinds of controls.

### Manager-Owned Controls

The right side of the page is owned by the Manager. These buttons are the same
for every module:

```text
Guide
Install
Update
Repair
Directory
Uninstall
Delete Data
```

These are install/admin buttons. You do not define these in your module.

Right-rail buttons should stay visually stable. The Manager should enable or
disable them based on module state instead of making individual buttons appear
and disappear during refresh.

`Uninstall` removes the installed runtime/tools. It must preserve module data by
default when the module supports preservation.

`Delete Data` is the separate wipe action. It deletes preserved datasets,
outputs, jobs, logs, config, and other module-owned data when a module reports
`data_present=true`. It should remain available after uninstall when preserved
data remains.

After any normal uninstall, the Manager must refresh the registry roster,
presence scan, displayed details page, and command state immediately. A user
must not have to leave the module page and return before `Install`, `Repair`,
`Directory`, `Uninstall`, or `Delete Data` reflect the post-uninstall state.

Do not use `Delete Data` to blindly remove shared model/cache folders. If model
files live in a shared cache such as `$HOME/NymphsData/cache/huggingface`, keep
that cache preserved and expose module-specific model cleanup through a
module-owned action such as `Fetch Models`, `Open Weights`, or a scoped model
delete action.

Modules that support the standard wipe action must declare both
`uninstall.data_only_arg` and `uninstall.supports_data_delete=true` in
`nymph.json`. The Manager owns the rail button; modules only provide the
data-only uninstall behavior.

Do not use a universal `Model Cache` rail button. Model, weight, output, and
data folders are module-specific and should be exposed by module-owned actions
such as `Open Weights`, `Open Outputs`, `Open Datasets`, or `Open LoRAs`.

### Module-Owned Controls

The action buttons inside the details pane are yours. Your module decides:

```text
which buttons appear
what they are called
what order they appear in
which script each button runs
what the Manager should do with the script output
```

The Manager renders these buttons and safely runs the entrypoint you declare.
The Manager should not hardcode a universal button set for every module.

### Module Logs Action

`Logs` inside a module detail pane is a module-owned action, separate from the
Manager's left-sidebar Logs page. It should open the module's actual `.log`
file in Windows Notepad.

Declare the action with `id: "logs"`, `entrypoint: "logs"`, and
`result: "open_notepad"`. The logs script must print a stable absolute log file
path using `last_log=/absolute/path/to/file.log`. It may also print
`logs_dir=...`, `server_log=...`, or `client_log=...` for context, but
`last_log` is the preferred path the Manager opens.

The logs script should create the log directory and touch the selected log file
if it does not exist yet. Do not route module `Logs` actions to the Manager Logs
page, and do not open a temporary copy of stdout when a real module log file is
known.

## Installed Module UI Contract

The Manager owns the shell, registry cards, install/update/uninstall controls,
standard module detail page, standard Back navigation, logs, and generic action
routing.

Installed modules own any custom frontend.

The Manager must not hardcode custom UI for Brain, Z-Image, LoRA, TRELLIS,
WORBI, Pixal3D, or future modules. Before a module is installed, the Manager can
only show registry and manifest metadata plus standard lifecycle controls.

An installed module may expose a local Manager UI from its installed
`nymph.json`:

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

- `type` must be `local_html` for installed HTML or `local_url` for a
  module-owned localhost service.
- For `local_html`, `entrypoint` must be a safe relative path inside the
  installed module root.
- For `local_url`, `url` must be a loopback URL and `start_action` must start or
  reuse the serving process.
- The Manager reads this only from the installed module folder, never from a
  remote registry preview.
- If the installed file is absent or invalid, the Manager hides the custom UI
  button.
- The module manifest owns the custom UI button label through
  `ui.manager_ui.title`.

### WebView2 Host

The Manager hosts installed module UI with Microsoft Edge WebView2.

Current local host behavior:

- The Manager keeps the left sidebar/shell.
- The hosted module UI page has a standard full-width, thin `Back` bar owned by
  the Manager.
- The module HTML owns only the content inside the hosted WebView2 surface.
- The Manager warms the real WebView2 module UI host during app startup, but it
  must not overlay or block the Home page.
- The Manager must not add module-specific frontend code for Z-Image, Brain,
  LoRA, TRELLIS, WORBI, Pixal3D, or future modules.

### Fast Load Rules

These rules came from the Z-Image module UI performance proof. Keep them intact
unless a replacement is timed and visually confirmed.

For `local_html`:

- Copy the installed module UI into the Manager's local module UI cache under
  `%LOCALAPPDATA%\NymphsCore\ModuleUiCache`.
- Refresh that cache when the installed module UI source changes. A stale cache
  makes module UI fixes look broken even when the module repo is correct.
- Preserve a newer cache over an older installed source. Do not refresh only
  because byte length differs; that can let an old installed module UI overwrite
  a fast, corrected cache and reintroduce stale controls.
- Load the cached HTML with WebView2 `NavigateToString`, not `file://`.
- Allow WebView2's internal `data:` navigation. `NavigateToString` becomes a
  `data:` navigation internally; blocking `data:` makes the page appear stuck on
  a blank surface.
- Keep `nymphs-module-action://` interception for module actions.
- Keep `file:`, `about:`, and `data:` allowed in the WebView2 navigation
  filter. Cancel other schemes unless the Manager explicitly supports them.

For the WebView2 host:

- Use an explicit local user-data folder: `%LOCALAPPDATA%\NymphsCore\WebView2`.
- Do not let WebView2 create `NymphsCoreManager.exe.WebView2` beside the EXE,
  especially when the EXE is launched from a WSL/UNC path.
- Prewarm the actual visible module UI `WebView2` control, not a separate
  throwaway hidden browser.
- When opening a module UI, switch the shell to the module UI page before
  setting `ModuleUiSource`.
- Queue module UI navigation at high dispatcher priority. Do not queue the first
  real navigation at idle/background priority; it can sit behind shell/status
  refresh work for many seconds.
- Skip repeated navigation only when the cached module UI source path,
  last-write time, and file size are unchanged.
- Background status refresh must not reopen or reload the current module UI
  page.

Timing logs belong in `%LOCALAPPDATA%\NymphsCore\manager-app.log` with the
`module-ui-host` prefix. When debugging load performance, compare:

```text
module UI opened
navigate_request
navigate_to_string_ms
navigation_complete
```

The expected healthy behavior is that `module UI opened` and `navigate_request`
happen in the same second, and `navigate_to_string_ms` is near zero for small
`local_html` pages. If a UI fix does not appear, compare the logged `bytes=`
value with the cached file under `%LOCALAPPDATA%\NymphsCore\ModuleUiCache`; a
smaller or older byte count usually means stale installed HTML has overwritten
the cache.

Current supported type:

```text
local_html
local_url
```

Planned types:

```text
local_web_app
served_web_app
external_browser
```

### Local URL UI Launch Contract

Use `local_url` when the module serves a browser UI from localhost, such as
WORBI, Brain WebUI, Pixal3D Gradio, or an advanced LoRA/AI Toolkit UI.

The installed manifest must declare the embedded URL and the action that starts
it:

```json
{
  "ui": {
    "manager_ui": {
      "type": "local_url",
      "title": "Module UI",
      "url": "http://127.0.0.1:8097",
      "requires_running": true,
      "start_action": "start",
      "stop_action": "stop"
    }
  }
}
```

Rules:

- `url` must be a loopback `http://` or `https://` URL that WebView2 can load.
- `requires_running: true` means the Manager starts `start_action` before
  loading the URL from the standard module UI button.
- `start_action` must name an action from `entrypoints`; it is an action id, not
  a script path.
- `stop_action` is optional. When present, the Manager runs it when the user
  closes the embedded module UI. Use this for Gradio or other heavyweight
  UI-owned servers that should not keep running after the embedded page closes.
- The start action must be idempotent. If the server is already running, exit 0.
- The start action must print the UI URL on success. Prefer both:

```text
url=http://127.0.0.1:8097
module_ui_url=http://127.0.0.1:8097
```

- The module's `open` action should also print the frontend URL, not the install
  directory. This keeps `open_in_manager` and external-browser fallbacks generic.
- Long startup is acceptable, but the action must either return a usable URL or
  fail with the real log path/error. Do not leave the Manager on a blank WebView.
- Do not use `local_url` for a static installed HTML file. Use `local_html` for
  Easy LoRA-style module-owned HTML pages that call the Manager action bridge.

`local_web_app` is for installed static web apps such as
React/Vite/Svelte/plain HTML builds:

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

For future static local apps, WebView2 virtual host mapping may be used instead
of raw `file://` when it is measured and proven locally:

```text
https://<module-id>.nymphs.invalid/index.html
```

mapped to:

```text
<installed module root>/ui/dist
```

Do not switch an already working `local_html` host to a new loading mechanism
without timing logs and local visual confirmation. First priority is boring,
reliable load behavior.

### Action Bridge

Module HTML can request module actions through:

```text
nymphs-module-action://<action>?name=value
```

The Manager validates the action against the installed module capabilities and
runs the module-owned entrypoint. Query pairs become safe CLI arguments:

```text
nymphs-module-action://fetch_models?quant=q4_k_m
```

becomes:

```text
fetch_models --quant q4_k_m
```

The Manager does not run arbitrary shell from HTML.

If an older installed manifest is missing a requested action, the Manager may
refresh the trusted registry repo cache and resolve the action from the current
module manifest before reporting it unavailable. This keeps action routing
generic while letting modules add buttons such as model fetchers without Manager
code changes.

When a module UI action starts, the Manager switches to the standard Logs page
and streams stdout, stderr, and carriage-return progress there. Module UI pages
should trigger long jobs with `nymphs-module-action://` and let the module
script print useful progress instead of trying to run downloads during page
load.

For compact in-place status refreshes, installed module HTML may also use the
generic WebView2 message bridge. This bridge is only for installed,
manifest-declared module actions and uses the same action/argument validation as
the URL bridge. It does not allow arbitrary shell execution.

Request:

```js
window.chrome.webview.postMessage({
  type: "module_action",
  requestId: "status-1",
  action: "job_status",
  args: { "lora-name": "my_lora" }
});
```

Response:

```js
window.chrome.webview.addEventListener("message", event => {
  if (event.data?.type === "module_action_result") {
    // event.data.ok, requestId, action, output, error
  }
});
```

Use this for short, non-destructive queries such as status/log polling. Long or
heavy actions should still use `nymphs-module-action://` so the Manager can show
standard logs and lifecycle feedback.

### Module UI Shutdown Contract

Closing the Manager cancels active module action and lifecycle process trees.
Module UI actions must not detach install, fetch, smoke-test, update, repair, or
uninstall work into untracked background processes. If an action starts child
processes, the module-owned script should trap `TERM` and `INT` and clean them
up before exiting.

Backend `start` actions may intentionally leave a service running, but only if
the module records ownership state and its `stop` action can cleanly terminate
that service.

### Emergency Kill Action Contract

Modules that run heavyweight backends may optionally expose a `kill` action.
This is for cases where the module's normal web UI or API is blocked and cannot
answer its own cleanup request.

The contract is split deliberately:

- The module owns the `kill` entrypoint and decides what has to be stopped.
- The Manager owns the emergency availability behavior for the action name
  `kill`.

Starting with Manager `0.9.38`, an installed module action named exactly `kill`
is treated like an emergency stop action. The Manager may keep it available
while another module action is busy or a module detail progress panel is active.
Do not use another action name if you need this behavior.

Declare it in the installed module manifest, not in the registry:

```json
{
  "entrypoints": {
    "stop": "scripts/example_stop.sh",
    "kill": "scripts/example_stop.sh"
  },
  "ui": {
    "manager_actions": [
      {
        "id": "kill",
        "label": "Kill",
        "entrypoint": "kill",
        "result": "show_output"
      }
    ]
  }
}
```

Rules for `kill`:

- It must be an installed module entrypoint script that can run without the
  module web server, API server, or UI answering.
- It must be idempotent. If nothing is running, exit 0 and say so.
- It should terminate all module-owned backend, UI, worker, and child processes
  that would keep GPU/CPU work alive.
- It may point at `stop` only when `stop` already performs a complete,
  out-of-process hard stop.
- It should clean stale module-owned pid/lock state, but it must not delete
  user outputs, model caches, install markers, or manifests.
- It should return quickly and print concise output that helps the user know
  what was stopped.

## Native Action Groups

Some module-owned UI does not need WebView2. If a module only needs compact
native controls, use `ui.manager_action_groups` in the module manifest.

Action groups are still module-owned:

- the module declares the controls
- the module owns the script that runs
- the module owns validation, downloads, cache paths, and persisted presets
- the Manager only renders the controls and routes the declared action

Use native action groups for:

```text
model fetch controls
small setup forms
runtime/profile selectors
token entry
source links
one submit action
```

Do not build a WebView2/local HTML page just to choose model files. The Z-Image
Turbo proof showed that model fetch can stay compact, Manager-styled, and
module-owned without custom Manager code.

### Model Fetch UI

Model fetch controls should live on the installed module detail page, below the
standard `// DETAILS` pane and above `// MODULE ACTIONS`.

Keep fetch/action groups visible after assets are cached. The Details pane
reports cached weights, missing models, and readiness; it should not remove the
normal maintenance/refetch controls once setup succeeds. State-gated
`show_when` belongs to temporary prompts such as `NEXT STEP`, not the ordinary
`Model Fetch` or `Training Assets` action group.

Do not create a second details card, a separate browser button, or a new
module-specific Manager page for simple model downloads.

The `// DETAILS` pane should explain what the user is choosing. The fetch
controls should stay compact and action-oriented.

Recommended layout:

```text
// DETAILS
<module status or guide>
clear beginner guide
source links

// Model Fetch
Hugging Face token: [masked token field] [// Apply Key] [// Remove Key]
Download:           [same width select] [// Fetch Models]

// MODULE ACTIONS
// Smoke Test   // Start   // Stop   // Logs
```

Standard rules:

- Explain that installing a backend is not the same as downloading model
  weights.
- If every fetch downloads a required base model first, say that clearly in the
  guide text. Do not let a user think the smallest weight is the whole download.
- If a fetch downloads shared files plus a selected quant bundle, say that too.
  TRELLIS, for example, fetches shared GGUF support files, the selected GGUF
  quant bundle, a required support checkpoint, and rembg u2net.
- If `All weights` exists, label it as optional and larger. Use it only when the
  downstream tool, such as the Blender addon, can switch between models later.
- Let the user choose the model or quantized weight manually. Do not silently
  auto-pick from GPU detection.
- Use real, beginner-readable labels. Use `Hugging Face token`, not `HF`.
- Show source model pages as links in the details guide, not button-looking
  controls floating near the submit button.
- Keep the token row separate from the model fetch row.
- Use the same compact width for the secret entry field and the download
  selector. Do not let either row push action buttons off the page at normal
  Windows scaling.
- Put `// Apply Key` immediately after the secret field. It saves the Manager
  secret without running the fetch action.
- Put `// Fetch Models` immediately after the selector. It runs the declared
  module action with the selected option and any saved secret environment
  variables.
- If a saved secret can be removed, use `// Remove Key` and the same module
  action button style, font size, and casing as the other buttons.
- Required license or access acknowledgements should be shown in the Manager's
  action confirmation popup. Do not add a visible dropdown for agreement.
- If the script needs an acknowledgement argument, declare it as a hidden field
  such as `license_ack` with `arg: "--license-ack"` and `default: "yes"`.
- The fetch script must enforce the same acknowledgement itself. If
  `--license-ack yes` or the module's equivalent acknowledgement flag is
  missing, exit before any gated, restricted, or license-sensitive download.
- Mask saved tokens across the width of the token field, not with a tiny token
  indicator that looks like only a few characters were saved.
- The selector should show the useful end of long model filenames, such as
  `int4_r32`, while the value sent to the script can still be the full filename.
- Long downloads should use `result: "show_logs"` and print progress lines.
- Short validation actions, such as `Smoke Test`, should use `result:
  "show_output"` and leave a clear pass/fail result in the details pane.
- Interactive terminal actions, such as Brain's model manager, may use `result:
  "open_terminal"` when the module declares an installed entrypoint and the user
  needs a real terminal session.
- Module-owned folder shortcuts, such as Brain's LLM directory, may use
  `result: "open_directory"` and print `directory=/absolute/linux/path`.
- A successful smoke test must say `SMOKE TEST PASSED` or equivalent. `finished`
  is too vague for a test result.
- The details pane should preserve the latest action result long enough for the
  user to read it. Background status refresh and delayed manifest refresh must
  not immediately overwrite a just-completed action result.
- Do not duplicate a grouped action in `ui.manager_actions`. If
  `ui.manager_action_groups` already declares `entrypoint: "fetch_models"`, do
  not also add a `Fetch Models` button to `manager_actions`.

Example:

```json
{
  "ui": {
    "manager_action_groups": [
      {
        "id": "model_fetch",
        "title": "Model Fetch",
        "layout": "compact",
        "entrypoint": "fetch_models",
        "result": "show_logs",
        "visibility": "installed",
        "description": "Install sets up the backend only. Fetch Models downloads the actual AI model files.\nEvery fetch downloads the required base model first.\nThen it downloads your selected quantized weight. Choose All weights only if another tool can switch between them later.",
        "links": [
          {
            "label": "Base model",
            "url": "https://huggingface.co/example/base-model"
          },
          {
            "label": "Quantized weights",
            "url": "https://huggingface.co/example/weights"
          }
        ],
        "fields": [
          {
            "name": "quantized_weight",
            "type": "select",
            "label": "Download",
            "arg": "--model",
            "default": "small",
            "options": [
              {
                "label": "small",
                "value": "small-model-file.safetensors",
                "description": "Smallest download and lowest VRAM"
              },
              {
                "label": "All weights",
                "value": "all",
                "description": "Downloads every selectable preset"
              }
            ]
          },
          {
            "name": "license_ack",
            "type": "hidden",
            "label": "License/access acknowledged in popup",
            "arg": "--license-ack",
            "default": "yes",
            "optional": false
          },
          {
            "name": "hf_token",
            "type": "secret",
            "label": "Hugging Face token",
            "secret_id": "huggingface.token",
            "env": "NYMPHS3D_HF_TOKEN",
            "optional": true
          }
        ],
        "submit": {
          "label": "Fetch Models"
        }
      }
    ],
    "manager_actions": [
      {
        "id": "smoke_test",
        "label": "Smoke Test",
        "entrypoint": "smoke_test",
        "result": "show_output"
      }
    ]
  }
}
```

### Compact Secret-Only Groups

Secret-only groups use the same action-group renderer, but they should not look
like a wide model-fetch form. Use this shape for small setup such as Brain's
OpenRouter key:

```text
// OpenRouter
   OpenRouter key: [masked key field] [// Apply Key] [// Remove Key]
```

Rules:

- Keep the row lightly indented under the group title.
- Do not use a wide fixed label column that creates a large gap before the
  field.
- Keep the field width compact and unchanged when adjusting alignment.
- `// Apply Key` may run the group's module action when the module needs to
  write the secret into its own installed config.
- `// Remove Key` clears the Manager's saved secret and uses the same action
  button style as the rest of the row.

### Model Fetch Script Output

The module-owned fetch script should print human-readable progress. The Manager
does not know the module's model sizes, Hugging Face repos, cache shape, or file
names beyond what the manifest declares.

Useful lines include:

```text
model_fetch_plan=1 required base model, then selected weight
MODEL FETCH STARTED: step=1/2 repo=example/base-model
MODEL FETCH STATUS: step=1/2 repo=example/base-model status=downloading this_repo_cache=2.14 GiB active_download_files=5
MODEL FETCH COMPLETE: step=1/2 repo=example/base-model
MODEL FETCH STARTED: step=2/2 repo=example/selected-weight
MODEL FETCH STATUS: step=2/2 repo=example/selected-weight status=downloading this_repo_cache=640.00 MiB active_download_files=2
MODEL FETCH COMPLETE: step=2/2 repo=example/selected-weight
```

For large Hugging Face downloads, do not only print `STARTED` and `COMPLETE`.
Wrap the blocking download call with a lightweight status reporter. Print one
`MODEL FETCH STATUS` line immediately after `STARTED`, then print again every
5-10 seconds with whatever the module can know.

The Manager details pane uses the latest standard progress keys to show a
compact fetch status. Keep this shape consistent across modules:

```text
Model download: downloading
This repo cache: 2.14 GiB
Active downloads: 5
```

To get that output, every large model fetch should emit a `MODEL FETCH STATUS`
line with formatted `this_repo_cache` and `active_download_files` values on the
same line:

```text
MODEL FETCH STATUS: step=1/2 repo=example/model status=downloading this_repo_cache=2.14 GiB active_download_files=5
```

`this_repo_cache` is the current cache size for the repo being fetched, not the
whole Hugging Face cache. `active_download_files` is the count of currently
active partial/incomplete/lock download files for that repo. The visible Manager
progress should stay small and consistent; do not print long cache paths or
dense telemetry as the primary progress signal.

Training asset fetches use the same compact display rule. Emit
`FETCH_ASSETS_PROGRESS` with `status`, `phase`, `this_repo_cache`, and
`active_download_files` instead of streaming repeated prose into the details
card:

```text
FETCH_ASSETS_PROGRESS status=downloading phase=model_bundle this_repo_cache=3.70 GiB active_download_files=1
```

The Manager must render those lines as a compact summary and must not append a
raw "latest output" tail to the details card. Full raw lines belong in Logs.

Optional keys such as `downloaded_this_step`, `recent_activity`,
`huggingface_cache_total`, and `cache_dir` may be logged for debugging, but they
are secondary. Do not rely on them for the main Manager progress display.

Large remote fetches must tolerate transient network breaks. Hugging Face and
other model hosts may throw partial-read or connection-reset errors after
several GiB. Wrap downloader calls with bounded retries, reuse the existing
cache, and print a readable retry line before sleeping:

```text
MODEL FETCH STATUS: step 1/4 TencentARC/Pixal3D download was interrupted. Retrying attempt 2/3 using the existing cache.
```

Model fetch actions must be single-flight. Use a module-owned lock file under
the module config/log root so repeated button clicks cannot start parallel
downloads for the same cache. If a fetch is already running, print a concise
status line and exit cleanly:

```text
MODEL FETCH STATUS: status=running waiting_on=existing_fetch
```

Status is the authority after a fetch. Once the module status entrypoint reports
`models_ready=true`, `aux_models_ready=true`, or the module's equivalent ready
state, the Manager must clear old model-fetch action feedback. A stale action
stream must not leave the details pane stuck in `Fetch Models running`.

The module should persist the selected generation preset in a module-owned config
file under a shared data root, for example:

```text
$HOME/NymphsData/config/<module-id>/
```

The Manager should not infer model readiness by scanning large model caches at
startup. Startup installed state comes from the module install marker. Model
readiness belongs in module-owned status/fetch scripts and user-triggered
actions.

### Source-Copy Updates

Module update scripts must support both install shapes:

- git checkout installs, where update can use `git pull --ff-only`
- source-copy installs, where update must sync the freshly cloned registry/cache
  repo into the installed module root while preserving `.venv`, data, logs, and
  config

Do not assume `$HOME/<Module>` is a git checkout. The Manager may install from a
trusted cached repo and run the module install entrypoint as a source copy.

### Smoke Test Result UI

Smoke tests are validation actions, so the UI must make pass/fail obvious.

The Manager should render successful smoke tests like:

```text
<Module Name>: SMOKE TEST PASSED
SUCCESS: backend started, answered /server_info, and stopped cleanly.
```

Do not show only `finished`. A user should not have to read JSON or logs to know
whether a test passed.

Module-owned smoke test scripts should print concise raw evidence after the
success line, such as the server URL and `/server_info` response. It is valid
for `/server_info` to report `loaded_model_id=null` if the smoke test only checks
that the backend starts and answers health/config endpoints. A generation test
is a separate, heavier validation.

## Shared Secrets

Module UI pages and action groups must not print secrets into logs or bake them
into installed HTML.

The standard shared Hugging Face token path is:

```text
%LOCALAPPDATA%\NymphsCore\shared-secrets.json
```

Modules should request this with a declared secret field:

```json
{
  "name": "hf_token",
  "type": "secret",
  "label": "Hugging Face token",
  "secret_id": "huggingface.token",
  "env": "NYMPHS3D_HF_TOKEN",
  "optional": true
}
```

The Manager saves the token once and passes it into the module action
environment as `NYMPHS3D_HF_TOKEN`. The token itself must not be logged.

## Z-Image Example

`nymphnerds/zimage` is the current proof for compact native model fetch controls.
It does not need a custom WebView2 page for model selection.

The installed Z-Image manifest declares:

- a `Model Fetch` action group
- a persistent `Hugging Face token` secret field
- a `Download` selector for Nunchaku-compatible Z-Image Turbo weights
- source links for the base model and quantized weights
- simple module actions for `Smoke Test`, `Start`, `Stop`, and `Logs`

## TRELLIS Example

`nymphnerds/trellis` is the current proof for compact native model fetch
controls with a multi-part 3D model bundle.

The installed TRELLIS manifest declares:

- a `Model Fetch` action group
- a persistent `Hugging Face token` secret field
- a `Download` selector for `Q4_K_M`, `Q5_K_M`, `Q6_K`, `Q8_0`, and optional
  `All quants`
- source links for the official TRELLIS project, GGUF models, support
  checkpoint, and rembg u2net
- simple module actions for `Smoke Test`, `Start`, `Stop`, and `Logs`
- a shared runtime venv at `$HOME/TRELLIS.2/.venv` that is also Pixal3D-ready

Its details guide must say that Fetch Models downloads:

```text
shared GGUF support files
selected Aero-Ex/Trellis2-GGUF quant bundle
required microsoft/TRELLIS.2-4B support checkpoint
rembg u2net background-removal model
```

Do not describe `Q4_K_M` as the whole download. It is the smallest selected
quant bundle, but the fetch still needs shared files and support models.

## Pixal3D Example

`nymphnerds/Pixal3D` shares the TRELLIS.2 native CUDA runtime venv but owns its
own source, Gradio UI, API wrapper, model fetch, outputs, logs, and license
terms.

The Pixal3D install contract is:

- `Install` creates or reuses `$HOME/TRELLIS.2/.venv`
- TRELLIS model weights are not required for Pixal3D
- Pixal3D model files are fetched separately into shared `NymphsData` caches
- if the shared venv is missing or incomplete, `Repair` on Pixal3D rebuilds it
- the Manager should not send the user to a different module page to fix the
  basic Pixal3D runtime
- native source needed only to build shared extensions, such as TRELLIS.2
  `o_voxel`, belongs under the shared runtime area, for example
  `$HOME/TRELLIS.2/runtime/TRELLIS.2-source`; do not assume Pixal3D itself has
  TRELLIS submodules such as `o-voxel/third_party/eigen`
- because Pixal3D and TRELLIS.2 share the same FlashAttention build, Pixal3D
  must expose the same install-time GPU arch, `MAX_JOBS`, and `NVCC_THREADS`
  fields as TRELLIS.2 and pass them through the shared `TRELLIS_FLASH_ATTN_*`
  environment variables

The Z-Image Fetch Models action currently offers all published compatible
weights: INT4 r32/r128/r256 and FP4 r32/r128. `All weights` is available so the
Blender addon can switch between presets later. These are generation weights,
not LoRA training precision.

## Module Repo Layout

Recommended repo layout:

```text
nymph.json
README.md
scripts/
  install_<module>.sh
  <module>_status.sh
  <module>_start.sh
  <module>_stop.sh
  <module>_open.sh
  <module>_logs.sh
  <module>_uninstall.sh
ui/
  manager.html
```

Use lowercase module ids in filenames. Keep scripts self-contained and safe to run inside the managed `NymphsCore` WSL distro.

For modules with model downloads, include the fetch action explicitly:

```text
scripts/
  <module>_fetch_models.sh
```

The fetch script should accept clear module-owned arguments such as `--model`,
`--weight`, or `--quant`, and it should persist selected runtime presets under:

```text
$HOME/NymphsData/config/<module-id>/
```

## Process Shutdown Standard

Closing the Manager must cancel active module lifecycle work for every module.

Module install, update, repair, uninstall, fetch, and smoke-test scripts must
keep their child work inside the process tree launched by the Manager. Do not
use `nohup`, `disown`, detached `setsid`, or untracked background workers for
these lifecycle jobs.

If a lifecycle script starts child processes, trap `TERM` and `INT`, then stop
those children before exiting. The Manager will cancel active module lifecycle
process trees when the app closes; module scripts must not escape that contract.

Long-running `start` scripts are the exception only when they intentionally
start a managed backend service. In that case the module must write enough PID
or ownership state for its `stop` script to terminate the backend cleanly.

## Manifest

Every module must include `nymph.json` at repo root or package root.

Minimum useful shape for a normal module:

```json
{
  "manifest_version": 1,
  "id": "example",
  "name": "Example Module",
  "short_name": "EX",
  "version": "0.1.0",
  "description": "A local tool managed by NymphsCore.",
  "category": "tool",
  "packaging": "repo",
  "source": {
    "type": "repo",
    "repo": "https://github.com/example/example-module.git",
    "ref": "main"
  },
  "install": {
    "root": "$HOME/Example",
    "entrypoint": "scripts/install_example.sh",
    "version_marker": "$HOME/Example/.nymph-module-version",
    "installed_markers": [
      "$HOME/Example/.nymph-module-version"
    ]
  },
  "entrypoints": {
    "install": "scripts/install_example.sh",
    "status": "scripts/example_status.sh",
    "start": "scripts/example_start.sh",
    "stop": "scripts/example_stop.sh",
    "open": "scripts/example_open.sh",
    "logs": "scripts/example_logs.sh",
    "uninstall": "scripts/example_uninstall.sh"
  },
  "ui": {
    "sort_order": 100,
    "manager_actions": [
      {
        "id": "start",
        "label": "Start",
        "entrypoint": "start",
        "result": "show_output"
      },
      {
        "id": "stop",
        "label": "Stop",
        "entrypoint": "stop",
        "result": "show_output"
      },
      {
        "id": "logs",
        "label": "Logs",
        "entrypoint": "logs",
        "result": "show_output"
      }
    ]
  },
  "uninstall": {
    "supports_purge": true,
    "requires_confirmation": true,
    "dry_run_arg": "--dry-run",
    "confirm_arg": "--yes",
    "purge_arg": "--purge",
    "preserve_by_default": [
      "outputs",
      "logs"
    ],
    "removes_by_default": [
      "runtime source files",
      "generated module scripts"
    ]
  }
}
```

Use this shape for simple tools such as WORBI-style modules. They still need
the install marker, lifecycle entrypoints, and safe uninstall metadata, but they
do not need model fetch controls, Hugging Face tokens, smoke tests, or model
cache paths unless the module actually uses them.

Model-fetch backend shape:

Use this larger shape for AI backends like Z-Image Turbo or TRELLIS where
installing the backend and downloading model files are separate steps.

```json
{
  "manifest_version": 1,
  "id": "example-backend",
  "name": "Example Backend",
  "short_name": "EX",
  "version": "0.1.0",
  "description": "A local AI backend managed by NymphsCore.",
  "category": "image",
  "packaging": "repo",
  "source": {
    "type": "repo",
    "repo": "https://github.com/example/example-backend.git",
    "ref": "main"
  },
  "install": {
    "root": "$HOME/ExampleBackend",
    "entrypoint": "scripts/install_example_backend.sh",
    "version_marker": "$HOME/ExampleBackend/.nymph-module-version",
    "installed_markers": [
      "$HOME/ExampleBackend/.nymph-module-version"
    ]
  },
  "runtime": {
    "host": "127.0.0.1",
    "port": 8090,
    "health_url": "http://127.0.0.1:8090/health",
    "server_info_url": "http://127.0.0.1:8090/server_info"
  },
  "artifacts": {
    "models_root": "$HOME/NymphsData/models",
    "cache_root": "$HOME/NymphsData/cache",
    "outputs_root": "$HOME/NymphsData/outputs/example-backend",
    "logs_root": "$HOME/NymphsData/logs/example-backend",
    "huggingface_cache": "$HOME/NymphsData/cache/huggingface"
  },
  "entrypoints": {
    "install": "scripts/install_example_backend.sh",
    "status": "scripts/example_backend_status.sh",
    "start": "scripts/example_backend_start.sh",
    "stop": "scripts/example_backend_stop.sh",
    "open": "scripts/example_backend_open.sh",
    "logs": "scripts/example_backend_logs.sh",
    "fetch_models": "scripts/example_backend_fetch_models.sh",
    "smoke_test": "scripts/example_backend_smoke_test.sh",
    "uninstall": "scripts/example_backend_uninstall.sh"
  },
  "ui": {
    "sort_order": 100,
    "manager_action_groups": [
      {
        "id": "model_fetch",
        "title": "Model Fetch",
        "layout": "compact",
        "entrypoint": "fetch_models",
        "result": "show_logs",
        "visibility": "installed",
        "description": "Install sets up the backend only. Fetch Models downloads the actual AI model files. Explain what will be downloaded, how large it may be, and which option a new user should choose.",
        "links": [
          {
            "label": "Base model",
            "url": "https://huggingface.co/example/base-model"
          },
          {
            "label": "Quantized weights",
            "url": "https://huggingface.co/example/quantized-weights"
          }
        ],
        "fields": [
          {
            "name": "model_choice",
            "type": "select",
            "label": "Download",
            "arg": "--model",
            "default": "recommended",
            "options": [
              {
                "label": "Recommended",
                "value": "recommended",
                "description": "Best first choice for most users"
              },
              {
                "label": "All models",
                "value": "all",
                "description": "Download every supported model option"
              }
            ]
          },
          {
            "name": "hf_token",
            "type": "secret",
            "label": "Hugging Face token",
            "secret_id": "huggingface.token",
            "env": "NYMPHS_HF_TOKEN",
            "optional": true
          }
        ],
        "submit": {
          "label": "Fetch Models"
        }
      }
    ],
    "manager_actions": [
      {
        "id": "smoke_test",
        "label": "Smoke Test",
        "entrypoint": "smoke_test",
        "result": "show_output"
      },
      {
        "id": "start",
        "label": "Start",
        "entrypoint": "start",
        "result": "show_output"
      },
      {
        "id": "stop",
        "label": "Stop",
        "entrypoint": "stop",
        "result": "show_output"
      },
      {
        "id": "logs",
        "label": "Logs",
        "entrypoint": "logs",
        "result": "show_output"
      }
    ]
  },
  "uninstall": {
    "supports_purge": true,
    "requires_confirmation": true,
    "dry_run_arg": "--dry-run",
    "confirm_arg": "--yes",
    "purge_arg": "--purge",
    "preserve_by_default": [
      "outputs",
      "logs",
      "model cache"
    ],
    "removes_by_default": [
      "runtime source files",
      "virtual environment",
      "generated module scripts"
    ]
  }
}
```

Use `packaging: "repo"` when the Manager clones the module repo. Use `packaging: "archive"` when the module is installed from a packaged archive. `packaging` is never the user-facing module type.

## Install Contract

Install scripts should be boring and recoverable.

Rules:

- Install into a staging/temp folder first.
- Install dependencies inside staging.
- Move staging into the real install root only after dependencies succeed.
- Write `.nymph-module-version` last.
- Print `installed_module_version=<version>` after success.
- If install fails or is interrupted, the install marker must not exist.
- Do not treat “folder exists” as installed.
- Do not create random backup folders like `~/module.backup.*`.

The Manager uses this rule:

```text
installed == .nymph-module-version marker exists
installed != install folder exists
installed != preserved data exists
```

Manager interpretation rule:

- The install marker is the source of truth for whether a module is installed.
- A `status` script must agree with the marker.
- If the marker exists but `status` fails, times out, or reports `installed=false`, the Manager should keep the module in the installed group and surface a status warning/detail. It should not demote the module to available, and it should not show a scary top-level install state.
- The proper fix for a marker/status mismatch is in the module status script or manifest path, not a Manager hardcode.

## Native Install Options

Use `install.fields` only for choices that must be known before the install
script runs, such as TRELLIS FlashAttention build limits or GPU architecture.
Do not hardcode module-specific install choices into the Manager.

Install fields follow the same compact field shape as native action-group
fields:

```json
{
  "install": {
    "title": "FLASH ATTENTION OPTIONS",
    "root": "$HOME/TRELLIS.2",
    "entrypoint": "scripts/install_trellis.sh",
    "fields": [
      {
        "name": "flash_attn_cuda_archs",
        "type": "select",
        "label": "GPU",
        "env": "TRELLIS_FLASH_ATTN_CUDA_ARCHS",
        "default": "auto",
        "options": [
          {
            "label": "Auto-detect",
            "value": "auto",
            "description": "Read the NVIDIA compute capability"
          },
          {
            "label": "SM80 / RTX 30+40",
            "value": "sm80",
            "description": "Compile FlashAttention for Ampere/Ada targets"
          }
        ]
      }
    ]
  }
}
```

Rules:

- The Manager renders these fields before install and passes selected values to
  the install script through the declared `env` names.
- The install script owns validation and must fail clearly if a value is
  unsupported.
- Defaults should be safe. For expensive native builds, prefer a conservative
  default and explain faster/riskier choices in the details pane.
- The Manager may show the selected install options in the details pane while
  the lifecycle action runs, but the module script still owns the real work.

## Installed Detection Standard

The working Manager detection path is deliberately two-stage.

Stage 1 is a fast marker scan:

```text
registry/manifest roster -> resolve install root -> check <install root>/.nymph-module-version
```

If the marker exists, the module is shown as installed immediately. This keeps
Home responsive and avoids waiting for every backend to prove runtime health.

The roster still controls visibility. In normal mode, local installed metadata
may restore installed public modules when the registry is offline, but it must
not reveal developer-only modules. In Developer Mode, the Manager may merge the
developer registry and local installed metadata so dev modules can be tested.

On Windows, the Manager must perform this startup marker scan from the Windows
side through the UNC view of the managed runtime distro:

```text
\\wsl.localhost\NymphsCore\home\nymph\<module>\.nymph-module-version
```

Do not implement startup install truth as a WSL bash probe, module `status`
call, model-cache scan, smoke test, or backend health check. The Manager may be
launched from the development/source distro, such as `NymphsCore_Lite`, while
installed modules live in the managed runtime distro, `NymphsCore`. Direct
Windows-side marker reads avoid that distro-boundary mistake and avoid WSL wake
races.

Stage 2 is the module `status` action:

```text
status -> key=value snapshot -> runtime health/detail
```

Status is the recovery and verification path. The Manager still runs status in
the background because the fast marker scan can fail when WSL is slow to wake,
when a manifest path is wrong, or when the distro service has just recovered.

The rule is:

```text
fast marker scan may promote Available -> Installed
status may promote Available -> Installed
status may add health/detail warnings
status must not demote marker-installed modules to Available
deferred marker/repair probes must not overwrite richer status labels
```

If the fast marker scan times out, a deferred marker-only retry is allowed. That
retry must still read only install markers and repair-candidate filesystem
signals. It must not become a deep status pass.

Do not remove the status recovery pass just to make startup feel faster. Make
the shell responsive first, then refresh status in the background.

For now, the canonical marker path is:

```text
<resolved install root>/.nymph-module-version
```

`install.root` is preferred, then `runtime.install_root`, then registry
`install_root`, then the Manager's module-id fallback. Future manifest fields
such as `install.version_marker` or `install.installed_markers` may be added
only after at least one proof module ships them and the Manager tests that path.

## Startup Performance Standard

Startup should be boring and quick:

- Load registry/manifest cards first.
- Prime installed state from Windows-side marker reads against the real managed
  `NymphsCore` runtime distro.
- Show the Home grid as soon as the roster is ready.
- Run Windows/system checks with short timeouts.
- Run module status in the background after the shell is usable.
- Keep per-module status bounded and safe.
- Do not run heavyweight health checks, model scans, downloads, or smoke tests
  as part of startup status.

The bottom status line should distinguish these phases:

```text
Manager shell loaded.
Refreshing live status...
Manager shell refreshed.
```

Z-Image/WORBI proof rule:

```text
startup install truth == Windows-side marker read from the real managed runtime distro
startup install truth != module status
startup install truth != model cache scan
startup install truth != smoke test
```

This is now part of the standard because the Manager may be launched from the
developer/source distro path, such as `NymphsCore_Lite`, while installed modules
live in the managed runtime distro, `NymphsCore`. The fast startup checker must
target the real runtime distro and read `.nymph-module-version` through the
Windows UNC view. Do not replace this with WSL bash startup probing unless the
Windows-side path is unavailable and the fallback is clearly bounded.

## Status Contract

`status` must be fast, timeout-safe, and safe when files are missing.
It is the home-card readiness contract, not a deep diagnostic scan. Keep normal
status to cheap filesystem/process checks for:

- available
- installed
- model download needed
- training assets needed
- obvious runtime wrapper problems

Do not probe every service URL, import heavy ML libraries, call Hugging Face
APIs, or run model validation from normal `status`. Put deep checks behind a
separate action such as `doctor`, `smoke_test`, or an explicit status mode like
`BRAIN_STATUS_DEEP=1`.

Print newline-separated `key=value` pairs. Minimum recommended keys:

```text
id=example
installed=true
runtime_present=true
data_present=true
version=0.1.0
running=false
state=installed
health=ready
install_root=/home/nymph/example
logs_dir=/home/nymph/NymphsData/logs/example
last_log=/home/nymph/NymphsData/logs/example/current.log
marker=/home/nymph/example/.nymph-module-version
detail=Example module is installed.
```

Modules that offer multiple model or weight choices must also report a generic
weight-profile summary. These keys let the Manager show what is already
downloaded without knowing module-specific filenames or cache layouts:

```text
weight_profile_selected=Q5_K_M
weight_profiles_available=Q4_K_M,Q5_K_M,Q6_K,Q8_0
weight_profiles_downloaded=Q5_K_M
weight_profile_ready=true
```

Use comma-separated values with no spaces. Use `none` when no profile in a list
is present. Keep this a cheap local cache check; do not call remote APIs or
load model libraries from normal `status`.

Hugging Face snapshot files are commonly symlinks into the repo `blobs/`
directory. Any status check that verifies a cached file must be symlink-safe:
use `find -L ... -type f`, `test -f` on the resolved snapshot path, or an
equivalent real-file check. Do not use plain `find ... -type f` inside
`snapshots/`; it can miss valid downloaded weights and incorrectly report
`model_download_needed`.

Normal Manager detail pages should show only the cached/downloaded weights,
using short literal text such as `Cached weights: Q5_K_M`.

If one Manager fetch selector mixes runtime profiles and true weight variants,
the module should still report every user-selectable cached choice in
`weight_profiles_downloaded`. For example, if `Low VRAM 1024` and
`Standard 1536` share the same safetensors cache, report both as downloaded once
that shared cache is present, then append any separately cached quantized
weights.

For a missing install marker:

```text
id=example
installed=false
runtime_present=false
data_present=false
version=not-installed
running=false
state=available
health=missing
detail=Example module is not installed.
```

Status must not run old bin wrappers from a partial install when `.nymph-module-version` is missing.

For installed modules that require downloaded model weights, use this standard
readiness shape whenever required model assets are missing:

```text
installed=true
models_ready=false
state=model_download_needed
health=model-download-needed
detail=Model files need downloading. Use Fetch Models to download the required assets.
```

If the module has auxiliary model bundles, also print an explicit readiness key
such as `aux_models_ready=false`. The Manager treats either `models_ready=false`
or `aux_models_ready=false` as `Model download needed`.

When `models_ready=true` and the selected weight profile is ready, optional
uncached weight choices must not make the Manager show `Model download needed`.
Lists such as `missing_weights` or `weight_profiles_missing` are informational
only for unselected choices. Use `models_ready=false` or `aux_models_ready=false`
for required assets that block the module.

Use `state=needs_attention` for broken runtime files, missing Python
environments, bad wrappers, or failed imports. Do not use `needs_attention` for
ordinary first-run model downloads; those should be `model_download_needed`.

For training-sidecar modules that need datasets or training assets rather than
runtime inference weights, use the separate training-assets shape:

```text
assets_ready=false
state=needs_assets
health=assets-needed
```

Training asset downloads are still long downloads. Declare them through
`ui.manager_action_groups` with `result: "show_logs"` and compact
`FETCH_ASSETS_PROGRESS` output. Do not leave asset fetches as plain
`manager_actions` with `show_output`, because that streams raw progress into the
details card.

Action groups may be submit-only. A fieldless `ui.manager_action_groups` entry
is valid and the Manager must render its submit button. Modules may also expose
the same long fetch as a plain `manager_actions` fallback only when its
`result` is `show_logs`; never use `show_output` for long fetches.

## Action Entrypoints

Common lifecycle actions:

```text
install
status
start
stop
open
logs
uninstall
```

Useful optional actions:

```text
update
fetch_models
smoke_test
repair
doctor
```

Each action should:

- return exit code `0` on success
- return nonzero on real failure
- print useful progress
- avoid interactive prompts unless explicitly called with a confirmation flag
- write useful logs to the declared module log folder

Lifecycle progress standard:

- While install, repair, update, uninstall, or delete-data is running, the
  standard details pane must show the live lifecycle output instead of the
  static module overview text.
- The Manager should preserve the final lifecycle output briefly after success
  or failure so the user can see what happened without opening Logs.
- Full raw logs still belong on the Logs page; the details pane should show the
  recent useful tail.

Installed action execution standard:

- After install, lifecycle and utility actions should run the installed
  module-owned script directly from the installed module root.
- The Manager may use the registry/cache manifest to discover actions, but it
  must not let a stale cache override an installed script that exists locally.
- Conventional installed scripts such as
  `scripts/<module_id>_<action>.sh` should be treated as the authoritative
  action implementation for installed modules.
- The module owns the script behavior and exit code. The Manager owns only
  rendering, routing, and progress capture.

## Smoke Test Standard

`smoke_test` is a lightweight health validation action.

For backend modules, a smoke test should usually:

```text
start the backend if it is not already running
wait for a health/config endpoint
print concise evidence, such as /server_info
stop the backend if the smoke test started it
exit 0 only when the health/config check passed
exit nonzero on real failure
```

Smoke tests should not silently run a full generation unless the module clearly
labels that as a heavier validation. A backend can pass smoke test with
`loaded_model_id=null` if the test only proves that the server starts and
answers `/server_info`. Model load or generation can be a separate action later.

The Manager UI must report the result plainly:

```text
SMOKE TEST PASSED
SMOKE TEST FAILED
```

Do not use vague success labels such as `finished` for tests. The user should not
have to decode raw JSON to know whether the test passed.

## Installed Module Buttons

Installed module buttons appear in the module details pane after install.

Use these when your module has actions like:

```text
Start
Stop
Browser
Logs
Fetch Models
Open Project
Backup
Reindex
Train
Export
```

Declare them in `ui.manager_actions`:

```json
{
  "ui": {
    "manager_actions": [
      {
        "id": "start",
        "label": "Start",
        "entrypoint": "start",
        "result": "open_in_manager"
      },
      {
        "id": "browser",
        "label": "Browser",
        "entrypoint": "start",
        "result": "open_external_browser"
      },
      {
        "id": "logs",
        "label": "Logs",
        "entrypoint": "logs",
        "result": "open_notepad"
      }
    ]
  }
}
```

Fields:

- `id`: stable action id, such as `start` or `browser`.
- `label`: button text shown to users.
- `entrypoint`: the key from your `entrypoints` block to run.
- `result`: what the Manager should do after the script succeeds.

Supported result modes:

```text
show_output
open_in_manager
open_external_browser
open_notepad
open_terminal
open_directory
```

Use `open_terminal` only for explicitly interactive installed module actions,
such as a terminal model manager. The Manager opens the declared installed
entrypoint in the managed WSL distro and leaves that terminal session under the
user's control.

Use `open_directory` for module-owned folders. The action should print
`directory=/absolute/linux/path` or `path=/absolute/linux/path`; the Manager
opens that folder in Explorer.

Your module owns the behavior. The Manager is only the renderer, launcher, and
safe host bridge.

Module actions should print structured hints when they want the Manager to open
something:

```text
url=http://localhost:8082
log_file=/home/nymph/worbi/logs/worbi-server.log
server_log=/home/nymph/worbi/logs/worbi-server.log
message=Action finished.
```

Simple rule:

```text
If you want a button, declare it in ui.manager_actions.
If you want the button to do something, point it at an entrypoint script.
```

## Native Action Groups

Use `ui.manager_action_groups` when a module needs compact native controls
instead of a single button.

This is the standard for model fetch panels such as Z-Image Turbo and TRELLIS.
It is also suitable for small module-owned setup forms, such as selecting a
runtime profile or optional model pack.

Normal fetch/action groups should remain visible after assets are ready. Use
Details/status output to show what is cached. Reserve `show_when` for temporary
guidance such as a first-run `NEXT STEP` link.

Use action groups when you need:

```text
select/dropdown fields
checkboxes
saved secret inputs
source links
one submit action
compact controls that still match the Manager style
```

Do not build a WebView2/local HTML page just to choose model files. Keep simple
model download choices native, compact, and module-owned.

Secret fields should declare a stable `secret_id` and safe environment variable
name. Proven shared secret ids:

```text
huggingface.token -> NYMPHS3D_HF_TOKEN
openrouter.api_key -> OPENROUTER_API_KEY
```

Example:

```json
{
  "ui": {
    "manager_action_groups": [
      {
        "id": "model_fetch",
        "title": "Model Fetch",
        "layout": "compact",
        "entrypoint": "fetch_models",
        "result": "show_logs",
        "visibility": "installed",
        "description": "Fetch the backend model files. Install only sets up the runtime.",
        "links": [
          {
            "label": "Base model",
            "url": "https://huggingface.co/example/base"
          },
          {
            "label": "Quantized weights",
            "url": "https://huggingface.co/example/weights"
          }
        ],
        "fields": [
          {
            "name": "model",
            "type": "select",
            "label": "Download",
            "arg": "--model",
            "default": "small",
            "options": [
              {
                "label": "Small",
                "value": "small",
                "description": "Lower VRAM"
              },
              {
                "label": "All weights",
                "value": "all",
                "description": "Downloads every selectable preset"
              }
            ]
          },
          {
            "name": "hf_token",
            "type": "secret",
            "label": "Hugging Face token",
            "secret_id": "huggingface.token",
            "env": "NYMPHS3D_HF_TOKEN",
            "optional": true
          }
        ],
        "submit": {
          "label": "Fetch Models"
        }
      }
    ]
  }
}
```

Field rules:

- `label` must be understandable to a beginner. Use `Hugging Face token`, not
  `HF`.
- `secret` fields are saved by the Manager once and injected into the module
  action through the declared environment variable. Do not print secrets.
- For model fetch groups, the secret entry field and the dropdown should use
  the same compact width.
- `// Apply Key` belongs immediately after the secret field and saves the token
  without starting the download action.
- `// Fetch Models` belongs immediately after the selector and runs the
  declared action.
- Saved-secret removal should appear as `// Remove Key` with the same module
  action button style and text size as the other buttons.
- `links` should be real links to source model pages, not button-looking UI.
- Long downloads should print progress and keep the Manager responsive.
- The module owns download validation, selected-model persistence, and cache
  layout. The Manager only renders controls and routes the declared action.

Compact secret-only groups, such as Brain's OpenRouter key, should use a small
indent under the action-group title and no wide fixed label column:

```text
// OpenRouter
   OpenRouter key: [masked key field] [// Apply Key] [// Remove Key]
```

The compact Z-Image proof established this behavior:

```text
Install = set up backend/runtime only
Fetch Models = download the real AI model files
Base model downloads can be large and must be explained in the guide text
All weights is optional and should be clearly labelled as larger
Blender/addon model choice can consume the fetched models later
```

## Uninstall And Data

Normal uninstall should remove runtime files while preserving user data.

Recommended manifest section:

```json
{
  "uninstall": {
    "supports_purge": true,
    "requires_confirmation": true,
    "dry_run_arg": "--dry-run",
    "confirm_arg": "--yes",
    "purge_arg": "--purge",
    "preserve_by_default": [
      "data",
      "config",
      "logs",
      "outputs"
    ],
    "removes_by_default": [
      "runtime source files",
      "virtual environments",
      "generated launch scripts"
    ]
  }
}
```

Normal uninstall should remove `.nymph-module-version`.

Purge/delete-data may remove only scopes declared by the module. Be conservative. A user should never lose models, outputs, datasets, or project files because a script guessed a path.

## Artifacts, Models, Cache, And Logs

Do not put user artifacts inside disposable runtime roots unless there is a clear migration plan.

Recommended shared roots:

```text
$HOME/NymphsData/models
$HOME/NymphsData/cache
$HOME/NymphsData/outputs
$HOME/NymphsData/logs
```

Recommended module roots:

```text
$HOME/NymphsData/models/<module-id>
$HOME/NymphsData/cache/<module-id>
$HOME/NymphsData/outputs/<module-id>
$HOME/NymphsData/logs/<module-id>
$HOME/NymphsData/logs/<module-id>/actions
$HOME/NymphsData/logs/<module-id>/current.log
```

Recommended manifest section:

```json
{
  "artifacts": {
    "models_root": "$HOME/NymphsData/models",
    "cache_root": "$HOME/NymphsData/cache",
    "outputs_root": "$HOME/NymphsData/outputs/example",
    "logs_root": "$HOME/NymphsData/logs/example"
  }
}
```

The module `status` output should include `logs_dir` and optionally `last_log`.

## Custom Manager UI

Custom UI belongs to the module, not the Manager.

If a module has useful controls beyond the standard lifecycle rail, put local HTML in the module repo and declare it in the installed manifest:

```json
{
  "ui": {
    "standard_lifecycle_rail": true,
    "manager_ui": {
      "type": "local_html",
      "entrypoint": "ui/manager.html",
      "title": "Example Controls"
    }
  }
}
```

Rules:

- The Manager reads `ui.manager_ui` only from the installed module folder.
- The Manager does not render custom UI from the remote registry before install.
- `entrypoint` must be a safe relative path inside the installed module root.
- The Manager shell remains standard across modules. The current WebView2 host keeps the sidebar and shows a full-width, thin standard Back bar above the hosted module UI.
- The UI should call module actions through the Manager action bridge, not by running shell directly.

Current local Manager builds use WebView2 for modern module frontends. The intended expansion is:

```text
local_html        simple installed HTML page
local_web_app     installed static web app
served_web_app    module-owned localhost UI
external_browser  open outside Manager when embedding is not suitable
```

This keeps the architecture the same: the Manager hosts and routes, while the module owns the web UI.

Module authors should keep local HTML lightweight and self-contained. Avoid remote CDN dependencies for Manager-hosted controls. Any expensive backend checks, model scans, or downloads should be triggered through explicit module actions, not during HTML page load.

## UI Action Bridge

Module HTML can request actions with:

```text
nymphs-module-action://<action>?name=value
```

Example:

```text
nymphs-module-action://fetch_models?quant=q4_k_m
```

The Manager validates the action against the module capabilities and turns safe query pairs into CLI arguments:

```text
fetch_models --quant q4_k_m
```

The Manager does not run arbitrary shell from HTML.

The module owns the web UI label and behavior:

- Set `ui.manager_ui.title` to the user-facing action label, such as `Fetch Models`.
- Add every callable HTML action to `entrypoints`, such as `fetch_models`.
- Keep the HTML lightweight; trigger model downloads, backend starts, scans, and other expensive work through explicit `nymphs-module-action://` links.
- Long actions should print useful progress lines. The Manager switches to the standard Logs page and streams stdout, stderr, and carriage-return progress there.
- The Manager may refresh the trusted module repo cache to find a newly declared action, but the action still belongs to the module manifest and script.

## Security And Safety Rules

Module scripts run inside the managed `NymphsCore` WSL runtime, so treat them like installer code.

Rules:

- Validate user-provided paths.
- Prefer paths under `$HOME`.
- Do not delete broad folders.
- Never depend on the developer/source WSL distro.
- Do not assume any developer/source WSL distro exists.
- Do not use source-tree UNC paths as runtime paths.
- Do not silently fetch unpinned code for release channels.
- Keep long-running actions cancellable where practical.

## Holistic Module Fixes

When changing or fixing shared module behavior, apply the fix holistically across
all modules.

If a change touches lifecycle behavior, status reporting, install markers,
update detection, version writing, registry metadata, manager action fields, or
other shared module contracts, do not patch only the module that exposed the
bug. Either fix the behavior in the Manager/platform layer so every module
inherits it, or audit and update every affected module to the same standard.

Module-specific fixes are allowed only when the behavior is genuinely unique to
that module. Make the exception explicit in the handoff or changelog so the next
person does not mistake a one-off patch for the shared standard.

## Versioning

Use semantic-ish module versions:

```text
0.1.0
0.1.1
0.2.0
1.0.0
```

Bump the module version when:

- install behavior changes
- entrypoint behavior changes
- the UI contract changes
- dependencies or model pins change
- data layout changes

The install script should write the same version to `.nymph-module-version`.

## Testing Checklist

Before asking for registry inclusion, test:

- clean install from the Manager
- status before install
- status during install
- status after install
- close Manager during install, then reopen
- start
- stop
- open
- logs
- custom UI button appears only after install
- custom UI action bridge works
- normal uninstall preserves declared user data
- reinstall after uninstall
- failed install leaves no `.nymph-module-version`
- partial install folder is not treated as installed
- purge/delete-data removes only declared scopes

The expected result is boring: no stuck `Checking`, no false `Installed`, no stale `Available` during install, no hidden logs, and no surprise data loss.

## Good First Module

A good first module is small:

- one install script
- one status script
- one start/stop/open/logs path
- no model downloads during install
- no custom UI until lifecycle is stable

Once lifecycle is boring, add custom UI and optional actions.
