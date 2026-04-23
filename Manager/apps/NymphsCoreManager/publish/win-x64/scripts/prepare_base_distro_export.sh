#!/usr/bin/env bash
set -euo pipefail

allow_destructive=0
remove_models=1
remove_venvs=1
remove_cuda=0

usage() {
  cat <<'EOF'
Usage: prepare_base_distro_export.sh [options]

This script is destructive. Run it only inside a disposable builder distro that
exists purely to produce a distributable NymphsCore base image.

Options:
  --allow-destructive  Required. Confirms that this builder distro can be cleaned.
  --keep-models        Keep Hugging Face and helper model caches.
  --keep-venvs         Keep Python virtual environments.
  --remove-cuda        Also remove CUDA files from /usr/local/cuda-13.0.
                       This does not attempt a full apt package uninstall.
  -h, --help           Show this help message.
EOF
}

while (($#)); do
  case "$1" in
    --allow-destructive)
      allow_destructive=1
      ;;
    --keep-models)
      remove_models=0
      ;;
    --keep-venvs)
      remove_venvs=0
      ;;
    --remove-cuda)
      remove_cuda=1
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
  shift
done

if [[ "$allow_destructive" -ne 1 ]]; then
  echo "Refusing to run without --allow-destructive." >&2
  echo "This script is intended for a disposable builder distro only." >&2
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"
HOME_DIR="${HOME}"

echo "Preparing builder distro for export"
echo "Current distro: ${WSL_DISTRO_NAME:-unknown}"
echo "Current user: ${USER}"
echo

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

cleanup_dir_contents() {
  local path="$1"
  if [[ -d "$path" ]]; then
    echo "- emptying $path"
    find "$path" -mindepth 1 -maxdepth 1 -exec rm -rf {} +
  fi
}

if [[ "$remove_models" -eq 1 ]]; then
  echo "Removing model caches..."
  cleanup_glob "${NYMPHS3D_HF_CACHE_DIR}/models--tencent--Hunyuan3D-2.1*"
  cleanup_glob "${NYMPHS3D_HF_CACHE_DIR}/models--Tencent-Hunyuan--Hunyuan3D-2.1*"
  cleanup_glob "${NYMPHS3D_HF_CACHE_DIR}/models--Tencent-Hunyuan--HunyuanDiT-v1.1-Diffusers-Distilled*"
  cleanup_glob "${NYMPHS3D_HF_CACHE_DIR}/models--playgroundai--playground-v2.5-1024px-aesthetic*"
  cleanup_glob "${NYMPHS3D_HF_CACHE_DIR}/models--microsoft--TRELLIS.2-4B*"
  cleanup_path "${NYMPHS3D_U2NET_DIR}"
  cleanup_path "${NYMPHS3D_TRELLIS_DIR}/models"
fi

if [[ "$remove_venvs" -eq 1 ]]; then
  echo "Removing Python virtual environments..."
  cleanup_path "${NYMPHS3D_N2D2_DIR}/.venv"
  cleanup_path "${NYMPHS3D_N2D2_DIR}/.venv-nunchaku"
  cleanup_path "${NYMPHS3D_TRELLIS_DIR}/.venv"
  cleanup_path "${HOME_DIR}/Hunyuan3D-2.1"
fi

echo "Removing runtime outputs and transient repo state..."
cleanup_dir_contents "${NYMPHS3D_N2D2_DIR}/outputs"
cleanup_path "${NYMPHS3D_N2D2_DIR}/__pycache__"
cleanup_dir_contents "${NYMPHS3D_TRELLIS_DIR}/output"
cleanup_dir_contents "${NYMPHS3D_TRELLIS_DIR}/gradio_cache"
cleanup_dir_contents "${NYMPHS3D_TRELLIS_DIR}/gradio_cache_tex"
cleanup_path "${NYMPHS3D_TRELLIS_DIR}/__pycache__"

if [[ "$remove_cuda" -eq 1 ]]; then
  echo "Removing CUDA files from ${NYMPHS3D_CUDA_HOME}..."
  if command -v sudo >/dev/null 2>&1; then
    sudo rm -rf "${NYMPHS3D_CUDA_HOME}"
  else
    rm -rf "${NYMPHS3D_CUDA_HOME}"
  fi
fi

echo "Cleaning package-manager caches and transient state..."
if command -v sudo >/dev/null 2>&1; then
  sudo apt clean || true
  sudo rm -rf /var/lib/apt/lists/* || true
  sudo find /var/log -type f -exec truncate -s 0 {} \; || true
else
  apt clean || true
  rm -rf /var/lib/apt/lists/* || true
  find /var/log -type f -exec truncate -s 0 {} \; || true
fi

cleanup_path "${HOME_DIR}/.cache/pip"
cleanup_path "${HOME_DIR}/.cache/uv"
cleanup_path "${HOME_DIR}/.cache/torch_extensions"
cleanup_path "${HOME_DIR}/.cache/triton"
cleanup_path "${HOME_DIR}/.nv/ComputeCache"
cleanup_path "${HOME_DIR}/.local/share/Trash"
cleanup_glob "${HOME_DIR}/.wslconfig.backup-*"
cleanup_path "${HOME_DIR}/.bash_history"
cleanup_glob "/tmp/*"

echo
echo "Builder distro cleanup complete."
echo "Next step: export this distro from Windows with wsl --export."
echo "Suggested follow-up: run scripts/audit_working_install.sh in the source distro to compare what was removed."
