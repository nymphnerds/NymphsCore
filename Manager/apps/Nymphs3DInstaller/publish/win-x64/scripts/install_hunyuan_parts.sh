#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"
source "${ROOT_DIR}/scripts/managed_repo_utils.sh"

REPO_DIR="${NYMPHS3D_PARTS_DIR}"
REPO_URL="${NYMPHS3D_PARTS_REPO_URL}"
REPO_BRANCH="${NYMPHS3D_PARTS_REPO_BRANCH}"
INSTALL_FLASH_ATTN="${NYMPHS3D_PARTS_INSTALL_FLASH_ATTN:-no}"
EXPECTED_FORK_URL="https://github.com/Babyjawz/Hunyuan3D-Part.git"
WRAPPER_SOURCE_DIR="${ROOT_DIR}/scripts/hunyuan_parts_wrappers"

configure_nymphs3d_hf_env

migrate_parts_origin_if_needed() {
  local current_origin=""
  local current_branch=""

  if [[ "${REPO_URL}" != "${EXPECTED_FORK_URL}" ]]; then
    return 0
  fi

  if [[ ! -d "${REPO_DIR}/.git" ]]; then
    return 0
  fi

  current_origin="$(git -C "${REPO_DIR}" remote get-url origin 2>/dev/null || true)"
  case "${current_origin}" in
    https://github.com/Tencent-Hunyuan/Hunyuan3D-Part|https://github.com/Tencent-Hunyuan/Hunyuan3D-Part.git)
      ;;
    *)
      return 0
      ;;
  esac

  current_branch="$(git -C "${REPO_DIR}" symbolic-ref --quiet --short HEAD 2>/dev/null || true)"
  if [[ -z "${current_branch}" ]]; then
    echo "Existing Hunyuan Parts checkout is detached. Leaving origin as ${current_origin}."
    return 0
  fi

  if [[ "${current_branch}" != "${REPO_BRANCH}" ]]; then
    echo "Existing Hunyuan Parts checkout is on ${current_branch}, expected ${REPO_BRANCH}. Leaving origin as ${current_origin}."
    return 0
  fi

  if managed_repo_is_effectively_dirty "${REPO_DIR}"; then
    echo "Existing Hunyuan Parts checkout has local changes. Leaving origin as ${current_origin}."
    return 0
  fi

  echo "Switching Hunyuan Parts origin from ${current_origin} to ${REPO_URL} so the installer uses the Nymphs3D-compatible fork."
  git -C "${REPO_DIR}" remote set-url origin "${REPO_URL}"
}

if ! command -v python3.10 >/dev/null 2>&1; then
  echo "python3.10 is not installed."
  echo "Run ./scripts/install_system_deps.sh first."
  exit 1
fi

migrate_parts_origin_if_needed
managed_repo_apply "Hunyuan Parts" "${REPO_DIR}" "${REPO_URL}" "${REPO_BRANCH}"

if [[ ! -d "${REPO_DIR}/.git" ]]; then
  echo "Expected repo checkout is still missing at ${REPO_DIR}"
  exit 1
fi

install_parts_wrapper_scripts() {
  local wrapper_name=""
  if [[ ! -d "${WRAPPER_SOURCE_DIR}" ]]; then
    echo "Missing Hunyuan Parts wrapper source directory: ${WRAPPER_SOURCE_DIR}"
    exit 1
  fi

  mkdir -p "${REPO_DIR}/scripts"
  for wrapper_name in run_p3sam_segment.py run_xpart_generate.py; do
    if [[ ! -f "${WRAPPER_SOURCE_DIR}/${wrapper_name}" ]]; then
      echo "Missing Hunyuan Parts wrapper source: ${WRAPPER_SOURCE_DIR}/${wrapper_name}"
      exit 1
    fi
    install -m 0644 "${WRAPPER_SOURCE_DIR}/${wrapper_name}" "${REPO_DIR}/scripts/${wrapper_name}"
  done
}

cd "${REPO_DIR}"
install_parts_wrapper_scripts

if [[ -L ".venv-official" && ! -e ".venv-official" ]]; then
  echo "Existing Hunyuan Parts venv symlink is broken. Recreating it."
  rm -f .venv-official
fi

if [[ -e ".venv-official" && ! -x ".venv-official/bin/python" ]]; then
  echo "Existing Hunyuan Parts venv is incomplete. Recreating it."
  rm -rf .venv-official
fi

if [[ ! -d ".venv-official" ]]; then
  echo "Creating dedicated Python 3.10 Hunyuan Parts venv"
  python3.10 -m venv .venv-official
fi

VENV_PYTHON="${REPO_DIR}/.venv-official/bin/python"
VENV_PIP="${REPO_DIR}/.venv-official/bin/pip"

if [[ ! -x "${VENV_PYTHON}" || ! -x "${VENV_PIP}" ]]; then
  echo "Hunyuan Parts venv was created, but the expected executables are missing."
  exit 1
fi

"${VENV_PYTHON}" --version
"${VENV_PIP}" install --upgrade pip setuptools wheel ninja

if ! "${VENV_PYTHON}" -c 'import torch, torchvision, torchaudio' >/dev/null 2>&1; then
  echo "Installing PyTorch for Hunyuan Parts experimental runtime"
  "${VENV_PIP}" install torch==2.4.0 torchvision==0.19.0 torchaudio==2.4.0 --index-url https://download.pytorch.org/whl/cu124
fi

echo "Installing Hunyuan Parts experimental Python dependencies"
"${VENV_PIP}" install \
  addict \
  diffusers \
  easydict \
  einops \
  fpsample \
  huggingface_hub \
  imageio \
  imageio-ffmpeg \
  networkx \
  numba \
  numpy \
  omegaconf \
  opencv-python-headless \
  packaging \
  pymeshlab==2023.12.post3 \
  pytorch-lightning \
  safetensors \
  scikit-image \
  scikit-learn \
  scipy \
  timm \
  trimesh

"${VENV_PIP}" install spconv-cu124

if ! "${VENV_PYTHON}" -c 'import torch_scatter' >/dev/null 2>&1; then
  echo "Installing torch_scatter for Hunyuan Parts experimental runtime"
  "${VENV_PIP}" install "https://data.pyg.org/whl/torch-2.4.0%2Bcu124/torch_scatter-2.1.2%2Bpt24cu124-cp310-cp310-linux_x86_64.whl"
fi

if ! "${VENV_PYTHON}" -c 'import torch_cluster' >/dev/null 2>&1; then
  echo "Installing torch_cluster for Hunyuan Parts experimental runtime"
  "${VENV_PIP}" install "https://data.pyg.org/whl/torch-2.4.0%2Bcu124/torch_cluster-1.6.3%2Bpt24cu124-cp310-cp310-linux_x86_64.whl"
fi

if [[ -f "${REPO_DIR}/XPart/requirements.txt" ]]; then
  "${VENV_PIP}" install -r "${REPO_DIR}/XPart/requirements.txt"
fi

if [[ "${INSTALL_FLASH_ATTN}" == "yes" ]]; then
  "${VENV_PIP}" install --no-build-isolation flash-attn
fi

echo "Running Hunyuan Parts wrapper sanity checks"
"${VENV_PYTHON}" "${REPO_DIR}/scripts/run_p3sam_segment.py" --help >/dev/null
"${VENV_PYTHON}" "${REPO_DIR}/scripts/run_xpart_generate.py" --help >/dev/null

echo
echo "Hunyuan Parts experimental install complete."
