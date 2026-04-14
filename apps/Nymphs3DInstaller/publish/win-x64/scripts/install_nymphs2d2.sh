#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"
source "${ROOT_DIR}/scripts/managed_repo_utils.sh"

REPO_DIR="${NYMPHS3D_N2D2_DIR}"
REPO_URL="${NYMPHS3D_N2D2_REPO_URL:-https://github.com/Babyjawz/Nymphs2D2.git}"
REPO_BRANCH="${NYMPHS3D_N2D2_REPO_BRANCH}"

configure_nymphs3d_cuda_env
configure_nymphs3d_hf_env

if ! command -v python3.11 >/dev/null 2>&1; then
  echo "python3.11 is not installed."
  echo "Run ./scripts/install_system_deps.sh first."
  exit 1
fi

managed_repo_apply "Z-Image backend" "${REPO_DIR}" "${REPO_URL}" "${REPO_BRANCH}"

if [[ ! -d "${REPO_DIR}/.git" ]]; then
  echo "Expected repo checkout is still missing at ${REPO_DIR}"
  echo "If the default repo location is not correct yet, override NYMPHS3D_N2D2_REPO_URL and/or NYMPHS3D_N2D2_REPO_BRANCH and rerun."
  exit 1
fi

cd "${REPO_DIR}"

if [[ -d ".venv-nunchaku" && ! -x ".venv-nunchaku/bin/python" ]]; then
  echo "Existing Nunchaku venv is incomplete. Recreating it."
  rm -rf .venv-nunchaku
fi

if [[ ! -d ".venv-nunchaku" ]]; then
  echo "Creating Python 3.11 Nunchaku venv"
  python3.11 -m venv .venv-nunchaku
fi

if [[ -x ".venv-nunchaku/bin/python3.11" ]]; then
  ln -sf python3.11 .venv-nunchaku/bin/python
  ln -sf python3.11 .venv-nunchaku/bin/python3
fi

VENV_PYTHON="${REPO_DIR}/.venv-nunchaku/bin/python"
VENV_PIP="${REPO_DIR}/.venv-nunchaku/bin/pip"

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
"${VENV_PIP}" install --upgrade pip setuptools wheel

if ! "${VENV_PYTHON}" -c 'import torch' >/dev/null 2>&1; then
  echo "Installing PyTorch for Z-Image Turbo via Nunchaku runtime"
  "${VENV_PIP}" install torch==2.11.0 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu130
fi

LOCK_FILE="${REPO_DIR}/requirements.lock.txt"
if [[ ! -f "${LOCK_FILE}" ]]; then
  echo "Missing lock file: ${LOCK_FILE}"
  exit 1
fi

echo "Installing locked Python environment from requirements.lock.txt"
"${VENV_PIP}" install -r "${LOCK_FILE}"

if ! "${VENV_PYTHON}" -c 'import nunchaku' >/dev/null 2>&1; then
  echo "Installing Nunchaku runtime package"
  "${VENV_PIP}" install --no-deps --pre --index-url https://appmana.github.io/forks-nunchaku-stable-abi/cu130 nunchaku
fi

echo "Pinning diffusers for the Nunchaku runtime path"
"${VENV_PIP}" install --no-deps --force-reinstall diffusers==0.36.0

echo
echo "Z-Image Turbo via Nunchaku install complete."
