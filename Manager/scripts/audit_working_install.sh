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

repo_n2d2="${NYMPHS3D_N2D2_DIR}"
repo_trellis="${NYMPHS3D_TRELLIS_DIR}"
repo_nymph="${HOME_DIR}/NymphsCore"

venv_n2d2="${repo_n2d2}/.venv-nunchaku"
venv_trellis="${repo_trellis}/.venv"
cuda_dir="${NYMPHS3D_CUDA_HOME}"

hf_zimage="${NYMPHS3D_HF_CACHE_DIR}/models--Tongyi-MAI--Z-Image-Turbo"
hf_trellis="${NYMPHS3D_HF_CACHE_DIR}/models--microsoft--TRELLIS.2-4B"
u2net_dir="${NYMPHS3D_U2NET_DIR}"

repo_n2d2_no_venv_bytes=$(( $(bytes_or_zero "$repo_n2d2") - $(bytes_or_zero "$venv_n2d2") ))
repo_trellis_no_venv_bytes=$(( $(bytes_or_zero "$repo_trellis") - $(bytes_or_zero "$venv_trellis") ))

human_from_bytes() {
  local bytes="$1"
  numfmt --to=iec --suffix=B "$bytes"
}

echo "NymphsCore working-install audit"
echo "This report is intended to help define a distributable base WSL image."

print_group "Core repos and runtime weight" \
  "Z-Image" "$repo_n2d2" \
  "TRELLIS.2" "$repo_trellis" \
  "NymphsCore helper repo" "$repo_nymph" \
  "CUDA 13.0" "$cuda_dir" \
  "Z-Image .venv-nunchaku" "$venv_n2d2" \
  "TRELLIS.2 .venv" "$venv_trellis"

print_group "Model and helper caches to defer" \
  "HF Z-Image Turbo" "$hf_zimage" \
  "HF TRELLIS.2-4B" "$hf_trellis" \
  "u2net" "$u2net_dir"

echo
echo "Base image profile estimates"
echo "============================================================"
printf "%-36s %8s\n" "Lean base (repos only)" "$(human_from_bytes $(( repo_n2d2_no_venv_bytes + repo_trellis_no_venv_bytes + $(bytes_or_zero "$repo_nymph") )))"
printf "%-36s %8s\n" "Lean + CUDA" "$(human_from_bytes $(( repo_n2d2_no_venv_bytes + repo_trellis_no_venv_bytes + $(bytes_or_zero "$repo_nymph") + $(bytes_or_zero "$cuda_dir") )))"
printf "%-36s %8s\n" "Prewarmed + CUDA + venvs" "$(human_from_bytes $(( repo_n2d2_no_venv_bytes + repo_trellis_no_venv_bytes + $(bytes_or_zero "$repo_nymph") + $(bytes_or_zero "$cuda_dir") + $(bytes_or_zero "$venv_n2d2") + $(bytes_or_zero "$venv_trellis") )))"

echo
echo "Recommendation"
echo "============================================================"
echo "- Target the Lean base image first."
echo "- Keep Hugging Face caches and helper model files out of the exported distro."
echo "- Prefer building Python envs after import."
echo "- Add CUDA after import unless an NVIDIA-ready image is intentionally produced."
