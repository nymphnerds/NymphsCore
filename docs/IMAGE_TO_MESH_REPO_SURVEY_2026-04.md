# Image-to-Mesh Repo Survey (April 2026)

This note converts the external research PDF at `/home/babyj/Nymphs3D_local_docs/image_to_mesh_repos.pdf` into repo-native markdown for `Nymphs3D-Blender-Addon`.

The source report surveyed public image-to-mesh repositories available as of April 5, 2026.

## Scope

- Total repos surveyed: `26`
- China-origin: `16`
- US / Western: `7`
- International: `3`
- Method buckets used by the report:
  - Feed-forward
  - Diffusion
  - NeRF / SDS
  - Framework / Dataset

## What Matters For Nymphs3D

The current addon and companion backend repos are already centered on the Hunyuan family. The report reinforces that this is the right default posture.

### Immediate fit

- `Hunyuan3D-2.1`
  - Best current production-style fit for single-image generation plus newer PBR-oriented texture work.
  - Report notes: image-to-mesh plus PBR texture, fully open-source including training code, production-ready positioning.
- `Hunyuan3D-2`
  - Still important for the existing multiview and texture path.
  - Report notes: two-stage DiT geometry plus high-resolution texture synthesis and lower VRAM shape-only path.

### Future Hunyuan track

- `Hunyuan3D-Omni`
  - Interesting future controlled-generation candidate.
  - The report positions it as a controllable 3D system with conditioning on point cloud, voxel, skeleton, and bounding boxes.
  - This is not a drop-in replacement for the current addon flow, but it is a strong future research direction.

### Useful, but not a runtime backend

- `HY3D-Bench`
  - This is a dataset / benchmark resource, not a normal inference dependency for the addon.
  - Report notes: large-scale watertight mesh, part-level, and synthetic benchmark suite plus a smaller baseline model.
  - Relevant when evaluating or training, not when deciding the addon's default backend.

## Recommended Backend Priority For This Addon

### Tier 1: keep building around these

| Repo | Why it matters here |
|---|---|
| `Hunyuan3D-2.1` | Best match for current single-image plus PBR-oriented addon workflow. |
| `Hunyuan3D-2` / `2mv` | Best match for the current multiview path and existing local WSL setup. |

### Tier 2: strong future alternative providers

| Repo | Why it is worth tracking |
|---|---|
| `InstantMesh` | Lower-friction feed-forward path, Apache-2.0 in the source report, and lighter-weight than many research systems. |
| `OpenLRM` | Open reconstruction model with an Apache-2.0 report classification and a clean feed-forward framing. |
| `TripoSG` / `TripoSF` / `TripoSR` | Strong quality and speed claims in the report, especially relevant if a cleaner alternative to Hunyuan becomes desirable. |
| `TRELLIS` / `TRELLIS.2` | High-capability future-facing systems for harder topology and richer material output. |
| `SPAR3D` | Interesting if interactive point-cloud-oriented reconstruction becomes part of the product direction. |
| `Hunyuan3D-Omni` | Best future candidate if Nymphs3D adds more guided or controllable structure inputs. |

### Tier 3: research references, not near-term addon targets

These remain useful background references, but they are not the first places to spend integration effort for this addon:

- `DreamCraft3D`
- `stable-dreamfusion`
- `Unique3D`
- `PartCrafter`
- `Wonder3D`
- `SV3D`
- `Zero-1-to-3`
- `Zero123++`
- `One-2-3-45`
- `SAM 3D Objects`
- `threestudio`
- `Freeplane-InstantMesh`

The common reason is that they are either:

- older research baselines
- slower multi-stage diffusion / NeRF-style pipelines
- framework layers rather than a direct backend target
- useful component technologies rather than the main addon-facing runtime

## Survey Snapshot

The report's most relevant entries for Nymphs3D were:

| Repo / Model | Year | Method | Origin | Report takeaway |
|---|---|---|---|---|
| `Hunyuan3D-2.1` | 2025 | Feed-forward | China (Tencent) | Best current production-style image-to-mesh plus PBR candidate. |
| `Hunyuan3D-2` | 2025 | Feed-forward | China (Tencent) | Strong current multiview and texture baseline. |
| `Hunyuan3D-Omni` | 2025 | Feed-forward | China (Tencent) | Strong controllable future backend candidate. |
| `HY3D-Bench` | 2026 | Dataset / Benchmark | China (Tencent) | Evaluation and training asset, not a normal addon runtime backend. |
| `TripoSG` | 2025 | Feed-forward | China (VAST AI) | High-quality open feed-forward alternative worth tracking. |
| `InstantMesh` | 2024 | Feed-forward | China (Tencent ARC) | Lightweight alternative with broad open-source adoption. |
| `OpenLRM` | 2024 | Feed-forward | China (Shanghai AI Lab) | Practical open reconstruction model worth watching. |
| `TRELLIS` | 2024 | Feed-forward | US (Microsoft) | Advanced future-facing system with editing flexibility. |
| `TRELLIS.2` | 2025 | Feed-forward | US (Microsoft) | High-end future candidate for hard topology and richer materials. |
| `SPAR3D` | 2025 | Feed-forward | US (Stability AI) | Interesting advanced reconstruction path with interactive editing ideas. |

## Full Repo List From The Source Report

The report surveyed these repositories or model families:

- `Hunyuan3D-2.1`
- `Hunyuan3D-2`
- `Hunyuan3D-Omni`
- `Hunyuan3D-1`
- `HY3D-Bench`
- `TripoSG`
- `TripoSF`
- `TripoSR`
- `InstantMesh`
- `CraftsMan3D`
- `DreamCraft3D`
- `Unique3D`
- `PartCrafter`
- `Wonder3D`
- `OpenLRM`
- `stable-dreamfusion`
- `TRELLIS`
- `TRELLIS.2`
- `SAM 3D Objects`
- `SPAR3D`
- `SV3D`
- `Zero-1-to-3`
- `Zero123++`
- `One-2-3-45`
- `threestudio`
- `Freeplane-InstantMesh`

## Bottom Line

For `Nymphs3D-Blender-Addon`, the report does not argue for replacing the current Hunyuan-first direction.

It supports a practical product roadmap:

1. Keep `Hunyuan3D-2.1` as the main single-image / newer texture path.
2. Keep `Hunyuan3D-2` / `2mv` as the main multiview path.
3. Treat `Hunyuan3D-Omni` as the most interesting future Hunyuan research branch.
4. Treat `HY3D-Bench` as a benchmark/training asset, not as a runtime dependency.
5. If Nymphs3D ever adds alternate engines, prioritize `InstantMesh`, `OpenLRM`, `TripoSG`, and the `TRELLIS` line before older diffusion or NeRF-style baselines.
