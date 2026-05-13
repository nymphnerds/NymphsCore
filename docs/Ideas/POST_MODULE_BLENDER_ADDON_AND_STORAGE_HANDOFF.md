# Post-Module Blender Addon And Storage Handoff

Date: 2026-05-13
Branch context: `modular`

Purpose: capture follow-up work for after the Z-Image and TRELLIS modules have
working module-owned install, status, model fetch, launch, logs, and smoke test
flows.

This is deliberately after the module work. Do not start by changing the
Blender addon while the module contracts are still moving.

## Context

The Manager is moving toward module-owned backend behavior:

- modules own model fetch choices and scripts
- modules own selected runtime/model preset files
- modules own cache/output/log path declarations
- Manager renders compact native model fetch controls
- Blender remains the normal generation UI for image/shape workflows

Once Z-Image and TRELLIS are stable as modules, the Blender addon needs to align
with those module contracts instead of carrying older hardcoded assumptions.

## Blender Addon Goals

The addon should:

- use the same model cache that Manager model fetch uses
- use module-owned output and log paths
- understand selected model presets written by module fetch actions
- avoid guessing backend variants from old addon-only settings
- keep Blender as the generation UI
- prefer module discovery over hardcoded backend paths

The addon should not:

- store generated user content inside backend source repos
- assume Z-Image rank alone is enough
- launch against a different model cache than the Manager fetched
- silently use old pre-modular paths when a module is installed

## Z-Image Addon Follow-Up

Module-owned paths:

```text
cache:   $HOME/NymphsData/cache/huggingface
outputs: $HOME/NymphsData/outputs/zimage
logs:    $HOME/NymphsData/logs/zimage
preset:  $HOME/NymphsData/config/zimage/generation-preset.env
```

The addon should read or mirror:

```text
Z_IMAGE_NUNCHAKU_PRECISION=int4|fp4
Z_IMAGE_NUNCHAKU_RANK=32|128|256
```

Important:

```text
INT4 and FP4 are Z-Image/Nunchaku generation weights.
BF16 belongs to LoRA training and is not a Z-Image fetch option.
```

The addon should eventually launch Z-Image with the same env/path assumptions
used by the module scripts:

```text
NYMPHS3D_HF_CACHE_DIR=$HOME/NymphsData/cache/huggingface
HF_HOME=$HOME/NymphsData/cache/huggingface
HF_HUB_CACHE=$HOME/NymphsData/cache/huggingface
Z_IMAGE_OUTPUT_DIR=$HOME/NymphsData/outputs/zimage
NYMPHS2D2_OUTPUT_DIR=$HOME/NymphsData/outputs/zimage
```

## TRELLIS Addon Follow-Up

Expected module-owned direction:

```text
cache:   $HOME/NymphsData/cache/huggingface
outputs: $HOME/NymphsData/outputs/trellis
logs:    $HOME/NymphsData/logs/trellis
preset:  $HOME/NymphsData/config/trellis/model-preset.env
```

The addon should read or mirror the selected GGUF quant:

```text
TRELLIS_GGUF_QUANT=Q4_K_M|Q5_K_M|Q6_K|Q8_0
```

The addon should not assume all TRELLIS quants are present. It should either
read module status/discovery output or handle missing quant choices clearly.

## Generated Content Storage

Generated user content should not live inside backend repos or runtime install
folders.

Avoid storing generated content under:

```text
$HOME/Z-Image
$HOME/TRELLIS.2
$HOME/NymphsModules/<module>
```

Recommended long-term shared generated content root:

```text
$HOME/NymphsData/generated/<module-id>
```

Examples:

```text
$HOME/NymphsData/generated/zimage
$HOME/NymphsData/generated/trellis
$HOME/NymphsData/generated/lora
```

During migration, existing paths such as `$HOME/NymphsData/outputs/zimage` can
remain for compatibility, but the product direction should be clear:

```text
source/runtime: backend repo and venv files
cache/models:   downloaded reusable model files
generated:      user-created outputs and run metadata
logs:           diagnostic logs
config:         selected presets and module-owned settings
```

If the Manager later implements the currently placeholder right-rail `Storage`
control, it should open/manage shared generated/cache/log/config roots. It
should not imply generated content belongs inside backend source repos.

## Preferred Discovery Shape

Long-term, Blender should not need to hardcode every module path.

Preferred direction:

- each installed module exposes a small discovery file or command
- discovery reports repo root, python path, port, cache root, generated root,
  logs root, config/preset file, and selected model variant
- Blender reads discovery when the module is installed
- hardcoded addon defaults remain only as compatibility fallback

Possible command shape:

```bash
scripts/<module>_status.sh --json
```

or:

```text
$HOME/NymphsData/config/<module-id>/module-discovery.json
```

The exact format can be chosen after Z-Image and TRELLIS module behavior is
stable.

## Acceptance Checks

After the modules are working:

1. Z-Image model fetch selects a preset and writes the module preset file.
2. Blender launches Z-Image using the same cache/output assumptions.
3. Blender can generate with the selected Z-Image precision/rank.
4. TRELLIS model fetch selects a GGUF quant and writes/declares that choice.
5. Blender launches TRELLIS with the selected quant or clearly reports if it is
   missing.
6. Generated images/meshes do not land inside backend repos.
7. Logs and generated outputs are easy to open from predictable shared roots.
8. Existing users with older output paths are not stranded during migration.

## Related Docs

- `docs/Ideas/UNIVERSAL_MODEL_FETCH_UI_PLAN.md`
- `docs/Ideas/NYMPH_PLUGIN_STANDARDIZATION_HANDOFF.md`
