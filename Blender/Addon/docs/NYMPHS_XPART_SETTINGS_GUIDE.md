# Nymphs X-Part Settings Guide

Date: 2026-04-13

This guide summarizes what the current X-Part controls do inside the addon, how
they relate to the upstream X-Part demo, and which presets are practical on
today's local runtime.

## Pipeline Relationship

There are three different ideas in the current workflow:

- Shape generation:
  - make a whole object mesh from text or images
  - this is the normal shape backend path
- Shape segmentation:
  - analyze an existing mesh and find semantic regions / masks / boxes
  - this is the P3-SAM Stage 1 path
- Shape decomposition:
  - use the Stage 1 analysis as prompts and try to generate separated coherent
    part meshes
  - this is the X-Part Stage 2 path

In practice:

```text
mesh -> P3-SAM Analyze Mesh -> stage1_manifest + aabb boxes -> X-Part Generate Parts
```

So P3-SAM does not generate the final decomposed parts. It produces the
structure bundle X-Part consumes.

## Upstream Reference

The public X-Part demo path in the repo uses:

- `float32`
- `octree_resolution=512`
- pipeline default `num_inference_steps=50`
- no explicit box cap

That is the right upstream reference, but it is not automatically a safe local
default on this machine.

## What The Main Settings Do

`Steps`

- diffusion denoising step count
- higher improves quality and stability of the generated latent field
- also keeps the GPU busy longer and increases the chance of long-run CUDA
  failure on this WSL setup

`Octree`

- export-time mesh extraction resolution
- `512` usually gives meaningfully better geometry than `256`
- mostly increases export cost, but it also raises the overall memory pressure
  of a successful run

`Max Boxes`

- cap on how many Stage 1 bounding boxes X-Part uses
- `0` means "use all boxes"
- this is currently the most dangerous quality knob for runtime stability
- more boxes increase conditioning cost and the amount of part generation work
  before diffusion even starts

`Precision`

- current safe path is `float32`
- `bfloat16` and `float16` remain experimental here

`CPU Threads`

- cap for CPU-side work
- more threads can reduce wall-clock time for the heavy conditioning phase
- more threads also make the machine feel more "stuck" because CPU usage can
  sit around `800%` to `1200%`

## Which Settings Seem To Trigger CUDA Failures Most

Based on current local evidence:

1. `Max Boxes`
   - strongest visible effect so far
   - going from `1` to "all boxes" changes the run much more than any other
     single knob we tested
2. `Steps`
   - medium to high effect
   - higher step counts keep the GPU context alive longer once diffusion starts
3. `Octree`
   - high effect for export pressure
   - but not the main cause of the latest heavy-lane failures, because those
     failed before export

Important nuance:

- the old export-stage CUDA crash was fixed by lowering export chunking
- the current heavy-lane failure now happens earlier, at diffusion start, which
  points more at `boxes + steps` than at octree alone

## Why Your Working Preset Works Better Than The Official-Like Default

Your practical preset:

```text
Steps: 20
Octree: 256
Max Boxes: 6
CPU Threads: 16
Precision: Float32
```

works better than the official-like lane because it reduces all three of the
high-pressure areas at once:

- fewer diffusion steps than `50`
- cheaper export than `512`
- fewer active boxes than "all boxes"

The official-style lane asks for:

- longer diffusion residency
- heavier Stage 1 prompt fanout
- heavier export

On this machine, that combination is enough to re-trigger `CUDA driver error:
device not ready` even on the clean official env.

## Practical Presets

### 16 GB GPU

Recommended current working-quality preset:

```text
Steps: 20
Octree: 256
Max Boxes: 6
Precision: Float32
CPU Threads: 16
```

Safer fallback preset:

```text
Steps: 20
Octree: 512
Max Boxes: 3
Precision: Float32
CPU Threads: 16
```

Minimal survival preset:

```text
Steps: 8
Octree: 256
Max Boxes: 1
Precision: Float32
CPU Threads: 16
```

This last lane is mainly for proving the runtime still works. It is not a
quality target.

### 32 GB GPU

This is a reasoned starting point, not yet locally validated on a 32 GB card:

```text
Steps: 30
Octree: 512
Max Boxes: 0
Precision: Float32
CPU Threads: 16
```

If that is stable, the next lane to try is the official-style target:

```text
Steps: 50
Octree: 512
Max Boxes: 0
Precision: Float32
CPU Threads: 16
```

## Current Runtime Interpretation

The shared old venv was a real problem, but it was not the only problem.

Current best explanation:

- mixed venv caused a lot of earlier instability
- export chunking was a real separate bug and had to be fixed
- the remaining heavy-lane failures are now mostly a workload / GPU-context
  stability problem on this specific machine

## Likely Future Improvement Direction

If we want the official-style defaults to work on a `4080 SUPER`, the most
promising direction is not lower precision. It is better staging:

- keep box-conditioned work off the GPU as long as possible
- hand work to CUDA in smaller slices
- avoid pushing all boxes through the heavy path at once
- keep export VRAM usage controlled like the current `export_num_chunks=20000`
  fix already does

That means future work should focus on controlled device handoff and staged box
processing, not on reintroducing the old mixed-runtime shortcuts.
