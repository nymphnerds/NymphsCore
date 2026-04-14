#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

HOME_DIR="${HOME}"

size_or_zero() {
  local path="$1"
  if [[ -e "$path" ]]; then
    du -sh "$path" 2>/dev/null | awk '{print $1}'
  else
    echo "0"
  fi
}

bytes_or_zero() {
  local path="$1"
  if [[ -e "$path" ]]; then
    du -sb "$path" 2>/dev/null | awk '{print $1}'
  else
    echo "0"
  fi
}

print_group() {
  local title="$1"
  shift
  echo
  echo "$title"
  echo "============================================================"
  while (($#)); do
    local label="$1"
    local path="$2"
    shift 2
    printf "%-36s %8s  %s\n" "$label" "$(size_or_zero "$path")" "$path"
  done
}

repo_h2="${NYMPHS3D_H2_DIR}"
repo_nymph="${HOME_DIR}/Nymphs3D"

venv_h2="${repo_h2}/.venv"
cuda_dir="${NYMPHS3D_CUDA_HOME}"

hf_h2="${NYMPHS3D_HF_CACHE_DIR}/models--tencent--Hunyuan3D-2"
hf_h2mv="${NYMPHS3D_HF_CACHE_DIR}/models--tencent--Hunyuan3D-2mv"
u2net_dir="${NYMPHS3D_U2NET_DIR}"

repo_h2_no_venv_bytes=$(( $(bytes_or_zero "$repo_h2") - $(bytes_or_zero "$venv_h2") ))

human_from_bytes() {
  local bytes="$1"
  numfmt --to=iec --suffix=B "$bytes"
}

echo "Nymphs3D working-install audit"
echo "This report is intended to help define a distributable base WSL image."

print_group "Core repos and runtime weight" \
  "Hunyuan3D-2" "$repo_h2" \
  "Nymphs3D helper repo" "$repo_nymph" \
  "CUDA 13.0" "$cuda_dir" \
  "Hunyuan3D-2 .venv" "$venv_h2"

print_group "Model and helper caches to defer" \
  "HF Hunyuan3D-2" "$hf_h2" \
  "HF Hunyuan3D-2mv" "$hf_h2mv" \
  "u2net" "$u2net_dir"

echo
echo "Base image profile estimates"
echo "============================================================"
printf "%-36s %8s\n" "Lean base (repos only)" "$(human_from_bytes $(( repo_h2_no_venv_bytes + $(bytes_or_zero "$repo_nymph") )))"
printf "%-36s %8s\n" "Lean + CUDA" "$(human_from_bytes $(( repo_h2_no_venv_bytes + $(bytes_or_zero "$repo_nymph") + $(bytes_or_zero "$cuda_dir") )))"
printf "%-36s %8s\n" "Prewarmed + CUDA + venvs" "$(human_from_bytes $(( repo_h2_no_venv_bytes + $(bytes_or_zero "$repo_nymph") + $(bytes_or_zero "$cuda_dir") + $(bytes_or_zero "$venv_h2") )))"

echo
echo "Recommendation"
echo "============================================================"
echo "- Target the Lean base image first."
echo "- Keep Hugging Face caches and helper model files out of the exported distro."
echo "- Prefer building Python envs after import."
echo "- Add CUDA after import unless an NVIDIA-ready image is intentionally produced."
