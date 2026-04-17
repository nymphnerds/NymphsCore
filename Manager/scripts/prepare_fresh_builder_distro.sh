#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "This script is intended to run as root inside a disposable builder distro." >&2
  exit 1
fi

NYMPHS3D_HELPER_ROOT="${NYMPHS3D_HELPER_ROOT:-/opt/nymphs3d/Nymphs3D}"
NYMPHS3D_RUNTIME_ROOT="${NYMPHS3D_RUNTIME_ROOT:-/opt/nymphs3d/runtime}"
NYMPHS3D_H2_REPO_URL="${NYMPHS3D_H2_REPO_URL:-https://github.com/Tencent-Hunyuan/Hunyuan3D-2.git}"
NYMPHS3D_N2D2_REPO_URL="${NYMPHS3D_N2D2_REPO_URL:-https://github.com/Babyjawz/Nymphs2D2.git}"
NYMPHS3D_TRELLIS_REPO_URL="${NYMPHS3D_TRELLIS_REPO_URL:-https://github.com/microsoft/TRELLIS.2.git}"

H2_DIR="${NYMPHS3D_RUNTIME_ROOT}/Hunyuan3D-2"
Z_IMAGE_DIR="${NYMPHS3D_RUNTIME_ROOT}/Z-Image"
TRELLIS_DIR="${NYMPHS3D_RUNTIME_ROOT}/TRELLIS.2"

echo "Preparing fresh Nymphs3D builder distro..."
echo "Helper root: ${NYMPHS3D_HELPER_ROOT}"
echo "Runtime root: ${NYMPHS3D_RUNTIME_ROOT}"

mkdir -p "${NYMPHS3D_RUNTIME_ROOT}"

if [[ ! -d "${NYMPHS3D_HELPER_ROOT}/.git" ]]; then
  echo "Expected helper repo checkout was not found at ${NYMPHS3D_HELPER_ROOT}" >&2
  exit 1
fi

clone_or_refresh_repo() {
  local repo_url="$1"
  local repo_dir="$2"

  if [[ ! -d "${repo_dir}/.git" ]]; then
    echo "Cloning ${repo_url} into ${repo_dir}"
    git clone "${repo_url}" "${repo_dir}"
  else
    echo "Repo already exists at ${repo_dir}, refreshing"
    git -C "${repo_dir}" fetch --all --prune || true
  fi

  rm -rf "${repo_dir}/.venv" "${repo_dir}/.venv-nunchaku" "${repo_dir}/.venv-official"
}

clone_or_refresh_repo "${NYMPHS3D_H2_REPO_URL}" "${H2_DIR}"
clone_or_refresh_repo "${NYMPHS3D_N2D2_REPO_URL}" "${Z_IMAGE_DIR}"
clone_or_refresh_repo "${NYMPHS3D_TRELLIS_REPO_URL}" "${TRELLIS_DIR}"

rm -rf "${HOME}/.cache/huggingface" \
       "${HOME}/.cache/pip" \
       "${HOME}/.cache/uv" \
       "${HOME}/.cache/torch_extensions" \
       "${HOME}/.cache/triton" \
       "${HOME}/.u2net"

cat >/etc/profile.d/nymphscore.sh <<'EOF'
export NYMPHS3D_HELPER_ROOT=/opt/nymphs3d/Nymphs3D
export NYMPHS3D_RUNTIME_ROOT="$HOME"
export NYMPHS3D_H2_DIR="$HOME/Hunyuan3D-2"
export NYMPHS3D_Z_IMAGE_DIR="$HOME/Z-Image"
export NYMPHS3D_N2D2_DIR="$NYMPHS3D_Z_IMAGE_DIR"
export NYMPHS3D_TRELLIS_DIR="$HOME/TRELLIS.2"
EOF

chmod 644 /etc/profile.d/nymphscore.sh
chmod +x "${NYMPHS3D_HELPER_ROOT}/scripts/"*.sh

echo "Fresh builder prep complete."
echo "This builder intentionally excludes venvs, model caches, and helper model downloads."
