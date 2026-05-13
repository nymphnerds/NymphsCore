# NymphsCore Changelog

This changelog tracks `NymphsCore`, the full local system made up of the Blender addon, managed runtime, backend helper scripts, and Windows Manager.

This file focuses on user-facing and system-level changes rather than package-by-package release notes.

## Detailed Timeline

Newest entries first.

### 2026-05-13 Z-Image module proof promoted to module standard
Source: live modular Manager testing with installed Z-Image Turbo, model fetch,
smoke test, and startup marker detection in the managed `NymphsCore` runtime
distro.

Changed in source:

- made installed module-owned actions run the installed script directly when the
  script exists, instead of letting registry/cache action resolution override an
  installed module
- made module action results sticky in the details pane so background status and
  manifest refreshes do not immediately erase the result
- changed successful smoke test feedback to say `SMOKE TEST PASSED` and start
  details with a clear `SUCCESS` line
- rebuilt the Win x64 published Manager EXE
- updated the module standards docs with the Z-Image proof rules:
  - fast startup installed state comes from Windows-side marker reads against
    the real `NymphsCore` runtime distro
  - startup must not run status, smoke tests, model-cache scans, or downloads
  - installed actions remain module-owned
  - native model fetch panels use `ui.manager_action_groups`
  - smoke tests must show obvious pass/fail results

Validated locally:

- Z-Image Smoke Test starts the backend, receives `/server_info`, stops the
  backend, and reports `SMOKE TEST PASSED`
- `/server_info` may report `loaded_model_id=null` during smoke test; that is
  valid for the lightweight health/config check because generation/model-load is
  not part of this smoke test

### 2026-05-13 Startup marker detection fixed for the real runtime distro
Source: live modular Manager testing from the dev/source `NymphsCore_Lite` WSL
distro against the actual managed runtime distro named `NymphsCore`.

Changed in source:

- fixed the startup installed-module marker pass so installed cards appear
  immediately from `.nymph-module-version` again
- changed the fast marker probe, when the Manager is running on Windows, to read
  markers directly through the Windows UNC view of the target runtime distro:
  `\\wsl.localhost\NymphsCore\home\nymph\<module>\.nymph-module-version`
- kept the WSL bash marker probe only as the non-Windows fallback path
- added a deferred marker-only retry if the first fast pass times out
- added a code comment at the marker scanner warning future work not to turn
  startup install truth back into a WSL bash/status probe

Why this matters:

- the Manager EXE may be launched from the dev/source distro path
  `\\wsl.localhost\NymphsCore_Lite\...`
- installed modules live in the managed runtime distro `NymphsCore`
- using WSL bash probing at startup can be slow, can race WSL wake-up, and can
  fail to see markers even though later per-module status recovers them
- startup installed state must come from cheap marker reads, not from backend
  status, model scans, smoke tests, or runtime health checks

Observed fix:

- before this fix, Z-Image appeared installed earlier because its background
  status path ran before WORBI, while the fast marker scan found zero markers
- after this fix, WORBI and Z-Image both appear installed immediately from their
  markers

Do not regress:

- `.nymph-module-version` remains install truth
- marker-installed modules must not be demoted to Available by failed status
- startup marker detection should stay Windows-side and target the real
  `NymphsCore` runtime distro
- module `status` remains the later background health/detail pass
- startup must not scan Hugging Face/model caches or run heavyweight checks

### 2026-05-13 Native model fetch panel and module-owned Z-Image fetch proof
Source: live modular Manager testing against the installed Z-Image Turbo module and the managed `NymphsCore` WSL runtime.

Changed in source:

- added a compact native Manager model-fetch panel driven by module manifest metadata rather than a module WebView2 page
- added persistent Manager-side module secret handling for the shared Hugging Face token field:
  - the visible label is expanded to `Hugging Face token`
  - saved tokens are masked with a longer password-style placeholder
  - module actions receive the token through the module-declared environment variable
- made model-fetch guide text and source links render inside the standard details pane:
  - links are clickable Manager hyperlinks, not fake buttons
  - the details pane can switch from guide text to action progress/failure feedback
- changed long module action feedback so model fetch progress stays in the module details flow instead of jumping to the global Logs page
- made the global Logs page copyable/selectable and stopped autoscroll while the user is selecting text
- hardened module action execution so installed module-owned scripts win before remote/cache refresh logic:
  - installed scripts such as `/home/nymph/Z-Image/scripts/zimage_fetch_models.sh` run directly
  - this avoids stale remote/cache manifests breaking installed module actions
  - this is the intended pattern for TRELLIS model fetch support too
- rebuilt the Win x64 published Manager EXE

Updated Z-Image module contract:

- changed the Z-Image model fetch selector from a single default weight to `Download`
- added `All weights` so Blender can later switch freely between r32, r128, and r256 presets
- kept individual published Nunchaku-compatible Z-Image Turbo weight choices available:
  - `int4_r32`
  - `int4_r128`
  - `int4_r256`
  - `fp4_r32`
  - `fp4_r128`
- improved the installed guide text for noob-friendly model choice:
  - r32 is the light/default Blender choice
  - r128 is balanced or portrait-friendly
  - r256 is highest quality and INT4-only
  - RTX 50 users should fetch `fp4_r128` or `All weights`

Validated locally:

- Z-Image Fetch Models loaded immediately after install from the standard Manager details page
- Z-Image Fetch Models began downloading through the installed module-owned script path
- the runtime manifest and script were synced into the managed `NymphsCore` WSL distro for testing
- `dotnet publish Manager/apps/NymphsCoreManager/NymphsCoreManager.csproj -c Release -r win-x64 -p:NoIncremental=true -p:EnableWindowsTargeting=true`
- `bash -n scripts/zimage_fetch_models.sh`
- `python3 -m json.tool nymph.json`

Current caveats:

- TRELLIS still needs its module manifest and fetch script updated to use the same native action group pattern
- Blender addon model-cache selection should be reviewed after Z-Image and TRELLIS model fetch paths settle

### 2026-05-13 Module-owned Manager actions and WORBI first standard proof
Source: live modular Manager testing with WORBI as the first module-owned action contract proof.

Changed in source:

- moved installed module action buttons out of Manager assumptions and into module manifests
- added Manager support for `ui.manager_actions`:
  - modules declare the button id, label, entrypoint, and result mode
  - Manager renders only the module-declared buttons in the details pane and module UI strip
  - modules with no declared actions do not get a generic fallback action strip
- added action result modes used by the first WORBI proof:
  - `open_in_manager` starts a module and opens the returned URL in the Manager WebView
  - `open_external_browser` starts a module and opens the returned URL in the default browser
  - `open_notepad` writes command output to a temp log file and opens it in Notepad
  - `show_output` keeps normal Manager feedback behavior
- changed the details label from `// MANAGER CONTRACT` to `// MODULE ACTIONS` to match ownership
- kept universal right-rail Manager controls separate from module-owned detail actions
- updated the forward-facing module guide so authors know:
  - registry is the catalog, not the installed-button contract
  - installed module actions belong in the module manifest
  - module repo changes must be pushed before registry hash/catalog updates
- updated WORBI as the first test module:
  - added `ui.manager_actions` for `Start`, `Stop`, `Browser`, and `Logs`
  - added `Nymphs Brain local AI stack` as a visible requirement
- updated `nymphs-registry` with the pushed WORBI manifest hash and matching requirement text
- rebuilt the Win x64 release EXE and ZIP

Validated locally:

- `dotnet.exe build '\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\NymphsCoreManager.csproj' -c Debug`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File '\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1'`
- WORBI module-owned buttons worked in the Manager during live testing
- pushed `nymphnerds/worbi` main commit `67b9c3c Add manager action contract`
- pushed `nymphnerds/nymphs-registry` main commit `27732ee Update WORBI manifest metadata`
- verified the remote WORBI manifest hash matches the registry hash:
  `c3f268d0970a7c5260a256efaf1b6bc27648c3415299f23be1f8c025497afec0`

Current caveats:

- this is the first working proof of the action standard; the next modules still need to adopt `ui.manager_actions`
- the Manager code is intentionally strict now: no module action manifest means no module action buttons
- richer action result types can be added later, but should be documented in the module guide before modules depend on them

### 2026-05-12 Manager UI-mode split, WORBI WebView proof, and module marker hardening
Source: live modular Manager testing in the `NymphsCore` WSL distro with WORBI as the fast reference module.

Changed in source:

- added fast installed-module marker probing so installed cards can appear before slow deep status checks finish
- kept marker-installed modules installed when status fails, instead of demoting them to Available
- added support for module-owned Manager UI metadata from manifests
- added embedded `local_url` WebView2 hosting for installed module UIs
- made WORBI `start` open its local web UI inside the Manager and added a `browser` contract action for opening it externally
- kept normal module details and custom module UI as two separate modes:
  - details mode shows the right-side `// MANAGE` rail and full contract sections
  - UI mode keeps the compact top `// manage` menu and compact contract strip
- kept update check in the gear menu
- restored module facts to the right rail in details mode while keeping them tucked away for UI mode
- made the module monogram square collapse/restore the left sidebar for more UI space
- tightened module details mode around a standard layout:
  - module header facts now sit beside the title as `// category | packaging | installed | remote | GitHub`
  - right-side actions are compact `// MANAGE` text links using the same typography as the main sidebar links
  - source/GitHub is treated as module metadata, not a large action button
  - module detail scrollbars were restyled to sit cleanly on the pane edge
- added visible update eligibility from installed/remote version comparison and exposed `Update` in the universal manage rail
- reworked the Base Runtime page toward the same details-page pattern:
  - compact `// current state | Ready` header line
  - right-side `// MANAGE` text links instead of large stacked action buttons
  - install drive display is locked to the existing runtime path once the managed distro exists
  - fresh install drive selection remains available only before the managed runtime exists
  - runtime progress now sits directly below the guide instead of hiding below empty space
- rebuilt the Win x64 release EXE and ZIP

Validated locally:

- `powershell.exe -ExecutionPolicy Bypass -File '\\wsl.localhost\NymphsCore\home\nymph\NymphsDev\NymphsCore\Manager\apps\NymphsCoreManager\build-release.ps1'`
- WORBI installed, started, and rendered its web UI inside the Manager during live testing
- `git diff --check`

Current caveats:

- Z-Image still needs the next full test pass after this UI/contract checkpoint
- module UI mode is promising, but WebView2 behavior should still be tested against Z-Image and future module UIs before treating the host contract as final
- moving an existing Base Runtime to a different Windows drive is not implemented yet; it needs an explicit migrate/reinstall flow rather than silently changing the repair target

### 2026-05-11 late Z-Image fetch models, live logs, and persistent HF token
Source: live modular Manager testing against Z-Image Fetch Models from the published Win x64 build.

Changed in source:

- fixed the Z-Image module UI cold-open regression; the hosted `local_html` page now opens instantly again in the tested build
- rebuilt and pushed the modular Manager release artifact at `Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip`
- expanded the Z-Image Fetch Models page to expose all published Nunchaku Turbo weight choices:
  - INT4 r32/r128/r256
  - FP4 r32/r128
  - Auto r32/r128 only, because r256 is INT4-only
- clarified the Z-Image UI copy: these are Nunchaku generation weights, not LoRA training precision; LoRA training BF16 is separate
- changed long module UI actions to switch to the standard Logs page so download progress is visible instead of hidden under a custom HTML panel
- kept the log stream pinned to the latest line while model downloads print progress
- made Z-Image model download status explicit, including cache size, bytes downloaded during the step, and active partial files
- added a persistent Hugging Face token field for model fetch:
  - the token is entered on the Fetch Models page
  - the Manager saves it under `%LOCALAPPDATA%\NymphsCore\shared-secrets.json`
  - the token is passed to the fetch action through `NYMPHS3D_HF_TOKEN`
  - the token itself is not printed to Manager logs
- added Manager-side hydration for cached/installed fetch-model pages so an already installed Z-Image page can show the token field without requiring a reinstall

Validated locally:

- `dotnet build Manager/apps/NymphsCoreManager/NymphsCoreManager.csproj -c Release -p:EnableWindowsTargeting=true`
- `dotnet publish ... -r win-x64`
- zip integrity check passed for `NymphsCoreManager-win-x64.zip`
- pushed `nymphnerds/NymphsCore` modular commit `b48b6f8 Add persistent HF token for model fetch`
- pushed `nymphnerds/zimage` main commit `9d59781 Add HF token field to model fetch UI`

Current caveats:

- the HF token field is now reliable enough for Z-Image fetch testing, but a future module-secret contract should replace the temporary Manager-side fetch-model compatibility shim
- Brain, LoRA, and TRELLIS still need the same start/stop/logs/install/uninstall proof pass as Z-Image and WORBI
- the Blender addon still needs a smoke test against the modular runtime layout before promotion

### 2026-05-11 Z-Image modular proof, marker recovery, and WebView2 follow-up
Source: live modular Manager testing against the managed `NymphsCore` WSL runtime and the new Z-Image module path.

Changed in source:

- bumped the local test Manager build through `0.9.13` while stabilizing the Z-Image proof loop
- added installed-module UI hosting from installed module manifests:
  - Manager reads `ui.manager_ui` only from the installed module folder
  - module-owned UI is opened from the standard right rail
  - Manager still owns the shell, simple module detail page, action rail, logs, and lifecycle routing
- moved the installed-module UI host from WPF `WebBrowser` to WebView2 in the local test build:
  - module UI still comes only from the installed module `nymph.json`
  - the module UI page keeps the Manager sidebar and uses a full-width, thin standard `Back` bar
  - WebView2 now uses an explicit local user-data folder under `%LOCALAPPDATA%\NymphsCore\WebView2`, avoiding slow browser profiles beside UNC-launched EXEs
  - WebView2 prewarm now targets the real module UI host instead of a separate offscreen helper browser
  - `local_html` module pages are loaded from the local Manager cache with `NavigateToString` instead of `file://`
  - WebView2 `data:` navigations are allowed because `NavigateToString` internally becomes a `data:` navigation
  - first module UI navigation is queued at high dispatcher priority so it does not wait behind shell/status refresh work
  - repeated navigation to the same cached module UI source is skipped
  - background module-status refresh no longer reopens the current module UI page just to update module references
  - the current tested path is intentionally simple: cached/local module HTML loaded by WebView2, no module-specific Manager UI logic
- added/updated docs for the custom module UI contract in `docs/NYMPH_MODULE_UI_STANDARD.md`
- began `docs/NYMPHS_MODULE_MAKING_GUIDE.md` as the community-facing module authoring guide
- restored selectable/copyable log text behavior without changing the visual direction, while preserving normal autoscroll when the log view is not actively selected
- preserved installed-module marker truth in Manager state:
  - `.nymph-module-version` remains the source of truth for installed runtime state
  - marker-installed modules stay in the installed group if `status` fails, times out, or incorrectly reports `installed=false`
  - status failures now become warning/detail text, not a scary top-level install state
- adjusted refresh wording so the Manager distinguishes fast shell/roster load from later live runtime/module-status refresh
- reverted the experiment that hid all module cards until live status completed; cards should remain visible from registry/manifest data and update as live status returns
- reverted the risky parallel WSL status/runtime probe experiment after it caused hangs; status probing should stay conservative until WSL/process contention is better understood
- kept old module-specific Manager code excluded from the active build while preserving legacy source files for reference during migration

Validated locally:

- Windows release build passed for `0.9.13`
- release publish completed to `Manager/apps/NymphsCoreManager/publish/win-x64/`
- release zip rebuilt at `Manager/apps/NymphsCoreManager/publish/NymphsCoreManager-win-x64.zip`
- after a Windows/WSL restart, initial system/runtime check recovered
- WORBI installed state recovered and displayed correctly again
- Z-Image install reached `installed_module_version=0.1.2`
- Manager detected Z-Image as installed from the marker

Current caveats as of the first 2026-05-11 pass:

- do not create extra local branches for this proof work
- startup/status should remain snappy but honest: show roster/cards quickly, update live status afterward, keep probes bounded, and avoid parallel WSL hammering until proven safe
- `Manager/scripts/legacy` remains packaged for now as migration reference, but should be removed from release packaging after official modules are fully migrated and tested

### 2026-05-10 Registry-driven module shell and V1 lifecycle contract
Source: Rauty plugin-manager standardization pass after hardcoded Manager module surfaces were removed from the active shell.

Changed in source:

- Manager module roster now loads from the remote `nymphs-registry` JSON instead of a hardcoded five-module seed
- registry card metadata was expanded so available modules can render useful cards before install:
  - `short_name`
  - `category`
  - `packaging`
  - `summary`
  - `install_root`
  - `sort_order`
- added a Base Runtime install/repair action to create or repair only the managed `NymphsCore` WSL shell
- Base Runtime now opens a dedicated page from the System Overview card; install/repair lives on that page instead of firing from the card
- Base Runtime page now has explicit Windows WSL readiness, install, progress, current-state, and unregister surfaces
- Base Runtime install is gated behind Windows WSL readiness; the Manager no longer implies the managed runtime can be installed before Windows WSL is available
- Base Runtime setup now avoids leaking dev-WSL paths into the target runtime by staging bootstrap scripts inside the managed `NymphsCore` distro
- Base Runtime unregister removes the managed WSL distro/runtime folder and warns that modules installed inside that distro are removed too
- runtime monitor mode was added so the Manager can collapse into a compact always-on-top-style monitor surface
- monitor mode now restores to a sane full app size even if it was entered from a tiny/sidebar-only window
- sidebar runtime monitor now includes collapsible Brain telemetry readouts for LLM state, model, context, and tokens/sec
- sidebar footer version now comes from the Manager assembly version instead of a hardcoded XAML string
- Manager app version was bumped to `0.9.3` for the current plugin-shell standardization build
- monitor mode now collapses in place instead of shifting the window right
- sidebar `Open Source` footer text now links to the main NymphsCore GitHub repository
- Logs page was changed to a selectable/copyable text surface while preserving the terminal-style visual direction
- Base Runtime progress spacing was tightened so common two-line install progress messages fit above the bottom status bar
- module detail live-progress output is now bounded so long install output cannot stretch the page and hide the Manager Contract controls
- Manager now holds a module in an active lifecycle state during install/update/uninstall/delete so background status refreshes cannot flip it back to Available mid-action
- module lifecycle scripts now write a generic action state file under `~/.cache/nymphs-modules/actions/` so a reopened Manager can identify in-progress module work
- Manager close now cancels active module lifecycle operations when the close is graceful and asks the managed `NymphsCore` distro to stop Manager-owned lifecycle process trees
- Manager status now also detects in-flight module install/uninstall scripts when an older action did not create an action-state file
- module status checks are now time-bounded so stale or broken module scripts cannot leave cards stuck in `Checking`
- status checks no longer run stale module bin wrappers when the install marker is missing, avoiding partial-install hangs
- module detail pages now stay on the selected module during install/status refresh instead of jumping back to Home when the module is not yet installed
- central Manager lifecycle wrappers are staged from the packaged Manager scripts first, keeping the EXE and install/uninstall wrapper behavior in sync
- old hardcoded module choices are intentionally skipped by base setup; modules are installed later from registry cards
- documented the next Base Runtime lifecycle direction: status, repair, helper-script updates, system-package updates, and explicit Ubuntu migration
- added generic `key=value` status parsing for module status scripts
- removed module-specific status projection helpers from the active Manager shell
- Manager lifecycle state is now driven by generic status snapshots instead of Brain/Z-Image/LoRA/TRELLIS/WORBI-specific view-model code
- normalized local module manifests for Brain, Z-Image, LoRA, TRELLIS, and WORBI toward Manifest Contract V1
- normalized local install/status/uninstall scripts around `.nymph-module-version` as installed-runtime truth
- preserved data no longer implies installed runtime in the local status contract
- release build still succeeds after the shell/contract changes

Remote registry:

- pushed `nymphnerds/nymphs-registry` commit `2e4a523 Add module card metadata to registry`
- `nymphs.json` now provides module card data for all five official modules

Remote module repos:

- pushed `nymphnerds/worbi` commit `d6d72ac Use module version marker for WORBI install status`
- pushed `nymphnerds/worbi` commit `87fa41d Keep WORBI status version on install marker`
- pushed `nymphnerds/worbi` commit `bcf03a4 Harden WORBI staged installs`
- WORBI status now follows the module standard: `.nymph-module-version` is installed-runtime truth; a leftover install folder alone is not installed
- WORBI status now reports the install marker version instead of the internal package/app version
- WORBI install now stages into a temp folder, installs production server dependencies before swapping into `~/worbi`, skips automatic backup folders, and times out dependency install instead of hanging forever
- WORBI install lessons were promoted into the module standard: staged install, marker written last, no random backup folders, bounded status, bounded progress, and manifest-declared data/log scopes
- module detail pages now expose an `Open GitHub` action when a repository URL can be derived from the module manifest or registry entry
- cleaned obsolete repository roots from the branch:
  - `ManagerFEUI/`
  - `Monitor/`
  - `WORBI-installer/`
  - `home/`
  - root `index.html`
- renamed the active development branch from `rauty` to `modular`; `main` remains the old-manager UI/workflow reference until this branch is ready to replace it
- documented the promotion checklist for branch-specific URLs
- bundled generic Manager lifecycle wrappers with the packaged EXE instead of fetching them from the branch raw URL:
  - `Manager/scripts/install_nymph_module_from_registry.sh`
  - `Manager/scripts/uninstall_nymph_module.sh`
- consolidated handoff docs around one active file: `docs/Ideas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md`
- repointed the Manager Guide button to repo docs because the old static `home/` site was removed
- moved module GitHub/source links out of the large action rail and into the compact `Module Facts` area as `Source: GitHub`

Validated locally:

- Debug build passed
- Debug build passed again after the Base Runtime shell action was added
- Debug build passed after the Base Runtime page and monitor-mode UI pass
- Debug build passed after the module lifecycle/progress stability pass
- Debug build passed after the close/reopen lifecycle cleanup pass
- Debug build passed after the sidebar footer/version and Base Runtime progress spacing pass
- Debug build passed after the module status timeout/page-pinning stability pass
- Debug build passed after the WORBI staged install note and monitor/full restore fix
- Windows release build script passed
- Windows release build script passed again after the final Base Runtime page layout update
- Windows release build script passed after the module lifecycle/progress stability pass
- Windows release build script passed after the close/reopen lifecycle cleanup pass
- Windows release build script passed after the sidebar footer/version and Base Runtime progress spacing pass
- Windows release build script passed after the module status timeout/page-pinning stability pass
- Windows release build script passed after the WORBI staged install note and monitor/full restore fix
- Windows release build script passed after bundling Manager lifecycle wrappers and after moving the module source link into facts
- registry JSON and all local module manifests parse as valid JSON
- install/status/uninstall scripts pass shell syntax checks
- central Manager install/uninstall scripts pass shell syntax checks after lifecycle action-state additions
- fake-root lifecycle contract tests passed for all five modules
- Manager registry loader returned all five modules with registry/manifest metadata
- central Manager install/uninstall wrapper dry-runs resolved module manifests and roots correctly
- live WORBI install completed inside the managed `NymphsCore` runtime and now reports installed `version=6.3.0` from `.nymph-module-version`
- direct clean WORBI install from the pushed remote script completed in the managed `NymphsCore` runtime in 14 seconds and reported `state=installed`

Current caveats:

- full heavyweight installs were not run for Brain, Z-Image, LoRA, or TRELLIS in this pass
- next proof phase should install modules one at a time into a fresh/clean managed `NymphsCore` runtime from registry cards
- WORBI still needs the full Manager-click abuse pass after the staged installer fix: install from card, close mid-install, reopen, uninstall, reinstall, start/stop/open/logs
- module-owned declarative Manager surfaces are not implemented yet
- old hardcoded Manager UI should be rebuilt as module-owned surfaces after lifecycle install/status/uninstall is proven per module
- `Delete Module + Data` remains intentionally WORBI-only until manifest-declared purge scopes are generalized
- local module repos were normalized, but individual remote module repos still need to be checked and pushed if they have not already been synced
- release artifacts under `Manager/apps/NymphsCoreManager/publish/` changed because the release build was run

### 2026-05-07 Available module cards open detail pages first
Source: WORBI reinstall UX testing after uninstall/delete became reliable.

Changed in source:

- available module cards now open the module detail page instead of immediately showing an install confirmation dialog
- available module pages show an `Install Module` action in the right rail
- installed-only actions such as `Open Install Folder` and `Uninstall Module` stay hidden until the module is installed
- opening a module page fetches the module's registry manifest and updates the live detail with manifest-backed description/version/source information

Why it matters:

- users should be able to inspect a Nymph before installing it
- community modules need a clear pre-install information surface pulled from `nymph.json`
- install should be a deliberate action from the page, not the card click itself

### 2026-05-07 Simple WSL lifecycle launcher
Source: live WORBI delete/install testing where generated `bash -lc` strings produced shell syntax errors before the helper script could run.

Fixed in source:

- replaced the install/uninstall launcher path that built one large shell command
- Manager now stages remote helper scripts through direct WSL process calls:
  - remove old temp script
  - `curl -fsSL <helper-url> -o /tmp/...`
  - `chmod +x /tmp/...`
  - `/bin/bash /tmp/... --module <id> ...`
  - remove temp script
- removed the fragile lifecycle-launcher use of `awk`, inline `$?`, and shell conditionals from the Manager command string

Why it matters:

- lifecycle actions should fail only because the module helper failed, not because the Manager mangled shell quoting
- this is the simpler long-term path for community modules
- install/uninstall/delete must be boring and predictable before more module repos are migrated

### 2026-05-07 Registry install false-failure hardening
Source: live WORBI install testing where the module installer printed success, but the Manager still showed `WORBI install needs attention`.

Fixed/recorded:

- hardened `install_nymph_module_from_registry.sh` so it captures the module install entrypoint exit code directly
- the helper now only prints `Installed <module>.` after the module entrypoint exits `0`
- the helper explicitly exits `0` after success, preventing a later wrapper cleanup/status quirk from turning a successful install into a Manager failure
- if the module entrypoint fails, the helper now prints a clear `ERROR: install entrypoint failed...` line and exits with that code

Why it matters:

- community modules must have a reliable install/update contract
- success output should not be followed by a scary false failure state
- future module install scripts should print a final version marker such as `installed_module_version=...` only after all wrapper/script setup has completed

### 2026-05-07 WORBI 6.2.54 update-loop and stop fallback fix
Source: live Manager testing where WORBI updated successfully but the module page still showed `Update available`.

Documented fixes:

- pushed WORBI module commit `0f809fb Fix WORBI update stamp and stop fallback`
- bumped WORBI's module manifest version to `6.2.54`
- kept the app archive at `packages/worbi-6.2.51.tar.gz` because this was still a wrapper/module-contract release
- made WORBI install output print `installed_module_version=...` after writing `~/worbi/.nymph-module-version`
- fixed Manager update checks to read the installed `.nymph-module-version` marker before cached module manifests
- fixed the module page update loop by treating `installed_module_version=...` from successful install output as authoritative and clearing stale `HasUpdate` state immediately
- hardened WORBI stop/status process detection:
  - scan Node processes more broadly under the WORBI install root
  - include command-line and working-directory checks
  - use port-based fallbacks when WORBI responds but PID ownership was not tracked
  - report the unmanaged/responding case as a fallback state instead of implying the user did something wrong

Why it matters:

- a successful module update must clear the Update button instead of trapping the user in an update loop
- module wrapper-only releases can fix lifecycle behavior without rebuilding the bundled WORBI archive
- stop/start wrappers need to handle real-world process ownership, not only the clean PID-file path

### 2026-05-07 WORBI 6.2.52 update loop: module-owned wrapper fix and Manager script-launch hardening
Source: live WORBI update testing through the Rauty module page after WORBI was bumped in `nymphnerds/worbi`.

Documented changes and discoveries:

- pushed WORBI module commit `f493d8f Fix WORBI manager lifecycle wrappers`
- bumped WORBI's module manifest version to `6.2.52`
- kept the app archive at `packages/worbi-6.2.51.tar.gz` because this was a wrapper-only module release
- added WORBI installer behavior that writes `~/worbi/.nymph-module-version`
- confirmed the Manager can pull a module-owned lifecycle fix without changing the module package archive
- confirmed the update flow can install WORBI successfully through the Manager:

```text
Node.js found: v18.20.8
Archive: /tmp/.../worbi-6.2.51.tar.gz
Install: /home/nymph/worbi
Existing installation found. Preserving user data...
Installing server dependencies...
WORBI installed successfully.
App: http://localhost:8082
Logs: /home/nymph/worbi/logs/
Installed worbi.
```

Manager-side launcher fixes learned from the test:

- do not inject complex shell scripts with nested heredocs into `wsl.exe ... bash -lc`
- nested heredocs broke the registry installer around its Python heredocs
- base64 staging was better but still too fragile in the live WSL command path
- the earlier branch-raw helper fetch was replaced by packaged Manager lifecycle wrappers staged into `/tmp` inside the managed distro
- critical temp paths are now literal paths like `/tmp/nymphs-manager-install-worbi.sh`, rather than relying on fragile shell variable expansion inside the command string
- install/update success must be treated separately from follow-up refresh/check warnings
- fixed a Home-page catch-22 discovered after uninstall/reinstall testing:
  - WORBI could be installed in the managed `NymphsCore` distro while the Home card still showed it under `Available Modules`
  - that blocked opening the module page because available cards ask to install instead of opening installed controls
  - WORBI state detection now prefers the module's own `status` contract over Windows-side UNC folder probing
  - install/update/uninstall success now flips the card state immediately, then runs a quieter verification refresh afterward
- intentionally avoided a background auto-refresh loop for module cards for now; module state refresh is event-based around install/update/uninstall actions

Current important caveat:

- generic Manager lifecycle wrappers are now bundled with the EXE zip under `scripts/`
- module-specific scripts still come from module repos through the registry/manifest contract

Why it matters:

- this proved the intended community-module loop:

```text
module repo changes -> nymph.json version bump -> Manager Check for Updates -> Update Module -> managed distro pulls new module scripts
```

- future module wrapper fixes should usually land in the individual module repo, not in core Manager code
- Manager only needs changes when the generic module contract or script-launch plumbing itself is wrong

### 2026-05-07 Rauty modular Manager lifecycle shell: Nymph cards, registry installs, and module pages
Source: `modular` branch module-skeleton work, especially commit `361e242` plus the follow-up live Manager/WORBI testing that happened before this changelog was updated.

Documented changes:

- added the first real modular Manager shell for the Rauty direction:
  - Home page with system overview
  - installed module cards
  - available module cards
  - module detail pages
  - logs page
  - guide page
  - shared top-bar update/settings controls
- added a dedicated module model for the new shell so modules can be represented as installed or available Nymphs instead of permanent hardcoded Manager sections
- added first-pass module roster entries for:
  - `brain`
  - `zimage`
  - `lora`
  - `trellis`
  - `worbi`
- renamed the AI Toolkit module surface to `LoRA` / `lora` in the new modular shell, because its user-facing purpose is local Z-Image Turbo LoRA training
- added module navigation entries only for installed modules
- added responsive module card layout work:
  - cards can wrap from one column to two, three, and wider layouts
  - the Manager window can now shrink to a one-card-width layout
  - home card spacing and section spacing were repeatedly tuned from screenshots
- added module page `// MANAGER CONTRACT` links for module-specific commands:
  - `status`
  - `start`
  - `stop`
  - `open`
  - `logs`
  - `configure` where applicable
- kept universal module page actions in the right rail:
  - Open Install Folder
  - Update Module when an update is available
  - Uninstall Module
  - Delete Module + Data
  - Back To Home
- moved `// LIVE DETAIL` to the wide main module page surface, because command output and failures need room
- moved `// MODULE FACTS` into a smaller right-rail card under the universal actions
- added module-specific logs handling:
  - `// logs` opens module log output on the module page
  - global Manager logs remain separate in the left sidebar Logs page
- added a gear-menu Dev Mode scaffold:
  - Dev Mode toggle
  - bottom status strip
  - future `// DEV CONTRACT` section for maintainer workflows
- added handoff docs for the modular shell and manifest direction, including:
  - `MANAGER_UI_DIRECTION_HANDOFF.md`
  - `MANAGER_UI_DESIGN_STAGE_HANDOFF.md`
  - `MANAGER_UI_CONTINUATION_HANDOFF.md`
  - `MODULAR_NYMPHSCORE_PLAN.md`
  - `NYMPH_MANIFEST_DRAFT.md`
  - `NYMPH_CORE_OBJECT_MODEL.md`
  - `NYMPH_UI_SHELL_BRIEF.md`
  - `CURRENT_NYMPH_MODULE_REPO_DEEP_DIVE.md`
  - `RAUTY_MODULE_LIFECYCLE_HANDOFF.md`

New scripts added for the module lifecycle:

- `Manager/scripts/install_nymph_module_from_registry.sh`
- `Manager/scripts/uninstall_nymph_module.sh`

Important behavior:

- registry install flow is now intended to be generic:

```text
nymphs-registry -> module nymph.json -> module repo -> install entrypoint
```

- uninstall supports preserving known user data by default
- destructive delete/purge remains explicit
- future Nymphs should not require rewriting core Manager installer scripts for every new backend

Why it matters:

- this is the point where Rauty starts becoming a real modular platform shell instead of a Manager with several permanent backend pages
- it establishes the basic contract that NymphsCore owns discovery/lifecycle/UI orchestration while each module repo owns its own package, scripts, wrappers, logs, and runtime details

Known remaining work:

- custom module pages from `main` are intentionally parked until the lifecycle loop is reliable
- one more simple module should be used to validate uninstall/reinstall/update after WORBI
- source edits after `361e242` still need a normal Windows rebuild before the running app reflects all current UI/lifecycle fixes

### 2026-05-07 WORBI registry/update test: 6.2.51, stale wrapper discovery, and WSL target confusion
Source: live WORBI update/start testing through the new module page, plus direct WSL verification after the Manager UI showed stale data and a failed start.

Documented changes and discoveries:

- connected WORBI to the registry/module lifecycle as the first real external test module
- confirmed WORBI can be represented as an installed Nymph with:
  - package kind `archive`
  - module id `worbi`
  - install root `~/worbi`
  - default URL `http://localhost:8082`
- added/used registry update comparison:
  - local cached installed manifest version
  - remote module manifest version
  - visible `Update available` state on cards/pages
- added an `Update Module` action that reruns the registry install/update flow for the selected module
- changed install/update UI feedback so `// LIVE DETAIL` streams installer progress instead of only showing a generic failure
- changed registry install failures so they include exit code and captured output
- changed module facts so installed version should be read from cached module manifests instead of showing `Manifest not wired yet`
- confirmed the old Manager UI attempt at updating WORBI failed, but the real reason the update state later disappeared was that WORBI had been manually updated successfully afterwards

Critical WSL lesson:

```text
NymphsCore_Lite = development/source checkout used by Codex and the IDE
NymphsCore      = actual managed runtime distro used by NymphsCore Manager
```

The source tree being edited lives under:

```text
\\wsl.localhost\NymphsCore_Lite\home\nymph\NymphsCore
```

The running Manager targets:

```text
\\wsl.localhost\NymphsCore\home\nymph\...
```

or:

```text
wsl.exe -d NymphsCore --user nymph -- ...
```

This mattered because WORBI was first updated manually in the dev WSL context, while the Manager was correctly still seeing the older WORBI install inside the real managed `NymphsCore` distro.

Managed distro state after correction:

```text
WORBI version: 6.2.51
Installed manifest: /home/nymph/.cache/nymphs-modules/worbi.nymph.json
Install root: /home/nymph/worbi
```

Direct managed-distro update command used:

```bash
wsl.exe -d NymphsCore --user nymph -- bash -lc 'set -euo pipefail; git -C /home/nymph/.cache/nymphs-modules/repos/worbi pull --ff-only; cp /home/nymph/.cache/nymphs-modules/repos/worbi/nymph.json /home/nymph/.cache/nymphs-modules/worbi.nymph.json; bash /home/nymph/.cache/nymphs-modules/repos/worbi/scripts/install_worbi.sh; grep "\"version\"" /home/nymph/.cache/nymphs-modules/worbi.nymph.json'
```

Confirmed output included:

```text
Archive: .../worbi-6.2.51.tar.gz
Existing installation found. Preserving user data...
WORBI installed successfully.
"version": "6.2.51"
```

Stale wrapper bug found and corrected in the real managed distro:

- old wrapper tried:

```text
/home/nymph/worbi/src/index.js
```

- current wrapper starts from:

```text
/home/nymph/worbi/server/src/index.js
```

Remaining WORBI-side issue discovered:

- `worbi-start` can report started and print the app URL, then the process exits after the WSL command returns
- manual `setsid -f` testing kept the Node server alive and made `/api/health` respond
- the likely next WORBI repo fix is to make `scripts/worbi_start.sh` detach with a strategy that survives `wsl.exe` returning and writes the correct server PID

Why it matters:

- this test proved the registry model is viable, but also proved the Manager must make target distro and module state painfully explicit
- future agents must not infer runtime state from `NymphsCore_Lite` files
- all module lifecycle smoke tests must target `NymphsCore`

### 2026-05-05 WORBI installer bootstrap and early module-repo direction
Source: `modular` branch commits `505d6b2`, `c378208`, `e426b74`, `320e4b2`, and `116a6c4`, before WORBI moved into the cleaner external module-repo/registry plan.

Documented changes:

- added a temporary `WORBI-installer/` folder to the main repo with:
  - `README.md`
  - `install.sh`
  - packaged `worbi-6.2.18.tar.gz`
- rewrote the WORBI installer README multiple times to make it more beginner-friendly
- clarified WSL usage and install steps
- simplified the early install shape:
  - `curl | tar` style install
  - can run from any directory
  - no hard dependency on being inside a `NymphsCore` checkout
  - installs to `~/worbi`
- handled the case where a user already had a `NymphsCore` folder
- removed unused Rauty home asset images after the branch merge cleanup

Why it matters:

- this was the bridge between "WORBI as a bundled folder" and "WORBI as a proper Nymph module"
- the later direction superseded this by moving WORBI to:

```text
github.com/nymphnerds/worbi
github.com/nymphnerds/nymphs-registry
```

- the temporary in-repo installer helped clarify what a module repo needs:
  - a manifest
  - install/start/stop/status/open/logs scripts
  - safe upgrade behavior
  - user-data preservation

### 2026-05-04 Z-Image LoRA usability pass: activation cleanup, runtime recovery, and follow-up performance concern
Source: live Blender -> Manager -> Z-Image -> Nunchaku LoRA testing, using both downloaded public control LoRAs and the user's own `yamamoto` LoRA, plus multiple addon / runtime correction passes after regressions were introduced mid-session.

Documented changes:

- confirmed Z-Image LoRAs are working again end-to-end from Blender through Manager into the Nunchaku runtime
- confirmed the user's own Manager-trained `yamamoto` LoRA is usable again, not just the downloaded public control LoRAs
- simplified the product direction for LoRA activation:
  - default activation is now the LoRA name itself
  - the Blender addon now inserts that activation into the visible managed prompt system instead of hiding it in a send-time append
- cleaned up the addon LoRA UI wording around activation so it is less theory-heavy
- updated the Trainer guide and LoRA handoff notes to reflect the real workflow as it exists now
- added recovery for stale saved TRELLIS GGUF enum values that were spamming Blender terminal warnings
- fixed a Manager nullability warning in `BuildRuntimeCodeModeSummary`
- adjusted the Manager sidebar/header spacing so the left logo area sits more cleanly

Important runtime reality from this pass:

- the LoRA path was working, then regressed after later backend/runtime changes were made around activation/VRAM/offload work
- the runtime was pushed back toward the simpler working-style backend path after that regression
- the current session ended with LoRAs working again, but not with a strong enough explanation of every intermediate regression to treat the backend as fully understood

Known unresolved concern going into the next session:

- Blender, and at times the whole PC, felt unusually sluggish during this work
- that still needs a focused investigation for possible:
  - timer/polling pressure
  - memory leak behavior
  - broader runtime residency / resource buildup

Why it matters:

- this is the point where LoRA usability got much closer to a real product surface:
  - easier activation behavior
  - working end-to-end LoRA path again
  - clearer docs
- but it also established the next priority clearly:
  - stop destabilizing the working LoRA backend
  - investigate performance/sluggishness before doing more feature drift

### 2026-05-03 Z-Image trainer completion fix, healthy LoRA milestone, and AI Toolkit UI caveat
Source: live end-to-end LoRA training on `yamamoto`, plus follow-up Manager work after a successful long run still looked unfinished in both Manager and AI Toolkit UI.

Documented changes:

- confirmed a real healthy Z-Image Turbo LoRA run completed through the rebuilt Manager / AI Toolkit path:
  - training reached `3000` steps
  - loss stayed finite and trended down instead of collapsing into `NaN`
  - intermediate checkpoints and final LoRA outputs were written successfully
- fixed a Manager completion bug where the trainer progress could stop at `2999/3000` and feel failed even though the final checkpoint had already been saved
- changed Manager completion detection so it no longer relies only on AI Toolkit’s stale job state or the last visible tqdm line
- taught Manager to treat the real final LoRA file:
  - `loras/<name>/<name>.safetensors`
  as the authoritative completion signal for the trainer page
- kept the Manager-side progress parser improvements so long Z-Image runs still show real warmup/training progress instead of only queue/handoff messages

Observed reality during validation:

- AI Toolkit’s own queue and overview UI can still stay stuck on:
  - `Starting job...`
  - `0 / 3000`
  - `running`
  even when the raw trainer log and final saved checkpoint prove the job is already far ahead or fully finished
- the Manager fix is specifically about making the Manager truthful at the end of a run even when AI Toolkit’s own UI remains stale

Why it matters:

- this is the first point where a long real LoRA run both completed healthily and also stopped looking like a silent failure inside the Manager
- it also locks in the practical rule for current Z-Image work:
  - trust raw trainer logs and final saved checkpoints more than AI Toolkit’s queue/overview progress surfaces

### 2026-05-03 Manager UI redesign pass: unified sidebar flow, darker shell, and ongoing control polish
Source: live visual iteration against the new Manager mockup, with repeated rebuild-and-review passes focused on making the Windows Manager feel like one coherent app instead of a mix of legacy beige panels and newer trainer UI fragments.

Documented changes:

- added a real `Manage Install` destination to the left sidebar so the install/setup flow is no longer hidden behind bottom-page back navigation
- changed sidebar behavior so:
  - `Manage Install` owns the install/setup flow pages
  - tool pages like `Runtime Tools`, `Z-Image Trainer`, and `Brain` no longer rely on the same bottom `Back` flow control
- reworked the Manager shell into a dark redesign pass with:
  - unified dark app chrome
  - a flatter green-grey shell direction
  - restyled buttons, inputs, and log surfaces
- styled more of the previously untouched pages in the install-management flow so `Welcome`, `System Check`, and related pages stop looking like an older beige app embedded inside the new shell
- restyled the embedded `Brain` monitor panel so it no longer ships as a hard black block against the new Manager theme
- centered the footer quote in the left sidebar
- kept iterating on the trainer-page controls, especially:
  - dropdown styling
  - button vividness
  - shell/background flattening

Important reality from this pass:

- the redesign direction improved a lot, but several style tweaks were noisy and required rework after live screenshots
- dropdown styling in particular regressed multiple times during the pass:
  - stock WPF white toggle chrome briefly leaked back in
  - geometry became chunkier than intended
  - later passes had to focus specifically on restoring slimmer, cleaner dropdowns
- button styling also drifted during shell recolor passes, and needed to be re-aligned with the brighter vivid shaded look the user preferred
- top-bar/title-bar matching remains a sensitive area because native Windows caption styling and the WPF client shell have to be kept in sync manually

Why it matters:

- this is the pass where the Manager finally started behaving like one navigable product instead of a collection of semi-related screens
- it also made clear that future polish work should be more disciplined:
  - preserve good geometry once it lands
  - separate background changes from control-style changes
  - stop letting shared resource edits accidentally fatten controls or mute buttons that were already approved

### 2026-05-03 Manager dark-theme redesign notes still open
Source: live visual review of the ongoing Manager charcoal/blue-grey restyle against the reference mockup.

Open notes captured:

- there is still a visible seam / tone mismatch between the native dark title bar and the app shell on some screens
- the redesign direction is now clearly graphite / blue-grey overall, with green used as a vivid accent rather than a full green sidebar shell
- some pages still need consistency cleanup so the app stops looking like a mix of old beige-era panels and new dark cards
- `Runtime Tools`, `Brain`, and `Z-Image Trainer` have all needed page-specific cleanup because shared shell styling alone was not enough
- form controls should keep moving toward the mockup style globally:
  - dropdowns
  - API/token/password fields
  - other text-entry controls
- there are still places where the page structure feels over-boxed, so flattening nested card-inside-card layouts remains part of the polish pass

### 2026-05-02 Z-Image Trainer breakthrough: `bf16` + lower LR finally produced a healthy-looking run
Source: follow-up debugging after the AI Toolkit-first handoff work, focused on repeated `loss is nan`, stale AI Toolkit status surfaces, and whether the Manager-generated trainer config itself was destabilizing Z-Image Turbo training.

Documented changes:

- traced the repeated `loss is nan` spam through AI Toolkit source instead of guessing:
  - confirmed AI Toolkit really does print `loss is nan` only when `torch.isnan(loss)` is true
  - confirmed it then replaces that loss with a zero tensor and keeps training
  - confirmed old runs were not poisoning the current run log because AI Toolkit rotates prior `log.txt` files into a `logs/` folder before each new launch
- compared Manager-generated Z-Image trainer config against AI Toolkit’s own Z-Image defaults and found a major mismatch:
  - Manager was generating `fp16`
  - AI Toolkit defaults and Z-Image model code both pointed toward `bf16`
  - this matched Tongyi-MAI / community reports that Z-Image can be numerically unstable in `fp16`
- changed Manager-generated trainer jobs to use `bf16` instead of `fp16`
- normalized learning-rate UI values so imported AI Toolkit jobs using decimal notation like `0.0001` no longer left the Manager dropdown looking blank
- validated that a later retry using:
  - `Fast Test`
  - `Low VRAM`
  - learning rate `8e-5`
  - `bf16`
  finally produced a healthy-looking live training line instead of immediate NaN spam

Observed healthy-looking signal:

- `lr: 8.0e-05 loss: 5.098e-01`

Why it matters:

- this is the first strong sign that the rebuilt Manager -> AI Toolkit trainer path is not only handing jobs off correctly, but can also drive a numerically sane Z-Image Turbo training run
- it strongly suggests `fp16` was a major part of the earlier failure pattern

What still remains messy:

- AI Toolkit overview can still sit on `Starting job...` / `Step 0` while the raw run is advancing
- AI Toolkit loss graph can still be empty even when the raw log is active, because it relies on separate `loss_log.db` telemetry
- Manager progress is now much more truthful, but the Manager live log panel can still lag behind the progress bar
- `Stop Job` appears to work, but slowly; the earlier assumption that it was outright broken turned out to be too harsh

### 2026-05-02 Z-Image Trainer handoff slog: from custom sidecar control to AI Toolkit-first job flow
Source: several days of local Manager + WSL debugging focused on one brutal theme: the trainer page had drifted into being its own orchestration system instead of a clean AI Toolkit front end, which made simple things like queueing, starting, stopping, and killing AI Toolkit far harder than they should have been.

Documented changes:

- traced the trainer history back to its original DiffSynth-style managed-sidecar form:
  - first shipped as a Manager-owned `Z-Image Trainer` flow in commit `acb21cb`
  - originally centered on its own dataset prep, caption drafting, YAML/job generation, and direct DiffSynth-style training control rather than the AI Toolkit job/queue model
  - only later began transitioning onto the AI Toolkit sidecar path
- confirmed the core architectural pain point:
  - Manager had grown a parallel control layer for trainer start/stop/status
  - AI Toolkit had its own job, queue, worker, and UI state
  - the mismatch between those two worlds caused most of the stop-button, stale-status, and “it says queued but nothing is there” failures
- pushed the trainer runtime back toward the official AI Toolkit layout:
  - repo-local AI Toolkit DB at `ai-toolkit/aitk_db.db`
  - repo-local AI Toolkit venv under `ai-toolkit`
  - official UI launch path based on the UI package scripts instead of bespoke Manager-only launch assumptions
- documented and reworked the Brain captioning side of the trainer too, because it was part of the same mess:
  - the original caption workflow revolved around `metadata.csv`
  - AI Toolkit training wanted per-image `.txt` sidecars
  - the Manager had to mirror or sync those two worlds instead of naturally sharing one source of truth
  - trainer captioning also suffered from isolated-venv confusion, image normalization issues, request-shape retries against the OpenAI-compatible vision path, and prompt-quality passes to stop captions collapsing into stale style phrases
- reworked the Manager job flow so it now hands off through AI Toolkit concepts instead of pretending to be the engine:
  - `Add Job`
  - `Start Job`
  - `Stop Job`
  - `Delete Job`
- separated job creation from job start:
  - `Add Job` now creates or updates the AI Toolkit job and prepares the trainer metadata/caption handoff
  - `Start Job` now starts the existing AI Toolkit job instead of silently recreating it
- fixed a real AI Toolkit job-config bug discovered during live testing:
  - Manager was serializing `lr` as a string like `"1e-4"`
  - AI Toolkit / bitsandbytes needed a numeric learning rate
  - this produced the runtime `'<=' not supported between instances of 'float' and 'str'` failure until the Manager config serialization was corrected
- changed the Manager-facing AI Toolkit launch target to the jobs page instead of the dashboard queue so created jobs are visible where AI Toolkit actually stores them before start
- improved Manager status so it now tries to reflect the selected AI Toolkit job and dataset rather than only dumping generic sidecar probes
- taught Manager to push AI Toolkit settings for the shared trainer folders so AI Toolkit can see the same datasets and LoRA output folders the Manager is using
- cleaned up the trainer log/status noise:
  - removed or reduced raw `ZIMAGE_TRAINER_*=` dump lines from the visible trainer log
  - cut back on sidecar-check spam after kill/start actions
  - made more of the visible status text track actual AI Toolkit job states instead of old Manager assumptions
- repeatedly reworked `Kill AI Toolkit` after live testing showed several ugly failure modes:
  - false failure logs even when the server was actually gone
  - stale browser tabs making it look alive even when the server was dead
  - process-pattern drift between `concurrently`, worker, `next start`, and `next-server`
  - misleading “not open” Manager status while the browser still showed the last rendered page
- reached a usable checkpoint where the fundamental flow now works again:
  - Manager can create/update a real AI Toolkit job
  - Manager can start that job
  - Manager can stop that job
  - Manager can kill the AI Toolkit server

Pain points this pass exposed:

- the original trainer module was not AI Toolkit-first, and that architectural drift cost days
- queue state, idle job state, and dashboard state were easy to confuse because AI Toolkit shows them in different places
- browser-page visibility after shutdown made the UI feel “still alive” even when the AI Toolkit server was actually gone
- Manager had several places where it was telling a simpler story than the underlying AI Toolkit runtime was actually living
- Brain captioning pain overlapped with the backend pain:
  - CSV vs `.txt` caption truth
  - caption review/edit expectations
  - local trainer-venv module drift
  - prompt wording and quality issues that made “captioning works” and “captioning is usable” two different milestones
- progress is real, but the trainer page is still not finished and still needs cleanup around dataset/image visibility, final wording, and overall simplicity

Validation:

- confirmed a real AI Toolkit job can now be created from the Manager and appears under the AI Toolkit jobs flow
- confirmed `Start Job` can move that job into AI Toolkit runtime state instead of only creating a placeholder
- confirmed `Stop Job` now works through the AI Toolkit job path
- confirmed `Kill AI Toolkit` can now stop the server even though stale browser tabs can still visually confuse the result until refreshed
- confirmed the trainer dataset visibility can now be checked against AI Toolkit instead of guessed from Manager-only state

Why it matters:

- this is the point where the trainer finally starts behaving like an AI Toolkit front end instead of a competing trainer controller
- just as importantly, the changelog now reflects the painful reality: this was not a neat one-shot integration, it was a multi-day cleanup of a split-brain trainer architecture, and the job still is not fully finished

### 2026-05-01 Z-Image Trainer AI Toolkit transition, preset cleanup, and UI launch recovery
Source: live Manager and `NymphsCore` WSL testing focused on bringing the trainer onto the AI Toolkit sidecar path, tightening the training presets, and restoring the Official UI / Nymphs UI launch flow so end-to-end LoRA testing could continue.

Documented changes:

- switched the managed trainer install flow onto the AI Toolkit sidecar script at `install_zimage_trainer_aitk.sh`
- updated trainer copy in the Manager away from stale DiffSynth wording and toward explicit `Z-Image Turbo` / `AI Toolkit` terminology
- added a cleaner preset model for training:
  - `Baseline`
  - `Style`
  - `Style High Noise`
- added an explicit training-adapter selector in the Manager:
  - `v1 (Recommended)`
  - `v2 (Experimental)`
- changed trainer job generation to AI Toolkit-style YAML configs and mirrored `metadata.csv` captions into per-image text captions for the sidecar flow
- added managed stop support for active trainer jobs from the Manager
- improved trainer live-log streaming so carriage-return progress updates show up instead of feeling frozen during long runs
- increased the trainer log panel height slightly for easier monitoring during training
- reorganized the trainer page flow so dataset/output actions sit nearer the LoRA name and setup flow
- changed the trainer name field into an editable existing-dataset picker so users can select current picture sets from `/home/nymph/ZImage-Trainer/datasets` instead of relying on hidden naming conventions
- repaired the trainer sidecar scripts for current AI Toolkit / Gradio behavior, including the Gradio 6 launch-path compatibility fixes
- restored the Official AI Toolkit UI launch path to the fast working `npm run start` behavior after confirming the heavier `build_and_start` path was blocking day-to-day launch reliability in live testing
- fixed the final Official UI browser-launch timing issue by waiting briefly for `localhost:8675` before opening the browser
- updated `Caption with Brain` to normalize source images before upload, retry a second OpenAI-compatible image request shape when needed, and use the trainer venv Python instead of bare `python3`
- added `Pillow` to the trainer install/repair path and to the caption-sync fallback so Brain-assisted captioning works inside the isolated trainer venv
- tuned the style-caption drafting prompt to push harder against medium/style phrases such as `woodblock`, `woodblock print`, and `Japanese woodblock print`
- added a standalone internal trainer feature map at `docs/z-image-trainer-features.md`

Validation:

- confirmed `Nymphs UI` launches successfully from the Manager after repair
- confirmed the live `Official UI` launcher in `NymphsCore` starts `next start --port 8675` and binds `localhost:8675`
- confirmed the remaining browser-side failure was launch timing rather than a dead UI service
- confirmed `Official UI` opens again once the Manager waits for the local port before opening the browser
- confirmed the live trainer datasets in `NymphsCore` are discoverable as `my_first_lora` and `ukiyo`, and switched dataset enumeration to direct WSL-path filesystem listing instead of a brittle shell roundtrip
- confirmed `Caption with Brain` now runs far enough to produce real caption drafts, with remaining quality issues narrowed down to prompt wording rather than missing modules or broken helper launch paths

Why it matters:

- NymphsCore’s trainer path is now aligned around the AI Toolkit sidecar instead of split old/new flows, the Manager is back to a usable state for real LoRA testing, and the caption/dataset workflow is more discoverable and much less brittle.

### 2026-04-29 Z-Image Trainer end-to-end LoRA training and Brain-assisted captioning
Source: live Manager testing took the new Z-Image Trainer from first install through Brain-drafted captions, a real training run, and produced LoRA checkpoint files.

Documented changes:

- added the managed `Z-Image Trainer` flow in the Windows Manager with trainer install, dataset prep, caption drafting, job creation, training start, repair, and runtime status hooks
- moved trainer assets into a self-contained `/home/<user>/ZImage-Trainer` layout for datasets, LoRAs, jobs, logs, adapters, and the isolated DiffSynth sidecar
- added `Caption Brain` integration so the trainer can temporarily start Nymphs-Brain with a local vision GGUF plus `mmproj`, write trainer-ready `metadata.csv`, and keep the captions user-reviewable instead of silently treating them as final
- aligned trainer metadata to the trainer-native `image,prompt` shape while still tolerating older caption rows during the transition
- added automatic Turbo training-adapter preparation for `ostris/zimage_turbo_training_adapter`
- added trainer install-time prefetch of the heavy `Tongyi-MAI/Z-Image-Turbo` training bundle so `Add Trainer` warms the large first-run model downloads instead of surprising the first training job
- fixed the managed training wrapper to accept Manager-written caption/image fields, resolve dataset image paths reliably, and launch real DiffSynth training successfully
- fixed trainer output discovery so finished LoRAs are found recursively inside subfolders like `loras/<name>/epoch-*.safetensors`
- improved the trainer log flow with clearer bootstrap messaging during the quiet first model load

Validation:

- user confirmed `Caption Brain` drafted valid captions into `metadata.csv` from a local Qwen2.5-VL-7B GGUF + `mmproj`
- user completed a full `Stylized Look / Quick Test` training run from the Manager
- user confirmed real LoRA outputs were created at `ZImage-Trainer/loras/my_first_lora/epoch-0.safetensors` and `epoch-1.safetensors`
- verified shell syntax, Python compile, and `git diff --check` for the managed trainer scripts and support files during the implementation pass

Why it matters:

- NymphsCore now has a real end-to-end local LoRA training path for Z-Image, with Brain-assisted caption drafting and a first successful managed training workflow instead of a hand-run experimental side process.

### 2026-04-28 Nymphs-Brain llama-server migration and Manager integration
Source: Rauty's Nymphs-Brain branch moved the local LLM runtime onto direct `llama-server` control while keeping LM Studio for model download and management.

Documented changes:

- migrated Nymphs-Brain from the LM Studio server runtime to `llama-server` on `http://localhost:8000/v1`
- kept LM Studio CLI as the local model download and model management surface
- reworked Brain model management from Plan/Act profiles to Local/Remote model settings
- added the embedded Brain monitor panel showing runtime state, model, context size, GPU VRAM, GPU temperature, and tokens/sec
- restored Open WebUI tool seeding through the MCP + `mcpo` OpenAPI bridge for Filesystem, Memory, Web Forager, Context7, and LLM Wrapper tools
- added OpenRouter key handling for the remote `llm-wrapper` tool path
- updated Brain start/update/stop flows for `llama-server`, MCP proxy, `mcpo`, and Open WebUI
- changed Update Stack to refresh the new Brain scripts and Open WebUI packages without running legacy LM Studio server update logic
- improved Brain stop scripts so stale PIDs and slow service shutdowns do not hang the Manager
- updated the Manager Brain page layout, live monitor refresh, and sidebar copy for the new stack

Validation:

- user rebuilt the Manager and ran repair against the test WSL distro
- confirmed Brain start brings up the LLM and MCP services
- confirmed Open WebUI loads all five seeded tools
- confirmed the monitor reports model, context, GPU VRAM, GPU temperature, and tokens/sec
- confirmed Update Stack completes on the new path after removing the legacy `lms-update` sequence

Why it matters:

- Nymphs-Brain now uses the more flexible `llama-server` backend while preserving LM Studio's model-management convenience, and the Manager can install, repair, monitor, update, and stop the full Brain stack from one page.

### 2026-04-27 Installer commit-pin checkout fix
Source: fresh installer testing failed while cloning the TRELLIS.2 GGUF helper package because the installer treated a pinned commit SHA as a Git branch name.

Changed:

- fixed `install_trellis.sh` so GGUF helper repos are cloned first, then fetched and checked out at the pinned ref
- applied the same fix to the packaged Windows Manager script copy
- kept the runtime dependency pins intact while allowing both branch refs and commit SHAs to work

Validation:

- verified shell syntax for the source and packaged script copies
- verified the pinned `ComfyUI-Trellis2-GGUF` and `ComfyUI-GGUF` commits can be fetched and checked out

### 2026-04-26 Site navigation, guides, and feedback page refresh
Source: live GitHub Pages polish pass focused on making the public site read less like an alpha landing page and more like a durable addon home with clearer support and guide paths.

Documented changes:

- turned `home/alpha.html` into the permanent Blender addon bug-report and feedback page instead of an alpha-only page
- added clearer bug-report links from the main site pages and unified the top-left NymphsCore branding across the core public pages
- built out a user-facing guides section with a demo-first landing page, a wiki-style guide index, and initial workflow guide pages for install, first image generation, refinement, and image-to-shape
- normalized hero spacing, header rhythm, and cross-page visual consistency for `index.html`, `brain.html`, `llmwrapper.html`, `guides.html`, and `alpha.html`
- changed small-width navigation from disappearing links to a two-row header pattern so all primary links remain visible on narrower screens
- refined the homepage hero headline treatment, including the final reflective copy direction and line-by-line color progression while keeping the Blender lockup intact
- removed an experimental homepage sigil overlay after live review showed it hurt clarity more than it helped

Why it matters:

- the public site now behaves more like a product home and support surface instead of a temporary alpha page, with clearer navigation, more durable guide structure, and cleaner responsive behavior

### 2026-04-25 Manager Runtime Tools backend-specific fetch fix
Source: fresh installer testing showed the TRELLIS.2 GGUF Runtime Tools card could report a missing managed adapter, but pressing its `Fetch` button ran the all-backend model prefetch path and rechecked/downloaded Z-Image/Nunchaku weights before failing on the missing TRELLIS adapter helper.

Changed:

- added backend selection to `prefetch_models.sh` so Runtime Tools can fetch `zimage`, `trellis`, or `all`
- added an installer TRELLIS.2 GGUF download selector, defaulting to `All quants`, so fresh installs can prefetch `Q4_K_M`, `Q5_K_M`, `Q6_K`, and `Q8_0` in one pass for later Blender-side switching
- added the same TRELLIS.2 GGUF download selector to Runtime Tools so backend-specific `Fetch` no longer hides which quant set will be downloaded
- made the TRELLIS.2 GGUF server report locally complete GGUF quants and filtered the Blender addon quant dropdown to only show available choices
- bumped the branch addon feed to `1.1.197`
- changed the Z-Image card fetch button to run only Z-Image model prefetch
- changed the TRELLIS card fetch button to run only TRELLIS GGUF model prefetch when models are missing
- changed the TRELLIS card to show `Repair` when the managed GGUF adapter or GGUF runtime packages are missing, and sync the packaged adapter scripts into the TRELLIS runtime instead of downloading unrelated models
- tightened TRELLIS Runtime Tools status so either missing GGUF adapter file is reported as an adapter repair problem
- fixed the TRELLIS adapter repair command to use an explicit `/home/<user>/TRELLIS.2/scripts` target path so it cannot collapse to `/scripts` if shell variable expansion fails
- corrected the Runtime Tools summary and post-fetch success text so one ready backend or model-ready backend no longer hides another backend that still needs repair before smoke testing

Validation:

- verified `prefetch_models.sh --help` and shell syntax for the source and packaged script copy
- Windows/.NET manager compile could not be run from this WSL shell because Windows executable interop is unavailable here

### 2026-04-25 TRELLIS GGUF shape/texture settings audit
Source: live branch testing of the TRELLIS.2 GGUF Shape panel found several controls that either did not apply on the new GGUF path or behaved differently between shape-only and shape+texture runs.

Documented changes:

- fixed GGUF `Faces` target handling for shape-only exports and kept it active for shape+texture exports
- hid unsupported GGUF Shape+Texture controls from the combined shape panel instead of presenting settings that the backend could not honor
- wired GGUF textured-export `UV Angle` and fixed Sparse Res `Auto` to match the selected pipeline
- consolidated TRELLIS shape presets into the user preset folder and cleaned stale legacy preset JSONs from older builds
- fixed GGUF retexture UV-angle conversion from degrees to radians
- preserved the user's `Also Generate Texture` checkbox state during backend refreshes so the panel no longer collapses or silently unchecks texture mid-run
- removed the current custom `Mesh Cleanup` / `Remove Flat Debris` UI and backend helper because it was a narrow shape-only postprocess, not a real TRELLIS pass
- documented that cleanup is still important: live textured GGUF testing can still produce a wide floor/backdrop plate even when `Auto Remove Background` is enabled
- fixed GGUF selected-mesh retexture startup by making the standalone GGUF model shim resolve the required non-GGUF shape SLat encoder as an explicit GGUF support checkpoint
- updated Manager model prefetch, Runtime Tools status, and install verification so this GGUF support checkpoint is fetched and checked with the rest of the TRELLIS.2 GGUF model bundle

Validation:

- user confirmed the Shape panel no longer collapses mid-pass after the texture-state fix
- user confirmed textured output is present on the imported mesh
- code review confirmed `Auto Remove Background` is wired into the GGUF adapter, but it depends on `rembg` and cannot guarantee removal of floor, shadow, or backdrop regions that remain in the source image
- verified the patched GGUF model shim resolves `shape_enc_next_dc_f16c32_fp16`; if absent, it now fetches only that required support checkpoint from `microsoft/TRELLIS.2-4B` rather than depending on an official TRELLIS runtime install
- verified Manager script syntax and local-only support-checkpoint detection

Why it matters:

- the GGUF Shape panel now exposes fewer fake controls, preserves user texture intent more reliably, and has a clear next cleanup target: a deliberate `Postprocess / Cleanup / Retopo` pass that works for both shape-only and shape+texture outputs

### 2026-04-23 Z-Image img2img installer branch and addon release
Source: live Lite distro testing confirmed local Z-Image image-to-image generation can run through Nunchaku with a compatibility shim against the current diffusers Z-Image pipeline.

Documented changes:

- added an experimental Nunchaku Z-Image img2img backend branch in `nymphnerds/Nymphs2D2`
- added a Nunchaku forward shim for current diffusers Z-Image transformer signatures
- resized guide images server-side before img2img generation to avoid latent shape mismatches
- made backend HTTP 500 responses and active-task state include the real exception detail
- updated Manager install, status, verify, and prefetch scripts so Z-Image readiness requires the base model, Nunchaku rank weights, the Z-Image img2img pipeline, and the shim import
- pointed the Lite installer test branch at the temporary Nymphs2D2 img2img branch
- bumped the Blender addon through `1.1.155`
- added Z-Image `Image to Image` controls and a guide-image picker in the Image panel
- moved Z-Image runtime controls outside the Image Generation foldout and moved Image output folder actions below Generate
- updated addon docs, user guides, feature docs, and architecture notes to remove stale multiview references and document local Z-Image img2img

Validation:

- generated a Lite distro output named with `img2img`
- confirmed its metadata used `mode=img2img`, `runtime=nunchaku`, strength `0.55`, and Z-Image Turbo
- verified addon and backend Python syntax, Manager shell script syntax, and extension zip hashes during the test pass

Why it matters:

- the Lite branch now has a testable local image-to-image path using the same Nunchaku-optimized Z-Image runtime as txt2img, and fresh installer testing can pull the matching backend branch instead of relying on hand-copied files

### 2026-04-23 Manager Z-Image prefetch verification fix
Source: live Lite distro testing showed `Fetch Models` could report completion while Z-Image still lacked required Nunchaku weights.

Documented changes:

- made the Manager fetch path refresh backend scripts/environments before model download so existing distros use the fixed Z-Image prefetcher
- replaced the Manager's optimistic post-fetch ready flag with a real runtime status probe
- surfaced Z-Image offline verification details in the Runtime Tools status card when required weights are missing
- updated fresh-install verification to run Z-Image's local-only model check when a model cache is present
- clarified the Manager model download list includes `nunchaku-ai/nunchaku-z-image-turbo` weights
- bumped the Blender addon to `1.1.151` and changed its default WSL target to the Lite distro name, `NymphsCore_Lite`
- changed `Fetch Models` to run the model prefetch script directly instead of running the full runtime finalize/repair path

Why it matters:

- the Manager no longer declares a Z-Image fetch healthy unless the backend can verify both the base model and the Nunchaku transformer weights offline, `Fetch Models` no longer reinstalls runtime packages, and the Lite addon no longer quietly launches the old `NymphsCore` distro by default

### 2026-04-23 Z-Image generation failure diagnostics and launch env alignment
Source: live testing showed Z-Image requests could fail with a generic HTTP 500 while the backend kept the useful exception detail in active-task state.

Documented changes:

- bumped the Blender addon version to `1.1.150`
- made addon HTTP errors query `/active_task` after a failed `/generate` call so Blender shows the real backend failure detail
- aligned the addon Z-Image launch environment with both `Z_IMAGE_*` and legacy `NYMPHS2D2_*` names
- updated the local Z-Image backend to accept both env-name families, include real exception text in HTTP 500 details, and prefetch required Nunchaku rank weights

Validation:

- local cache check confirmed the base Z-Image model was present but `nunchaku-ai/nunchaku-z-image-turbo/svdq-int4_r32-z-image-turbo.safetensors` was missing
- `prefetch_model.py --local-files-only` now fails explicitly on the missing Nunchaku rank file instead of letting generation discover it later

Why it matters:

- the UI will stop hiding the real reason Z-Image failed, and the model prefetch path now covers the lazy-loaded Nunchaku transformer file needed for generation

### 2026-04-23 Lite Blender addon 4-view UI removal
Source: Lite branch review showed the remaining `4-View MV` toggle was a leftover from the removed Hunyuan multiview workflow.

Documented changes:

- bumped the Blender addon version to `1.1.148`
- removed the visible `4-View MV` toggle from the Z-Image prompt panel
- stopped saved legacy `4-View MV` state from redirecting `Generate Image` into the old multiview generation path
- removed the registered MV generation operator from the Lite addon package
- kept the legacy property only as a no-op compatibility field for older `.blend` state

Why it matters:

- Lite now presents Z-Image as a single-image/variant generator feeding TRELLIS, without a Hunyuan-era multiview control that no longer belongs in this edition

### 2026-04-23 Blender addon runtime rediscovery fix
Source: live addon testing showed Z-Image could keep running while a restarted Blender session showed it as stopped.

Documented changes:

- bumped the Blender addon version to `1.1.147`
- made the runtime status poller probe all known local service ports instead of only services with addon-owned process handles
- scheduled the first runtime probe shortly after addon registration

Why it matters:

- after restarting Blender, the addon can rediscover an already-running Z-Image server on `8090` and mark it ready without forcing a pointless restart

### 2026-04-23 Blender addon prompt text editor round-trip fix
Source: live addon testing showed the prompt area could feel collapsed after applying text from Blender's Text Editor.

Documented changes:

- bumped the Blender addon version to `1.1.146`
- restored visible editable prompt and extraction-guidance fields under the Text Editor controls
- kept the relevant prompt foldouts open when opening, applying, quick-editing, or clearing text

Why it matters:

- users can open the Blender Text Editor for long prompt edits, return to the Viewport, click `Apply`, and see the applied text remain in the prompt surface instead of losing the working area

### 2026-04-22 hardened `nymphscore-lite` Manager and Brain follow-up testing
Source: live Manager testing against the `NymphsCore_Lite` distro after the tarless repair path completed.

Documented changes:

- removed the optional base tar/package row from visible System Check because Lite no longer requires a hosted prewarmed tar
- adjusted the Manager UI scale to 85% and trimmed Runtime Tools/System Check copy so pages fit without unnecessary micro-scroll
- moved Manager branding assets into the app-local `AppAssets` directory so rebuilt branding is predictable
- rebuilt and committed the Lite Windows Manager release artifacts so `publish/NymphsCoreManager-win-x64.zip` and `publish/win-x64/NymphsCoreManager.exe` match the current source
- made the Brain page plan-first:
  - local `Plan` is the primary configured model role
  - `Act` can remain external for workflows that use an online action model
  - model manager menus and profile helpers now present/set `Plan` before `Act`
- hardened Brain service controls:
  - `Start Brain` starts the LLM and MCP gateway
  - the primary action becomes `Stop Brain` whenever any Brain service is running
  - `Stop Brain` stops WebUI, MCP, and LM Studio/LLM as applicable
- hardened Brain wrapper refresh:
  - `Update Stack` now refreshes local Brain wrapper scripts before updating LM Studio/Open WebUI
  - Brain reinstall/update preserves existing Plan/Act profile selections
  - `brain-status` falls back to the OpenAI-compatible model list if LM Studio's `lms ps` output is empty
- made LM Studio model loads non-interactive with `lms load -y` so script-driven starts do not hang on model-choice prompts

Validation:

- local Brain services were manually stopped and ports `1234`, `8081`, and `8100` were confirmed clear
- Brain load output showed Qwen loading successfully, which exposed the UI state bug that kept `Stop Brain` hidden
- shell syntax checks passed for both source and packaged Brain install scripts after the plan/act hardening pass

Why it matters:

- the Lite Manager no longer suggests a tar is required
- the Brain page has an always-available stop path for partial service states
- repair/update is less likely to leave old installed Brain wrappers stuck inside the distro
- Plan-local / Act-external workflows are now supported by the intended UI and script flow

### 2026-04-22 validated `nymphscore-lite` tarless repair flow
Source: live Manager repair testing against the `NymphsCore_Lite` WSL distro after removing the prewarmed `.tar` dependency from the Lite branch.

Documented changes:

- created the permanent `nymphscore-lite` branch as the no-Hunyuan, no-prewarmed-tar edition track
- changed the Lite distro identity to `NymphsCore_Lite` so it can coexist with an existing `NymphsCore` distro during testing
- removed Hunyuan 2MV from the Lite installer, Manager runtime surface, addon surface, verification scripts, smoke tests, and docs
- replaced the prewarmed distro assumption with a tarless bootstrap/finalize path that installs required Linux packages and managed repos into a fresh distro
- updated managed backend repo handling for the `nymphnerds` repo locations and public clone flow
- improved managed repo repair so incomplete Git checkouts can be repaired in place or recloned cleanly without leaving timestamped backup folders behind
- kept `Z-Image` plus Nunchaku as the fast local image lane and kept official `TRELLIS.2` as the native 3D lane
- made `flash-attn` remain mandatory for TRELLIS while restricting its build to the detected local CUDA architecture where possible
- added planning docs for future local Gemini-style analysis, Brain-managed GGUF profiles, and possible low-VRAM Trellis GGUF experiments

Validation:

- live tarless Manager repair completed successfully against `NymphsCore_Lite`
- Z-Image clone and managed repo repair behavior were exercised during installer testing
- TRELLIS flash-attn diagnostics confirmed the installer was no longer compiling unrelated CUDA architecture families when detection succeeded

Why it matters:

- Lite no longer needs a hosted 3.4 GB prewarmed distro tar to repair or finish setup
- the branch now has a viable path toward a redistributable installer that rebuilds from scripts, public repos, and model downloads
- `NymphsCore_Lite` can be tested side-by-side with the existing full `NymphsCore` distro

### 2026-04-21 reworked the Brain page and added role-aware `Act` / `Plan` model profiles
Source: iterative Manager UI work on the `brain-activity` branch, plus Linux-side Brain script refactors to support separate Cline planning and execution models cleanly.

Documented changes:

- moved `Nymphs-Brain` out of `Runtime Tools` and into its own dedicated Manager sidebar page
- reworked the Brain page UI:
  - added Brain-specific status cards for `LLM Server`, `MCP Gateway`, `Open WebUI`, and `Current Model`
  - added a dedicated Brain activity log panel
  - replaced the old inline Brain footer/actions with a dedicated Brain control surface
  - added Brain-specific sidebar artwork using `NymphBrain.png`
- improved Brain page controls and behavior:
  - Brain actions now stay on the Brain page instead of bouncing to `Runtime Tools`
  - WebUI now uses a true start/stop toggle instead of only an open action
  - added stack update support for the Linux-side Brain components
  - improved log auto-follow behavior for long model-load output
- refactored the Brain model-loading flow from a single mutable `lms-start` selection into saved role profiles:
  - added one primary `Act` model profile
  - added one optional `Plan` model profile
  - made `lms-start` load `Act`, then `Plan` if configured
  - kept single-model behavior by allowing `Plan` to remain blank
- made the Brain model manager role-aware:
  - `Set Act Model From Downloaded`
  - `Set Plan Model From Downloaded`
  - `Download New Model For Act`
  - `Download New Model For Plan`
  - `Clear Act Model`
  - `Clear Plan Model`
- added Brain profile helper commands:
  - `lms-get-profile`
  - `lms-set-profile`
- updated `brain-status`, the Brain page model card, READMEs, and Cline quickstart docs to report and explain the new role-aware model setup

Why it matters:

- the Manager now treats Brain as a first-class subsystem instead of a bolted-on runtime footer
- the local Brain stack can now support separate Cline `Plan` and `Act` models without forcing a second unrelated server design
- users can safely configure one model or two models, depending on hardware and workflow, while keeping the Linux Brain runtime understandable

### 2026-04-21 moved active managed repos to `nymphnerds` and validated installer repair flow
Source: repo-owner migration, Manager script/default URL cleanup, rebuilt Manager artifacts, and repair-log validation on the live WSL runtime.

Documented changes:

- moved active GitHub repos to `nymphnerds`:
  - `NymphsCore`
  - `Hunyuan3D-2`
  - `Nymphs2D2`
- updated Manager source scripts and bundled publish scripts so active repo defaults no longer point at `Babyjawz`
- repointed the live helper checkout under `/opt/nymphs3d/NymphsCore` to `nymphnerds/NymphsCore`
- repointed local backend checkouts:
  - `/home/nymph/Hunyuan3D-2` -> `nymphnerds/Hunyuan3D-2`
  - `/home/nymph/Z-Image` -> `nymphnerds/Nymphs2D2`
- verified repair/install logs after the migration cleanup:
  - backend repo remote mismatches cleared
  - helper repo fetch permissions fixed
  - final repair completed successfully without active `Babyjawz` pulls

Why it matters:

- the managed runtime no longer depends on the old `Babyjawz` account for active installs or repair flows
- the distributed Manager package and the live WSL runtime now agree on the new `nymphnerds` repo defaults
- any future cleanup of legacy `Nymphs3D` naming can happen as a separate, lower-risk refactor

### 2026-04-19 optional Nymphs-Brain module integrated into Manager
Source: fresh Windows installer testing of the optional local LLM stack and the follow-up source fixes needed to make it behave like a real supported module.

Documented changes:

- added optional `Nymphs-Brain` install flow to the Manager under `/home/nymph/Nymphs-Brain`
- added Runtime Tools controls for Brain:
  - `Start LLM`
  - `Open WebUI`
  - `Manage Models`
  - `Stop LLM`
- documented Brain in the Manager README and beginner install guides
- documented Brain as optional and safe to skip for Blender-only users
- added the local Open WebUI / MCP / LM Studio runtime story behind the Brain module
- corrected the Brain runtime wiring after fresh-user testing:
  - fixed status checks that could hang the Runtime Tools page
  - fixed MCP gateway startup
  - fixed packaged-script sync and publish artifacts
  - cleaned manager live-log output

Why it matters:

- the NymphsCore system now includes an optional local LLM/WebUI/tooling layer instead of only the 3D backends
- the Brain module is now part of the user-facing docs and not just an internal experimental thread
- Blender-first users can still ignore it completely without affecting the main backend install

### 2026-04-24 Brain llm-wrapper integrated as a Manager-first feature
Source: follow-up Brain installer work after testing `context7`, `mcpo`, Open WebUI seeding, and the new cached remote wrapper runtime on live Windows + WSL installs.

Documented changes:

- integrated the dropped `remote_llm_mcp` bundle into the repo as a tracked NymphsCore component
- changed the Brain installer to copy and launch the bundled `cached_llm_mcp_server.py` runtime instead of the older pip-only path
- added a compact Brain-page `OpenRouter key` row plus `Apply Key` action in Manager
- made `llm-wrapper` optional so Brain still starts cleanly when no OpenRouter key is present
- added remote `llm-wrapper` model selection to the same `Manage Models` flow that already handles local `Plan` / `Act`
- updated Brain status so the Manager card shows the assigned remote model when present
- verified Open WebUI tool-server seeding for `filesystem`, `memory`, `web-forager`, `context7`, and `llm-wrapper`
- documented the direct wrapper test endpoint and confirmed cache `MISS` / `HIT` behavior
- refreshed Brain docs, HTML pages, README copy, and the bundled wrapper README around the Manager-first setup path

Why it matters:

- `llm-wrapper` is now part of the actual NymphsCore Brain product path instead of a sidecar shell-thread
- Brain can expose local and remote model roles from one central management surface
- users can enable or skip the remote delegation layer without destabilizing the rest of Brain
- repo docs now describe the same workflow people actually use in the Manager UI

### 2026-04-18 commercial docs and addon preset cleanup
Source: remote repository cleanup for the planned commercial Blender addon product.

Documented changes:

- expanded the customer-facing Blender addon user guide
- removed local handoff notes from tracked public docs
- added ignore rules so future handoff notes stay local
- corrected public Manager download links to the current `Manager/apps/...` path
- consolidated packaged addon subject and style prompt preset JSON under one `prompt_presets/` source folder

### 2026-04-17 moved Unity packages to `nymphnerds/unity-packages`
Source: repository structure cleanup after deciding Unity packages should live outside the NymphsCore runtime/addon repo.

Documented changes:

- created `https://github.com/nymphnerds/unity-packages`
- split the existing `Unity/` tree into the new repo with package history preserved
- added a root README to the new Unity packages repo
- updated the Unity install URL to `https://github.com/nymphnerds/unity-packages.git?path=/TDC-Camera`
- removed `Unity/` from NymphsCore
- updated the NymphsCore README structure and related-repo links

Why it matters:

- NymphsCore is now focused on the Manager, runtime, and Blender addon source
- Unity package development has its own clean repo and install URL

### 2026-04-17 published Nymphs `1.1.141` to the extension feed
Source: release pass after Blender testing confirmed the guided image part extraction workflow was ready to publish.

Documented changes:

- committed the current Blender addon source to `nymphnerds/NymphsCore`
- mirrored the addon package to `nymphnerds/NymphsExt`
- updated the Blender extension feed `index.json` to point at `nymphs-1.1.141.zip`
- updated feed archive size and SHA256 hash
- updated Core and extension README docs with the master-image -> plan -> extract selected workflow
- left unrelated Manager publish binary changes out of the release commit

### 2026-04-17 clear stale part plans on source change
Source: follow-up after testing showed the planned parts list could remain from an older source image.

Documented changes:

- bumped the local addon test package to `1.1.141`
- clear the old part plan, results path, and checklist when the part-extraction source image changes
- made `Use Image` read the current Image field directly instead of preferring a stale part-extraction source

### 2026-04-17 shape texture direct layout and part selection clarity
Source: follow-up after testing showed Shape and Texture had redundant inner request collapsibles and part extraction selection needed clearer confirmation.

Documented changes:

- bumped the local addon test package to `1.1.140`
- removed the inner `Shape Request` and `Texture Request` collapsible rows
- added an `Extract Selected (n)` action label for image part extraction

### 2026-04-17 eyeball-only extraction hardening
Source: follow-up after testing showed the separate eyeball part was still producing an eye-region crop.

Documented changes:

- bumped the local addon test package to `1.1.139`
- renamed the separate part toggle to `Add Eyeball Part`
- tightened the forced Eyeball prompt so it asks for one isolated spherical eyeball with no eyelids, lashes, skin, socket, or face crop

### 2026-04-17 server summary direct layout
Source: follow-up after testing showed the Server summary should not have its own inner collapse row.

Documented changes:

- bumped the local addon test package to `1.1.138`
- removed the inner `Server` collapsible row so the server status box appears directly under `Nymphs Server`

### 2026-04-17 server panel sibling sections
Source: follow-up after testing showed collapsing the Server summary should not hide Runtimes or Advanced.

Documented changes:

- bumped the local addon test package to `1.1.137`
- kept Runtimes and Advanced visible when the Server summary section is collapsed

### 2026-04-17 image status visibility hotfix
Source: follow-up after testing showed the top image status box disappeared in a clean panel state.

Documented changes:

- bumped the local addon test package to `1.1.136`
- kept the image status and output folder actions always visible at the top of `Nymphs Image`

### 2026-04-17 image panel sibling sections
Source: follow-up after testing showed Image Part Extraction should stay usable when Image Generation is collapsed.

Documented changes:

- bumped the local addon test package to `1.1.135`
- moved the image status and output folder actions to the top of the `Nymphs Image` panel
- split Image Generation and Image Part Extraction into sibling collapsible sections inside the same panel

### 2026-04-17 four-view generation checkbox
Source: follow-up after testing showed the separate 4-view button still felt like the wrong interaction model in the image panel.

Documented changes:

- bumped the local addon test package to `1.1.134`
- replaced the separate `Generate 4-View MV` action with a `4-View MV` checkbox beside `Variants`
- the main `Generate Image` action now runs the existing multiview generation path when that checkbox is enabled

### 2026-04-17 generate action visibility polish
Source: follow-up after testing showed the main generate action was getting lost in the image panel flow.

Documented changes:

- bumped the local addon test package to `1.1.133`
- reorganized the generate area so `Generate 4-View MV` sits as the smaller secondary action
- made the main `Generate Image` action the larger final button
- added a `Generate` label above the action area

### 2026-04-17 text editor prompt sync fix
Source: testing showed dropdown-driven prompt changes could update the visible prompt field while the linked Blender text block still showed stale text.

Documented changes:

- bumped the local addon test package to `1.1.132`
- fixed the linked prompt text block to resync whenever the stored prompt text differs from the text block contents

### 2026-04-17 saved prompt autoload and extraction prompt wording
Source: follow-up after aligning the saved-prompt dropdown with the managed prompt-builder behavior and polishing nearby UI labels.

Documented changes:

- bumped the local addon test package to `1.1.131`
- selecting a saved full prompt now loads it immediately
- removed the saved prompt `Insert` button
- renamed the part prompt section label from `Guidance` to `Extraction Prompt`
- widened the Gemini model dropdown row slightly by shrinking the left label area

### 2026-04-17 manual prompt editing label polish
Source: small UI wording follow-up after switching Subject and Style to managed prompt blocks.

Documented changes:

- bumped the local addon test package to `1.1.130`
- renamed the lower prompt area label to `Manual Prompt Editing`

### 2026-04-17 managed subject-style prompt flow polish
Source: follow-up after moving Subject and Style onto managed prompt blocks and testing the resulting prompt-builder flow.

Documented changes:

- bumped the local addon test package to `1.1.129`
- kept Subject and Style as managed prompt blocks instead of stackable inserts
- removed the duplicate managed-block clear button under Subject and Style
- full saved prompts now reset the prompt-builder state before loading

### 2026-04-17 auto guidance leak hotfix
Source: testing showed the inserted eyeball guidance could leak into unrelated part prompts and contaminate anatomy-base extraction.

Documented changes:

- bumped the local addon test package to `1.1.128`
- fixed auto guidance blocks so they are fully stripped from the final Gemini guidance text before requests are sent
- prevents eyeball-only guidance from leaking into anatomy-base extraction

### 2026-04-17 auto guidance blocks for part options
Source: follow-up after deciding the face/eyes/eyeball options should stay visible and tweakable inside Guidance.

Documented changes:

- bumped the local addon test package to `1.1.127`
- toggling `Face`, `Eyes In Base`, or `Include Eyeball Part` now inserts or removes matching guidance blocks automatically
- those auto guidance markers are stripped before the final Gemini prompt is sent
- turning `Face` off now also clears `Eyes In Base`

### 2026-04-17 single eye extraction hardening
Source: follow-up after testing showed the eye option still was not reliably producing one isolated reusable eye image.

Documented changes:

- bumped the local addon test package to `1.1.126`
- normalized the planned eye part into one dedicated `Eye` asset when `Include Eye Part` is enabled
- replaced weak generic eye wording with a dedicated single-eye extraction prompt
- explicitly forbids face crops and both-eye outputs for the eye asset path

### 2026-04-17 image part extraction options follow-up
Source: follow-up pass after testing the new planned-part checklist flow.

Documented changes:

- bumped the local addon test package to `1.1.125`
- renamed `Character Part Extraction` to `Image Part Extraction`
- added `Face`, `Eyes In Base`, and `Include Eye Part` planning controls
- added a per-part symmetry checkbox in the planned parts list
- strengthened extraction prompt wording for symmetry-sensitive wearable parts

### 2026-04-17 part extraction checkbox test build
Source: local test pass for the guided character part extraction UI.

Documented changes:

- bumped the local addon test package to `1.1.124`
- changed planned character parts into selectable checkboxes
- extraction now sends only the checked planned parts to Gemini
- the parts list now shows every planned part instead of hiding extras
- kept the compact source, model, Style Lock, and editable Guidance controls from the previous UI pass

### 2026-04-17 guided character part extraction prototype
Source: follow-up after Nano/OpenRouter sometimes returned a true multi-image breakout set and sometimes collapsed the same request into a single parts-sheet style image.
Context: the production path needs related, scale-consistent part images without requiring the user to guess a variant count or pay for unreliable one-shot breakout attempts.

Documented changes:

- added a local guided character-part extraction workflow for Nano-backed image editing
- added a `Plan Parts` step that uses the current `Image` as the master reference and asks a vision model for structured part JSON
- added a separate `Extract Parts` step that runs one guided Nano image-edit request per planned part
- added planner model and max-part controls so the user can balance cost and recognition quality before extraction
- added editable extraction guidance that is applied to every part request
- saves planning/results metadata and assigns the anatomy/base-body output back to `Image` when extraction finishes
- added a `Character Master Reference` prompt preset for creating the canonical full-character image before extraction
- added compact source controls so the extraction section can use the current image, last generated image, or a picked master image
- strengthened style injection wording and the Storybook Inkwash preset so style direction is less likely to be drowned out by asset-reference wording
- moved art-direction wording out of the main image prompt presets so style language lives in style presets only
- added Japanese Watercolor Woodblock and Minimalist Chinese Watercolor style presets
- added extraction style locking so guided part extraction can strongly preserve the active Style preset and the master image media feel
- reworked the image prompt UI around one visible prompt: Subject and Style snippets insert into the prompt, Saved Prompt stores full reusable prompts, and generation no longer sends hidden style text
- moved editable prompt snippets into one shared user preset folder with `kind` metadata for subject, style, and saved full prompts
- made the OpenRouter API key persist in the user config folder
- fixed shared prompt preset keys so Subject, Style, and Saved Prompt files do not duplicate or crash after saving
- changed Saved Prompt insertion so it clears the current prompt before inserting the saved full prompt
- replaced the tall inline prompt preview with a compact one-line summary and a popup Preview button
- simplified Character Part Extraction around a chosen Source Image, Model/Max planning, visible Style Lock, Guidance, and Parts extraction controls
- tightened anatomy/base-body extraction so it preserves inferred body type and silhouette proportions instead of defaulting to a generic slim anatomy reference
- tightened anatomy/base-body extraction again so it asks for body/base mesh only and removes every clothing, hair, beard, cloak, boot, prop, and covering artifact while staying non-explicit
- preserved the existing Shape panel and existing image-to-shape flow

### 2026-04-17 style preset registration hotfix for Blender 4.5
Source: live Blender 4.5 extension install failure after adding the style preset dropdown.
Context: Blender rejects string defaults on `EnumProperty` values when the enum items come from a callback function, which caused addon registration to fail before the panel could load.

Documented changes:

- removed the invalid string default from the dynamic `imagegen_style_preset` enum property
- kept the runtime fallback logic so the style preset still resolves cleanly to `No Style` when nothing is selected yet

### 2026-04-17 Nano breakout now requests the full part set automatically
Source: follow-up after it became clear that asking the user to guess a good `Variants` count for breakout generation was bad UX.
Context: for Nano breakout runs, the addon should request the full part set in one go rather than making the user predict how many separate outputs the character design will need.

Documented changes:

- changed Nano `Character Part Breakout` generation so `Generate Image` requests the full breakout set automatically
- removed the need to guess a useful `Variants` count just to get the anatomy base, hair, and major carried/worn items
- reinforced the breakout prompt so each returned image should contain exactly one centered subject with no side-by-side layout or combined items

### 2026-04-17 separate reusable style presets for image generation
Source: addon UX follow-up after prompt-style repetition became tedious during image generation work.
Context: style direction was getting manually typed into prompts over and over, even though it really belongs as a separate reusable layer.

Documented changes:

- added a separate `Style Preset` system for image generation, independent from the main prompt preset dropdown
- added editable packaged `style_presets/` JSON files and matching user-editable style preset storage
- wired final image prompt assembly so a selected style fragment is injected automatically at generation time instead of having to be pasted into every prompt by hand
- added UI controls to load, save, delete, open, and clear reusable styles

### 2026-04-17 breakout sequencing now applies even for a single image
Source: follow-up after a live Nano test produced a side-by-side composite instead of a single breakout target.
Context: `Character Part Breakout` only switched into strict per-item sequencing when `Variants > 1`, so single-image runs could still send the broad preset text and let the model improvise a composite layout.

Documented changes:

- changed breakout generation so `Character Part Breakout` always uses preset-specific per-item sequencing, even when `Variants = 1`
- tightened per-item instructions to explicitly require exactly one centered subject and forbid side-by-side layouts or extra items in the same image
- preserved the existing first-image assignment behavior for multi-image breakout batches

### 2026-04-17 Nano breakout anatomy-safe base and richer server status
Source: local Nano breakout testing and follow-up UI/status work.
Context: the previous breakout wording asked Nano for a nude base body, which OpenRouter/Gemini was refusing, and the Server panel was still underselling Nano-specific job context.

Documented changes:

- changed `Character Part Breakout` so the first image is a neutral anatomy base body suitable for game base-mesh reference rather than nude-body wording
- kept hair as its own separate generated asset and preserved the body -> hair -> remaining items breakout sequence
- improved Nano/OpenRouter image error reporting so provider finish reasons show up when no image is returned
- added Nano guide-image context to runtime status reporting
- updated the Server panel to show the active image backend plus Nano model, aspect, size, and guide-image state during image jobs

### 2026-04-17 breakout runs now auto-assign the first image
Source: shape workflow follow-up after testing breakout batches in Blender.
Context: breakout mode generates the nude base body first, but the addon was still assigning the last generated image back to Blender after the batch finished, which made the body less convenient as the first shape source.

Documented changes:

- added preset-specific image assignment for `Character Part Breakout`
- when breakout mode generates more than one image, Blender now assigns the first generated image back to `Image`
- kept the existing last-image assignment behavior for other prompts and normal single-image asset generation
- bumped the addon package metadata to `1.1.114`

Why it matters:

- breakout runs now land on the body/first target automatically for shape generation
- ordinary single-image asset workflows keep their current behavior

### 2026-04-17 Gemini breakout sequencing and separate hair target
Source: follow-up fix after testing the `Character Part Breakout` preset on Gemini Flash.
Context: the breakout prompt needed hair as its own asset image, and repeated variant requests were repeatedly starting from the first target because the addon was sending the same prompt for every request. Gemini also only kept the first image returned from a response.

Documented changes:

- updated `Character Part Breakout` so the nude base body explicitly excludes hair
- added hair as its own required standalone target image
- changed breakout variant generation for both Gemini and Z-Image so repeated requests are sequenced:
  - base body first
  - hair second
  - then one different remaining item per request
- changed Gemini response handling to save every returned image instead of discarding everything after the first one
- bumped the addon package metadata to `1.1.113`

Why it matters:

- the base body should stop arriving with hair fused into the first image
- both Gemini and Z-Image can now step through breakout targets instead of restarting from the first target on every variant
- Gemini image batches can now surface all returned images into the output folder instead of keeping only the first one

### 2026-04-17 Nymphs addon identity rename
Source: naming cleanup for the Blender-facing addon and extension feed.
Context: `NymphsCore` is the full system/runtime name, while the Blender addon should present as `Nymphs` and no longer reuse the legacy `nymphs3d2` extension id.

Documented changes:

- renamed the live addon implementation file from `Nymphs3D2.py` to `Nymphs.py`
- changed the Blender-visible addon name to `Nymphs`
- changed the extension id to `nymphs`
- bumped the addon package metadata to `1.1.112`
- updated the addon README with the current image -> shape/texture -> retexture workflow
- kept `NymphsCore` as the managed runtime, distro, and Manager system name

Why it matters:

- new Blender installs can use the clean `Nymphs` identity instead of inheriting the old test-track name
- `Nymphs` now reads as the creative Blender tool, while `NymphsCore` remains the local system that powers it

### 2026-04-17 editable packaged prompt preset library
Source: prompt audit workflow cleanup.
Context: the current image prompts were embedded in code, which made them awkward to review and tune as actual prompt assets.

Documented changes:

- added `Blender/Addon/prompt_presets/` as a source-controlled prompt preset library
- exported every built-in image prompt preset into editable JSON files
- updated the addon to load packaged prompt JSON files when present, while keeping the embedded prompts as startup-safe fallbacks
- updated the extension build script so packaged prompt preset files are included in addon zips

Why it matters:

- prompt wording can now be audited and edited directly without digging through the Python addon file
- edited prompt JSON files can ship with future extension builds

### 2026-04-17 character part breakout prompt preset
Source: prompt-library pass for generating character component references.
Context: the image backend needs a reusable preset that takes a full character description and generates separate standalone images for the character's clothing, weapons, accessories, and carried objects.

Documented changes:

- added a built-in `Character Part Breakout` image prompt preset
- worded the preset to generate exactly one standalone reference per image
- added a nude uncensored base character body reference in a neutral A-pose or T-pose as the first generated target
- kept clothing, armor, accessories, weapons, and carried objects as separate item-only images
- explicitly blocked parts sheets, grids, collages, labels, scenery, and multi-item layouts
- guided batch/variant output so each generated image should choose a different single target from the same character design
- bumped the addon package metadata to `1.1.111`

Why it matters:

- character descriptions can now be broken into separate base-body, clothing, weapon, and prop references for downstream modeling or texturing
- the prompt avoids the old "parts sheet" behavior and asks for standalone output images instead

### 2026-04-17 NymphsCore extension feed test build
Source: extension repository publishing pass for the renamed NymphsCore addon channel.
Context: the addon and runtime now ship as part of the wider NymphsCore system, while Blender should still update through the existing `nymphs3d2` extension id for continuity.

Documented changes:

- bumped the packaged addon metadata to `1.1.110`
- changed the Blender-visible addon name from `Nymphs3D2` to `NymphsCore`
- refreshed extension metadata copy around NymphsCore image, shape, and texture backends
- kept the extension id as `nymphs3d2` so existing test installs can update from the same feed

Why it matters:

- testers can install a fresh package from the `NymphsExt` feed without colliding with the older `1.1.109` package
- the addon now presents itself under the new system name while preserving the current Blender extension update path

### 2026-04-17 OpenRouter Nano Banana image backend
Source: follow-up image-generation integration pass after retiring the heavy local Parts lane.
Context: Z-Image remains the local prompt-to-image workflow, but the addon now needs a cloud image option for Gemini Flash / Nano Banana without adding another WSL runtime or Manager install dependency.

Documented changes:

- added an image backend selector to the `Nymphs Image` panel:
  - `Z-Image`
  - `Gemini Flash`
- kept the existing Z-Image flow as the default local backend
- added OpenRouter-backed Gemini image generation using the OpenAI-compatible chat completions endpoint
- added support for OpenRouter's `google/gemini-2.5-flash-image` Nano Banana model
- added optional model choices for newer Gemini image models exposed through OpenRouter
- added OpenRouter API key handling through either the panel field or `OPENROUTER_API_KEY`
- added Gemini aspect-ratio and image-size controls where the selected model supports them
- routed generated Gemini images into the same addon image-output state used by Z-Image, so the result can feed the existing image-to-3D and multiview workflows

Verification:

- addon Python compile passed
- live OpenRouter request to `google/gemini-2.5-flash-image` returned one base64 PNG image in `message.images`

Why it matters:

- local Z-Image stays available for offline/GPU-backed generation
- Nano Banana can be used without installing a new backend into the managed distro
- generated images still land in the existing Blender handoff path instead of creating a separate workflow island

### 2026-04-17 retired Hunyuan Parts lane and installer payload cleanup
Source: cleanup pass after deciding the Hunyuan Parts/P3-SAM/X-Part workflow was too heavy for the current GPU and not strong enough to keep shipping.
Context: the Blender addon, Manager UI, installer scripts, and local runtime had grown an experimental mesh-decomposition lane. The supported addon backends are now the image backend, Hunyuan 2mv, Z-Image/Nymphs2D2, and TRELLIS.2, so the retired lane needed to disappear from both source and packaged distro tooling.

Documented changes:

- removed the Nymphs Parts panel, operators, settings, status handling, subprocess tracking, and import-routing code from the Blender addon
- removed the Parts-oriented prompt preset and old guide/roadmap docs so the addon no longer points users toward that workflow
- removed the Manager checkbox and settings flow for "Experimental Parts Tools"
- removed the `-InstallParts` / `--install-parts` installer path and the `NYMPHS3D_PARTS_DIR` shell export from source scripts and the packaged `publish/win-x64` payload
- deleted the Hunyuan Parts installer script and the P3-SAM/X-Part wrapper scripts from both source and packaged scripts
- rebuilt the Manager release payload so the shipped installer scripts match the cleaned source tree
- removed the installed local Hunyuan Parts repo/cache from the managed runtime while leaving Hunyuan3D-2, Z-Image, and TRELLIS.2 intact

Verification:

- addon Python compile passed
- Windows `dotnet build` for the Manager passed with zero warnings and zero errors
- Manager release packaging completed successfully
- shell syntax checks passed for the edited source and packaged installer scripts
- repo search found no remaining Hunyuan Parts/P3-SAM/X-Part installer or addon references outside this changelog note

Why it matters:

- the addon surface is back to the supported backend set instead of carrying a GPU-heavy experimental lane
- new installs and repairs no longer clone, configure, verify, or advertise the retired backend
- packaged Manager builds no longer ship stale scripts that could reinstall it by accident

### 2026-04-13 panel responsiveness pass and manager-aligned runtime defaults
Source: follow-up Blender-side cleanup after the TRELLIS/Hunyuan backend selector fixes and the managed installer rollout
Context: the addon had the right feature surface, but it was starting to feel heavier than it should during normal sidebar use. The managed runtime target had also changed to the installer-created `NymphsCore` distro and `nymph` user, so the addon defaults needed to match the real installed environment.

Documented changes:

- bumped the packaged addon version to `1.1.109`
- updated the addon default WSL target to the managed installer runtime:
  - distro: `NymphsCore`
  - user: `nymph`
- corrected backend defaults and ordering so both feature panels now lead with `TRELLIS.2`
- updated `Nymphs Shape` so the backend selector is explicit and ordered:
  - `TRELLIS.2`
  - `Hunyuan 2mv`
- updated `Nymphs Texture` so the backend selector also defaults to and orders as:
  - `TRELLIS.2`
  - `Hunyuan 2mv`
- made the main addon panels collapsed by default:
  - `Server`
  - `Image Generation`
  - `Shape Request`
  - `Texture Request`
- changed collapsed panels to early-return before running heavier UI work
- reduced idle redraw pressure by stopping the unconditional one-second full `VIEW_3D` refresh loop when nothing is active
- kept frequent redraws only while launches or active backend jobs are actually active
- added short-lived caching around redraw-time expensive lookups:
  - WSL distro enumeration
  - image prompt preset folder scans
  - image settings preset folder scans
  - TRELLIS preset folder scans
- aligned the source `bl_info` version with the packaged extension version so source metadata and extension metadata no longer drift

Why it matters:

- the addon should feel noticeably less sticky during normal panel browsing, especially on Windows when WSL path checks are involved
- fresh installs now point at the managed `NymphsCore` runtime by default instead of older local assumptions
- the visible backend choices now match the intended product direction:
  - `TRELLIS.2` first for shape and single-image texture work
  - `Hunyuan 2mv` available as the alternate lane

### 2026-04-14 NymphsCore monorepo consolidation
Source: repository migration work that brought the Manager, Blender addon, and Unity package into one main repo under `nymphnerds`.

Documented changes:

- created the `NymphsCore` monorepo structure with:
  - `Manager/` for the WSL backend, Windows installer, and setup scripts
  - `Blender/Addon/` for the Blender addon source and extension build tooling
  - `Unity/TDC-Camera/` for the Unity package
- merged the Manager history into the monorepo under `Manager/`
- merged the Blender addon history into the monorepo under `Blender/Addon/`
- imported the Unity top-down camera package under `Unity/TDC-Camera/`
- updated documentation and repo links to the `nymphnerds` organization
- documented why Blender extension feed publishing remains in the separate `NymphsExt` repo
- kept old legacy repos referenced as backups where needed

Why it matters:

- NymphsCore became the single source repo for the local runtime, manager, addon source, and Unity package
- Blender can still install from `NymphsExt`, while development happens in the monorepo

### 2026-04-11 Unity top-down camera package import
Source: Unity package work folded into the NymphsCore history during monorepo consolidation.

Documented changes:

- added the `Unity/TDC-Camera/` package
- added the top-down camera guide
- added and iterated quick test scene assets during package development
- removed the old Oak Tree demo scene from the package history

Why it matters:

- Unity tooling now lives beside the Blender and Manager work in NymphsCore
- consumers can install the package through the Git URL with `?path=/Unity/TDC-Camera`

### 2026-04-11 runtime card and image-panel cleanup
Source: iterative Blender UI testing around the image panel and Runtimes panel
Context: the Z-Image/Nunchaku path made negative prompts misleading, the MV Source profile blurred the difference between profiles and actions, and runtime config fields needed clearer, consistent advanced controls.

Documented changes:

- bumped the packaged addon through `1.1.64`
- removed negative prompt UI and request payload plumbing from the Z-Image image-generation flow
- stopped building and sending MV negative prompts because MV images use the same Z-Image Turbo/Nunchaku backend
- widened Profile and Prompt Preset dropdowns by moving their Apply/Load actions next to the section labels
- moved prompt `Text Editor`, `Edit`, and `Clear` into a more compact prompt control layout
- removed the built-in `MV Source` generation profile and ignored stale seeded `turbo_mv_source` profile JSON
- renamed the MV image button to `Generate 4-View MV` and clarified that Variants only affect single-image generation
- simplified the Runtimes panel by removing the checkbox/`Start Enabled` workflow
- gave runtime cards clearer backend names, compact `Port: ####` rows, and direct Start/Stop controls
- made runtime `Config Details` open directly under the foldout row
- changed runtime config fields to label-above-field layout so long paths do not crush labels
- added configurable Python executable paths for all runtimes:
  - `Hunyuan Python`
  - `Z-Image Python`
  - `TRELLIS Python`
- added hover descriptions for the Hunyuan launch toggles:
  - `Shape`
  - `Texture`
  - `Turbo`
  - `FlashVDM`

Why it matters:

- the image panel no longer advertises negative-prompt behavior that the active Nunchaku backend ignores
- users no longer need a confusing MV-specific profile to generate front/left/right/back images
- runtime configuration is more consistent across Z-Image, TRELLIS, and Hunyuan3D-2mv
- the Runtimes panel is now the explicit home for detailed backend names and launch configuration

### 2026-04-10 enum callback default fix
Source: Blender extension registration error on `imagegen_prompt_preset`
Context: Blender does not allow a string `default` value on `EnumProperty` when `items` is provided by a callback. The preset dropdown was still doing that, so `NymphsV2State` could not register even after the preset loader itself was made safer.

Documented changes:

- bumped the packaged addon version to `1.1.52`
- removed the invalid string default from the callback-backed preset enum
- added preset-key normalization so the current preset resolves to the shipped default or first available preset when the stored value is blank or stale
- updated load/save/delete and panel draw paths to keep the selected preset synchronized with the available preset set

Why it matters:

- Blender should stop rejecting `imagegen_prompt_preset` during class registration
- the preset dropdown remains dynamic and file-backed without depending on an invalid enum default declaration
- stale or missing preset selections should fall back cleanly instead of wedging addon startup

### 2026-04-10 reload-safe Blender registration fix
Source: Blender extension reload failure after the preset registration fix was published
Context: after fixing the enum-preset registration issue, Blender could still fail when reloading the extension metadata because `NymphsV2State` was already registered from the previous module instance, which blocked the next `register()` call with an "already registered as a subclass" error.

Documented changes:

- bumped the packaged addon version to `1.1.51`
- made addon registration clear any stale `Scene.nymphs3d2v2_state` pointer before rebuilding the module state
- made class registration reload-safe by unregistering any existing Blender class with the same name before registering the current module's class objects
- switched addon unregistration to the same safe cleanup path so stale reload state does not linger across extension refreshes

Why it matters:

- Blender extension metadata reloads should stop failing on stale class state
- the preset fix from `1.1.50` is still present, but the addon should now survive extension refresh/update cycles cleanly
- local extension installs are less likely to get wedged in a half-registered state after a failed reload

### 2026-04-10 prompt preset registration fix
Source: Blender extension startup failure after the image-panel preset system landed
Context: the new prompt preset dropdown could fail during extension registration because the enum items callback depended on preset-folder JSON state too early, which made Blender reject `NymphsV2State` with an `EnumProperty` registration error.

Documented changes:

- bumped the packaged addon version to `1.1.50`
- changed the prompt preset loader so built-in shipped presets are always available in memory before any filesystem-backed custom presets are read
- guarded preset-folder seeding/loading so registration no longer depends on the preset directory being readable during class registration
- kept JSON-backed custom prompt presets layered on top of the shipped defaults instead of removing that workflow

Why it matters:

- the extension should register cleanly again on startup
- the prompt preset system no longer depends on user config folder state just to let Blender load the addon
- custom presets remain supported without making addon registration brittle

### 2026-04-10 prompt-flow simplification pass
Source: addon UX cleanup after real prompt-writing feedback
Context: the prompt starter system was technically working, but the panel still felt more fragmented than it needed to. Negative prompt, seed, and MV stance controls were crowding the main writing surface, and the starter workflow was not reading as one coherent action.

Documented changes:

- bumped the packaged addon version to `1.1.42`
- kept direct inline editing for the main prompt
- changed the starter action to read as `Apply` rather than a vague `Load`
- simplified starter labels so the dropdown reads more like a practical recipe list
- added starter description text in the panel so the current selection explains itself
- moved negative prompt, seed, and MV-set stance under one `Optional Controls` foldout
- added `Use Base` for the shipped default negative prompt
- clarified that MV-set stance only affects `Generate MV Set`
- kept the larger editor for copy/paste work without forcing previews into it

Why it matters:

- the panel should feel more like one prompt workflow instead of several partial systems
- the main writing area stays visible while the less-common controls stay available
- users get a clearer explanation of what the starter, negative prompt, seed, and MV stance actually do

### 2026-04-10 text-prompt surface removal
Source: addon cleanup after the backend-family lock-in
Context: the addon source still exposed a leftover text-prompt bridge path even though the intended product flow had already shifted to `Z-Image` for prompt-to-image and then image-guided 3D generation.

Documented changes:

- bumped the packaged addon version to `1.1.41`
- removed the remaining addon-side text-prompt capability flag from the fallback capability surface
- removed the old text-prompt launch flag from the local `2mv` launch path
- removed the visible text-prompt toggle from the addon UI and runtime state
- replaced the old text-prompt VRAM estimate with explicit guidance to use `Z-Image` first

Why it matters:

- the published addon surface now matches the intended supported workflow more closely
- users are less likely to assume the old text-bridge path is still a supported lane

### 2026-04-10 backend-surface cleanup pass
Source: distro-slimming pass after locking the intended backend families
Context: the working product direction is now `Z-Image` via `Nunchaku`, official `TRELLIS.2`, and `Hunyuan 2mv`. The addon still carried stale `2.1` and stock non-Nunchaku image-runtime branches that no longer fit that target.

Documented changes:

- bumped the packaged addon version to `1.1.40`
- removed the remaining `Hunyuan 2.1` launch/display branches from the addon runtime surface
- removed the old stock `Z-Image Standard` path from the visible addon model-selection logic
- simplified `Z-Image` runtime handling so the addon now treats `Nunchaku` as the intended image runtime family
- removed dead legacy texture-size property surface that no longer belonged to the kept backends

Why it matters:

- the addon surface is now much closer to the intended lighter distro shape
- users only see the three backend families you actually want to keep investing in

### 2026-04-10 TRELLIS shape-panel readability pass
Source: first screenshot-driven cleanup after TRELLIS shape-panel wiring started working end to end
Context: the TRELLIS branch was live in the addon, but the shape options had become hard to read in a narrow Blender sidebar, and the result box was dumping ugly truncated paths.

Documented changes:

- bumped the packaged addon version to `1.1.39`
- changed TRELLIS option rendering so core values read as full-width controls instead of cramped paired rows
- grouped TRELLIS sampling settings into separate `Sparse Structure`, `Shape`, and `Texture` blocks
- shortened shape-result labels so the panel shows the folder name and mesh filename instead of chopped full paths

Why it matters:

- TRELLIS parameters should now be readable in the normal sidebar width
- the shape result area should be much easier to scan after a run

### 2026-04-10 TRELLIS startup and default-runtime follow-up
Source: first addon-side cleanup pass after the official TRELLIS adapter tests
Context: the TRELLIS backend itself was working, but the addon was not surfacing its startup progress clearly enough, and the fresh-session runtime defaults still were not quite right.

Documented changes:

- bumped the packaged addon version to `1.1.36`
- changed fresh-session runtime defaults so no services start pre-selected
- taught the server-panel startup parser to recognize TRELLIS adapter output such as:
  - `Preparing TRELLIS adapter`
  - `Preparing TRELLIS runtime`
  - `Loading TRELLIS models`
  - `Server ready`

Why it matters:

- Blender now opens with no runtime checkboxes pre-selected in a fresh session
- TRELLIS startup should stop looking like an immediate silent failure when it is actually booting and serving `/server_info`

### 2026-04-10 TRELLIS path-launch fix
Source: follow-up after testing the TRELLIS start action from Blender
Context: the default TRELLIS Python path used `~`, and the addon launch path was quoting it before shell expansion, so the start action could fail immediately while looking like it did nothing.

Documented changes:

- bumped the packaged addon version to `1.1.37`
- resolved the TRELLIS Python path to an absolute Linux path before building the WSL launch command

Why it matters:

- the default TRELLIS interpreter path should now launch correctly from the addon without requiring a manual path edit first

### 2026-04-10 3D target auto-switch follow-up
Source: runtime-selection UX cleanup after the first TRELLIS addon tests
Context: `Use For 3D` was too easy to miss, so starting a 3D runtime could still leave the shape panel pointed at a different backend.

Documented changes:

- bumped the packaged addon version to `1.1.38`
- starting a 3D runtime now automatically makes it the active 3D target
- the manual `Use For 3D` control remains as an override for multi-runtime cases

Why it matters:

- if you start `TRELLIS.2`, the shape panel should now switch to TRELLIS immediately
- the same behavior applies to `Hunyuan 2mv` and `Hunyuan 2.1`

### 2026-04-09 late texture guidance follow-up
Source: dedicated texture-panel MV guidance and wider 2mv texture-size choices
Context: once the end-to-end 2mv texture path was proven, the dedicated texture panel still lagged behind by only exposing single-image guidance.

Documented changes:

- bumped the packaged addon version to `1.1.33`
- added `Guidance Mode` to `Nymphs Texture`:
  - `Auto`
  - `Image`
  - `Multiview`
  - `Text`
- taught the dedicated texture request to send:
  - `mv_image_front`
  - `mv_image_back`
  - `mv_image_left`
  - `mv_image_right`
  when MV guidance is selected
- reused the existing MV slots and `Received from Z-Image` handoff marker instead of inventing a separate second MV store
- widened the 2mv texture-size presets to include:
  - `256`
  - `768`
  - `1536`
  in addition to the existing values

Why it matters:

- the separate texture panel can now logically follow the image-to-shape workflow instead of forcing everything back through one image
- 2mv users can test more texture-size tradeoffs without manually editing payloads

### 2026-04-09 late MV handoff UI follow-up
Source: MV image handoff cleanup between `Nymphs Image` and `Nymphs Shape`
Context: once MV generation was working, the image panel was repeating information that was more useful in the shape workflow itself.

Documented changes:

- bumped the packaged addon version to `1.1.32`
- removed the redundant `Assigned MV Views` summary box from `Nymphs Image`
- added a simple MV handoff marker in `Nymphs Shape`:
  - `Received from Z-Image`
  - plus a timestamp when available

Why it matters:

- the image panel stays focused on image generation
- the shape panel now shows the MV handoff where it is actually relevant
- the UI keeps the useful signal without repeating four more file-path rows

### 2026-04-09 late prompt preset follow-up
Source: additional watercolor starter for the remake prompt flow
Context: once the starter-load workflow was in place, it needed a style starter tailored to the kind of elegant painterly result you wanted to test.

Documented changes:

- bumped the packaged addon version to `1.1.31`
- added a new shipped prompt starter:
  - `Style: Minimalist Chinese Watercolor`

Why it matters:

- it gives you a usable stylistic starting point without losing the cleaner `2mv`-friendly framing and silhouette guidance
- it keeps the current prompt workflow simple:
  - load
  - tweak
  - generate

### 2026-04-09 late prompt UX follow-up
Source: `Z-Image` prompt workflow cleanup in the remake addon
Context: the image runtime became usable, but prompt editing still felt fragile and too easy to lose during iteration.

Documented changes:

- bumped the packaged addon version to `1.1.30`
- added a lightweight `Prompt Starter` selector in `Nymphs Image`
- added `Load` so a starter prompt can be dropped straight into the current prompt fields and then tweaked
- added direct in-panel editing for:
  - `Prompt`
  - `Negative Prompt`
- kept the wider `Expand` editor for longer prompt work instead of forcing everything through a popup
- added shipped starters for:
  - MV character base
  - fantasy painted character
  - cartoon identity sheet
  - object identity sheet
  - full-body framing fix
- added a clearer note that negative prompts have limited effect on the current `Z-Image Turbo / Nunchaku` lane

Why it matters:

- prompt iteration is now much less awkward during real testing
- the addon can start moving toward a reusable preset system without blocking on a larger prompt-library feature
- selecting a starter prompt now fits the workflow you asked for:
  - load it
  - tweak it
  - generate

### 2026-04-09 late follow-up
Source: `MV Lite` image-set generation and `Z-Image` default runtime cleanup
Context: after proving `Nunchaku r32` was the first practical `Z-Image` runtime, the addon needed a better default and a direct path from one prompt into a usable `2mv` multiview set.

Documented changes:

- bumped the packaged addon version to `1.1.29`
- made `Z-Image Nunchaku r32` the default `Z-Image` runtime preset in the addon
- added `Generate MV Set` to `Nymphs Image`
- added a lightweight MV prompt builder that derives:
  - `front`
  - `left`
  - `right`
  - `back`
  prompts from the current image prompt
- added an `MV Pose` control with:
  - `Soft A-Pose`
  - `Natural`
  - `T-Pose`
- auto-filled the existing multiview image slots from the generated MV image set
- switched the shape workflow to `Multiview` automatically after a successful MV image run

Why it matters:

- this is the first direct bridge from the new `Z-Image` runtime lane into the `2mv` multiview workflow
- it keeps MV development lightweight and prompt-driven instead of waiting for a bigger preset system first
- the addon default now reflects the runtime that actually worked in practice, not the slower stock path

### 2026-04-09
Source: `Z-Image` runtime breakthrough and remake workflow stabilization
Context: the stock BF16 `Z-Image-Turbo` path proved too heavy in real use, so the remake needed a practical runtime and cleaner Blender-side controls for testing it.

Documented changes:

- added explicit `Z-Image` runtime controls to the remake addon line
- added a simple `Model Choice` selector for:
  - `Z-Image Standard`
  - `Z-Image Nunchaku r32`
  - `Z-Image Nunchaku r128`
  - `Custom`
- kept the raw runtime controls available behind `Custom` instead of forcing users to edit low-level settings all the time
- taught the addon launch path to switch between the normal `.venv` and the dedicated `.venv-nunchaku` runtime env
- updated the `Z-Image` runtime card so its detail line can reflect live task progress rather than falling back too quickly to `Server ready`
- updated the top overview so it can reuse the richer `Z-Image` runtime detail when the generic image-progress path is too thin
- published the remake extension line through:
  - `1.1.26`
  - `1.1.27`
  - `1.1.28`

Why it matters:

- this is the first point where `Z-Image` genuinely felt viable for the product instead of just theoretically attractive
- the practical runtime breakthrough came from `Nunchaku`, not the stock BF16 path
- the addon now exposes the comparison points you actually need for product decisions:
  - stock runtime
  - `Nunchaku r32`
  - `Nunchaku r128`
- it also marks a shift in focus:
  - less time fighting raw runtime viability
  - more time improving prompt quality, prompt UX, and workflow fit for `2mv`

### 2026-04-08
Source: product naming cleanup and panel naming normalization
Context: `Nymphs3D2` is the stable public product name, while the older
`Blender 5.1 Test` wording and `v2` panel labels were leftover transitional
labels.

Documented changes:

- bumped the packaged addon version to `1.0.11`
- normalized the public extension name to `Nymphs3D2`
- removed the temporary `Blender 5.1 Test` naming from the package metadata
- published the renamed package line into the extension feed as:
  - `nymphs3d2-1.0.11.zip`
- normalized the preserved older extension archives in the feed to the same
  `nymphs3d2-<version>.zip` naming pattern
- renamed the visible addon panels to:
  - `Nymphs Server`
  - `Nymphs3D2 Shape`
  - `Nymphs3D2 Texture`

Why it matters:

- the addon now presents one stable product name across the source repo,
  package metadata, and Blender UI
- the extension feed now reads as one coherent `Nymphs3D2` release line instead
  of mixing stable branding with leftover temporary test package names
- the shared server layer is no longer labeled like an internal 3D-only `v2`
  panel, which leaves room for future sibling frontends such as `Nymphs2D2`

### 2026-04-07 late afternoon hotfix +0100
Source: Blender startup regression after the `1.0.9` publish
Context: the new WSL user dropdown appears to break Blender startup on at least one real install

Documented changes:

- bumped the packaged addon version to `1.0.10`
- kept the `WSL Target` controls beside `API Root`
- kept the installed-distro dropdown for `Distro`
- rolled `User` back to a plain text field for startup safety

Why it matters:

- the useful WSL target placement remains
- the most likely startup-risking change from `1.0.9` is removed
- this gives a safer recovery build before revisiting automatic user discovery

### 2026-04-07 early afternoon user-target follow-up +0100
Source: WSL user selection usability pass
Context: after adding the distro dropdown, the remaining manual field was the WSL username

Documented changes:

- bumped the packaged addon version to `1.0.9`
- replaced the free-text `WSL User` field with a dropdown
- query the selected distro for likely human users plus `root`
- keep `nymphs3d` as the preferred fallback for the managed installer flow
- moved the WSL target controls beside `API Root` and shortened their visible labels for Blender sidebar readability

Why it matters:

- selecting a different distro no longer leaves the username as a separate manual typo risk
- the local launch target is now much closer to a point-and-click WSL selection flow

### 2026-04-07 early afternoon follow-up +0100
Source: WSL target selection usability pass
Context: users needed a real installed-distro picker instead of manually typing the distro name

Documented changes:

- bumped the packaged addon version to `1.0.8`
- replaced the free-text `WSL Distro` field with a dropdown of installed WSL distros
- kept `Nymphs3D2` as the preferred default entry for the managed installer path

Why it matters:

- launching a backend from a non-default distro no longer depends on typing the exact distro name correctly
- the intended `Nymphs3D2` target remains obvious, but other installed distros are easier to select safely

### 2026-04-07 early afternoon +0100
Source: server panel discoverability pass
Context: the WSL distro chooser existed, but it was buried inside the hidden Advanced block and easy to miss

Documented changes:

- bumped the packaged addon version to `1.0.7`
- moved `WSL Distro` into the visible `Startup Settings` section
- moved `WSL User` into the visible `Startup Settings` section
- removed those two fields from the deeper `Advanced` block to avoid duplicate controls

Why it matters:

- the WSL target selection is now where users expect it when starting a local server
- changing distros no longer requires hunting through the less obvious advanced settings

### 2026-04-07 shortly after midday +0100
Source: hotfix for local server startup on managed installer distros
Context: the just-published `1.0.5` package restored old `/opt/nymphs3d/runtime/...` defaults that no longer match the managed distro layout

Documented changes:

- bumped the packaged addon version to `1.0.6`
- restored the default `2mv` path to `~/Hunyuan3D-2`
- restored the default `2.1` path to `~/Hunyuan3D-2.1`
- kept the new WSL distro and WSL user controls from the recent package line

Why it matters:

- the addon once again targets the actual managed `Nymphs3D2` runtime layout created by the installer
- local server start should no longer fail just because the addon is looking in the old `/opt` runtime location

### 2026-04-07 midday +0100
Source: extension feed refresh from the current addon source
Context: publishing the current live addon implementation instead of leaving the extension repo on the older `1.0.4` package

Documented changes:

- bumped the packaged addon version to `1.0.5`
- prepared a fresh extension package from the current source repo state
- preserved the previous published extension feed state with a git backup point before updating the public package

Why it matters:

- the published extension feed now matches the current addon source instead of serving stale `1.0.4` code
- users get a real update path in Blender instead of a silently replaced package under the same version number

### 2026-04-06 shortly after 01:00 +0100
Source: addon source cleanup for public packaging
Context: making the source repo itself cleaner for manual `Install from Disk` fallback use

Documented changes:

- removed the tracked `experiments/` directory from the public addon repo
- kept local-only experiment files in an ignored `.local_experiments/` stash instead
- kept the live addon implementation at repo root via `Nymphs3D2.py`
- removed stale tracked wheel files that no longer match the live `urllib`-based networking path
- bumped the packaged addon version to `1.0.4`

Why it matters:

- the public addon repo is now much closer to what users would expect if they download the source zip manually
- the supported install path is still the extension feed repo, but the source repo is no longer dragging obvious local-dev baggage into the public package shape

### 2026-04-06 early morning +0100
Source: local addon packaging and extension release follow-up
Context: promoting `Nymphs3D2v3` from local dev track to the live pullable Blender extension

Documented changes:

- switched the live addon entrypoint from `Nymphs3D2v2` to `Nymphs3D2v3`
- bumped the packaged Blender extension version to `1.0.3`
- updated the repo docs layout so research notes and workflow guides now live under `docs/`
- added a dedicated retexturing user guide and `2mv` texture-controls note
- prepared the addon state for publication through the separate `Nymphs3D2-Extensions` feed repo

Why it matters:

- this is the point where the newer `v3` workflow stops being just the lab rat and becomes the thing Blender should actually pull
- the extension package now lines up with the current retexturing work instead of shipping the older `v2` surface

### 2026-04-05 late evening +0100
Source: local `Nymphs3D2v3` and `Hunyuan3D-2.1` development session
Context: making `2.1` selected-mesh retexture real on a `16 GB` GPU without breaking normal `2.1` shape generation

Documented changes:

- proved that the earlier "just keep extending the normal `2.1` backend" direction was the wrong shape for this hardware and this workflow
- after testing both the broken and the overloaded paths, the project pivoted to a duplicated backend surface:
  - keep the normal `2.1` server for shape generation and shape-plus-texture
  - add a separate `2.1` texture-only server dedicated to selected-mesh retexture
- added a dedicated texture-only backend entrypoint and paint-only worker so the shape model no longer has to sit in VRAM during a retexture run
- confirmed that the texture-only server can take a selected Blender mesh, run the full `2.1` PBR texturing pipeline, and return a textured GLB successfully
- most importantly, this finally made full `2.1` selected-mesh PBR retexture complete successfully on a `16 GB` GPU instead of collapsing under the combined shape-plus-texture memory load
- tightened the Blender addon around that new path, including:
  - longer request timeout so Blender does not give up before long `2.1` texture jobs return
  - richer `2.1` texture-stage progress reporting
  - cleaner server status handling during long jobs
  - texture controls in the Texture panel for `Face Limit`, `Max Views`, `Texture Size`, and `Remesh Uploaded Mesh`
- shifted uploaded-mesh handoff toward a safer normalization path for `2.1` texture-only work, matching the lesson from Tencent's own app flow that `OBJ` is the safer paint input than a raw Blender-exported `GLB`

Why it matters:

- this is the first point where `2.1` selected-mesh retexture became real rather than theoretical
- this is a real hardware breakthrough for the project: `2.1` texture-only PBR retexture was made to run to completion on a `16 GB` card, which honestly felt a bit like sorcery by the end of the session
- it preserved the stable normal `2.1` shape path instead of sacrificing it for experiments
- most importantly, the user's instinct to duplicate the backend rather than keep bending the normal server turned out to be exactly right
- in practice, that intuition was the turning point: once the backend was allowed to split into a stable shape server and a dedicated texture-only server, the whole workflow finally clicked into place
- this was the "wait... it actually did it" moment: after all the VRAM misery, stalls, broken paths, and suspicious half-successes, the goblin came back textured and the machine did not explode
- it is also one of the more human moments in the project history: the hardware was clearly complaining, the vibe check was correct, and trusting that instinct produced the solution

### 2026-04-05
Commit: `699b690`
Repo: `Nymphs3D-Blender-Addon`
Title: Publish Blender 5.1 test build without bundled wheels

Documented changes:

- removed the bundled `requests` wheel set from the extension manifest
- kept the live addon on the `Nymphs3D2v2` rewrite and bumped the shipped addon version to `1.0.2`
- switched the packaged extension from a CPython-3.11-specific wheel bundle to a wheel-free package
- labeled the package as `Nymphs3D2 Blender 5.1 Test` so the public extension feed clearly marked it as a compatibility build at that stage
- kept `blender_version_min = 4.2.0`, so the test build still targets the existing `4.2+` addon line rather than a separate 5.1-only fork

Why it matters:

- the live addon code already used Python's stdlib `urllib` stack and did not import `requests`
- the previous package was unnecessarily tied to a specific Blender Python ABI through `charset_normalizer` wheels
- this made the extension package materially easier to install from Blender's remote repository flow on newer Blender builds such as `5.1`

### 2026-04-05 17:34:00 +0100
Commit: `8941b6b`
Repo: `Nymphs3D2-Extensions`
Title: `Publish Nymphs3D2 1.0.1 extension package`

Documented changes:

- published `nymphs3d2-1.0.1.zip` to the public extension repository
- refreshed `index.json` and `index.html` to point Blender at the new package version
- aligned the public extension feed with the newly promoted `Nymphs3D2v2` implementation

Why it matters:

- this is the first public extension-feed publish that explicitly tracks the promoted `v2` implementation rather than the earlier root-package baseline

### 2026-04-05 17:33:00 +0100
Commit: `beebc43`
Repo: `Nymphs3D-Blender-Addon`
Title: `Promote Nymphs3D2v2 as live addon implementation`

Documented changes:

- replaced the previous monolithic live addon implementation with the clean-slate `Nymphs3D2v2` rewrite
- rewrote addon state flow around a queue-driven event model and explicit backend snapshots instead of the older property-cache-heavy structure
- rewrote local backend control so Blender manages:
  - backend family selection between `2mv` and `2.1`
  - startup flags like texture-at-startup, text bridge, turbo, FlashVDM, and low-VRAM mode
  - repo path and port targeting for local WSL launches
- made workflow choice explicit in the addon logic, including direct selected-mesh retexture as a first-class mode
- kept backend truth polling as part of the rewrite, including `/server_info`, `/active_task`, and GPU status refresh
- switched the live addon entrypoint at repo root to import the `Nymphs3D2v2` code path
- added `experiments/__init__.py` so that implementation could be imported cleanly as a package module
- updated the v2 implementation metadata to present itself as `Nymphs3D2`
- bumped the packaged addon manifest version to `1.0.1`

Why it matters:

- this is the point where the clean rewrite became the shipped addon, not just an experimental sidecar file
- it marks a real code-architecture swap in the Blender client, not only a packaging change

### 2026-04-05 10:21:18 +0100
Commit: `519641d`
Repo: `Nymphs3D-Blender-Addon`
Title: `Move Nymphs3D2 extension package to repo root`

Documented changes:

- moved `__init__.py`, `blender_manifest.toml`, and `wheels/` to repo root
- made the repo ZIP much closer to a Blender `Install from Disk` artifact
- simplified friend-testing and distribution from GitHub

Why it matters:

- this made the repo itself behave more like an installable extension package instead of a source tree wrapped around one

### 2026-04-05 10:16:12 +0100
Commit: `e24fbf6`
Repo: `Nymphs3D-Blender-Addon`
Title: `Clean published addon repo links`

Documented changes:

- replaced local filesystem references in published docs with GitHub URLs
- cleaned the addon repo README for real external use

Why it matters:

- this made the addon repo usable as a shareable project surface rather than a local dev note

### 2026-04-05 10:14:18 +0100
Commit: `223f526`
Repo: `Nymphs3D-Blender-Addon`
Title: `Remove legacy addon file`

Documented changes:

- removed `blender_addon.py` from the addon repo
- focused the addon repo solely on `Nymphs3D2`

Why it matters:

- this simplified the repo and made `Nymphs3D2` the only active addon path

### 2026-04-05 10:12:55 +0100
Commit: `857894a`
Repo: `Nymphs3D-Blender-Addon`
Title: `Bundle requests in Nymphs3D2 extension`

Documented changes:

- restored `requests`-based HTTP behavior for the addon
- bundled `requests` and its dependencies as extension wheels
- removed the need for Blender users to have `requests` preinstalled separately

Bundled dependency set:

- `requests`
- `urllib3`
- `certifi`
- `charset_normalizer`
- `idna`

Why it matters:

- this fixed the distribution problem around third-party HTTP dependency availability while preserving the original addon networking approach

### 2026-04-05 10:07:40 +0100
Commit: `feaa346`
Repo: `Nymphs3D-Blender-Addon`
Title: `Package Nymphs3D2 as Blender extension`

Documented changes:

- converted `Nymphs3D2` from a loose addon file into a proper Blender extension package
- added `blender_manifest.toml`
- added extension-oriented README guidance
- added a build path for Blender's `extension build` flow

Why it matters:

- this is the point where the addon stopped being only a script and became a real packaged Blender extension

### 2026-04-05 09:54:10 +0100
Commit: `d6d9f4c`
Repo: `Nymphs3D` helper repo
Title: `Harden installer and split addon repo`

Documented changes:

- hardened the Windows + WSL installer flow
- made rerunning `Install_NymphsCore.bat` an intended repair/update path
- reframed the helper repo as setup/backend infrastructure rather than the addon product
- strengthened verification and repair framing in the docs

Repo-boundary changes documented here:

- helper repo becomes the public-facing setup layer
- addon/frontend is treated as separate distribution
- standalone launcher stays in the helper repo as helper/runtime tooling

Why it matters:

- this is the helper-side half of the repo split that began with `ce3c989`

### 2026-04-05 09:47:50 +0100
Commit: `ce3c989`
Repo: `Nymphs3D-Blender-Addon`
Title: `Initial addon repo split`

Documented changes:

- created the separate private addon repo
- moved the addon source out of the backend/helper repo
- established the addon repo as the Blender-side frontend codebase

Why it matters:

- this is the formal repo-level split between helper/backend distribution and the Blender product surface

### 2026-04-04 19:15:23 +0100
Commit: `8ff3671`
Repo: `Nymphs3D` helper repo
Title: `Add texture upgrade audit`

Documented changes:

- created a formal audit of current texture controls versus backend capabilities
- recorded the gap between:
  - what `2.0 / 2mv` and `2.1` can do
  - what the launcher and addons currently expose

Key findings recorded in the doc:

- current UI reduced texture to a mostly binary switch
- `2.0` supports more texture-path distinction than the UI exposed
- `2.1` already had richer PBR-oriented controls than the UI exposed
- the safest next frontend wins would be:
  - explicit `2.1` PBR wording
  - explicit `2.0` standard vs turbo texture modes
  - later resolution and face-count controls for `2.1`

Why it matters:

- this is the clearest documented transition from baseline stability work to quality-of-result and product-surface refinement

### 2026-04-04 19:06:46 +0100
Commit: `75f52c4`
Repo: `Nymphs3D` helper repo
Title: `Clean roadmap and remove duplicate installer`

Documented changes:

- simplified roadmap framing
- removed duplicate installer confusion
- tightened the high-level project direction around one intended path

Why it matters:

- this pushed the repo closer to a single supported install story

### 2026-04-04 19:00:36 +0100
Commit: `963d4f4`
Repo: `Nymphs3D` helper repo
Title: `Clarify launcher and addon workflow split`

Documented changes:

- clarified the split between launcher responsibilities and addon responsibilities
- recorded the product direction more explicitly:
  - Blender as the main workflow surface
  - launcher as support, startup, and helper tooling
  - backend repos as implementation layers

Why it matters:

- this commit documents the beginning of the cleaner product boundary later completed on 2026-04-05

### 2026-04-04 18:54:00 +0100
Commit: `8c368d4`
Repo: `Nymphs3D` helper repo
Title: `Reduce repo docs to public-facing set`

Documented changes:

- removed a large set of internal planning, handoff, and session docs from the public-facing doc set
- kept only the docs considered useful for public users
- preserved the detailed internal/project narrative in git history rather than the visible docs tree

Important consequence:

- after this point, a lot of the project's real evolution stops being visible from the current docs alone
- historical reconstruction requires reading git history, not just the surviving files

### 2026-04-04 18:52:14 +0100
Commit: `1ad8e59`
Repo: `Nymphs3D` helper repo
Title: `Make beginner guide Blender-first after install`

Documented changes:

- rewrote the beginner guide around the expected post-install Blender workflow
- made the local API endpoint and Blender handoff more explicit
- reflected the intended user story: backend first, Blender immediately after

Why it matters:

- this is where the docs stopped reading like backend notes and started reading like a workflow product

### 2026-04-04 18:15:34 to 2026-04-04 18:56:16 +0100
Commits:

- `413bf4d` `Update install flow for Nymphs3D repo rename`
- `396b747` `Fix live Nymphs3D doc links`
- `77c571f` `Simplify README entry points`
- `8b61db5` `Clarify Blender-first frontend in README`
- `4aa2574` `Rename installer entry point and refine docs`
- `6573978` `Use direct installer download link in README`
- `96b6c7d` `Use release-based installer download`
- `5ca4744` `Use repo zip download in README`
- `e62e699` `Point README to installer archive asset`
- `023072f` `Use GitHub asset page link for installer archive`
- `d04b58e` `Use direct GitHub download URL for installer archive`
- `484d062` `Clarify installer archive extraction step`
- `c79ca7d` `Use direct launcher download link in README`
- `81c2077` `Fix launcher download path in README`

Documented changes:

- rebranded the public-facing flow around the `Nymphs3D` name
- repeatedly refined the installer and launcher download entrypoints
- moved README wording toward simpler entrypoints for first-time users
- made archive extraction and installer entrypoint wording more explicit

Why it matters:

- this cluster of commits shows the project actively searching for the least confusing public install path

### 2026-04-04 18:10:51 +0100
Commit: `5afadd1`
Repo: `Nymphs3D` helper repo
Title: `Add Nymphs3D2 and harden install flow`

Documented changes:

- introduced `Nymphs3D2` as a separate experimental addon file
- preserved the original addon while allowing a Blender-first evolution path
- added the Goblin single-image example as a public-facing proof of workflow
- added the disk/model footprint doc with practical measured storage numbers
- expanded install guidance around the hardened flow

Recorded `Nymphs3D2` direction from the session summary:

- separate addon file rather than replacing the original
- server control and generation workflow integrated into Blender
- capability-aware UI against the running server
- quieter console and cleaner progress wording

Recorded launcher/backend outcomes from the same period:

- launcher recovery after `2.0` startup regressions
- launcher-side port clearing before startup
- `2.0` backend made more robust under launcher pipe/logging conditions

Why it matters:

- this commit marks the project becoming more explicitly Blender-first while still keeping the standalone launcher alive as support tooling

### 2026-04-04 12:00:22 +0100
Commit: `45c5913`
Repo: `Nymphs3D` helper repo
Title: `Install from working environment locks`

Documented changes:

- recorded the move toward checked-in `requirements.lock.txt` based installs
- positioned lockfile installs as the way to reproduce a validated working machine state
- tightened install docs around reproducibility instead of loose dependency resolution

Why it matters:

- this reduced drift between a known-good setup and a fresh install

### 2026-04-04 11:39:57 +0100
Commit: `63a81f6`
Repo: `Nymphs3D` helper repo
Title: `Add one-click install flow and launcher updates`

Documented changes:

- introduced the first clearly documented one-click install path
- consolidated a large amount of project documentation into the repo's `docs/` directory
- added docs for launcher behavior, install flow, fork strategy, and broader pipeline thinking

What the one-click direction meant:

- Windows bootstrap became the intended beginner entrypoint
- WSL setup, backend installation, and local runtime preparation were treated as one guided flow

Other documented themes added at this point:

- packaging the Python launcher as a Windows `.exe`
- keeping backend repos as implementation layers
- treating installer reliability as product work, not just setup chores

Why it matters:

- this is where the repo became a full onboarding surface rather than just notes plus scripts

### 2026-04-03 to 2026-04-04 early morning
Sources:

- `session_handoff_2026-04-03_wsl_restart_quality_progress.md`
- `session_log_2026-04-04_hunyuan_progress_texture_recovery.md`

Documented changes:

- reverted Blender handoff back to the simpler direct-file `/generate -> GLB` contract
- removed an overly short timeout from non-textured direct generation because successful runs took longer than 30 seconds
- separated launcher progress work from Blender async handoff work
- recovered `2.1` generation stability, Blender handoff stability, and texture generation
- established the lesson that progress reliability is mostly a backend responsiveness and state-contract problem, not just a log parsing problem

Why it matters:

- this is a core architectural turning point for the project
- later `2.0` and launcher work explicitly built on the same lesson

### 2026-04-03 19:38:48 +0100
Source file timestamp: `session_handoff_2026-04-03_launcher_textbridge_regression.md`
Context: addon and backend contract debugging

Documented changes:

- identified the Nymphs addon line as the only Blender addon surface that mattered going forward
- recorded a key regression caused by changing `2.1` to async `uid/status/file` behavior while the addon still expected direct GLB bytes from `POST /generate`
- documented the practical requirement to keep launcher progress improvements from breaking the working text-to-mesh path

Why it matters:

- this is the first explicit statement of a contract that keeps showing up later:
  - Blender handoff must stay stable
  - launcher progress work must not be allowed to break generation

### 2026-04-03 14:14:29 +0100
Commit: `5e59cfa`
Repo: `Nymphs3D` helper repo
Title: `Refine launcher wording and add text-to-multiview roadmap`

Documented changes:

- clarified launcher wording for artist-facing use
- added a forward-looking roadmap for experimental text-to-multiview generation
- positioned text-to-multiview as a `2mv` experiment rather than a baseline feature

Technical direction recorded in docs:

- current text mode was documented as `text -> one generated image -> image-to-3D`
- future idea was `text -> generated multiview set -> 2mv -> mesh`
- main expected blocker was cross-view consistency, not just implementation effort

Why it matters:

- the docs show an early split between the stable baseline and experimental research ideas

### 2026-04-03 14:08:35 +0100
Commit: `c3b097c`
Repo: `Nymphs3D` helper repo
Title: `Add launcher prototype and 2.1 text-mode install support`

Documented changes:

- introduced the standalone launcher prototype
- defined the launcher as a Windows app that builds and runs WSL commands internally
- documented early support for `Hunyuan 2.1` text-prompt bridge startup

Launcher direction at this stage:

- user chooses backend, input mode, texture mode, turbo, FlashVDM, low-VRAM, and port
- app launches the corresponding WSL command
- launcher becomes the main non-terminal control surface

Why it matters:

- this is the start of the launcher as a real product surface rather than a temporary helper

### 2026-04-03 13:40:48 +0100
Commit: `165faf1`
Repo: `Nymphs3D` helper repo
Title: `Improve install docs and add preflight checks`

Documented changes:

- tightened the install documentation for non-technical users
- added preflight-style thinking to the setup flow
- started moving the project toward a guided install instead of a loose script collection

Why it matters:

- this is the first clear sign that beginner installation reliability had become a project priority

### 2026-04-03 13:37:29 +0100
Commit: `8df172f`
Repo: `Nymphs3D` helper repo
Title: `Add initial WSL install and run bundle for forked Hunyuan setup`

Documented changes:

- established the first public-facing repo docs around a forked Hunyuan local setup
- positioned the project as a Windows + WSL bundle for running custom Hunyuan workflows locally
- created the first artist-facing notes and installation framing

Why it matters:

- this is the point where the project became more than backend experiments and started to present itself as an installable workflow

### 2026-04-03 13:36:31 +0100
Source file timestamp: `artist_notes.md`
Context: early user-facing local note

Documented changes:

- defined the basic product split between `Hunyuan3D-2` for multiview/custom addon workflow and `Hunyuan3D-2.1` for newer single-image and PBR work
- recorded first-run expectations around slow model downloads and cache-building
- captured practical troubleshooting assumptions for venv activation, CUDA failures, and low-VRAM fallbacks

Why it matters:

- this is the first concise user-level articulation of how the two backend families were meant to coexist

### 2026-04-03 12:37:44 +0100
Source file timestamp: `fork_strategy_for_team_install.md`
Context: local strategy doc

Documented changes:

- explicitly recognized both `Hunyuan3D-2` and `Hunyuan3D-2.1` local checkouts as real forks, not clean upstream clones
- listed concrete local changes such as:
  - modified API servers
  - modified Gradio apps and model worker behavior
  - deletion/replacement of upstream addon files
  - custom multiview and Nymphs addon files
- recommended moving to personal/team forks as the basis for reproducible installs

Why it matters:

- this is where the project became clearly "your Hunyuan workflow" rather than just "Tencent upstream with notes"

### 2026-04-03 12:30:56 +0100
Source file timestamp: `reverse_engineered_wsl_hunyuan_blender_mcp_install.md`
Context: local machine state reconstruction

Documented changes and findings:

- reconstructed the actual WSL machine state under `/home/babyj`
- recorded that both `Hunyuan3D-2` and `Hunyuan3D-2.1` existed locally with Python `3.10` virtual environments
- documented CUDA `13.0`, large Hugging Face model cache footprint, and the Blender MCP server being present on the same machine
- treated the machine setup itself as something that needed to be turned into a reproducible install story

Why it matters:

- this is the bridge from "working personal machine" to "something that can be documented and reproduced"

### 2026-04-03
Source: `session_handoff_hunyuan_launcher_restart_summary.md`
Context: restart handoff

Documented changes:

- both Hunyuan repos were cleaned and forked to your GitHub
- `game3d-wsl-setup` was created as a setup/documentation repo
- `2.1` text prompt mode was made to work on your machine
- a Windows launcher app was built and was already close to replacing the raw WSL console workflow

Why it matters:

- this is the true formation point of the repo-backed project structure that later became `Nymphs3D`

### 2026-04-01
Source: `/home/babyj/Hunyuan3D-2_backup_2026-04-01/hunyuan3d_2mv_tomorrow_handoff.md`
Context: pre-`Nymphs3D` repo project state

Documented project state:

- main target was `Hunyuan3D 2.0 MV` running in Windows + WSL from the official Tencent repo
- Blender addon was already being treated as a key control surface that needed support for:
  - text prompt
  - single image
  - multiview `front/back/left/right`
  - texture toggle
  - selected-mesh texturing
- `2.1` was already recognized as the better single-image path, but not the right multiview target

Critical environment work already completed by this point:

- WSL Ubuntu baseline established
- CUDA `13.0` installed in WSL because the active Torch build required `cu130`
- texture build steps repaired with `CUDA_HOME=/usr/local/cuda-13.0`
- cache/symlink fixes worked around Hunyuan cache path mismatches
- `2.0` API, `2.0` Gradio, and `2.0 MV` Gradio were all recorded as working
- text-to-3D bridge support was already patched into `2.0`

Important early direction:

- use official Tencent repo in WSL as the backend base
- patch the Blender addon rather than abandoning Blender integration
- keep both API and Gradio working while adding multiview-focused Blender support

Why it matters:

- this is the earliest documented project baseline I found
- it shows the project did not begin as a general helper repo; it began as hands-on backend patching plus Blender workflow hacking around `2.0 MV`

## Backend And Launcher Work Recorded In Historical Docs

The addon repo did not exist when these changes were made, but they are important context for how `Nymphs3D2` emerged.

### `2.0 / 2mv` backend stability and progress work

Recorded in `docs/session_summary_2026-04-04_2mv_progress_and_docs.md` from the helper repo history:

- multiview texture handoff was fixed by normalizing dict-based prompt images into stable ordered image input
- `2.0` gained structured shared progress state and `/active_task`
- direct `POST /generate` on `2.0` was moved through `run_in_threadpool(...)` so the server stayed responsive during generation
- request validation was added for invalid combinations such as texture without texture mode enabled
- real texture-stage timing and sub-step logging were added

Project impact:

- launcher progress stopped being treated purely as a terminal parsing problem
- backend responsiveness and progress-state contracts became first-class concerns

### Launcher quality and packaging work

Recorded in `docs/frontend_launcher.md`, `docs/build_windows_exe.md`, and the same historical session summary:

- launcher gained start/stop control, live logs, status display, and command preview
- log noise was reduced substantially
- `2.0` progress handling was improved with a hybrid of structured task state and console-derived progress
- Tk-threading issues in followed-log updates were fixed
- launcher was repeatedly rebuilt as `dist/hunyuan_launcher.exe`
- docs consistently described the launcher as a controller, not a full AI package

Project impact:

- the standalone launcher became a serious runtime-control tool instead of a convenience wrapper

## Current Addon-Repo State

As of commit `beebc43` on `2026-04-05 17:33:00 +0100`, the addon repo's documented position is:

- the repo contains the Blender-side frontend code for `Nymphs3D2`
- the repo root is the actual extension package
- the repo root delegates to the promoted `Nymphs3D2v2` implementation
- the extension bundles `requests` and its dependencies
- Blender `4.2+` extension packaging/build is the intended distribution model
- the current published extension feed version is `1.0.1`
- the backend/helper repo is separate and responsible for local Windows + WSL setup, backend cloning/repair, runtime verification, and the standalone helper launcher

## Short Version

If the whole documented history is compressed to one line, it is this:

`Nymphs3D2` began as an experimental Blender-side branch inside the original Hunyuan helper/setup project, then became the main Blender-first frontend surface, and is now packaged as a proper standalone Blender extension in its own repo with bundled dependencies and a public extension feed tracking the promoted `v2` implementation.
