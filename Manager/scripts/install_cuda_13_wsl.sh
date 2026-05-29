#!/usr/bin/env bash
set -euo pipefail

CUDA_HOME="/usr/local/cuda-13.0"
CUDA_KEYRING_DEB="${HOME}/cuda-keyring_1.1-1_all.deb"
CUDA_KEYRING_URL="https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/cuda-keyring_1.1-1_all.deb"

cuda_include_dir() {
  if [[ -e "${CUDA_HOME}/targets/x86_64-linux/include/cuda.h" ]]; then
    printf '%s\n' "${CUDA_HOME}/targets/x86_64-linux/include"
  elif [[ -e "${CUDA_HOME}/include/cuda.h" ]]; then
    printf '%s\n' "${CUDA_HOME}/include"
  fi
}

cuda_lib_dir() {
  if [[ -e "${CUDA_HOME}/targets/x86_64-linux/lib/libcudart.so" ]]; then
    printf '%s\n' "${CUDA_HOME}/targets/x86_64-linux/lib"
  elif [[ -e "${CUDA_HOME}/lib64/libcudart.so" ]]; then
    printf '%s\n' "${CUDA_HOME}/lib64"
  fi
}

verify_cuda_toolkit() {
  local missing=0
  local include_dir
  local lib_dir
  include_dir="$(cuda_include_dir || true)"
  lib_dir="$(cuda_lib_dir || true)"

  if [[ ! -x "${CUDA_HOME}/bin/nvcc" ]]; then
    echo "Missing CUDA toolkit component: ${CUDA_HOME}/bin/nvcc" >&2
    missing=1
  fi

  if [[ -z "${include_dir}" ]]; then
    echo "Missing CUDA toolkit component: cuda.h under ${CUDA_HOME}/include or ${CUDA_HOME}/targets/x86_64-linux/include" >&2
    missing=1
  fi

  if [[ -z "${lib_dir}" ]]; then
    echo "Missing CUDA toolkit component: libcudart.so under ${CUDA_HOME}/lib64 or ${CUDA_HOME}/targets/x86_64-linux/lib" >&2
    missing=1
  elif [[ ! -e "${lib_dir}/libcudart_static.a" ]]; then
    echo "Missing CUDA toolkit component: ${lib_dir}/libcudart_static.a" >&2
    missing=1
  fi

  if [[ -x "${CUDA_HOME}/bin/nvcc" ]]; then
    if ! "${CUDA_HOME}/bin/nvcc" --version >/dev/null 2>&1; then
      echo "CUDA nvcc exists but failed to run: ${CUDA_HOME}/bin/nvcc" >&2
      missing=1
    fi
  fi

  return "${missing}"
}

write_cuda_profile() {
  local include_dir
  local lib_dir
  include_dir="$(cuda_include_dir)"
  lib_dir="$(cuda_lib_dir)"

  sudo tee /etc/profile.d/nymphscore-cuda.sh >/dev/null <<EOF
export CUDA_HOME="${CUDA_HOME}"
export CUDA_PATH="${CUDA_HOME}"
export CUDAToolkit_ROOT="${CUDA_HOME}"
export CUDACXX="${CUDA_HOME}/bin/nvcc"
export CUDA_INCLUDE_DIRS="${include_dir}"
export CUDA_LIBRARY_DIR="${lib_dir}"
export PATH="${CUDA_HOME}/bin:\${PATH}"
export LD_LIBRARY_PATH="${lib_dir}:\${LD_LIBRARY_PATH:-}"
export LIBRARY_PATH="${lib_dir}:\${LIBRARY_PATH:-}"
export CMAKE_PREFIX_PATH="${CUDA_HOME}:${CUDA_HOME}/targets/x86_64-linux:\${CMAKE_PREFIX_PATH:-}"
EOF
  sudo chmod 644 /etc/profile.d/nymphscore-cuda.sh
}

if [[ -d "${CUDA_HOME}" ]]; then
  echo "CUDA 13.0 path found at ${CUDA_HOME}; verifying toolkit files..."
  if verify_cuda_toolkit; then
    write_cuda_profile
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

write_cuda_profile

echo
echo "CUDA 13.0 install complete."
