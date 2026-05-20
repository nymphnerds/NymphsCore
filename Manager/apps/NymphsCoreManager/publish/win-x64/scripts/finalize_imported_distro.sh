#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

install_cuda=1
install_backend_envs=0
download_models=0
skip_verify=0
check_updates_only=0

usage() {
  cat <<'EOF'
Usage: finalize_imported_distro.sh [options]

This script is intended for a post-import NymphsCore base distro workflow.
It finalizes the imported distro by installing runtime pieces that were
deliberately left out of the exported base image.

Options:
  --skip-cuda           Do not run CUDA installation
  --skip-backend-envs   Compatibility flag; backend environments are module-owned
  --skip-models         Compatibility flag; model fetch is module-owned
  --skip-verify         Compatibility flag; backend verification is module-owned
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
      skip_verify=1
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
export NYMPHS3D_HELPER_ROOT=/opt/nymphs3d/NymphsCore
export NYMPHS3D_RUNTIME_ROOT="$HOME"
EOF
sudo chmod 644 /etc/profile.d/nymphscore.sh

if [[ "$check_updates_only" -eq 1 ]]; then
  echo
  echo "Registry module update checks are handled by the Manager shell."
  exit 0
fi

"${ROOT_DIR}/scripts/preflight_wsl.sh"
"${ROOT_DIR}/scripts/install_system_deps.sh"

if [[ "$install_cuda" -eq 1 ]]; then
  echo
  echo "Installing CUDA runtime/toolkit support..."
  "${ROOT_DIR}/scripts/install_cuda_13_wsl.sh"
else
  echo
  echo "Skipping CUDA installation."
fi

echo
echo "Skipping module backend environment creation."
echo "Module installs, model fetches, and backend verification are owned by installed Nymph modules."

echo
echo "Imported distro finalize step complete."
