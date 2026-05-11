# Nymph Plugin Standardization Handoff

Date: 2026-05-10
Branch context: `modular`

`modular` is the future-main path for the registry-driven Manager. Keep the current `main` branch available as the old-manager UI/workflow reference until module-owned frontend surfaces replace those hardcoded pages.

Promotion checklist for when `modular` becomes `main`:

```text
README.md
- Download link:
  https://github.com/nymphnerds/NymphsCore/raw/modular/Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip
  -> raw/main/...

Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
- GuideUrl:
  blob/modular/docs/GETTING_STARTED.md
  -> blob/main/docs/GETTING_STARTED.md

Bundled Manager lifecycle wrappers:
- Manager/scripts/install_nymph_module_from_registry.sh
- Manager/scripts/uninstall_nymph_module.sh

These are packaged under `scripts/` beside the EXE and staged into the managed
`NymphsCore` distro at action time. They should not be fetched from this repo
over the network during normal EXE use.

Docs
- Branch context labels saying `modular` should become `main` or be removed after merge.
```

## Where This Session Landed

The Manager is now much closer to a clean plugin shell.

The active shell no longer depends on a hardcoded five-module roster for discovery. It loads official module metadata from `nymphs-registry`, combines that with module manifests, and projects module cards from generic metadata.

Repo cleanup landed in this session too:

```text
Keep one active handoff:
docs/Ideas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md

Removed stale roots:
ManagerFEUI/
Monitor/
WORBI-installer/
home/
index.html

Removed old handoff scratch files from docs/.
```

The app Guide button now points at the repo docs instead of the removed static `home/` site.

The lifecycle layer is now moving around one standard contract:

```text
registry -> manifest -> entrypoints -> status key/value snapshot -> Manager UI truth
```

The important rule is now explicit across the local module scripts:

```text
installed runtime == .nymph-module-version marker exists
installed runtime != install root exists
installed runtime != preserved data exists
```

## 2026-05-11 Recovery Note

This session became unstable because too many architectural and startup/status changes were made while Z-Image was being proven. Start the next session conservatively.

Hard guardrails:

```text
Work locally on modular until the Manager and Z-Image proof are tested.
Do not push remote modular until the user explicitly confirms the build works.
Do not create extra local branches unless the user asks.
Do not redesign the shell while debugging a module lifecycle issue.
Do not put Z-Image/TRELLIS/Brain/LoRA/WORBI-specific UI back into Manager source.
```

Known good recovery points from live testing:

```text
Initial system/runtime check recovered after restart.
WORBI installed state recovered and shows correctly.
The Manager can install Z-Image far enough to write .nymph-module-version=0.1.2.
The Manager can detect Z-Image as installed from the marker.
Logs page text selection/copy was restored without changing the visual design.
```

Do not regress the marker contract:

```text
The .nymph-module-version marker is installation truth.
If marker exists, the module is installed.
If marker exists but status fails, times out, or reports installed=false, keep the module installed and show only a status warning/detail.
Do not demote marker-installed modules back to Available.
Do not show a scary top-level "Needs attention" install state just because status is broken.
Fix the module status script/manifest path that contradicted the marker.
```

Current Z-Image proof state:

```text
Z-Image install reached installed_module_version=0.1.2.
The installed marker exists from the Manager perspective.
The Z-Image status entrypoint currently fails or reports installed=false even though the marker exists.
That is a Z-Image module contract bug, not proof that the marker system is wrong.
Next step: inspect the installed Z-Image nymph.json and status_zimage.sh inside the managed NymphsCore distro, then fix the module repo so status reads the same marker path the installer writes.
```

Current local Manager artifact:

```text
Version: 0.9.13
Exe: Manager/apps/NymphsCoreManager/publish/win-x64/NymphsCoreManager.exe
Zip: Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip
Remote push: do not push until the user confirms this local build behaves correctly.
```

Manager-side source fix now in the local build:

```text
If .nymph-module-version exists, Manager keeps the module Installed even if status fails/times out/reports installed=false.
The status problem is surfaced as warning/detail text.
The top-level module state should not show Needs attention solely because the status entrypoint failed.
```

Startup/status refresh lesson:

```text
Cards should appear from registry/manifest roster quickly.
Cards may show Checking until live status updates them.
Do not hide the whole installed/available grid behind slow module status probes.
Do not run many WSL status/runtime probes in parallel until ProcessRunner/WSL contention is proven safe.
Keep status timeouts bounded.
The bottom status line should distinguish "shell loaded" from "live status refreshed".
```

Module UI lesson:

```text
The local Manager test build now uses WebView2 for installed module HTML instead of WPF WebBrowser.
Keep this simple: no module-specific Manager frontend code, no virtual-host experiments until measured, and no visible startup WebView overlay on Home.
The module still owns ui/manager.html; the Manager should only host it after install.
For Z-Image, keep the simple module detail page intact and open the module-owned UI from the standard right rail.
The current Manager-side module UI surface keeps the sidebar and uses a full-width but thin standard Back bar above the hosted UI.
WebView2 prewarm is attempted offscreen during app startup while runtime/module checks run; local testing still needs to confirm whether this makes the first module UI open acceptable.
```

## Tomorrow Starts Here

The next session should not start by redesigning the shell again. The shell is now good enough to begin the proof phase.

Recommended prompt for the next session:

```text
Read docs/Ideas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md and continue the modular Manager proof phase.

Start with the managed NymphsCore WSL runtime. Test one official module at a time from the Manager registry cards.

Focus on:
1. install
2. status
3. start/stop/open/logs
4. uninstall
5. interrupted/closed-app behavior
6. whether the module follows the .nymph-module-version contract

Use WORBI as the reference standard, then move to Z-Image, LoRA, Brain, and TRELLIS.

Do not redesign the shell unless a test exposes a real stability problem.
Keep docs/Ideas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md and CHANGELOG.md updated.
```

Start with the managed runtime and official modules, in this order:

```text
1. Confirm the Base Runtime install/unregister loop on the target NymphsCore WSL.
2. Install exactly one module from its registry card.
3. Test that module's status/start/stop/open/logs/uninstall contract.
4. Fix the module script or manifest until the generic Manager shell can understand it cleanly.
5. Repeat module-by-module.
```

Important lifecycle stability rule from live WORBI testing:

```text
During install/update/uninstall/delete, the Manager must show an active lifecycle state.
Status refresh must not overwrite that module back to Available while the action is still running.
Long progress output must not resize the module detail page.
Closing and reopening the Manager during a module action must not leave the user guessing.
Closing the Manager should actively stop Manager-owned lifecycle jobs inside the target `NymphsCore` distro; cancelling the Windows `wsl.exe` process alone is not enough.
Status checks must be bounded and disposable. A stale module wrapper or dead local service must not leave the Manager stuck in `Checking`.
```

Module install standard learned from the WORBI proof:

```text
Install into a temp/staging folder first.
Install dependencies inside staging.
Only swap staging into the real install root after dependency install succeeds.
Write .nymph-module-version last.
No .nymph-module-version means not installed, even if an install folder exists.
Failed or interrupted installs must leave a clean not-installed state.
Do not create random ~/module.backup.* folders.
Preserve only manifest-declared user data during normal uninstall/update.
Delete/purge may remove only manifest-declared purge scopes.
Status must be fast, timeout-bounded, and safe when files are missing.
Status must not run stale bin wrappers when the marker is missing.
Long install output must be clipped/bounded in the Manager detail page.
Every module should have declared log roots so logs are findable.
```

Recommended module order:

```text
WORBI first
Z-Image second
LoRA third
Brain fourth
TRELLIS fifth
```

Why:

```text
WORBI has already had the most live lifecycle testing.
Z-Image and LoRA prove heavier AI/image/training style module contracts.
Brain proves service/LLM monitor integration.
TRELLIS proves the larger 3D stack after the contract is less wobbly.
```

Once module lifecycle is boring, rebuild the old Manager module UI as module-owned frontend surfaces. Do not put Brain/Z-Image/LoRA/TRELLIS/WORBI-specific UI logic back into the Manager shell.

The direction should be:

```text
module manifest declares the page/UI host
module scripts own lifecycle and status truth
Manager renders a standard module shell + declared installed-module UI
```

This is the key next standardization step for community modules.

## WebView2 Direction For Module UI

The current installed-module UI contract is intentionally small:

```text
ui.manager_ui.type = local_html
ui.manager_ui.entrypoint = ui/manager.html
```

The local v0.9.13 Manager test build has replaced WPF `WebBrowser` with Microsoft Edge WebView2 for installed module UI.

Reason:

```text
WebView2 is the current Microsoft-supported WPF web host.
It is Edge/Chromium based, supports modern HTML/CSS/JS, and fits the existing resizable WPF Manager shell.
It can host module-owned static web apps and served localhost web apps without putting module-specific UI code in Manager.
```

Current local host behavior:

```text
Manager keeps the shell/sidebar.
Manager shows a standard full-width thin Back bar.
Manager opens installed module ui.manager_ui in WebView2.
Manager routes nymphs-module-action:// links to validated module entrypoints.
Manager prewarms the real WebView2 module UI host on app launch.
Modules own the HTML/CSS/JS and lifecycle scripts.
```

Fast-load lesson from the Z-Image proof:

```text
For local_html, do not use file:// navigation.
Copy installed module UI to %LOCALAPPDATA%\NymphsCore\ModuleUiCache.
Read the cached HTML and load it with WebView2 NavigateToString.
Allow WebView2 data: navigations, because NavigateToString internally becomes data:.
Use %LOCALAPPDATA%\NymphsCore\WebView2 as the explicit WebView2 user-data folder.
Do not let WebView2 create NymphsCoreManager.exe.WebView2 beside a UNC-launched EXE.
Prewarm the actual visible module UI WebView2 control, not a separate dummy browser.
Set the module UI page visible before setting ModuleUiSource.
Queue the first module UI navigation at high dispatcher priority, not Background/idle priority.
Skip repeated navigation to the same cached source only when the path, timestamp, and file size match.
Keep the fast ModuleUiCache, but never overwrite a newer cached HTML file with an older installed module UI source just because file length differs. That specific failure made Z-Image's rank selector fall back to stale r32-only HTML.
Do not let background module-status refresh reopen or reload the current module UI page.
```

If future UI work regresses open speed, check `%LOCALAPPDATA%\NymphsCore\manager-app.log` for `module-ui-host` lines. A healthy open has `module UI opened`, `navigate_request`, `navigate_to_string_ms`, and `navigation_complete` in the same second for small local HTML pages.

Planned module UI types:

```text
local_html        simple installed HTML control page
local_web_app     installed static app such as ui/dist/index.html
served_web_app    module-owned localhost app, started/stopped by module actions
external_browser  open externally when embedding is not appropriate
```

For future local static module web apps, prefer WebView2 virtual host mapping over raw `file://` paths only after it is measured and proven locally. The first v0.9.13 WebView2 recovery path should stay boring and working before more hosting changes are attempted.

```text
https://<module-id>.nymphs.invalid/index.html -> <installed module root>/ui/dist
```

Avoid `.local` hostnames because WebView2/Microsoft docs note they can cause navigation delays. Use a reserved non-real hostname such as `.invalid`.

For served UIs such as AI Toolkit or Gradio-like dashboards, the module manifest should declare the URL and lifecycle actions:

```json
{
  "ui": {
    "manager_ui": {
      "type": "served_web_app",
      "start_action": "start_ui",
      "stop_action": "stop_ui",
      "url": "http://127.0.0.1:7860",
      "title": "AI Toolkit"
    }
  }
}
```

Manager still validates actions and owns the bridge. Modules still own the frontend.

Small UI rule from the latest pass:

```text
Monitor mode should collapse in place, preserving the window's top-left position.
Module GitHub/source links belong on the module detail page actions rail, not on compact Home cards.
After the final design pass, module GitHub/source links should live as compact
`Source: GitHub` facts, not as full-width lifecycle action buttons.
```

End-of-session repo state:

```text
Active branch: modular
Remote branch: origin/modular
Old remote branch: rauty deleted
Main branch: keep as old Manager UI/workflow reference
Latest focus: module lifecycle standardization, Base Runtime shell, WORBI proof module
Next phase: test each official module from registry cards, one at a time
```

## Critical WSL Boundary Warning

Do not confuse the two WSL worlds.

This is the dev/source distro:

```text
NymphsCore_Lite
```

This is the target managed runtime distro:

```text
NymphsCore
```

The Manager may be built and launched from a path like:

```text
\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\publish\win-x64
```

That path is a Windows UNC view into the dev distro. It is not a valid path inside the target runtime distro.

Never make the target `NymphsCore` distro execute scripts by converting a dev-distro UNC path with `wslpath`.

Bad pattern:

```text
Manager launched from NymphsCore_Lite UNC path
PowerShell gets $PSScriptRoot under \\wsl.localhost\NymphsCore_Lite\...
installer calls wslpath on that UNC path
target NymphsCore tries to execute a script path from the dev distro
fresh base install fails
```

The failure seen during live testing looked like:

```text
wsl.exe : wslpath:
\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\publish\win-x64\scripts\bootstrap_fresh_distro_root.sh
NativeCommandError
Base runtime setup warning: Base distro import failed.
```

Correct rule:

```text
Source/dev scripts may live in NymphsCore_Lite.
Target runtime setup must not depend on NymphsCore_Lite paths being visible inside NymphsCore.
```

Correct approaches:

```text
stream script contents into the target distro
stage scripts into a target-local temp path
clone/fetch helper scripts inside the target distro
use module registry URLs and cached target-local module repos
```

Do not use source-tree path conversion as a runtime installation boundary. It is acceptable for old dev tooling, but not for the plugin/base-runtime install path.

This bug was patched in:

```text
Manager/scripts/import_base_distro.ps1
```

The fresh-bootstrap path now reads `bootstrap_fresh_distro_root.sh` on the Windows side and streams the script content into `/tmp/nymphscore-bootstrap-fresh-distro-root.sh` inside the target runtime distro before execution. This avoids relying on `\\wsl.localhost\NymphsCore_Lite\...` from inside `NymphsCore`.

Current Base Runtime UI/behavior:

```text
System Overview -> Base Runtime card opens a dedicated Base Runtime page.
Windows WSL readiness is shown separately from managed runtime install state.
Install Base Runtime is disabled until Windows WSL is ready.
Set Up Windows WSL is only for missing/not-ready Windows WSL support.
Runtime Progress is always visible on the Base Runtime page.
Unregister WSL Runtime warns that the managed distro, runtime folder, and modules inside it are removed.
Monitor mode has a `mon`/`full` sidebar toggle. Restoring to full should always return to a usable full app rectangle, even if monitor mode was entered from a very small/sidebar-only window.
```

Important wording rule:

```text
Do not put dev/source WSL names in normal user-facing UI.
NymphsCore_Lite belongs in developer handoff/debug docs only.
The user-facing runtime is NymphsCore.
```

The next documentation target should be a public community-module standard. The bar is:

```text
A developer can build a Nymph module without reading Manager source code.
```

Suggested split:

```text
NYMPH_BASE_RUNTIME_STANDARD.md
NYMPH_MODULE_STANDARD.md
```

The community module standard should clearly define:

```text
registry entry shape
nymph.json manifest fields
required entrypoints
required status keys
exit code rules
install marker rules
uninstall and preserved-data policy
purge/delete-data safety
model/artifact/cache paths
module log paths
Manager card metadata
optional Manager surface schema
validation/checker expectations
```

## Core Manager Changes

Changed files in `NymphsCore`:

```text
Manager/apps/NymphsCoreManager/Models/NymphModuleManifestInfo.cs
Manager/apps/NymphsCoreManager/Models/NymphStatusSnapshot.cs
Manager/apps/NymphsCoreManager/Services/InstallerWorkflowService.cs
Manager/apps/NymphsCoreManager/ViewModels/ManagerShellViewModel.cs
```

Summary:

- `NymphStatusSnapshot` parses module `key=value` status output generically.
- `NymphModuleManifestInfo` now carries registry/card metadata such as short name, category, packaging, install root, capabilities, dev capabilities, and sort order.
- `InstallerWorkflowService` can load the remote registry JSON from:

```text
https://raw.githubusercontent.com/nymphnerds/nymphs-registry/main/nymphs.json
```

- The active shell now has a Base Runtime install/repair action that creates or repairs only the managed `NymphsCore` WSL shell.
- Base Runtime has a dedicated shell page opened from its System Overview card, so the card behaves like navigation and the slower install/repair action is explicit on the page.
- Base Runtime page now shows Windows WSL readiness, current runtime state, a compact guide, runtime progress, and unregister warning in the default view.
- Base Runtime install is gated behind Windows WSL readiness.
- Base Runtime bootstrap avoids target-runtime execution of source/dev WSL paths by staging scripts inside the target distro.
- Runtime monitor mode was added so the app can collapse to a compact sidebar-only monitor.
- Brain monitor telemetry is shown as a collapsible sidebar section.
- Logs page uses copyable/selectable text while preserving the terminal-style visual.
- The old installer's hardcoded optional module choices are intentionally not part of that base runtime action.
- Base Runtime should get its own lifecycle next: status, repair, helper-script update, system-package update, and explicit Ubuntu migration.
- `ManagerShellViewModel` refreshes the module roster from registry/manifest data before refreshing status.
- Module-specific status projection methods were removed from the active shell.
- The shell still special-cases WORBI for `Delete Module + Data` because purge scopes are not safely generalized yet.
- Graceful Manager close now cancels active operations and asks the managed distro to stop Manager-owned lifecycle process trees.
- Module status checks now detect active install/uninstall scripts even if an old action started before the generic action-state file existed.
- Module status checks now have a Manager-side timeout and module-command timeout.
- Status checks do not use stale module bin wrappers unless the module install marker exists.
- Module detail pages stay pinned to the selected module during install/status refresh, even before the module moves into the installed list.

## Registry Update

Remote repo:

```text
/home/nymph/NymphsModules/nymphs-registry
```

Pushed commit:

```text
2e4a523 Add module card metadata to registry
```

`nymphs.json` now includes module card metadata for all five official modules:

```text
brain
zimage
lora
trellis
worbi
```

Each registry entry now includes enough information to show a useful available-module card before install:

```text
id
name
short_name
category
channel
packaging
summary
install_root
sort_order
trusted
manifest_url
```

## Local Module Contract Changes

Local module repos touched under:

```text
/home/nymph/NymphsModules/brain
/home/nymph/NymphsModules/zimage
/home/nymph/NymphsModules/lora
/home/nymph/NymphsModules/trellis
/home/nymph/NymphsModules/worbi
```

The local manifests were normalized toward Manifest Contract V1:

```text
packaging
source
install.root
install.entrypoint
install.version_marker
install.installed_markers
entrypoints
ui.page
ui.page_kind
ui.standard_lifecycle_rail
ui.manager_ui
```

Next contract addition to make before the heavyweight install proof:

```text
artifacts.models_root
artifacts.module_models
artifacts.shared_models
artifacts.cache_root
artifacts.outputs_root
```

Default shared roots should be:

```text
$HOME/NymphsData/models
$HOME/NymphsData/cache
$HOME/NymphsData/outputs
$HOME/NymphsData/logs
```

Model and weight downloads should not live inside disposable runtime install roots. Uninstall runtime should preserve shared artifacts by default, and purge/delete-data should only remove artifact scopes explicitly declared by the module manifest.

Module logs should be just as predictable as models:

```text
$HOME/NymphsData/logs/<module-id>/
$HOME/NymphsData/logs/<module-id>/actions/
$HOME/NymphsData/logs/<module-id>/current.log
```

Each lifecycle/action script should tee useful output into that module log root. The Manager should read `logs_dir` and optional `last_log` from status output instead of guessing old per-module folders. Normal uninstall preserves logs; purge/delete-data may remove only the module's declared log scope.

The local scripts were normalized so status reports a standard minimum:

```text
id
installed
runtime_present
data_present
version
running
state
health
install_root
logs_dir
last_log
marker
detail
```

Install scripts now write `.nymph-module-version` at the successful end of install and print:

```text
installed_module_version=<version>
```

Uninstall scripts remove the runtime marker during normal uninstall while preserving declared user data.

Central Manager lifecycle wrappers now also write a generic action state file while a module action is running:

```text
$HOME/.cache/nymphs-modules/actions/<module-id>.state
```

Minimum fields:

```text
module
action
status
pid
started_at
detail
```

The Manager checks this before normal module `status`. If the PID is still alive, the module reports an active state such as `Installing`, `Updating`, `Uninstalling`, or `Deleting` instead of falling back to `Available`.

The Manager also has a fallback for older in-flight actions that do not have an action-state file yet. It looks for Manager-owned module install/uninstall scripts in the target `NymphsCore` distro before accepting a normal `status` answer.

The module detail page also now uses a bounded live-progress area. Module install output should never stretch the page enough to hide the Manager Contract controls or bottom status bar.

Graceful Manager close now cancels active module lifecycle operations through the shared operation cancellation token and then stops Manager-owned lifecycle process trees inside the managed distro. The action-state file still matters because abrupt close/crash/Windows weirdness can leave the target-side process alive.

Partial install rule from live WORBI testing:

```text
If /home/nymph/<module> exists but .nymph-module-version is missing,
the Manager should treat it as available/not installed and should not execute old bin wrappers from that folder during status checks.
```

## Tests Passed

Builds:

```text
dotnet.exe build .../NymphsCoreManager.csproj -c Debug
powershell.exe -ExecutionPolicy Bypass -File .../build-release.ps1
```

The later Base Runtime shell action also passed Debug build.
The close/reopen lifecycle cleanup pass also passed Debug and release builds.
The module status timeout/page-pinning stability pass also passed Debug and release builds.

Static checks:

```text
registry JSON parses
all five local nymph.json manifests parse
install/status/uninstall scripts pass bash -n
manifest entrypoints exist and stay within module repos
install scripts write marker and print installed_module_version
```

Contract tests:

```text
empty fake HOME reports installed=false and state=available
preserved data without marker reports installed=false and data_present=true
marker present reports installed=true and runtime_present=true
dry-run uninstall does not remove marker or data
real fake-root uninstall removes marker/runtime and preserves data
post-uninstall status reports installed=false
```

The fake-root lifecycle harness passed for:

```text
worbi
zimage
lora
trellis
brain
```

Live WORBI test notes:

```text
WORBI install reached npm install inside the target NymphsCore distro.
Closing the Manager during that install left the target-side process running.
This exposed two standardization bugs:
1. Manager status refresh could overwrite active install UI with Available.
2. WORBI status still treated an install folder as installed even without .nymph-module-version.
```

Fixes made:

```text
Manager lifecycle guard added for active module actions.
Manager lifecycle progress box bounded on module pages.
Module detail page stays open during install/status refresh.
Module status checks are bounded so stale scripts cannot leave the UI stuck in Checking.
Status avoids stale bin wrappers when no .nymph-module-version marker exists.
Manager packaged lifecycle scripts now stage from the shipped EXE folder before falling back to GitHub.
Central lifecycle wrappers now write action state files.
Manager close now stops Manager-owned lifecycle process trees inside NymphsCore.
Manager status now detects old in-flight installer scripts even when no action state file exists.
WORBI remote repo patched so status requires .nymph-module-version.
WORBI remote repo patched so status reports the install marker version, not package.json app version.
WORBI remote repo patched so install stages into /tmp first, installs production dependencies before swapping into ~/worbi, avoids automatic ~/worbi.backup folders, and times out npm dependency install instead of hanging forever.
```

Remote WORBI commit:

```text
d6d72ac Use module version marker for WORBI install status
87fa41d Keep WORBI status version on install marker
bcf03a4 Harden WORBI staged installs
```

Current live WORBI result after the patch:

```text
installed=true
runtime_present=true
version=6.3.0
state=installed
health=unknown
detail=WORBI is installed but stopped.
```

Direct clean install timing after `bcf03a4`:

```text
Target runtime: NymphsCore
Source: pushed nymphnerds/worbi main
Result: installed=true
Version: 6.3.0
Elapsed: 14 seconds
No ~/worbi.backup.* folder was created.
```

Manager registry loader test returned all five modules with expected metadata and action capabilities.

Central wrapper tests:

```text
install_nymph_module_from_registry.sh --dry-run
uninstall_nymph_module.sh --dry-run
purge blocked for brain/zimage/lora/trellis
WORBI staged uninstall preserved data and removed marker
```

## Important Gaps

Heavy installs were not run for:

```text
Brain
Z-Image
LoRA
TRELLIS
```

The `NymphsCore` test WSL distro was cleaned for the next proof phase by removing old main-branch module installs:

```text
/home/nymph/Nymphs-Brain
/home/nymph/Z-Image
/home/nymph/ZImage-Trainer
/home/nymph/TRELLIS.2
/home/nymph/worbi
```

Old WORBI local-bin wrappers were removed from `/home/nymph/.local/bin`.

Module-owned declarative Manager surfaces are not implemented yet.

The old rich per-module Manager UI has not been moved into modules yet. This should happen after each module proves:

```text
install
status
start/stop where applicable
open/logs where applicable
uninstall
update/version marker behavior
```

Do not make the module UI migration first. A pretty module page is not useful until that module's lifecycle contract is stable.

`Delete Module + Data` is still only enabled for WORBI. Keep it that way until each module declares safe purge scopes in its manifest.

The local module repos have been normalized. The individual remote module repos still need a final push/check if they are not already synced.

The release build changed publish artifacts:

```text
Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip
Manager/apps/NymphsCoreManager/publish/win-x64/NymphsCoreManager.exe
Manager/apps/NymphsCoreManager/publish/win-x64/NymphsCoreManager.pdb
```

Release packaging currently includes only the active sidebar artwork:

```text
Manager/apps/NymphsCoreManager/AppAssets/SidebarPortraits/NymphMycelium1.png
```

The other local sidebar images are intentionally still in the source tree as WIP
art for later editing. Once those images are polished, re-expand the packaging
rule and re-enable startup/sidebar artwork variation so each Manager launch can
show a different portrait. Until then, do not ship the full WIP image set in the
release zip.

Temporary source-tree migration note:

```text
Manager/scripts/legacy now contains monolith-era helpers for Z-Image, TRELLIS,
Brain, training, runtime dependency checks, smoke tests, and old overlays.
Keep that folder for now as migration/reference material while each official
module is tested and moved into its own module repo.

Do not package or call Manager/scripts/legacy from the generic Manager shell.
Temporary exception: Z-Image `fetch_models` is currently bridged through the
old `legacy/prefetch_models.sh --backend zimage` path, with `legacy/common_paths.sh`
staged beside it inside WSL at runtime. This keeps the modular Manager using the
known-good monolith prefetch behavior while the real module-owned fetch surface is
being designed. Do not replace this with registry cloning or manifest guessing.
After WORBI, Z-Image, LoRA, Brain, and TRELLIS are all tested from their module
repos, delete whatever is left in Manager/scripts/legacy.
```

## Suggested Next Moves

1. Review the Manager diff and commit the shell/base-runtime/contract changes as one checkpoint if it looks sane.
2. Confirm the release EXE opens the new Base Runtime page and monitor mode correctly.
3. Fresh-test Base Runtime install, repair, and unregister on the target `NymphsCore` WSL.
4. Check each module repo remote and push the normalized manifest/scripts where needed.
5. Add shared artifact/model/log roots to the manifest contract before heavyweight module installs.
6. Run real WORBI install/status/open/logs/uninstall/reinstall from the Manager using the staged installer fix.
7. Explicitly test abuse paths: close Manager mid-install, reopen during install, click refresh during install, uninstall while stopped, reinstall after partial install, and force-close/crash while an action is running. The expected result is boring: no stuck Checking state, no Home-page jump, no stale Available state during install, no backup clutter, and no partial folder treated as installed.
8. Run Z-Image or LoRA next to prove the contract against a heavier AI module.
9. Use `docs/NYMPH_MODULE_UI_STANDARD.md` for custom installed-module UI. The Manager hosts installed module `ui.manager_ui` only; it must not hardcode module frontend pages.
10. After all official modules install/start/stop/log/uninstall cleanly from their own repos, delete `Manager/scripts/legacy`.
