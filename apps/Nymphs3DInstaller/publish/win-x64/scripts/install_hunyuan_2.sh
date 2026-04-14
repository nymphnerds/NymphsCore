#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"
source "${ROOT_DIR}/scripts/managed_repo_utils.sh"

REPO_DIR="${NYMPHS3D_H2_DIR}"
REPO_URL="${NYMPHS3D_H2_REPO_URL}"
REPO_BRANCH="${NYMPHS3D_H2_REPO_BRANCH}"

configure_nymphs3d_cuda_env

if [[ ! -d "${CUDA_HOME}" ]]; then
  echo "CUDA 13.0 was not found at ${CUDA_HOME}"
  echo "Install CUDA first, then rerun this script."
  exit 1
fi

if ! command -v python3.10 >/dev/null 2>&1; then
  echo "python3.10 is not installed."
  echo "Run ./scripts/install_system_deps.sh first."
  exit 1
fi

managed_repo_apply "Hunyuan3D-2" "${REPO_DIR}" "${REPO_URL}" "${REPO_BRANCH}"

if [[ ! -d "${REPO_DIR}/.git" ]]; then
  echo "Expected repo checkout is still missing at ${REPO_DIR}"
  exit 1
fi

cd "${REPO_DIR}"

if [[ -d ".venv" && ! -x ".venv/bin/python" ]]; then
  echo "Existing venv is incomplete. Recreating it."
  rm -rf .venv
fi

if [[ ! -d ".venv" ]]; then
  echo "Creating Python 3.10 venv"
  python3.10 -m venv .venv
fi

VENV_PYTHON="${REPO_DIR}/.venv/bin/python"
VENV_PIP="${REPO_DIR}/.venv/bin/pip"

if [[ ! -x "${VENV_PYTHON}" || ! -x "${VENV_PIP}" ]]; then
  echo "Python 3.10 venv was created, but the expected executables are missing."
  exit 1
fi

"${VENV_PYTHON}" --version
"${VENV_PIP}" install --upgrade pip setuptools wheel

LOCK_FILE="${REPO_DIR}/requirements.lock.txt"
if [[ ! -f "${LOCK_FILE}" ]]; then
  echo "Missing lock file: ${LOCK_FILE}"
  exit 1
fi

TEMP_LOCK="$(mktemp)"
# The lockfile can include repo-local extensions that are built below.
grep -Ev '^-e |^(custom_rasterizer|mesh_processor)==' "${LOCK_FILE}" > "${TEMP_LOCK}"

echo "Installing locked Python environment from requirements.lock.txt"
"${VENV_PIP}" install -r "${TEMP_LOCK}"
rm -f "${TEMP_LOCK}"

"${VENV_PIP}" install -e .

echo "Building Hunyuan3D-2 texture extensions"
cd "${REPO_DIR}/hy3dgen/texgen/custom_rasterizer"
"${VENV_PYTHON}" setup.py install

cd "${REPO_DIR}/hy3dgen/texgen/differentiable_renderer"
"${VENV_PYTHON}" setup.py install

echo
echo "Hunyuan3D-2 install complete."
