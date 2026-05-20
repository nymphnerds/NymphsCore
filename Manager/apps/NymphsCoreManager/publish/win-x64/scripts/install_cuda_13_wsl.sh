#!/usr/bin/env bash
set -euo pipefail

CUDA_HOME="/usr/local/cuda-13.0"
CUDA_KEYRING_DEB="${HOME}/cuda-keyring_1.1-1_all.deb"
CUDA_KEYRING_URL="https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/cuda-keyring_1.1-1_all.deb"

if [[ -d "${CUDA_HOME}" ]]; then
  echo "CUDA 13.0 already present at ${CUDA_HOME}"
  exit 0
fi

if ! command -v sudo >/dev/null 2>&1; then
  echo "This script needs sudo, but sudo is not available."
  exit 1
fi

echo "Installing NVIDIA CUDA repository keyring for WSL..."
if [[ ! -f "${CUDA_KEYRING_DEB}" ]]; then
  wget -O "${CUDA_KEYRING_DEB}" "${CUDA_KEYRING_URL}"
fi

sudo dpkg -i "${CUDA_KEYRING_DEB}"
sudo apt update
sudo apt install -y cuda-toolkit-13-0

if [[ ! -d "${CUDA_HOME}" ]]; then
  echo "CUDA install did not create ${CUDA_HOME}"
  exit 1
fi

echo
echo "CUDA 13.0 install complete."
