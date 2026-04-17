#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

install_cuda=1
install_backend_envs=1
download_models=1
verify_install=1
check_updates_only=0

usage() {
  cat <<'EOF'
Usage: finalize_imported_distro.sh [options]

This script is intended for a post-import Nymphs3D base distro workflow.
It finalizes the imported distro by installing runtime pieces that were
deliberately left out of the exported base image.

Options:
  --skip-cuda           Do not run CUDA installation
  --skip-backend-envs   Do not create backend Python environments
  --skip-models         Do not prefetch model weights
  --skip-verify         Do not run final verification
  --check-updates-only  Only report managed repo update state, then exit
  -h, --help       Show this help message
EOF
}

while (($#)); do
  case "$1" in
    --skip-cuda)
      install_cuda=0
      ;;
    --skip-backend-envs)
      install_backend_envs=0
      ;;
    --skip-models)
      download_models=0
      ;;
    --skip-verify)
      verify_install=0
      ;;
    --check-updates-only)
      check_updates_only=1
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

echo "Finalizing imported NymphsCore distro from ${ROOT_DIR}"
echo

echo "Normalizing runtime shell paths..."
cat <<'EOF' | sudo tee /etc/profile.d/nymphscore.sh >/dev/null
export NYMPHS3D_HELPER_ROOT=/opt/nymphs3d/Nymphs3D
export NYMPHS3D_RUNTIME_ROOT="$HOME"
export NYMPHS3D_H2_DIR="$HOME/Hunyuan3D-2"
export NYMPHS3D_Z_IMAGE_DIR="$HOME/Z-Image"
export NYMPHS3D_N2D2_DIR="$NYMPHS3D_Z_IMAGE_DIR"
export NYMPHS3D_TRELLIS_DIR="$HOME/TRELLIS.2"
EOF
sudo chmod 644 /etc/profile.d/nymphscore.sh

if [[ "$check_updates_only" -eq 1 ]]; then
  echo
  echo "Checking managed repo update state only..."
  "${ROOT_DIR}/scripts/check_managed_repo_updates.sh"
  echo
  echo "Managed repo update check complete."
  exit 0
fi

"${ROOT_DIR}/scripts/preflight_wsl.sh"
"${ROOT_DIR}/scripts/install_system_deps.sh"

echo
echo "Pruning legacy backends that are no longer part of the managed stack..."
"${ROOT_DIR}/scripts/prune_legacy_runtime.sh"

if [[ "$install_cuda" -eq 1 ]]; then
  echo
  echo "Installing CUDA runtime/toolkit support..."
  "${ROOT_DIR}/scripts/install_cuda_13_wsl.sh"
else
  echo
  echo "Skipping CUDA installation."
fi

if [[ "$install_backend_envs" -eq 1 ]]; then
  echo
  echo "Checking managed repo update state..."
  "${ROOT_DIR}/scripts/check_managed_repo_updates.sh"
  echo
  echo "Installing backend environments..."
  "${ROOT_DIR}/scripts/install_hunyuan_2.sh"
  "${ROOT_DIR}/scripts/install_nymphs2d2.sh"
  "${ROOT_DIR}/scripts/install_trellis.sh"
else
  echo
  echo "Skipping backend environment creation."
fi

if [[ "$download_models" -eq 1 ]]; then
  echo
  echo "Downloading required models..."
  "${ROOT_DIR}/scripts/prefetch_models.sh"
else
  echo
  echo "Skipping model prefetch. Models can be downloaded later."
fi

if [[ "$verify_install" -eq 1 ]]; then
  echo
  echo "Running verification..."
  "${ROOT_DIR}/scripts/verify_install.sh"
else
  echo
  echo "Skipping verification."
fi

echo
echo "Imported distro finalize step complete."
