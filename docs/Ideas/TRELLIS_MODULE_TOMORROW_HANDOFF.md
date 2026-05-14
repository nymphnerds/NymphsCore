# TRELLIS Module Handoff

Use this handoff to continue or audit the NymphsCore modular backend port with
TRELLIS.

## Current State

Current NymphsCore branch:

```text
modular
```

Latest pushed NymphsCore standardization/doc commits:

```text
da131cd Document module install option flow
2e9a85f Copy visible install option selections before install
```

Current pushed TRELLIS module commit:

```text
ea5263b Improve TRELLIS model fetch guide
```

Current pushed addon commit:

```text
930762e Align addon module runtime paths
```

Read these first:

```text
/home/nymph/NymphsCore/docs/NYMPHS_MODULE_MAKING_GUIDE.md
/home/nymph/NymphsCore/docs/NYMPH_MODULE_UI_STANDARD.md
/home/nymph/NymphsCore/docs/Ideas/UNIVERSAL_MODEL_FETCH_UI_PLAN.md
/home/nymph/NymphsCore/CHANGELOG.md
```

## Reference Modules

Z-Image remains the working proof for image model fetch.

```text
source module repo: /home/nymph/NymphsModules/zimage
installed runtime:   /home/nymph/Z-Image
manifest:            nymph.json
scripts:             scripts/zimage_*.sh
```

TRELLIS is now the working proof for 3D GGUF model fetch.

```text
source module repo: /home/nymph/NymphsModules/trellis
installed runtime:   /home/nymph/TRELLIS.2
manifest:            nymph.json
scripts:             scripts/trellis_*.sh
```

## Rules Proven

The Manager must stay generic.

Module-owned scripts do the real work.

Installed actions run from the installed module root, not stale registry/cache
scripts.

Fast startup installed state comes from Windows-side `.nymph-module-version`
marker reads against the real managed runtime distro:

```text
NymphsCore
```

Startup must not run:

```text
status
model cache scans
smoke tests
downloads
backend health checks
```

Status is a later background health/detail pass and must not demote
marker-installed modules to Available.

Model fetch does not need WebView2. Use compact native
`ui.manager_action_groups`.

`Hugging Face token` is a persistent secret field and is passed to module scripts
as:

```text
NYMPHS3D_HF_TOKEN
```

Model fetch UI belongs on the module details page, below `// DETAILS` and above
`// MODULE ACTIONS`.

The details pane must explain what gets downloaded.

Source model pages should be clickable links in the details guide.

Long fetches use:

```json
"result": "show_logs"
```

Smoke Test uses:

```json
"result": "show_output"
```

Successful smoke tests must clearly say:

```text
SMOKE TEST PASSED
```

A smoke test can pass with `loaded_model_id=null` if it only proves the backend
starts and answers `/server_info`.

Closing the Manager must cancel active module lifecycle process trees. Module
install/fetch/smoke-test/update/repair/uninstall scripts must not detach
untracked background work.

## TRELLIS Module State

The TRELLIS module repo now provides:

```text
nymph.json
scripts/install_trellis.sh
scripts/trellis_status.sh
scripts/trellis_start.sh
scripts/trellis_stop.sh
scripts/trellis_logs.sh
scripts/trellis_fetch_models.sh
scripts/trellis_smoke_test.sh
scripts/trellis_uninstall.sh
```

The module has:

- module-owned install/status/start/stop/logs/fetch_models/smoke_test/uninstall
  scripts
- native compact `Model Fetch` action group
- simple module actions: `Smoke Test`, `Start`, `Stop`, `Logs`
- `.nymph-module-version` marker behavior
- status behavior based on marker install truth plus model/runtime readiness
- model/cache/output/log/config paths aligned with `$HOME/NymphsData`

Current installed/runtime paths:

```text
runtime: /home/nymph/TRELLIS.2
cache:   /home/nymph/NymphsData/cache/huggingface
outputs: /home/nymph/NymphsData/outputs/trellis
logs:    /home/nymph/NymphsData/logs/trellis
config:  /home/nymph/NymphsData/config/trellis
rembg:   /home/nymph/NymphsData/models/rembg
port:    8095
```

## TRELLIS Fetch Contract

The user can pick quantized GGUF models manually:

```text
Q4_K_M
Q5_K_M
Q6_K
Q8_0
All quants
```

The installed details guide must explain that Fetch Models downloads:

```text
shared GGUF support files
selected Aero-Ex/Trellis2-GGUF quant bundle
required microsoft/TRELLIS.2-4B support checkpoint
rembg u2net background-removal model
```

Beginner-friendly choice guide:

```text
Q4_K_M  smallest download and lowest VRAM; best first proof test
Q5_K_M  recommended balance of quality, download size, and VRAM
Q6_K    heavier quality-focused option after Q5 works
Q8_0    largest local GGUF option; use with plenty of VRAM and disk
All     optional large download for Blender/addon quant switching later
```

Source links:

```text
Official TRELLIS.2 repo:     https://github.com/microsoft/TRELLIS.2
Official project page:       https://microsoft.github.io/TRELLIS.2/
GGUF models:                 https://huggingface.co/Aero-Ex/Trellis2-GGUF
Support checkpoint:          https://huggingface.co/microsoft/TRELLIS.2-4B
rembg u2net:                 https://github.com/danielgatis/rembg/releases/tag/v0.0.0
```

## FlashAttention Install State

FlashAttention options are module-owned install fields under:

```text
// FLASH ATTENTION OPTIONS
GPU
Max jobs
NVCC threads
```

Validated live:

```text
TRELLIS_FLASH_ATTN_CUDA_ARCHS=sm80
TRELLIS_FLASH_ATTN_MAX_JOBS=4
TRELLIS_FLASH_ATTN_NVCC_THREADS=2
```

The successful FlashAttention build installed:

```text
flash-attn 2.8.3
```

Observed successful build timing from Manager logs on 2026-05-14:

```text
install step started: 11:31:52
wheel build started: 11:31:57
wheel build finished: 12:07:12
package installed:    12:07:13
```

Actual wheel compile time was about 35 minutes with:

```text
FLASH_ATTN_CUDA_ARCHS=80
MAX_JOBS=4
NVCC_THREADS=2
```

The tested build path compiled only SM80:

```text
-gencode arch=compute_80,code=sm_80
```

## Addon Compatibility State

Addon source repo:

```text
/home/nymph/NymphsAddon
```

Pushed addon commit:

```text
930762e Align addon module runtime paths
```

Built test zip:

```text
/home/nymph/NymphsAddon/dist/nymphs-1.1.236.zip
```

Addon path alignment completed:

- Z-Image repo remains `~/Z-Image`
- Z-Image Python remains `~/Z-Image/.venv-nunchaku/bin/python`
- TRELLIS repo remains `~/TRELLIS.2`
- TRELLIS Python remains `~/TRELLIS.2/.venv/bin/python`
- Z-Image port remains `8090`
- TRELLIS default port is now `8095`
- stale saved addon TRELLIS port `8094` migrates to `8095`
- addon-launched Z-Image/TRELLIS cache, outputs, logs, and config env vars now
  align with `$HOME/NymphsData`

Still useful later:

- replace hardcoded addon runtime paths with a small module discovery
  file/command
- make addon TRELLIS quant launch read the module-owned
  `$HOME/NymphsData/config/trellis/model-preset.env`
- smoke-test Blender against module-fetched Z-Image and TRELLIS models

## Old Main References

Use old main branch Manager source only as a reference for what TRELLIS used to
do.

Do not switch local branches.

Use read-only commands such as:

```text
git show origin/main:<path>
```

Useful reference files:

```text
origin/main:Manager/scripts/install_trellis.sh
origin/main:Manager/scripts/prefetch_models.sh
origin/main:Manager/scripts/smoke_test_server.sh
origin/main:Manager/scripts/runtime_tools_status.sh
origin/main:Manager/scripts/trellis_adapter/api_server_trellis_gguf.py
origin/main:Manager/scripts/trellis_adapter/trellis_gguf_common.py
```

## Validation Checklist

Completed:

1. TRELLIS manifest parses with `python3 -m json.tool nymph.json`.
2. Scripts pass `bash -n`.
3. Install writes `.nymph-module-version` last.
4. Status reports installed truth from marker and includes useful model/runtime
   readiness.
5. Model Fetch UI is declared through native `ui.manager_action_groups`.
6. Fetch action runs the installed module-owned script.
7. Fetch progress explains large downloads.
8. Smoke Test script reports `SMOKE TEST PASSED` on success.
9. Generated outputs, model files, logs, and caches are not stored inside the
   disposable repo/runtime root unless preserved as legacy compatibility paths.

Still worth rechecking in the UI after every Manager/module refresh:

1. Manager shows TRELLIS installed immediately from marker after restart.
2. Model Fetch UI appears after install without WebView2.
3. The installed details page shows the `0.1.14` TRELLIS model-fetch guide.
4. Clicking Fetch Models uses the installed `scripts/trellis_fetch_models.sh`.
5. Blender addon can launch TRELLIS on port `8095`.

## Environment Caution

The dev/source WSL is:

```text
NymphsCore_Lite
```

The actual managed runtime distro is:

```text
NymphsCore
```

Do not accidentally test against the wrong distro.
