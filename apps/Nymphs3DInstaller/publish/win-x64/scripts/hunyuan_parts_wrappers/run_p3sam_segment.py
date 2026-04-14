#!/usr/bin/env python3
import argparse
import json
import os
import random
import sys
from pathlib import Path

import numpy as np
import trimesh
import torch


REPO_ROOT = Path(__file__).resolve().parents[1]
P3SAM_ROOT = REPO_ROOT / "P3-SAM"
P3SAM_DEMO_DIR = REPO_ROOT / "P3-SAM" / "demo"
if str(P3SAM_ROOT) not in sys.path:
    sys.path.insert(0, str(P3SAM_ROOT))
if str(P3SAM_DEMO_DIR) not in sys.path:
    sys.path.insert(0, str(P3SAM_DEMO_DIR))

from auto_mask import AutoMask, Timer, save_mesh  # noqa: E402


def set_seed(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed(seed)
        torch.cuda.manual_seed_all(seed)


def build_color_map(face_ids: np.ndarray) -> dict[int, np.ndarray]:
    color_map = {}
    for part_id in np.unique(face_ids):
        if part_id < 0:
            continue
        color_map[int(part_id)] = (np.random.rand(3) * 255).astype(np.uint8)
    color_map[-1] = np.array([255, 0, 0], dtype=np.uint8)
    color_map[-2] = np.array([0, 0, 0], dtype=np.uint8)
    return color_map


def _path_if_exists(path: Path) -> str:
    return str(path) if path.exists() else ""


def main() -> int:
    parser = argparse.ArgumentParser(description="Run P3-SAM part segmentation and export usable outputs.")
    parser.add_argument("--mesh_path", required=True, help="Input mesh path")
    parser.add_argument("--output_dir", required=True, help="Directory to write outputs into")
    parser.add_argument("--ckpt_path", default=None, help="Optional local p3sam checkpoint path")
    parser.add_argument("--point_num", type=int, default=30000, help="Sampled point count for the low-memory lane")
    parser.add_argument("--prompt_num", type=int, default=96, help="Prompt count for the low-memory lane")
    parser.add_argument("--prompt_bs", type=int, default=4, help="Prompt batch size for the low-memory lane")
    parser.add_argument("--threshold", type=float, default=0.95, help="Post-process threshold")
    parser.add_argument("--post_process", type=int, default=0, help="Enable the upstream post-process step")
    parser.add_argument("--save_mid_res", type=int, default=0, help="Keep upstream intermediate artifacts")
    parser.add_argument("--show_info", type=int, default=1, help="Show upstream progress output")
    parser.add_argument("--show_time_info", type=int, default=1, help="Show upstream timing output")
    parser.add_argument("--seed", type=int, default=42, help="Random seed")
    parser.add_argument("--parallel", type=int, default=0, help="Use DataParallel when multiple GPUs exist")
    parser.add_argument("--clean_mesh", type=int, default=1, help="Run the upstream mesh cleanup step")
    args = parser.parse_args()

    Timer.STATE = bool(args.show_time_info)
    os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")

    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    original_mesh_path = str(Path(args.mesh_path).resolve())
    mesh = trimesh.load(args.mesh_path, force="mesh")
    set_seed(args.seed)

    automask = AutoMask(args.ckpt_path)
    aabb, face_ids, mesh = automask.predict_aabb(
        mesh,
        save_path=str(output_dir),
        point_num=args.point_num,
        prompt_num=args.prompt_num,
        threshold=args.threshold,
        post_process=bool(args.post_process),
        save_mid_res=bool(args.save_mid_res),
        show_info=bool(args.show_info),
        seed=args.seed,
        is_parallel=bool(args.parallel),
        clean_mesh_flag=bool(args.clean_mesh),
        prompt_bs=args.prompt_bs,
    )

    # Export the cleaned/post-processed mesh that Stage 1 actually segmented.
    # Stage 2 should consume this canonical mesh instead of assuming the
    # original input survived preprocessing unchanged.
    stage1_mesh_path = output_dir / "source_mesh_stage1.glb"
    mesh.export(stage1_mesh_path)

    segmented_glb_path = output_dir / "p3sam_segmented.glb"
    color_map = build_color_map(face_ids)
    save_mesh(str(segmented_glb_path), mesh, face_ids, color_map)

    segmented_ply_path = output_dir / "p3sam_segmented.ply"
    segmented_aabb_glb_path = output_dir / "p3sam_segmented_aabb.glb"
    segmented_aabb_npy_path = output_dir / "p3sam_segmented_aabb.npy"
    segmented_face_ids_npy_path = output_dir / "p3sam_segmented_face_ids.npy"
    summary_path = output_dir / "summary.json"
    aabb_json_path = output_dir / "p3sam_aabb.json"
    manifest_path = output_dir / "stage1_manifest.json"

    semantic_label_count = int(len([x for x in np.unique(face_ids) if x >= 0]))
    aabb_count = int(len(aabb))
    summary = {
        "mesh_path": original_mesh_path,
        "mesh_path_original": original_mesh_path,
        "mesh_path_stage1": str(stage1_mesh_path),
        "output_dir": str(output_dir),
        "point_num": int(args.point_num),
        "prompt_num": int(args.prompt_num),
        "prompt_bs": int(args.prompt_bs),
        "part_count": semantic_label_count,
        "semantic_label_count": semantic_label_count,
        "aabb_count": aabb_count,
        "face_count": int(len(mesh.faces)),
        "vertex_count": int(len(mesh.vertices)),
    }
    with open(summary_path, "w", encoding="utf-8") as handle:
        json.dump(summary, handle, indent=2)

    aabb_json = []
    for min_xyz, max_xyz in aabb:
        aabb_json.append(
            {
                "min": [float(v) for v in min_xyz],
                "max": [float(v) for v in max_xyz],
            }
        )
    with open(aabb_json_path, "w", encoding="utf-8") as handle:
        json.dump(aabb_json, handle, indent=2)

    produced_files = sorted(str(path) for path in output_dir.iterdir() if path.is_file())
    produced_files.append(str(manifest_path))
    manifest = {
        "schema_version": 1,
        "stage": "p3sam_stage1_analysis",
        "output_dir": str(output_dir),
        "artifacts": {
            "source_mesh_original": original_mesh_path,
            "source_mesh_stage1": str(stage1_mesh_path),
            "segmented_glb": _path_if_exists(segmented_glb_path),
            "segmented_ply": _path_if_exists(segmented_ply_path),
            "segmented_aabb_glb": _path_if_exists(segmented_aabb_glb_path),
            "segmented_aabb_npy": _path_if_exists(segmented_aabb_npy_path),
            "segmented_face_ids_npy": _path_if_exists(segmented_face_ids_npy_path),
            "aabb_json": str(aabb_json_path),
            "summary_json": str(summary_path),
            "manifest_json": str(manifest_path),
        },
        "settings": {
            "ckpt_path": args.ckpt_path,
            "point_num": int(args.point_num),
            "prompt_num": int(args.prompt_num),
            "prompt_bs": int(args.prompt_bs),
            "threshold": float(args.threshold),
            "post_process": bool(args.post_process),
            "save_mid_res": bool(args.save_mid_res),
            "show_info": bool(args.show_info),
            "show_time_info": bool(args.show_time_info),
            "seed": int(args.seed),
            "parallel": bool(args.parallel),
            "clean_mesh": bool(args.clean_mesh),
        },
        "counts": {
            "part_count": semantic_label_count,
            "semantic_label_count": semantic_label_count,
            "aabb_count": aabb_count,
            "face_count": int(len(mesh.faces)),
            "vertex_count": int(len(mesh.vertices)),
        },
        "produced_files": produced_files,
    }
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)

    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
