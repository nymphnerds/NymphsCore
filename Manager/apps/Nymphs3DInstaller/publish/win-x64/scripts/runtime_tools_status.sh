#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

N2D2_DIR="${NYMPHS3D_N2D2_DIR}"
TRELLIS_DIR="${NYMPHS3D_TRELLIS_DIR}"

configure_nymphs3d_hf_env

sanitize_detail() {
  local raw="$1"
  printf '%s' "${raw//$'\n'/ }" | tr '|' '/'
}

summarize_probe_detail() {
  local backend="$1"
  local raw="$2"

  if [[ "${backend}" == "zimage" ]] && [[ "${raw}" == *"LocalEntryNotFoundError"* || "${raw}" == *"Cannot find the requested files in the disk cache"* ]]; then
    echo "Z-Image Nunchaku weights are missing from the local cache. Click Fetch to download them, then Refresh."
    return
  fi

  if [[ "${backend}" == "trellis" ]] && [[ "${raw}" == *"missing TRELLIS model files"* ]]; then
    echo "TRELLIS.2 model files are missing from the local cache. Click Fetch to download them, then Refresh."
    return
  fi

  if [[ -z "${raw}" ]]; then
    echo "Required models are missing. Click Fetch before testing."
    return
  fi

  printf '%s' "${raw}" | head -c 220
}

emit_status() {
  local backend="$1"
  local label="$2"
  local env_ready="$3"
  local models_ready="$4"
  local test_ready="$5"
  local detail="$6"

  printf 'backend=%s|label=%s|env_ready=%s|models_ready=%s|test_ready=%s|detail=%s\n' \
    "${backend}" \
    "${label}" \
    "${env_ready}" \
    "${models_ready}" \
    "${test_ready}" \
    "$(sanitize_detail "${detail}")"
}

probe_zimage_models() {
  (
    cd "${N2D2_DIR}"
    source .venv-nunchaku/bin/activate
    configure_nymphs3d_hf_env
    export Z_IMAGE_RUNTIME="nunchaku"
    export Z_IMAGE_NUNCHAKU_IMG2IMG="1"
    python -m py_compile api_server.py model_manager.py nunchaku_compat.py scripts/run_nunchaku_zimage_test.py
    python - <<'PY'
import os

from diffusers.pipelines.z_image.pipeline_z_image import ZImagePipeline
from diffusers.pipelines.z_image.pipeline_z_image_img2img import ZImageImg2ImgPipeline
from nunchaku import NunchakuZImageTransformer2DModel
from nunchaku_compat import patch_zimage_transformer_forward
from config import get_settings
from model_manager import ModelManager

patched_forward = patch_zimage_transformer_forward(NunchakuZImageTransformer2DModel)
os.environ["Z_IMAGE_NUNCHAKU_IMG2IMG"] = "1"
manager = ModelManager(get_settings())
modes = manager.supported_modes()
if "img2img" not in modes:
    raise RuntimeError(f"Z-Image img2img mode is not advertised by the Nunchaku runtime: {modes}")
print(f"zimage_pipeline_ready={ZImagePipeline.__name__}")
print(f"zimage_img2img_pipeline_ready={ZImageImg2ImgPipeline.__name__}")
print(f"nunchaku_transformer_ready={NunchakuZImageTransformer2DModel.__name__}")
print(f"zimage_forward_shim_ready={patched_forward}")
print(f"supported_modes={','.join(modes)}")
PY
    python scripts/prefetch_model.py --local-files-only
    python - <<'PY'
import os
from huggingface_hub import hf_hub_download

repo_id = os.getenv("Z_IMAGE_NUNCHAKU_MODEL_REPO") or "nunchaku-ai/nunchaku-z-image-turbo"
rank = os.getenv("Z_IMAGE_NUNCHAKU_RANK") or os.getenv("NYMPHS2D2_NUNCHAKU_RANK") or "32"
precision = (os.getenv("Z_IMAGE_NUNCHAKU_PRECISION") or os.getenv("NYMPHS2D2_NUNCHAKU_PRECISION") or "auto").strip().lower()
precisions = ["int4", "fp4"] if precision == "auto" else [precision]
cache_dir = os.getenv("NYMPHS3D_HF_CACHE_DIR") or None
token = os.getenv("NYMPHS3D_HF_TOKEN") or None

for item in precisions:
    filename = f"svdq-{item}_r{rank}-z-image-turbo.safetensors"
    path = hf_hub_download(
        repo_id=repo_id,
        filename=filename,
        cache_dir=cache_dir,
        token=token,
        local_files_only=True,
    )
    print(f"nunchaku_weight_ready={path}")
PY
  )
}

probe_trellis_models() {
  (
    cd "${TRELLIS_DIR}"
    source .venv/bin/activate
    configure_nymphs3d_hf_env
    python - <<'PY' >/dev/null 2>&1
import json
from pathlib import Path
from huggingface_hub import snapshot_download

try:
    root = Path(snapshot_download(repo_id="microsoft/TRELLIS.2-4B", local_files_only=True))
except Exception:
    root = Path("models/trellis2")
    if not root.exists():
        raise

required = {root / "pipeline.json", root / "texturing_pipeline.json"}
for config_name in ("pipeline.json", "texturing_pipeline.json"):
    config = json.loads((root / config_name).read_text())
    args = config.get("args", {})
    for model_ref in (args.get("models") or {}).values():
        if isinstance(model_ref, str) and model_ref.startswith("ckpts/"):
            required.add(root / f"{model_ref}.json")
            required.add(root / f"{model_ref}.safetensors")

missing = [path for path in required if not path.exists()]
if missing:
    raise RuntimeError("missing TRELLIS model files")
PY
  )
}

check_zimage() {
  if [[ ! -d "${N2D2_DIR}/.git" ]]; then
    emit_status "zimage" "Z-Image" "no" "no" "no" "Repo is missing from the managed runtime."
    return
  fi

  if [[ ! -x "${N2D2_DIR}/.venv-nunchaku/bin/python" ]]; then
    emit_status "zimage" "Z-Image" "no" "no" "no" "Runtime environment is missing. Run repair or install again."
    return
  fi

  local probe_detail
  if probe_detail="$(probe_zimage_models 2>&1)"; then
    emit_status "zimage" "Z-Image" "yes" "yes" "yes" "All components present. Ready for smoke test."
  else
    emit_status "zimage" "Z-Image" "yes" "no" "no" "$(summarize_probe_detail "zimage" "${probe_detail}")"
  fi
}

check_trellis() {
  if [[ ! -d "${TRELLIS_DIR}/.git" ]]; then
    emit_status "trellis" "TRELLIS.2" "no" "no" "no" "Repo is missing from the managed runtime."
    return
  fi

  if [[ ! -x "${TRELLIS_DIR}/.venv/bin/python" ]]; then
    emit_status "trellis" "TRELLIS.2" "no" "no" "no" "Runtime environment is missing. Run repair or install again."
    return
  fi

  if [[ ! -f "${TRELLIS_DIR}/scripts/api_server_trellis.py" ]]; then
    emit_status "trellis" "TRELLIS.2" "no" "no" "no" "Managed TRELLIS adapter scripts are missing. Run repair or update the manager package."
    return
  fi

  if probe_trellis_models; then
    emit_status "trellis" "TRELLIS.2" "yes" "yes" "yes" "All components present. Ready for smoke test."
  else
    emit_status "trellis" "TRELLIS.2" "yes" "no" "no" "Runtime env is ready, but required models are missing. Fetch models before testing."
  fi
}

echo "Checking managed runtime tool status..."
check_zimage
check_trellis
