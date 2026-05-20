#!/usr/bin/env bash
set -euo pipefail

MIN_ROOT_GB=100
MIN_HOME_GB=100

available_gb() {
  local path="$1"
  df -BG --output=avail "$path" | tail -n 1 | tr -dc '0-9'
}

require_cmd() {
  local cmd="$1"
  if ! command -v "$cmd" >/dev/null 2>&1; then
    echo "Missing required command: $cmd"
    exit 1
  fi
}

echo "Running WSL preflight..."

require_cmd bash
require_cmd git
require_cmd sudo

root_gb="$(available_gb /)"
home_gb="$(available_gb "${HOME}")"

if [[ -z "${root_gb}" || "${root_gb}" -lt "${MIN_ROOT_GB}" ]]; then
  echo "At least ${MIN_ROOT_GB} GB free is recommended on the WSL root filesystem."
  echo "Current free space on /: ${root_gb:-unknown} GB"
  exit 1
fi

if [[ -z "${home_gb}" || "${home_gb}" -lt "${MIN_HOME_GB}" ]]; then
  echo "At least ${MIN_HOME_GB} GB free is recommended in ${HOME}."
  echo "Current free space in HOME: ${home_gb:-unknown} GB"
  exit 1
fi

if ! grep -qi microsoft /proc/version; then
  echo "This script must be run inside WSL."
  exit 1
fi

if ! command -v nvidia-smi >/dev/null 2>&1; then
  echo "nvidia-smi was not found in WSL."
  echo "Install or fix NVIDIA's Windows driver with WSL CUDA support, then rerun."
  exit 1
fi

if ! nvidia-smi >/dev/null 2>&1; then
  echo "nvidia-smi exists but failed."
  echo "GPU access from WSL is not healthy yet."
  exit 1
fi

echo "WSL preflight passed."
echo "- Free space on /: ${root_gb} GB"
echo "- Free space in HOME: ${home_gb} GB"
echo "- NVIDIA GPU access from WSL: OK"
echo "- Python 3.10 and 3.11 will be installed later if missing"
