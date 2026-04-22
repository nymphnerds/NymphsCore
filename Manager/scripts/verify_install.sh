#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

N2D2_DIR="${NYMPHS3D_N2D2_DIR}"
TRELLIS_DIR="${NYMPHS3D_TRELLIS_DIR}"
SMOKE_TEST_BACKEND=""

usage() {
  cat <<'EOF'
Usage: verify_install.sh [--smoke-test zimage|trellis|all]

Default behavior performs a fast local verification:
- repo presence
- venv presence
- core Python entrypoint compile checks
- critical runtime import checks
- prefetched model availability checks

Optional smoke tests start a local API server and poll /server_info:
- --smoke-test zimage
- --smoke-test trellis
- --smoke-test all
EOF
}

require_file() {
  local path="$1"
  if [[ ! -e "$path" ]]; then
    echo "Missing expected file: $path"
    exit 1
  fi
}

configure_cuda_env() {
  configure_nymphs3d_cuda_env
}

configure_hf_env() {
  configure_nymphs3d_hf_env
}

verify_nymphs2d2() {
  echo "Verifying Z-Image backend..."
  require_file "${N2D2_DIR}/.git"
  require_file "${N2D2_DIR}/.venv-nunchaku/bin/python"
  require_file "${N2D2_DIR}/requirements.lock.txt"
  require_file "${N2D2_DIR}/api_server.py"
  require_file "${N2D2_DIR}/scripts/prefetch_model.py"

  (
    cd "${N2D2_DIR}"
    source .venv-nunchaku/bin/activate
    configure_cuda_env
    configure_hf_env
    export Z_IMAGE_RUNTIME="nunchaku"
    python --version
    python -m py_compile api_server.py config.py image_store.py model_manager.py progress_state.py schemas.py scripts/prefetch_model.py
    python - <<'PY'
import diffusers
import fastapi
import huggingface_hub
import nunchaku
import PIL
import torch

print("Runtime imports available for the Z-Image backend.")
PY
    python scripts/prefetch_model.py --local-files-only
  )
}

verify_trellis() {
  echo "Verifying TRELLIS.2..."
  require_file "${TRELLIS_DIR}/.git"
  require_file "${TRELLIS_DIR}/.venv/bin/python"
  require_file "${TRELLIS_DIR}/scripts/api_server_trellis.py"
  require_file "${TRELLIS_DIR}/scripts/trellis_official_common.py"

  (
    cd "${TRELLIS_DIR}"
    source .venv/bin/activate
    configure_cuda_env
    configure_hf_env
    python --version
    python -m py_compile scripts/api_server_trellis.py scripts/trellis_official_common.py scripts/run_official_image_to_3d.py scripts/run_official_shape_only.py
    python - <<'PY'
import importlib
import json
from pathlib import Path
from huggingface_hub import snapshot_download

for module_name in (
    "cumesh",
    "flex_gemm",
    "nvdiffrast",
    "o_voxel",
    "torch",
    "trimesh",
    "cv2",
    "kornia",
    "timm",
):
    importlib.import_module(module_name)

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

missing = sorted(str(path.relative_to(root)) for path in required if not path.exists())
if missing:
    raise RuntimeError(f"Missing TRELLIS model files: {', '.join(missing[:8])}")

print("Runtime imports available for TRELLIS.2.")
print("Shared-cache TRELLIS model bundle is present.")
PY
    python scripts/api_server_trellis.py --help >/dev/null
  )
}

run_smoke_tests() {
  local target="$1"
  case "${target}" in
    zimage)
      "${ROOT_DIR}/scripts/smoke_test_server.sh" --backend zimage
      ;;
    trellis)
      "${ROOT_DIR}/scripts/smoke_test_server.sh" --backend trellis
      ;;
    all)
      "${ROOT_DIR}/scripts/smoke_test_server.sh" --backend zimage
      "${ROOT_DIR}/scripts/smoke_test_server.sh" --backend trellis
      ;;
    *)
      echo "Unknown smoke test backend: ${target}"
      usage
      exit 1
      ;;
  esac
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --smoke-test)
      shift
      if [[ $# -eq 0 ]]; then
        echo "--smoke-test requires one of: zimage, trellis, all"
        exit 1
      fi
      SMOKE_TEST_BACKEND="$1"
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1"
      usage
      exit 1
      ;;
  esac
  shift
done

echo "Running post-install verification..."

verify_nymphs2d2
verify_trellis
require_file "${ROOT_DIR}/scripts/install_one_click_windows.ps1"

if [[ -n "${SMOKE_TEST_BACKEND}" ]]; then
  echo "Running smoke test: ${SMOKE_TEST_BACKEND}"
  run_smoke_tests "${SMOKE_TEST_BACKEND}"
else
  echo "Smoke test skipped."
  echo "Run ${ROOT_DIR}/scripts/verify_install.sh --smoke-test zimage for a Z-Image API startup check."
  echo "Run ${ROOT_DIR}/scripts/verify_install.sh --smoke-test trellis for a TRELLIS API startup check."
fi

echo "Post-install verification passed."
echo "- All managed repos exist"
echo "- Z-Image Turbo via Nunchaku and TRELLIS.2 venvs are present"
echo "- Core API entrypoints compile"
echo "- Critical runtime imports succeed"
echo "- Required prefetched model snapshots and local TRELLIS bundle are present"
