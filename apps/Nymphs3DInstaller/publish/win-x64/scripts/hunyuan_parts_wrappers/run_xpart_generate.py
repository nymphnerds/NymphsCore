#!/usr/bin/env python3
import argparse
import json
import os
import random
import sys
import threading
import time
from pathlib import Path

import numpy as np
import pytorch_lightning as pl
import torch


REPO_ROOT = Path(__file__).resolve().parents[1]
XPART_ROOT = REPO_ROOT / "XPart"
P3SAM_ROOT = REPO_ROOT / "P3-SAM"
P3SAM_DEMO_DIR = P3SAM_ROOT / "demo"
if str(XPART_ROOT) not in sys.path:
    sys.path.insert(0, str(XPART_ROOT))
if str(P3SAM_ROOT) not in sys.path:
    sys.path.insert(0, str(P3SAM_ROOT))
if str(P3SAM_DEMO_DIR) not in sys.path:
    sys.path.insert(0, str(P3SAM_DEMO_DIR))

from partgen.partformer_pipeline import PartFormerPipeline  # noqa: E402


def set_seed(seed: int) -> None:
    random.seed(seed)
    np.random.seed(seed)
    torch.manual_seed(seed)
    if torch.cuda.is_available():
        torch.cuda.manual_seed(seed)
        torch.cuda.manual_seed_all(seed)
    pl.seed_everything(seed, workers=True)


def _parts_cache_root() -> Path:
    raw = os.environ.get("NYMPHS3D_PARTS_CACHE_ROOT", "~/.cache/hunyuan3d-part")
    return Path(os.path.expanduser(raw)).resolve()


def _ensure_xpart_model_root() -> None:
    # X-Part's smart_load_model resolves repo IDs under HY3DGEN_MODELS.
    # Keep it under the same parts cache root unless the caller already set it.
    os.environ.setdefault("HY3DGEN_MODELS", str(_parts_cache_root() / "models"))


def log_phase(message: str) -> None:
    print(f"[X-Part] {message}", flush=True)


def log_diag(message: str) -> None:
    print(f"[X-PartDiag] {message}", flush=True)


def parse_dtype(name: str) -> torch.dtype:
    normalized = (name or "float32").strip().lower()
    if normalized in {"float32", "fp32"}:
        return torch.float32
    if normalized in {"bfloat16", "bf16"}:
        return torch.bfloat16
    if normalized in {"float16", "fp16", "half"}:
        return torch.float16
    raise ValueError(f"Unsupported dtype: {name}")


def load_aabb_json(path: Path) -> np.ndarray:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, list) or not data:
        raise ValueError("AABB JSON must be a non-empty list.")

    boxes = []
    for idx, item in enumerate(data):
        if not isinstance(item, dict) or "min" not in item or "max" not in item:
            raise ValueError(f"AABB entry {idx} must contain 'min' and 'max'.")
        min_xyz = np.asarray(item["min"], dtype=np.float32)
        max_xyz = np.asarray(item["max"], dtype=np.float32)
        if min_xyz.shape != (3,) or max_xyz.shape != (3,):
            raise ValueError(f"AABB entry {idx} must contain 3D min/max coordinates.")
        boxes.append([min_xyz, max_xyz])
    return np.asarray(boxes, dtype=np.float32)


def load_stage1_manifest(path: Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError("Stage-1 manifest must be a JSON object.")
    if data.get("stage") != "p3sam_stage1_analysis":
        raise ValueError("Stage-1 manifest has an unexpected stage value.")
    artifacts = data.get("artifacts")
    if not isinstance(artifacts, dict):
        raise ValueError("Stage-1 manifest must contain an artifacts object.")
    return data


def resolve_stage1_inputs(args) -> tuple[Path, Path, Path | None]:
    manifest_path = None
    if args.stage1_manifest:
        manifest_path = Path(args.stage1_manifest).resolve()
        if not manifest_path.exists():
            raise FileNotFoundError(f"Stage-1 manifest not found: {manifest_path}")
        manifest = load_stage1_manifest(manifest_path)
        artifacts = manifest["artifacts"]
        mesh_raw = artifacts.get("source_mesh_stage1") or ""
        aabb_raw = artifacts.get("aabb_json") or ""
        if not mesh_raw:
            raise ValueError("Stage-1 manifest is missing artifacts.source_mesh_stage1.")
        if not aabb_raw:
            raise ValueError("Stage-1 manifest is missing artifacts.aabb_json.")
        mesh_path = Path(mesh_raw).resolve()
        aabb_json_path = Path(aabb_raw).resolve()
    else:
        if not args.mesh_path or not args.aabb_json:
            raise ValueError("Provide either --stage1_manifest or both --mesh_path and --aabb_json.")
        mesh_path = Path(args.mesh_path).resolve()
        aabb_json_path = Path(args.aabb_json).resolve()

    if not mesh_path.exists():
        raise FileNotFoundError(f"Mesh path not found: {mesh_path}")
    if not aabb_json_path.exists():
        raise FileNotFoundError(f"AABB JSON not found: {aabb_json_path}")
    return mesh_path, aabb_json_path, manifest_path


def configure_cpu_threads(cpu_threads: int) -> None:
    if cpu_threads <= 0:
        return
    torch.set_num_threads(cpu_threads)
    try:
        torch.set_num_interop_threads(cpu_threads)
    except RuntimeError:
        pass


def _target_cuda_device_index(device_name: str) -> int:
    target = torch.device(device_name)
    if target.type != "cuda":
        return -1
    if target.index is not None:
        return int(target.index)
    try:
        return int(torch.cuda.current_device())
    except Exception:
        return 0


def _safe_torch_cuda_summary(device_name: str) -> str:
    pieces = [
        f"python={sys.executable}",
        f"torch_file={getattr(torch, '__file__', '<unknown>')}",
        f"torch_version={getattr(torch, '__version__', '<unknown>')}",
        f"torch_cuda_build={getattr(torch.version, 'cuda', None)}",
        f"venv={os.environ.get('VIRTUAL_ENV', '') or '<unset>'}",
        f"cuda_visible_devices={os.environ.get('CUDA_VISIBLE_DEVICES', '') or '<unset>'}",
        f"pythonpath={os.environ.get('PYTHONPATH', '') or '<unset>'}",
        f"hy3dgen_models={os.environ.get('HY3DGEN_MODELS', '') or '<unset>'}",
        f"device_request={device_name}",
    ]
    try:
        cuda_available = torch.cuda.is_available()
        device_count = torch.cuda.device_count()
        pieces.append(f"cuda_available={cuda_available}")
        pieces.append(f"device_count={device_count}")
        if cuda_available and device_count > 0:
            idx = _target_cuda_device_index(device_name)
            if 0 <= idx < device_count:
                pieces.append(f"resolved_device=cuda:{idx}")
                try:
                    pieces.append(f"device_name={torch.cuda.get_device_name(idx)}")
                except Exception as exc:
                    pieces.append(f"device_name_error={exc}")
                try:
                    free_bytes, total_bytes = torch.cuda.mem_get_info(idx)
                    pieces.append(
                        f"mem_free_mib={int(free_bytes // (1024 * 1024))}"
                    )
                    pieces.append(
                        f"mem_total_mib={int(total_bytes // (1024 * 1024))}"
                    )
                except Exception as exc:
                    pieces.append(f"mem_info_error={exc}")
    except Exception as exc:
        pieces.append(f"cuda_query_error={exc}")
    return " | ".join(str(part) for part in pieces)


def log_runtime_diagnostics(device_name: str, phase_name: str) -> None:
    log_diag(f"{phase_name} env: {_safe_torch_cuda_summary(device_name)}")


def log_cuda_smoke(device_name: str, phase_name: str) -> None:
    target = torch.device(device_name)
    if target.type != "cuda":
        log_diag(f"{phase_name} cuda_smoke: skipped for non-cuda device {device_name}")
        return
    try:
        if not torch.cuda.is_available():
            log_diag(f"{phase_name} cuda_smoke: unavailable")
            return
        device_index = _target_cuda_device_index(device_name)
        label = f"cuda:{device_index}"
        a = torch.randn((64, 64), device=label, dtype=torch.float32)
        b = torch.randn((64, 64), device=label, dtype=torch.float32)
        c = a @ b
        checksum = float(c.sum().item())
        torch.cuda.synchronize(device_index)
        del a
        del b
        del c
        log_diag(
            f"{phase_name} cuda_smoke: ok device={label} checksum={checksum:.4f}"
        )
    except Exception as exc:
        log_diag(f"{phase_name} cuda_smoke: failed error={exc}")


def ensure_cuda_ready(device_name: str, phase_name: str) -> None:
    target = torch.device(device_name)
    if target.type != "cuda":
        return
    if not torch.cuda.is_available():
        raise RuntimeError(
            f"CUDA preflight failed during {phase_name}: torch.cuda.is_available() is False. "
            "Restart WSL or reboot Windows before retrying X-Part."
        )

    device_count = int(torch.cuda.device_count())
    device_index = _target_cuda_device_index(device_name)
    if device_count <= 0:
        raise RuntimeError(
            f"CUDA preflight failed during {phase_name}: no CUDA devices are visible. "
            "Restart WSL or reboot Windows before retrying X-Part."
        )
    if device_index < 0 or device_index >= device_count:
        raise RuntimeError(
            f"CUDA preflight failed during {phase_name}: requested device {device_name} but only {device_count} CUDA device(s) are visible."
        )

    device_label = f"cuda:{device_index}"
    try:
        device_name_readable = torch.cuda.get_device_name(device_index)
    except Exception:
        device_name_readable = device_label

    try:
        probe = torch.empty((1,), device=device_label)
        probe.fill_(1)
        torch.cuda.synchronize(device_index)
        del probe
    except Exception as exc:
        raise RuntimeError(
            f"CUDA preflight failed during {phase_name} on {device_name_readable}: {exc}. "
            "Restart WSL or reboot Windows before retrying X-Part."
        ) from exc

    log_phase(f"CUDA ready on {device_name_readable} for {phase_name}.")


def format_runtime_failure(exc: Exception, device_name: str, phase_name: str) -> RuntimeError:
    detail = str(exc).strip() or exc.__class__.__name__
    lowered = detail.lower()
    if "device not ready" in lowered:
        return RuntimeError(
            f"CUDA device became unavailable during {phase_name} on {device_name}. "
            "This usually means the WSL GPU context is unhealthy or a prior run wedged the device. "
            f"Original error: {detail}"
        )
    if "no nvidia driver" in lowered or "cuda driver error" in lowered:
        return RuntimeError(
            f"CUDA runtime failed during {phase_name} on {device_name}. "
            "Restart WSL or reboot Windows before retrying X-Part. "
            f"Original error: {detail}"
        )
    return RuntimeError(f"X-Part failed during {phase_name}: {detail}")


def start_heartbeat(interval_seconds: int = 10):
    stop_event = threading.Event()
    started_at = time.time()

    def _worker():
        while not stop_event.wait(interval_seconds):
            elapsed = int(time.time() - started_at)
            log_phase(f"Still working... {elapsed}s elapsed")

    thread = threading.Thread(target=_worker, daemon=True)
    thread.start()
    return stop_event


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Run X-Part generation from a canonical mesh and saved Stage-1 AABBs."
    )
    parser.add_argument("--stage1_manifest", default="", help="Optional Stage-1 manifest path from run_p3sam_segment.py")
    parser.add_argument("--mesh_path", default="", help="Canonical Stage-1 mesh path")
    parser.add_argument("--aabb_json", default="", help="Stage-1 p3sam_aabb.json path")
    parser.add_argument("--output_dir", required=True, help="Directory to write outputs into")
    parser.add_argument("--seed", type=int, default=42, help="Random seed")
    parser.add_argument("--num_inference_steps", type=int, default=50, help="X-Part diffusion step count")
    parser.add_argument("--octree_resolution", type=int, default=512, help="Marching cubes/octree resolution")
    parser.add_argument("--mc_level", type=float, default=(-1.0 / 512.0), help="Marching-cubes level")
    parser.add_argument("--device", default="cuda", help="Target device, usually cuda")
    parser.add_argument("--dtype", default="float32", help="Model dtype: float32, bfloat16, or float16")
    parser.add_argument("--max_aabb", type=int, default=0, help="Optional cap on how many Stage-1 boxes to use")
    parser.add_argument(
        "--export_num_chunks",
        type=int,
        default=20000,
        help="Implicit-function export chunk size. Lower values are slower but reduce CUDA pressure.",
    )
    parser.add_argument("--cpu_threads", type=int, default=0, help="Optional CPU thread cap for Torch/BLAS-heavy phases")
    parser.add_argument("--progress_interval", type=int, default=10, help="Heartbeat interval in seconds during long phases")
    args = parser.parse_args()

    os.environ.setdefault("PYTORCH_CUDA_ALLOC_CONF", "expandable_segments:True")
    output_dir = Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    mesh_path, aabb_json_path, manifest_path = resolve_stage1_inputs(args)
    if args.octree_resolution < 256:
        raise ValueError("X-Part export requires --octree_resolution >= 256.")

    _ensure_xpart_model_root()
    set_seed(args.seed)
    configure_cpu_threads(args.cpu_threads)
    log_runtime_diagnostics(args.device, "startup")
    log_cuda_smoke(args.device, "startup")
    ensure_cuda_ready(args.device, "startup")

    log_phase("Resolving Stage-1 analysis inputs...")
    aabb = load_aabb_json(aabb_json_path)
    if args.max_aabb and args.max_aabb > 0:
        aabb = aabb[: int(args.max_aabb)]
    dtype = parse_dtype(args.dtype)
    log_phase(f"Stage-1 mesh ready. Using {len(aabb)} bounding boxes.")
    if args.cpu_threads > 0:
        log_phase(f"CPU thread cap set to {args.cpu_threads}.")
    heartbeat = start_heartbeat(max(1, int(args.progress_interval)))
    try:
        log_phase("Loading X-Part model...")
        pipeline = PartFormerPipeline.from_pretrained(
            model_path="tencent/Hunyuan3D-Part",
            verbose=True,
        )
        log_runtime_diagnostics(args.device, "pre-model-transfer")
        log_cuda_smoke(args.device, "pre-model-transfer")
        ensure_cuda_ready(args.device, "model transfer")
        log_phase("Moving X-Part model to target device...")
        try:
            pipeline.to(device=args.device, dtype=dtype)
        except Exception as exc:
            raise format_runtime_failure(exc, args.device, "model transfer") from exc
        log_phase("Model loaded. Starting X-Part pipeline...")
        log_phase(
            "X-Part settings: "
            f"dtype={str(dtype).replace('torch.', '')} "
            f"octree_resolution={int(args.octree_resolution)} "
            f"export_num_chunks={int(args.export_num_chunks)} "
            f"staged_conditioner={int(bool(getattr(pipeline, '_staged_conditioner_cuda', False)))}"
        )
        log_runtime_diagnostics(args.device, "pre-generation")
        log_cuda_smoke(args.device, "pre-generation")

        try:
            obj_mesh, extras = pipeline(
                mesh_path=str(mesh_path),
                aabb=aabb,
                num_inference_steps=args.num_inference_steps,
                octree_resolution=args.octree_resolution,
                mc_level=args.mc_level,
                num_chunks=args.export_num_chunks,
                output_type="trimesh",
                seed=args.seed,
            )
            if len(getattr(obj_mesh, "geometry", {})) == 0:
                raise RuntimeError(
                    "X-Part returned no exportable meshes; see earlier export errors in the log."
                )
        except Exception as exc:
            raise format_runtime_failure(exc, args.device, "generation") from exc
        log_phase("Pipeline returned. Exporting part meshes...")
    finally:
        heartbeat.set()

    parts_path = output_dir / "xpart_parts.glb"
    obj_mesh.export(parts_path)

    out_bbox_path = None
    input_bbox_path = None
    explode_path = None
    if extras is not None:
        out_bbox, mesh_gt_bbox, explode_object = extras
        out_bbox_path = output_dir / "xpart_parts_bbox.glb"
        input_bbox_path = output_dir / "xpart_input_bbox.glb"
        explode_path = output_dir / "xpart_explode.glb"
        out_bbox.export(out_bbox_path)
        mesh_gt_bbox.export(input_bbox_path)
        explode_object.export(explode_path)
    log_phase("Export complete.")

    summary = {
        "mesh_path": str(mesh_path),
        "aabb_json": str(aabb_json_path),
        "stage1_manifest": str(manifest_path) if manifest_path else "",
        "output_dir": str(output_dir),
        "seed": int(args.seed),
        "num_inference_steps": int(args.num_inference_steps),
        "octree_resolution": int(args.octree_resolution),
        "mc_level": float(args.mc_level),
        "export_num_chunks": int(args.export_num_chunks),
        "dtype": str(dtype).replace("torch.", ""),
        "aabb_count": int(len(aabb)),
        "parts_output_path": str(parts_path),
        "parts_bbox_output_path": str(out_bbox_path) if out_bbox_path else "",
        "input_bbox_output_path": str(input_bbox_path) if input_bbox_path else "",
        "explode_output_path": str(explode_path) if explode_path else "",
    }
    with open(output_dir / "summary.json", "w", encoding="utf-8") as handle:
        json.dump(summary, handle, indent=2)

    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
