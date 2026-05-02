#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"
source "${ROOT_DIR}/scripts/managed_repo_utils.sh"

REPO_DIR="${NYMPHS3D_N2D2_DIR}"
REPO_URL="${NYMPHS3D_N2D2_REPO_URL:-https://github.com/nymphnerds/Nymphs2D2.git}"
REPO_BRANCH="${NYMPHS3D_N2D2_REPO_BRANCH}"
DIFFUSERS_SPEC="${NYMPHS3D_Z_IMAGE_DIFFUSERS_SPEC:-diffusers==0.37.1}"
OVERLAY_DIR="${ROOT_DIR}/scripts/zimage_backend_overlay"

configure_nymphs3d_cuda_env
configure_nymphs3d_hf_env

apply_zimage_backend_overlay() {
  if [[ ! -d "${OVERLAY_DIR}" ]]; then
    echo "Expected Z-Image backend overlay directory is missing: ${OVERLAY_DIR}"
    exit 1
  fi

  local overlay_file
  for overlay_file in api_server.py model_manager.py schemas.py; do
    if [[ ! -f "${OVERLAY_DIR}/${overlay_file}" ]]; then
      echo "Expected Z-Image backend overlay file is missing: ${OVERLAY_DIR}/${overlay_file}"
      exit 1
    fi
  done

  echo "Applying managed Z-Image backend overlay"
  cp "${OVERLAY_DIR}/api_server.py" "${REPO_DIR}/api_server.py"
  cp "${OVERLAY_DIR}/model_manager.py" "${REPO_DIR}/model_manager.py"
  cp "${OVERLAY_DIR}/schemas.py" "${REPO_DIR}/schemas.py"
}

if ! command -v python3.11 >/dev/null 2>&1; then
  echo "python3.11 is not installed."
  echo "Run ./scripts/install_system_deps.sh first."
  exit 1
fi

managed_repo_apply "Z-Image backend" "${REPO_DIR}" "${REPO_URL}" "${REPO_BRANCH}"
managed_repo_checkout_ref "Z-Image backend" "${REPO_DIR}" "${NYMPHS3D_N2D2_REPO_REF:-}"

if [[ ! -d "${REPO_DIR}/.git" ]]; then
  echo "Expected repo checkout is still missing at ${REPO_DIR}"
  echo "If the default repo location is not correct yet, override NYMPHS3D_N2D2_REPO_URL and/or NYMPHS3D_N2D2_REPO_BRANCH and rerun."
  exit 1
fi

LOCK_FILE="${REPO_DIR}/requirements.lock.txt"
if [[ ! -f "${LOCK_FILE}" ]]; then
  echo "Missing lock file: ${LOCK_FILE}"
  echo "Trying to repair the existing Z-Image git checkout before cloning from scratch."

  if ! managed_repo_repair_checkout "Z-Image backend" "${REPO_DIR}" "${REPO_URL}" "${REPO_BRANCH}"; then
    echo "In-place Z-Image checkout repair failed."
  fi
fi

if [[ ! -f "${LOCK_FILE}" ]]; then
  echo "Missing lock file after repair attempt: ${LOCK_FILE}"
  echo "Removing the incomplete managed Z-Image checkout and cloning a clean copy."
  managed_repo_remove_path "Z-Image backend" "${REPO_DIR}"
  managed_repo_clone "Z-Image backend" "${REPO_DIR}" "${REPO_URL}" "${REPO_BRANCH}"

  if [[ ! -d "${REPO_DIR}/.git" ]]; then
    echo "Expected repo checkout is still missing at ${REPO_DIR} after retry."
    exit 1
  fi

  if [[ ! -f "${LOCK_FILE}" ]]; then
    echo "Missing lock file after clean clone: ${LOCK_FILE}"
    echo "The Z-Image backend clone completed without the expected installer lockfile."
    exit 1
  fi
fi

cd "${REPO_DIR}"
apply_zimage_backend_overlay

LIVE_VENV_DIR="${REPO_DIR}/.venv-nunchaku"
STAGING_VENV_DIR="${REPO_DIR}/.venv-nunchaku.staging"
BACKUP_VENV_DIR="${REPO_DIR}/.venv-nunchaku.backup"
FILTERED_LOCK_FILE=""
EXPECTED_NUNCHAKU_REPO=""
EXPECTED_NUNCHAKU_COMMIT=""
EXPECTED_DIFFUSERS_VERSION=""

if [[ "${NYMPHS3D_NUNCHAKU_SPEC}" == git+*@* ]]; then
  EXPECTED_NUNCHAKU_REPO="${NYMPHS3D_NUNCHAKU_SPEC#git+}"
  EXPECTED_NUNCHAKU_REPO="${EXPECTED_NUNCHAKU_REPO%@*}"
  EXPECTED_NUNCHAKU_COMMIT="${NYMPHS3D_NUNCHAKU_SPEC##*@}"
fi

if [[ "${DIFFUSERS_SPEC}" == diffusers==* ]]; then
  EXPECTED_DIFFUSERS_VERSION="${DIFFUSERS_SPEC#diffusers==}"
fi

normalize_git_url() {
  local url="${1:-}"
  url="${url%.git}"
  url="${url%/}"
  printf '%s' "${url,,}"
}

live_nunchaku_venv_is_current() {
  local live_python="${LIVE_VENV_DIR}/bin/python"
  [[ -x "${live_python}" ]] || return 1

  local live_version
  live_version="$("${live_python}" -c 'import sys; print(f"{sys.version_info.major}.{sys.version_info.minor}")' 2>/dev/null || true)"
  if [[ "${live_version}" != "3.11" ]]; then
    echo "Existing Nunchaku venv uses Python ${live_version:-unknown}; expected 3.11."
    return 1
  fi

  EXPECTED_NUNCHAKU_REPO="${EXPECTED_NUNCHAKU_REPO}" \
  EXPECTED_NUNCHAKU_COMMIT="${EXPECTED_NUNCHAKU_COMMIT}" \
  EXPECTED_DIFFUSERS_VERSION="${EXPECTED_DIFFUSERS_VERSION}" \
  NORMALIZED_EXPECTED_NUNCHAKU_REPO="$(normalize_git_url "${EXPECTED_NUNCHAKU_REPO}")" \
  "${live_python}" -m py_compile api_server.py model_manager.py nunchaku_compat.py scripts/run_nunchaku_zimage_test.py >/dev/null 2>&1 || return 1

  EXPECTED_NUNCHAKU_REPO="${EXPECTED_NUNCHAKU_REPO}" \
  EXPECTED_NUNCHAKU_COMMIT="${EXPECTED_NUNCHAKU_COMMIT}" \
  EXPECTED_DIFFUSERS_VERSION="${EXPECTED_DIFFUSERS_VERSION}" \
  NORMALIZED_EXPECTED_NUNCHAKU_REPO="$(normalize_git_url "${EXPECTED_NUNCHAKU_REPO}")" \
  "${live_python}" - <<'PY' >/dev/null 2>&1 || return 1
import json
import os
from importlib import metadata

import diffusers
from diffusers.pipelines.z_image.pipeline_z_image import ZImagePipeline
from diffusers.pipelines.z_image.pipeline_z_image_img2img import ZImageImg2ImgPipeline
from nunchaku import NunchakuZImageTransformer2DModel
from nunchaku_compat import patch_zimage_transformer_forward

expected_repo = os.environ.get("NORMALIZED_EXPECTED_NUNCHAKU_REPO", "")
expected_commit = os.environ.get("EXPECTED_NUNCHAKU_COMMIT", "")
expected_diffusers = os.environ.get("EXPECTED_DIFFUSERS_VERSION", "")

if expected_diffusers and diffusers.__version__ != expected_diffusers:
    raise SystemExit(f"diffusers version drift: {diffusers.__version__} != {expected_diffusers}")

dist = metadata.distribution("nunchaku")
direct_url_text = dist.read_text("direct_url.json") or ""
if expected_repo or expected_commit:
    if not direct_url_text:
        raise SystemExit("nunchaku direct_url metadata is missing")
    direct_url = json.loads(direct_url_text)
    actual_url = (direct_url.get("url") or "").lower().removesuffix(".git").rstrip("/")
    actual_commit = ((direct_url.get("vcs_info") or {}).get("commit_id") or "").lower()
    if expected_repo and actual_url != expected_repo:
        raise SystemExit(f"nunchaku repo drift: {actual_url} != {expected_repo}")
    if expected_commit and actual_commit != expected_commit.lower():
        raise SystemExit(f"nunchaku commit drift: {actual_commit} != {expected_commit.lower()}")

patched_forward = patch_zimage_transformer_forward(NunchakuZImageTransformer2DModel)
if not patched_forward:
    raise SystemExit("nunchaku forward patch validation failed")

_ = ZImagePipeline.__name__
_ = ZImageImg2ImgPipeline.__name__
PY
}

if live_nunchaku_venv_is_current; then
  echo "Existing Nunchaku runtime already matches the pinned install. Skipping rebuild."
  echo
  echo "Z-Image Turbo via Nunchaku install complete."
  exit 0
fi

rm -rf "${STAGING_VENV_DIR}" "${BACKUP_VENV_DIR}"

cleanup_on_exit() {
  local exit_code=$?
  if [[ -n "${FILTERED_LOCK_FILE}" ]]; then
    rm -f "${FILTERED_LOCK_FILE}"
  fi
  if [[ "${exit_code}" -ne 0 ]]; then
    rm -rf "${STAGING_VENV_DIR}" "${BACKUP_VENV_DIR}"
  fi
}
trap cleanup_on_exit EXIT

echo "Creating staged Python 3.11 Nunchaku venv"
python3.11 -m venv "${STAGING_VENV_DIR}"

if [[ -x "${STAGING_VENV_DIR}/bin/python3.11" ]]; then
  ln -sf python3.11 "${STAGING_VENV_DIR}/bin/python"
  ln -sf python3.11 "${STAGING_VENV_DIR}/bin/python3"
fi

VENV_PYTHON="${STAGING_VENV_DIR}/bin/python"
VENV_PIP="${STAGING_VENV_DIR}/bin/pip"

if [[ ! -x "${VENV_PYTHON}" || ! -x "${VENV_PIP}" ]]; then
  echo "Python 3.11 Nunchaku venv was created, but the expected executables are missing."
  exit 1
fi

VENV_VERSION="$("${VENV_PYTHON}" -c 'import sys; print(f"{sys.version_info.major}.{sys.version_info.minor}")')"
if [[ "${VENV_VERSION}" != "3.11" ]]; then
  echo "Expected Z-Image Turbo via Nunchaku venv to use Python 3.11, got ${VENV_VERSION}."
  exit 1
fi

"${VENV_PYTHON}" --version
"${VENV_PIP}" install --upgrade pip "setuptools<82" wheel

if ! "${VENV_PYTHON}" -c 'import torch' >/dev/null 2>&1; then
  echo "Installing PyTorch for Z-Image Turbo via Nunchaku runtime"
  "${VENV_PIP}" install torch==2.11.0 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu130
fi

FILTERED_LOCK_FILE="$(mktemp)"
grep -Evi '(diffusers|safetensors)' "${LOCK_FILE}" > "${FILTERED_LOCK_FILE}"

echo "Installing locked Python environment from requirements.lock.txt"
"${VENV_PIP}" install -r "${FILTERED_LOCK_FILE}"

if ! "${VENV_PYTHON}" -c 'import nunchaku' >/dev/null 2>&1; then
  echo "Installing Nunchaku runtime package"
  if [[ -n "${NYMPHS3D_NUNCHAKU_INDEX_URL}" ]]; then
    "${VENV_PIP}" install --no-deps --pre --index-url "${NYMPHS3D_NUNCHAKU_INDEX_URL}" "${NYMPHS3D_NUNCHAKU_SPEC}"
  else
    "${VENV_PIP}" install --no-deps --pre "${NYMPHS3D_NUNCHAKU_SPEC}"
  fi
fi

echo "Installing Z-Image compatibility packages for the Nunchaku runtime path"
"${VENV_PIP}" install httpx importlib_metadata einops "peft>=0.17" protobuf sentencepiece
"${VENV_PIP}" install --no-deps --force-reinstall safetensors==0.7.0
diffusers_attempts=3
diffusers_success=0
for diffusers_attempt in $(seq 1 "${diffusers_attempts}"); do
  if [[ "${diffusers_attempt}" -gt 1 ]]; then
    echo "Retrying diffusers install (${diffusers_attempt}/${diffusers_attempts})..."
    sleep $((5 * diffusers_attempt))
  fi

  if "${VENV_PIP}" install --no-deps --force-reinstall "${DIFFUSERS_SPEC}"; then
    diffusers_success=1
    break
  fi
done

if [[ "${diffusers_success}" -ne 1 ]]; then
  echo "diffusers install failed after ${diffusers_attempts} attempts."
  exit 1
fi

echo "Validating Z-Image Nunchaku imports"
"${VENV_PYTHON}" -m py_compile api_server.py model_manager.py nunchaku_compat.py scripts/run_nunchaku_zimage_test.py
"${VENV_PYTHON}" - <<'PY'
from diffusers.pipelines.z_image.pipeline_z_image import ZImagePipeline
from diffusers.pipelines.z_image.pipeline_z_image_img2img import ZImageImg2ImgPipeline
from nunchaku import NunchakuZImageTransformer2DModel
from nunchaku_compat import patch_zimage_transformer_forward

patched_forward = patch_zimage_transformer_forward(NunchakuZImageTransformer2DModel)
print(f"zimage_pipeline={ZImagePipeline.__name__}")
print(f"zimage_img2img_pipeline={ZImageImg2ImgPipeline.__name__}")
print(f"nunchaku_transformer={NunchakuZImageTransformer2DModel.__name__}")
print(f"zimage_forward_shim={patched_forward}")
PY

echo "Swapping staged Nunchaku venv into place"
if [[ -d "${LIVE_VENV_DIR}" ]]; then
  mv "${LIVE_VENV_DIR}" "${BACKUP_VENV_DIR}"
fi
mv "${STAGING_VENV_DIR}" "${LIVE_VENV_DIR}"

# Python's venv bakes the absolute path of the venv into its activate scripts
# and its pyvenv.cfg. After renaming the staging dir into the live location we
# must rewrite those references so `source .venv-nunchaku/bin/activate` places
# the correct bin directory on PATH (otherwise `python` is not found).
echo "Rewriting staged venv paths to point at ${LIVE_VENV_DIR}"
for activate_file in \
  "${LIVE_VENV_DIR}/bin/activate" \
  "${LIVE_VENV_DIR}/bin/activate.csh" \
  "${LIVE_VENV_DIR}/bin/activate.fish" \
  "${LIVE_VENV_DIR}/bin/Activate.ps1" \
  "${LIVE_VENV_DIR}/pyvenv.cfg"; do
  if [[ -f "${activate_file}" ]]; then
    sed -i "s|${STAGING_VENV_DIR}|${LIVE_VENV_DIR}|g; s|\\.venv-nunchaku\\.staging|.venv-nunchaku|g" "${activate_file}"
  fi
done

rm -rf "${BACKUP_VENV_DIR}"
rm -f "${FILTERED_LOCK_FILE}"
trap - EXIT

echo
echo "Z-Image Turbo via Nunchaku install complete."
