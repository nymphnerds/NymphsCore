import argparse
import base64
import gc
import io
import json
import os
import sys
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

from run_official_image_to_3d import OfficialImageTo3DPipeline, export_glb
from run_official_shape_only import OfficialShapeOnlyPipeline, export_mesh
from trellis_official_common import (
    MODEL_REFERENCE,
    REPO_ROOT,
    patch_public_model_ids,
    resolve_local_model_root,
    resolve_model_reference,
)
from trellis2.pipelines import Trellis2TexturingPipeline


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
PIPELINE_CACHE: dict[str, Any] = {"kind": None, "key": None, "pipeline": None}
PATCHED = False
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


def ensure_patches() -> None:
    global PATCHED
    if PATCHED:
        return
    patch_public_model_ids()
    PATCHED = True


def resolve_texturing_config_path(model_root: Path) -> Path | None:
    config_path = model_root / "texturing_pipeline.json"
    if config_path.exists():
        return config_path
    return None


def retexture_status(model_root: Path | None = None) -> tuple[bool, str]:
    model_root = model_root or resolve_local_model_root()
    config_path = resolve_texturing_config_path(model_root)
    if config_path is None:
        return (
            False,
            "Local TRELLIS mesh retexturing is unavailable: missing texturing_pipeline.json in the model bundle.",
        )

    try:
        args = json.loads(config_path.read_text()).get("args", {})
    except Exception as exc:
        return (False, f"Local TRELLIS mesh retexturing is unavailable: could not read {config_path.name} ({exc}).")

    missing: list[str] = []
    for model_ref in (args.get("models") or {}).values():
        if not isinstance(model_ref, str) or not model_ref.startswith("ckpts/"):
            continue
        prefix = model_root / model_ref
        config_file = prefix.with_suffix(".json")
        weights_file = prefix.with_suffix(".safetensors")
        if not config_file.exists():
            missing.append(str(config_file.relative_to(model_root)))
        if not weights_file.exists():
            missing.append(str(weights_file.relative_to(model_root)))

    if missing:
        preview = ", ".join(missing[:4])
        if len(missing) > 4:
            preview += ", ..."
        return (
            False,
            f"Local TRELLIS mesh retexturing is unavailable: missing {preview}.",
        )

    return (True, "")


def clear_cached_pipeline() -> None:
    pipeline = PIPELINE_CACHE.get("pipeline")
    PIPELINE_CACHE["kind"] = None
    PIPELINE_CACHE["key"] = None
    PIPELINE_CACHE["pipeline"] = None
    if pipeline is not None:
        del pipeline
    gc.collect()
    if torch.cuda.is_available():
        torch.cuda.empty_cache()


def get_shape_pipeline(pipeline_type: str, *, with_texture: bool):
    kind = "full" if with_texture else "shape"
    key = (kind, pipeline_type)
    if PIPELINE_CACHE["key"] == key and PIPELINE_CACHE["pipeline"] is not None:
        return PIPELINE_CACHE["pipeline"]

    clear_cached_pipeline()
    model_ref = resolve_model_reference()
    if with_texture:
        OfficialImageTo3DPipeline.configure_for(pipeline_type)
        pipeline = OfficialImageTo3DPipeline.from_pretrained(model_ref)
    else:
        OfficialShapeOnlyPipeline.configure_for(pipeline_type)
        pipeline = OfficialShapeOnlyPipeline.from_pretrained(model_ref)
    pipeline.cuda()
    PIPELINE_CACHE["kind"] = kind
    PIPELINE_CACHE["key"] = key
    PIPELINE_CACHE["pipeline"] = pipeline
    return pipeline


def get_texturing_pipeline():
    key = ("retexture", "texturing_pipeline")
    if PIPELINE_CACHE["key"] == key and PIPELINE_CACHE["pipeline"] is not None:
        return PIPELINE_CACHE["pipeline"]

    clear_cached_pipeline()
    model_ref = resolve_model_reference()
    model_root = resolve_local_model_root(local_files_only=True)
    ready, detail = retexture_status(model_root)
    if not ready:
        raise RuntimeError(detail)
    pipeline = Trellis2TexturingPipeline.from_pretrained(
        model_ref,
        config_file="texturing_pipeline.json",
    )
    pipeline.cuda()
    PIPELINE_CACHE["kind"] = "retexture"
    PIPELINE_CACHE["key"] = key
    PIPELINE_CACHE["pipeline"] = pipeline
    return pipeline


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


def write_temp_suffix(suffix: str, data: bytes) -> Path:
    with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as handle:
        handle.write(data)
        return Path(handle.name)


def load_mesh(mesh_b64: str, mesh_format: str) -> trimesh.Trimesh:
    suffix = f".{(mesh_format or 'glb').strip().lower()}"
    temp_path = write_temp_suffix(suffix, decode_base64_blob(mesh_b64))
    try:
        mesh = trimesh.load(str(temp_path))
        if isinstance(mesh, trimesh.Scene):
            mesh = mesh.to_mesh()
        if not isinstance(mesh, trimesh.Trimesh):
            raise RuntimeError("Uploaded mesh could not be converted to a trimesh.Trimesh.")
        return mesh
    finally:
        temp_path.unlink(missing_ok=True)


def read_file_bytes(path: Path) -> bytes:
    with open(path, "rb") as handle:
        return handle.read()


def export_trimesh_glb(mesh: trimesh.Trimesh) -> bytes:
    with tempfile.NamedTemporaryFile(delete=False, suffix=".glb") as handle:
        temp_path = Path(handle.name)
    try:
        mesh.export(str(temp_path), extension_webp=True)
        return read_file_bytes(temp_path)
    finally:
        temp_path.unlink(missing_ok=True)


def export_shape_glb(mesh) -> bytes:
    with tempfile.NamedTemporaryFile(delete=False, suffix=".glb") as handle:
        temp_path = Path(handle.name)
    try:
        export_mesh(mesh, temp_path)
        return read_file_bytes(temp_path)
    finally:
        temp_path.unlink(missing_ok=True)


def export_textured_mesh(mesh, texture_size: int, decimation_target: int) -> bytes:
    with tempfile.NamedTemporaryFile(delete=False, suffix=".glb") as handle:
        temp_path = Path(handle.name)
    try:
        export_glb(mesh, temp_path, texture_size=texture_size, decimation_target=decimation_target)
        return read_file_bytes(temp_path)
    finally:
        temp_path.unlink(missing_ok=True)


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


def sampler_params(payload: dict[str, Any], prefix: str) -> dict[str, Any]:
    params: dict[str, Any] = {}
    mapping: tuple[tuple[str, str, Any], ...] = (
        ("sampling_steps", "steps", int),
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


def image_from_payload(payload: dict[str, Any], *, allow_front_fallback: bool = False) -> Image.Image:
    if payload.get("image"):
        return decode_image(payload["image"])
    if allow_front_fallback and payload.get("mv_image_front"):
        return decode_image(payload["mv_image_front"])
    raise RuntimeError("TRELLIS currently needs a single guidance image.")


def run_shape_request(payload: dict[str, Any]) -> bytes:
    pipeline_type = str(payload.get("pipeline_type") or "512").strip()
    if pipeline_type not in {"512", "1024", "1024_cascade", "1536_cascade"}:
        raise RuntimeError(f"Unsupported TRELLIS pipeline type: {pipeline_type}")
    texture_requested = bool(payload.get("texture", False))
    preprocess_image = bool(payload.get("remove_background", True))
    seed = optional_int(payload, "seed", 42)
    max_num_tokens = optional_int(payload, "max_num_tokens", 49152)
    texture_size = optional_int(payload, "texture_size", 2048)
    decimation_target = optional_int(payload, "decimation_target", 500000)
    image = image_from_payload(payload, allow_front_fallback=False)

    if texture_requested:
        set_task(
            status="processing",
            stage="loading_shape_pipeline",
            detail=f"Loading TRELLIS {pipeline_type} image-to-3D pipeline...",
            progress_current=2,
            progress_total=5,
            progress_percent=40.0,
        )
        pipeline = get_shape_pipeline(pipeline_type, with_texture=True)
        set_task(
            status="processing",
            stage="sampling_shape",
            detail=f"Running TRELLIS {pipeline_type} shape + texture generation...",
            progress_current=3,
            progress_total=5,
            progress_percent=60.0,
        )
        meshes = pipeline.run(
            image=image,
            num_samples=1,
            seed=seed,
            sparse_structure_sampler_params=sampler_params(payload, "ss_"),
            shape_slat_sampler_params=sampler_params(payload, "shape_"),
            tex_slat_sampler_params=sampler_params(payload, "tex_"),
            preprocess_image=preprocess_image,
            return_latent=False,
            pipeline_type=pipeline_type,
            max_num_tokens=max_num_tokens,
        )
        set_task(
            status="processing",
            stage="exporting_textured_mesh",
            detail="Exporting textured mesh...",
            progress_current=4,
            progress_total=5,
            progress_percent=80.0,
        )
        return export_textured_mesh(meshes[0], texture_size=texture_size, decimation_target=decimation_target)

    set_task(
        status="processing",
        stage="loading_shape_pipeline",
        detail=f"Loading TRELLIS {pipeline_type} shape-only pipeline...",
        progress_current=2,
        progress_total=4,
        progress_percent=50.0,
    )
    pipeline = get_shape_pipeline(pipeline_type, with_texture=False)
    set_task(
        status="processing",
        stage="sampling_shape",
        detail=f"Running TRELLIS {pipeline_type} shape generation...",
        progress_current=3,
        progress_total=4,
        progress_percent=75.0,
    )
    _preprocessed, meshes, _subs, _resolution = pipeline.run_shape_only(
        image=image,
        pipeline_type=pipeline_type,
        seed=seed,
        sparse_structure_sampler_params=sampler_params(payload, "ss_"),
        shape_slat_sampler_params=sampler_params(payload, "shape_"),
        max_num_tokens=max_num_tokens,
        preprocess_image=preprocess_image,
    )
    set_task(
        status="processing",
        stage="exporting_mesh",
        detail="Exporting shape mesh...",
        progress_current=4,
        progress_total=4,
        progress_percent=100.0,
    )
    return export_shape_glb(meshes[0])


def run_retexture_request(payload: dict[str, Any]) -> bytes:
    ready, detail = retexture_status()
    if not ready:
        raise RuntimeError(detail)
    resolution = optional_int(payload, "texture_resolution", 1024)
    if resolution not in {512, 1024, 1536}:
        raise RuntimeError(f"Unsupported TRELLIS texture resolution: {resolution}")
    texture_size = optional_int(payload, "texture_size", 2048)
    seed = optional_int(payload, "seed", 42)
    preprocess_image = bool(payload.get("remove_background", True))

    set_task(
        status="processing",
        stage="loading_input_mesh",
        detail="Loading uploaded mesh...",
        progress_current=1,
        progress_total=4,
        progress_percent=25.0,
    )
    mesh = load_mesh(payload.get("mesh", ""), str(payload.get("mesh_format") or "glb"))
    image = image_from_payload(payload, allow_front_fallback=True)
    set_task(
        status="processing",
        stage="loading_texture_pipeline",
        detail="Loading TRELLIS texturing pipeline...",
        progress_current=2,
        progress_total=4,
        progress_percent=50.0,
    )
    pipeline = get_texturing_pipeline()
    set_task(
        status="processing",
        stage="generating_texture",
        detail=f"Running TRELLIS mesh texturing at {resolution}...",
        progress_current=3,
        progress_total=4,
        progress_percent=75.0,
    )
    textured_mesh = pipeline.run(
        mesh,
        image,
        seed=seed,
        tex_slat_sampler_params=sampler_params(payload, "tex_"),
        preprocess_image=preprocess_image,
        resolution=resolution,
        texture_size=texture_size,
    )
    set_task(
        status="processing",
        stage="exporting_textured_mesh",
        detail="Exporting textured mesh...",
        progress_current=4,
        progress_total=4,
        progress_percent=100.0,
    )
    return export_trimesh_glb(textured_mesh)


class Handler(BaseHTTPRequestHandler):
    server_version = "NymphsTrellis/0.1"

    def log_message(self, format: str, *args) -> None:
        print(f"[trellis-api] {self.address_string()} - {format % args}")

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
        raw = self.rfile.read(content_length)
        try:
            return json.loads(raw.decode("utf-8"))
        except Exception as exc:
            raise RuntimeError("Request body was not valid JSON.") from exc

    def do_GET(self) -> None:
        if self.path == "/server_info":
            model_path = resolve_model_reference()
            model_root = resolve_local_model_root(local_files_only=True)
            mesh_retexture_ready, mesh_retexture_detail = retexture_status(model_root)
            payload = {
                "status": "ready",
                "backend": "TRELLIS.2",
                "model_path": model_path,
                "resolved_model_path": str(model_root),
                "subfolder": "official",
                "enable_tex": True,
                "mesh_retexture": mesh_retexture_ready,
                "mesh_retexture_detail": mesh_retexture_detail,
                "enable_t23d": False,
                "texture_only": False,
                "repo_path": str(REPO_ROOT),
                "python_path": str(SERVER_ARGS.python_path),
            }
            self.send_json(200, payload)
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
            self.send_json(409, {"detail": "TRELLIS is already processing another request."})
            return

        try:
            payload = self.read_json()
            set_task(
                status="processing",
                stage="request_received",
                detail="Request received.",
                progress_current=1,
                progress_total=5 if not payload.get("mesh") else 4,
                progress_percent=20.0 if not payload.get("mesh") else 25.0,
            )
            if payload.get("mesh"):
                mesh_bytes = run_retexture_request(payload)
            else:
                mesh_bytes = run_shape_request(payload)
            set_task(
                status="completed",
                stage="job_complete",
                detail="Completed",
                progress_current=None,
                progress_total=None,
                progress_percent=100.0,
            )
            self.send_glb(mesh_bytes)
        except Exception as exc:
            traceback.print_exc()
            set_task(
                status="failed",
                stage="failed",
                detail=str(exc),
                progress_current=None,
                progress_total=None,
                progress_percent=None,
                message=str(exc),
            )
            self.send_json(500, {"detail": str(exc)})
        finally:
            INFERENCE_LOCK.release()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Local TRELLIS.2 adapter server for Nymphs")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8094)
    parser.add_argument(
        "--python-path",
        default=sys.executable,
        help="Interpreter path reported in /server_info for debugging.",
    )
    return parser.parse_args()


def main() -> None:
    global SERVER_ARGS
    SERVER_ARGS = parse_args()
    ensure_patches()
    model_root = resolve_local_model_root()
    print(f"[trellis-api] repo={REPO_ROOT}")
    print(f"[trellis-api] model_root={model_root}")
    print(f"[trellis-api] python={SERVER_ARGS.python_path}")
    set_task(status="idle", stage="", detail="", progress_current=None, progress_total=None, progress_percent=None)
    server = ThreadingHTTPServer((SERVER_ARGS.host, SERVER_ARGS.port), Handler)
    print(f"[trellis-api] listening on http://{SERVER_ARGS.host}:{SERVER_ARGS.port}")
    server.serve_forever()


if __name__ == "__main__":
    main()
