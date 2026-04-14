#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

HOME_DIR="${HOME}"
legacy_repo_h21="${NYMPHS3D_RUNTIME_ROOT}/Hunyuan3D-2.1"
legacy_n2d2_venv="${NYMPHS3D_N2D2_DIR}/.venv"

cleanup_path() {
  local path="$1"
  if [[ -e "$path" ]]; then
    echo "- removing $path"
    rm -rf "$path"
  fi
}

cleanup_glob() {
  local pattern="$1"
  shopt -s nullglob
  local matches=( $pattern )
  shopt -u nullglob
  if ((${#matches[@]})); then
    echo "- removing ${pattern}"
    rm -rf "${matches[@]}"
  fi
}

echo "Pruning legacy runtime state..."

cleanup_path "${legacy_repo_h21}"
cleanup_path "${legacy_n2d2_venv}"

cleanup_glob "${NYMPHS3D_HF_CACHE_DIR}/models--tencent--Hunyuan3D-2.1*"
cleanup_glob "${NYMPHS3D_HF_CACHE_DIR}/models--Tencent-Hunyuan--Hunyuan3D-2.1*"
cleanup_glob "${NYMPHS3D_HF_CACHE_DIR}/models--Tencent-Hunyuan--HunyuanDiT-v1.1-Diffusers-Distilled*"
cleanup_glob "${NYMPHS3D_HF_CACHE_DIR}/models--playgroundai--playground-v2.5-1024px-aesthetic*"

cleanup_path "${HOME_DIR}/.cache/nymphs3d"

echo "Legacy runtime prune complete."
