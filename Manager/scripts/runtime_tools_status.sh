#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

H2_DIR="${NYMPHS3D_H2_DIR}"
N2D2_DIR="${NYMPHS3D_N2D2_DIR}"
TRELLIS_DIR="${NYMPHS3D_TRELLIS_DIR}"

configure_nymphs3d_hf_env

sanitize_detail() {
  local raw="$1"
  printf '%s' "${raw//$'\n'/ }" | tr '|' '/'
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

probe_hunyuan_models() {
  (
    cd "${H2_DIR}"
    source .venv/bin/activate
    configure_nymphs3d_hf_env
    python - <<'PY' >/dev/null 2>&1
from huggingface_hub import snapshot_download

specs = [
    {
        "repo_id": "tencent/Hunyuan3D-2mv",
        "allow_patterns": [
            "hunyuan3d-dit-v2-mv/*",
            "hunyuan3d-dit-v2-mv-turbo/*",
        ],
    },
    {
        "repo_id": "tencent/Hunyuan3D-2",
        "allow_patterns": [
            "hunyuan3d-vae-v2-0/*",
            "hunyuan3d-vae-v2-0-turbo/*",
            "hunyuan3d-delight-v2-0/*",
            "hunyuan3d-paint-v2-0-turbo/*",
        ],
    },
]

for spec in specs:
    snapshot_download(
        repo_id=spec["repo_id"],
        allow_patterns=spec["allow_patterns"],
        local_files_only=True,
    )
PY
  )
}

probe_zimage_models() {
  (
    cd "${N2D2_DIR}"
    source .venv-nunchaku/bin/activate
    configure_nymphs3d_hf_env
    export Z_IMAGE_RUNTIME="nunchaku"
    python scripts/prefetch_model.py --local-files-only >/dev/null 2>&1
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

check_hunyuan() {
  if [[ ! -d "${H2_DIR}/.git" ]]; then
    emit_status "2mv" "Hunyuan 2mv" "no" "no" "no" "Repo is missing from the managed runtime."
    return
  fi

  if [[ ! -x "${H2_DIR}/.venv/bin/python" ]]; then
    emit_status "2mv" "Hunyuan 2mv" "no" "no" "no" "Runtime environment is missing. Run repair or install again."
    return
  fi

  if probe_hunyuan_models; then
    emit_status "2mv" "Hunyuan 2mv" "yes" "yes" "yes" "All components present. Ready for smoke test."
  else
    emit_status "2mv" "Hunyuan 2mv" "yes" "no" "no" "Runtime env is ready, but required models are missing. Fetch models before testing."
  fi
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

  if probe_zimage_models; then
    emit_status "zimage" "Z-Image" "yes" "yes" "yes" "All components present. Ready for smoke test."
  else
    emit_status "zimage" "Z-Image" "yes" "no" "no" "Runtime env is ready, but required models are missing. Fetch models before testing."
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
check_hunyuan
check_zimage
check_trellis
