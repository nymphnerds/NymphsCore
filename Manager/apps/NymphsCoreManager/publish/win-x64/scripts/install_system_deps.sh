#!/usr/bin/env bash
set -euo pipefail

if ! command -v sudo >/dev/null 2>&1; then
  echo "This script needs sudo, but sudo is not available."
  exit 1
fi

has_apt_candidate() {
  local pkg="$1"
  local candidate
  candidate="$(apt-cache policy "$pkg" 2>/dev/null | awk '/Candidate:/ {print $2; exit}')"
  [[ -n "${candidate}" && "${candidate}" != "(none)" ]]
}

echo "Installing base Ubuntu packages..."
sudo apt update
sudo apt install -y \
  ca-certificates \
  git \
  wget \
  curl \
  unzip \
  build-essential \
  pkg-config \
  software-properties-common \
  python3 \
  python3-venv \
  python3-pip \
  libegl1-mesa-dev \
  libgl1 \
  libglib2.0-0 \
  ccache

if ! has_apt_candidate python3.10 || ! has_apt_candidate python3.10-venv || ! has_apt_candidate python3.10-dev || \
   ! has_apt_candidate python3.11 || ! has_apt_candidate python3.11-venv || ! has_apt_candidate python3.11-dev; then
  echo "Python 3.10 and/or 3.11 are not available in the current apt sources. Adding deadsnakes PPA..."
  sudo add-apt-repository -y ppa:deadsnakes/ppa
  sudo apt update
fi

sudo apt install -y \
  python3.10 \
  python3.10-venv \
  python3.10-dev \
  python3.11 \
  python3.11-venv \
  python3.11-dev

if apt-cache show python3.10-distutils >/dev/null 2>&1; then
  sudo apt install -y python3.10-distutils
fi

if apt-cache show python3.11-distutils >/dev/null 2>&1; then
  sudo apt install -y python3.11-distutils
fi

echo
echo "Important:"
echo "- This setup expects CUDA at /usr/local/cuda-13.0"
echo "- If CUDA 13.0 is not installed yet, install it before building Hunyuan"
echo "- GitHub CLI is optional once the forks are already public"

if [[ -d /usr/local/cuda-13.0 ]]; then
  echo "- CUDA 13.0 path found"
else
  echo "- CUDA 13.0 path not found yet"
fi
