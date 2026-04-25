import argparse
import base64
import gc
import io
import json
import os
import random
import tempfile
import threading
import time
import traceback
from dataclasses import asdict, dataclass
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any

import torch
import trimesh
from PIL import Image
import o_voxel

from trellis_gguf_common import (
    DEFAULT_GGUF_QUANT,
    GGUF_MODEL_REPO_ID,
    ensure_trellis2_gguf_ready,
    prepare_dinov3_dir,
    preferred_attention_backend,
    resolve_gguf_model_root,
    resolve_gguf_quant,
)


@dataclass
class TaskState:
    status: str = "idle"
    stage: str = ""
    detail: str = ""
    progress_current: int | None = None
    progress_total: int | None = None
    progress_percent: float | None = None
    message: str = ""


TASK_LOCK = threading.Lock()
TASK_STATE = TaskState()
INFERENCE_LOCK = threading.Lock()
PIPELINE_CACHE: dict[str, Any] = {"key": None, "pipeline": None, "model_root": None}
SERVER_ARGS = None


def set_task(
    *,
    status: str,
    stage: str = "",
    detail: str = "",
    progress_current: int | None = None,
    progress_total: int | None = None,
    progress_percent: float | None = None,
    message: str = "",
) -> None:
    with TASK_LOCK:
        TASK_STATE.status = status
        TASK_STATE.stage = stage
        TASK_STATE.detail = detail
        TASK_STATE.progress_current = progress_current
        TASK_STATE.progress_total = progress_total
        TASK_STATE.progress_percent = progress_percent
        TASK_STATE.message = message


def task_snapshot() -> dict[str, Any]:
    with TASK_LOCK:
        return asdict(TASK_STATE)


def decode_base64_blob(raw: str) -> bytes:
    value = (raw or "").strip()
    if not value:
        raise RuntimeError("Missing base64 payload.")
    if "," in value and value.split(",", 1)[0].startswith("data:"):
        value = value.split(",", 1)[1]
    return base64.b64decode(value)


def decode_image(raw: str) -> Image.Image:
    image = Image.open(io.BytesIO(decode_base64_blob(raw)))
    image.load()
    return image


def read_file_bytes(path: Path) -> bytes:
    with open(path, "rb") as handle:
        return handle.read()


def write_temp_suffix(suffix: str, data: bytes) -> Path:
    with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as handle:
        handle.write(data)
        return Path(handle.name)


def optional_int(payload: dict[str, Any], key: str, default: int) -> int:
    value = payload.get(key, default)
    if value in {"", None}:
        return default
    return int(value)


def optional_float(payload: dict[str, Any], key: str, default: float) -> float:
    value = payload.get(key, default)
    if value in {"", None}:
        return default
    return float(value)


def optional_bool(payload: dict[str, Any], key: str, default: bool) -> bool:
    value = payload.get(key, default)
    if value in {"", None}:
        return default
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return bool(value)
    return str(value).strip().lower() not in {"0", "false", "no", "off"}


def resolve_seed(payload: dict[str, Any]) -> int:
    seed = optional_int(payload, "seed", -1)
    return random.randint(1, 2**31 - 1) if seed <= 0 else seed


def sampler_params(payload: dict[str, Any], prefix: str, *, steps_key: str | None = None, default_steps: int = 25) -> dict[str, Any]:
    params: dict[str, Any] = {
        "steps": optional_int(payload, steps_key or f"{prefix}sampling_steps", default_steps),
    }
    mapping: tuple[tuple[str, str, Any], ...] = (
        ("guidance_strength", "guidance_strength", float),
        ("guidance_rescale", "guidance_rescale", float),
        ("rescale_t", "rescale_t", float),
    )
    for suffix, target, caster in mapping:
        key = f"{prefix}{suffix}"
        if key in payload and payload[key] not in {"", None}:
            params[target] = caster(payload[key])
    interval_start = payload.get(f"{prefix}guidance_interval_start")
    interval_end = payload.get(f"{prefix}guidance_interval_end")
    if interval_start not in {"", None} and interval_end not in {"", None}:
        params["guidance_interval"] = [float(interval_start), float(interval_end)]
    return params


def preprocess_image(image: Image.Image, *, fg_ratio: float, remove_background: bool, force_cpu: bool = False) -> Image.Image:
    image = image.convert("RGBA")
    if remove_background:
        try:
            import rembg

            if force_cpu:
                session = rembg.new_session(providers=["CPUExecutionProvider"])
            else:
                session = rembg.new_session()
            image = rembg.remove(image, session=session)
        except Exception as exc:
            print(f"[trellis-gguf-api] Background removal skipped: {exc}")

    bg = Image.new("RGBA", image.size, (255, 255, 255, 255))
    bg.paste(image, mask=image.split()[3])
    image = bg.convert("RGB")
    return resize_foreground(image, fg_ratio)


def resize_foreground(image: Image.Image, ratio: float) -> Image.Image:
    import numpy as np

    arr = np.array(image)
    mask = ~np.all(arr >= 250, axis=-1)
    if not mask.any():
        return image

    rows = np.any(mask, axis=1)
    cols = np.any(mask, axis=0)
    rmin, rmax = np.where(rows)[0][[0, -1]]
    cmin, cmax = np.where(cols)[0][[0, -1]]
    fg = image.crop((cmin, rmin, cmax + 1, rmax + 1))
    fw, fh = fg.size
    iw, ih = image.size
    scale = ratio * min(iw, ih) / max(fw, fh)
    nw = max(1, int(fw * scale))
    nh = max(1, int(fh * scale))
    fg = fg.resize((nw, nh), Image.LANCZOS)

    result = Image.new("RGB", (iw, ih), (255, 255, 255))
    result.paste(fg, ((iw - nw) // 2, (ih - nh) // 2))
    return result


def clear_cached_pipeline() -> None:
    pipeline = PIPELINE_CACHE.get("pipeline")
    PIPELINE_CACHE["key"] = None
    PIPELINE_CACHE["pipeline"] = None
    PIPELINE_CACHE["model_root"] = None
    if pipeline is not None:
        del pipeline
    gc.collect()
    if torch.cuda.is_available():
        torch.cuda.empty_cache()


def get_pipeline(quant: str, *, include_texture: bool):
    model_root = resolve_gguf_model_root(local_files_only=False, quant=quant, include_texture=include_texture)
    key = (str(model_root), quant)
    if PIPELINE_CACHE["key"] == key and PIPELINE_CACHE["pipeline"] is not None:
        return PIPELINE_CACHE["pipeline"]

    clear_cached_pipeline()
    ensure_trellis2_gguf_ready(model_root)
    from trellis2_gguf.pipelines import Trellis2ImageTo3DPipeline

    set_task(
        status="processing",
        stage="loading_shape_pipeline",
        detail=f"Loading TRELLIS.2 GGUF pipeline ({quant})...",
        progress_current=2,
        progress_total=5,
        progress_percent=35.0,
    )
    pipeline = Trellis2ImageTo3DPipeline.from_pretrained(
        str(model_root),
        keep_models_loaded=False,
        enable_gguf=True,
        gguf_quant=quant,
        precision="bf16",
    )

    local_dino = prepare_dinov3_dir(model_root)
    if local_dino and hasattr(pipeline, "_pretrained_args"):
        args = pipeline._pretrained_args.get("image_cond_model", {}).get("args", {})
        args["model_name"] = local_dino

    device = "cuda" if torch.cuda.is_available() else "cpu"
    pipeline._device = device
    PIPELINE_CACHE["key"] = key
    PIPELINE_CACHE["pipeline"] = pipeline
    PIPELINE_CACHE["model_root"] = str(model_root)
    return pipeline


def remove_floor_like_components(mesh: trimesh.Trimesh, *, enabled: bool) -> trimesh.Trimesh:
    env_value = os.environ.get("TRELLIS_GGUF_REMOVE_FLOOR_PLANE")
    if env_value is not None:
        enabled = str(env_value).strip().lower() not in {"0", "false", "no", "off"}
    if not enabled:
        return mesh
    if mesh.faces is None or len(mesh.faces) == 0:
        return mesh

    bounds = mesh.bounds
    scene_extent = bounds[1] - bounds[0]
    scene_height = float(scene_extent[2])
    if scene_height <= 0:
        return mesh

    try:
        components = mesh.split(only_watertight=False)
    except Exception:
        return mesh
    if len(components) <= 1:
        return mesh

    floor_z = float(bounds[0][2])
    z_tol = max(scene_height * 0.035, 0.01)
    min_span_xy = max(float(scene_extent[0]), float(scene_extent[1])) * 0.55
    min_area = max(float(mesh.area) * 0.05, 0.01)
    kept = []
    removed = 0

    for component in components:
        cb = component.bounds
        ce = cb[1] - cb[0]
        near_floor = float(cb[0][2]) <= floor_z + z_tol
        flat = float(ce[2]) <= max(scene_height * 0.08, 0.02)
        wide = max(float(ce[0]), float(ce[1])) >= min_span_xy
        large = float(component.area) >= min_area
        if near_floor and flat and wide and large:
            removed += 1
            continue
        kept.append(component)

    if not removed or not kept:
        return mesh
    print(f"[trellis-gguf-api] removed {removed} floor-like mesh component(s)")
    return trimesh.util.concatenate(kept)


def export_geometry(mesh_with_voxel, remesh_resolution: int, *, remove_floor_plane: bool) -> bytes:
    import numpy as np

    verts = mesh_with_voxel.vertices
    faces = mesh_with_voxel.faces
    if hasattr(verts, "cpu"):
        verts = verts.cpu().numpy()
    if hasattr(faces, "cpu"):
        faces = faces.cpu().numpy()
    verts = np.asarray(verts, dtype=np.float32)
    faces = np.asarray(faces, dtype=np.int32)

    try:
        import cumesh
        from cumesh import CuMesh

        vt = torch.from_numpy(verts).float().cuda().contiguous()
        ft = torch.from_numpy(faces).int().cuda().contiguous()
        cm = CuMesh()
        cm.init(vt, ft)
        try:
            torch.cuda.empty_cache()
            bvh = cumesh.cuBVH(vt, ft)
            aabb_min = vt.min(dim=0).values
            aabb_max = vt.max(dim=0).values
            center = (aabb_min + aabb_max) / 2.0
            scale = (aabb_max - aabb_min).max().item()
            band = 2 if remesh_resolution >= 768 else 1
            remesh_scale = (remesh_resolution + 3 * band) / remesh_resolution * scale
            cm.init(
                *cumesh.remeshing.remesh_narrow_band_dc(
                    vt,
                    ft,
                    center=center,
                    scale=remesh_scale,
                    resolution=remesh_resolution,
                    band=band,
                    project_back=0.9,
                    bvh=bvh,
                )
            )
        except Exception as exc:
            print(f"[trellis-gguf-api] cumesh remesh unavailable ({exc}); using cleanup fallback")
            cm.fill_holes(max_hole_perimeter=0.1)
            cm.remove_duplicate_faces()
            cm.repair_non_manifold_edges()
            cm.remove_small_connected_components(1e-5)
        cm.unify_face_orientations()
        verts, faces = cm.read()
        verts = verts.cpu().numpy().astype(np.float32)
        faces = faces.cpu().numpy().astype(np.int32)
    except Exception as exc:
        print(f"[trellis-gguf-api] cumesh cleanup skipped ({exc})")

    x = verts[:, 0].copy()
    y = verts[:, 1].copy()
    z = verts[:, 2].copy()
    verts[:, 0] = -x
    verts[:, 1] = z
    verts[:, 2] = y

    mesh = trimesh.Trimesh(vertices=verts, faces=faces, process=True)
    mesh = remove_floor_like_components(mesh, enabled=remove_floor_plane)
    mesh.visual = trimesh.visual.TextureVisuals(material=trimesh.visual.material.PBRMaterial(doubleSided=True))
    with tempfile.NamedTemporaryFile(delete=False, suffix=".glb") as handle:
        temp_path = Path(handle.name)
    try:
        mesh.export(str(temp_path))
        return read_file_bytes(temp_path)
    finally:
        temp_path.unlink(missing_ok=True)


def export_textured_geometry(mesh_with_voxel, texture_size: int, decimation_target: int) -> bytes:
    with tempfile.NamedTemporaryFile(delete=False, suffix=".glb") as handle:
        temp_path = Path(handle.name)
    try:
        mesh_with_voxel.simplify(16_777_216)
        glb = o_voxel.postprocess.to_glb(
            vertices=mesh_with_voxel.vertices,
            faces=mesh_with_voxel.faces,
            attr_volume=mesh_with_voxel.attrs,
            coords=mesh_with_voxel.coords,
            attr_layout=mesh_with_voxel.layout,
            voxel_size=mesh_with_voxel.voxel_size,
            aabb=[[-0.5, -0.5, -0.5], [0.5, 0.5, 0.5]],
            decimation_target=decimation_target,
            texture_size=texture_size,
            remesh=True,
            remesh_band=1,
            remesh_project=0,
            verbose=True,
        )
        glb.export(str(temp_path), extension_webp=True)
        return read_file_bytes(temp_path)
    finally:
        temp_path.unlink(missing_ok=True)


def run_shape_request(payload: dict[str, Any]) -> bytes:
    quant = resolve_gguf_quant(payload.get("gguf_quant"))
    pipeline_type = str(payload.get("pipeline_type") or "1024_cascade").strip()
    texture_requested = bool(payload.get("texture", False))
    image = decode_image(payload.get("image", ""))
    fg_ratio = optional_float(payload, "foreground_ratio", 0.85)
    image = preprocess_image(
        image,
        fg_ratio=fg_ratio,
        remove_background=bool(payload.get("remove_background", True)),
    )
    pipeline = get_pipeline(quant, include_texture=texture_requested)
    seed = resolve_seed(payload)
    max_num_tokens = optional_int(payload, "max_num_tokens", 150000)

    set_task(
        status="processing",
        stage="sampling_shape",
        detail=f"Running TRELLIS.2 GGUF {pipeline_type} ({quant})...",
        progress_current=3,
        progress_total=5,
        progress_percent=60.0,
    )
    with torch.no_grad():
        if texture_requested:
            out_mesh = pipeline.run(
                image=image,
                seed=seed,
                pipeline_type=pipeline_type,
                max_num_tokens=max_num_tokens,
                sparse_structure_sampler_params=sampler_params(payload, "ss_", default_steps=25),
                shape_slat_sampler_params=sampler_params(payload, "shape_", default_steps=25),
                tex_slat_sampler_params=sampler_params(payload, "tex_", default_steps=12),
                generate_texture_slat=True,
            )[0]
            set_task(status="processing", stage="exporting_textured_mesh", detail="Exporting textured mesh...", progress_current=4, progress_total=5, progress_percent=85.0)
            return export_textured_geometry(
                out_mesh,
                texture_size=optional_int(payload, "texture_size", 2048),
                decimation_target=optional_int(payload, "decimation_target", 500000),
            )

        mesh_with_voxel = pipeline.run(
            image=image,
            seed=seed,
            pipeline_type=pipeline_type,
            max_num_tokens=max_num_tokens,
            sparse_structure_sampler_params=sampler_params(payload, "ss_", default_steps=25),
            shape_slat_sampler_params=sampler_params(payload, "shape_", default_steps=25),
            generate_texture_slat=False,
        )[0]

    set_task(status="processing", stage="exporting_mesh", detail="Exporting mesh...", progress_current=4, progress_total=5, progress_percent=85.0)
    return export_geometry(
        mesh_with_voxel,
        optional_int(payload, "remesh_resolution", 768),
        remove_floor_plane=optional_bool(payload, "remove_floor_plane", True),
    )


def run_retexture_request(payload: dict[str, Any]) -> bytes:
    quant = resolve_gguf_quant(payload.get("gguf_quant"))
    pipeline = get_pipeline(quant, include_texture=True)
    mesh_path = write_temp_suffix(f".{str(payload.get('mesh_format') or 'glb').strip().lower()}", decode_base64_blob(payload.get("mesh", "")))
    out_path: Path | None = None
    try:
        mesh = trimesh.load(str(mesh_path), force="mesh")
        image = decode_image(payload.get("image", ""))
        image = preprocess_image(
            image,
            fg_ratio=optional_float(payload, "foreground_ratio", 0.85),
            remove_background=bool(payload.get("remove_background", True)),
            force_cpu=True,
        )
        set_task(status="processing", stage="generating_texture", detail=f"Running TRELLIS.2 GGUF texture ({quant})...", progress_current=3, progress_total=4, progress_percent=75.0)
        with torch.no_grad():
            out_mesh, _, _ = pipeline.texture_mesh(
                mesh=mesh,
                image=image,
                seed=resolve_seed(payload),
                tex_slat_sampler_params=sampler_params(payload, "tex_", default_steps=12),
                resolution=optional_int(payload, "texture_resolution", 1024),
                texture_size=optional_int(payload, "texture_size", 2048),
            )
        with tempfile.NamedTemporaryFile(delete=False, suffix=".glb") as handle:
            out_path = Path(handle.name)
        out_mesh.export(str(out_path))
        return read_file_bytes(out_path)
    finally:
        mesh_path.unlink(missing_ok=True)
        if out_path is not None:
            out_path.unlink(missing_ok=True)


def server_info() -> dict[str, Any]:
    quant = resolve_gguf_quant(getattr(SERVER_ARGS, "gguf_quant", None))
    model_root = ""
    model_ready = False
    detail = ""
    try:
        root = resolve_gguf_model_root(local_files_only=True, quant=quant, include_texture=True)
        model_root = str(root)
        model_ready = (root / "pipeline.json").exists()
    except Exception as exc:
        detail = str(exc)

    return {
        "status": "ready",
        "backend": "TRELLIS.2-GGUF",
        "model_path": GGUF_MODEL_REPO_ID,
        "resolved_model_path": model_root,
        "subfolder": f"gguf/{quant}",
        "enable_tex": True,
        "mesh_retexture": True,
        "mesh_retexture_detail": "",
        "enable_t23d": False,
        "texture_only": False,
        "gguf_quant": quant,
        "attention_backend": os.environ.get("ATTN_BACKEND") or preferred_attention_backend(),
        "sparse_attention_backend": os.environ.get("SPARSE_ATTN_BACKEND") or os.environ.get("ATTN_BACKEND") or preferred_attention_backend(),
        "model_ready": model_ready,
        "model_detail": detail,
        "python_path": str(getattr(SERVER_ARGS, "python_path", "")),
    }


class Handler(BaseHTTPRequestHandler):
    server_version = "NymphsTrellisGGUF/0.1"

    def log_message(self, format: str, *args) -> None:
        print(f"[trellis-gguf-api] {self.address_string()} - {format % args}")

    def send_json(self, status: int, payload: dict[str, Any]) -> None:
        body = json.dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def send_glb(self, payload: bytes) -> None:
        self.send_response(200)
        self.send_header("Content-Type", "model/gltf-binary")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def read_json(self) -> dict[str, Any]:
        content_length = int(self.headers.get("Content-Length", "0") or 0)
        if content_length <= 0:
            return {}
        return json.loads(self.rfile.read(content_length).decode("utf-8"))

    def do_GET(self) -> None:
        if self.path == "/server_info":
            self.send_json(200, server_info())
            return
        if self.path == "/active_task":
            self.send_json(200, task_snapshot())
            return
        self.send_json(404, {"detail": "Not found"})

    def do_POST(self) -> None:
        if self.path != "/generate":
            self.send_json(404, {"detail": "Not found"})
            return
        if not INFERENCE_LOCK.acquire(blocking=False):
            self.send_json(409, {"detail": "TRELLIS.2 GGUF is already processing another request."})
            return
        try:
            payload = self.read_json()
            set_task(status="processing", stage="request_received", detail="Request received.", progress_current=1, progress_total=5, progress_percent=20.0)
            mesh_bytes = run_retexture_request(payload) if payload.get("mesh") else run_shape_request(payload)
            set_task(status="completed", stage="job_complete", detail="Completed", progress_percent=100.0)
            self.send_glb(mesh_bytes)
        except Exception as exc:
            traceback.print_exc()
            set_task(status="failed", stage="failed", detail=str(exc), message=str(exc))
            self.send_json(500, {"detail": str(exc)})
        finally:
            INFERENCE_LOCK.release()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Local TRELLIS.2 GGUF adapter server for Nymphs")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8095)
    parser.add_argument("--python-path", default=os.sys.executable)
    parser.add_argument("--gguf-quant", default=DEFAULT_GGUF_QUANT)
    return parser.parse_args()


def main() -> None:
    global SERVER_ARGS
    SERVER_ARGS = parse_args()
    resolve_gguf_quant(SERVER_ARGS.gguf_quant)
    set_task(status="idle")
    print(f"[trellis-gguf-api] backend=TRELLIS.2-GGUF quant={SERVER_ARGS.gguf_quant}")
    print(f"[trellis-gguf-api] attention={os.environ.get('ATTN_BACKEND') or preferred_attention_backend()}")
    print(f"[trellis-gguf-api] listening on http://{SERVER_ARGS.host}:{SERVER_ARGS.port}")
    server = ThreadingHTTPServer((SERVER_ARGS.host, SERVER_ARGS.port), Handler)
    server.serve_forever()


if __name__ == "__main__":
    main()
