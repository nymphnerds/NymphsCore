#!/usr/bin/env bash
set -euo pipefail

CUDA_HOME="/usr/local/cuda-13.0"
CUDA_KEYRING_DEB="${HOME}/cuda-keyring_1.1-1_all.deb"
CUDA_KEYRING_URL="https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/cuda-keyring_1.1-1_all.deb"

verify_cuda_toolkit() {
  local missing=0
  local path
  for path in \
    "${CUDA_HOME}/bin/nvcc" \
    "${CUDA_HOME}/lib64/libcudart.so" \
    "${CUDA_HOME}/lib64/libcudart_static.a"; do
    if [[ ! -e "${path}" ]]; then
      echo "Missing CUDA toolkit component: ${path}" >&2
      missing=1
    fi
  done

  if [[ ! -e "${CUDA_HOME}/include/cuda.h" && ! -e "${CUDA_HOME}/targets/x86_64-linux/include/cuda.h" ]]; then
    echo "Missing CUDA toolkit component: cuda.h under ${CUDA_HOME}/include or ${CUDA_HOME}/targets/x86_64-linux/include" >&2
    missing=1
  fi

  if [[ -x "${CUDA_HOME}/bin/nvcc" ]]; then
    if ! "${CUDA_HOME}/bin/nvcc" --version >/dev/null 2>&1; then
      echo "CUDA nvcc exists but failed to run: ${CUDA_HOME}/bin/nvcc" >&2
      missing=1
    fi
  else
    missing=1
  fi

  return "${missing}"
}

if [[ -d "${CUDA_HOME}" ]]; then
  echo "CUDA 13.0 path found at ${CUDA_HOME}; verifying toolkit files..."
  if verify_cuda_toolkit; then
    echo "CUDA 13.0 toolkit is ready."
    exit 0
  fi

  echo "CUDA 13.0 path exists but toolkit verification failed; reinstalling package set..."
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
  echo "CUDA install did not create ${CUDA_HOME}" >&2
  exit 1
fi

if ! verify_cuda_toolkit; then
  echo "CUDA install completed, but toolkit verification failed." >&2
  exit 1
fi

echo
echo "CUDA 13.0 install complete."
