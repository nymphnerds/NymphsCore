#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"
source "${ROOT_DIR}/scripts/managed_repo_utils.sh"

REPO_DIR="${NYMPHS3D_TRELLIS_DIR}"
REPO_URL="${NYMPHS3D_TRELLIS_REPO_URL}"
REPO_BRANCH="${NYMPHS3D_TRELLIS_REPO_BRANCH}"
UTILS3D_REF="${NYMPHS3D_TRELLIS_UTILS3D_REF:-9a4eb15e4021b67b12c460c7057d642626897ec8}"
INSTALL_FLASH_ATTN="${NYMPHS3D_TRELLIS_INSTALL_FLASH_ATTN:-yes}"

print_flash_attn_diagnostics() {
  local torch_report
  local mem_available_kb="unknown"
  local swap_total_kb="unknown"
  local swap_free_kb="unknown"

  echo "flash-attn diagnostics begin"
  echo "flash-attn diagnostics: python=$("${VENV_PYTHON}" --version 2>&1)"
  echo "flash-attn diagnostics: pip=$("${VENV_PIP}" --version 2>&1)"

  torch_report="$("${VENV_PYTHON}" - <<'PY'
import os

try:
    import torch
except Exception as exc:
    print(f"torch_import_error={exc}")
else:
    print(f"torch_version={torch.__version__}")
    print(f"torch_cuda_version={torch.version.cuda}")
    print(f"torch_cuda_available={torch.cuda.is_available()}")

print(f"cuda_home={os.environ.get('CUDA_HOME', '')}")
print(f"torch_cuda_arch_list={os.environ.get('TORCH_CUDA_ARCH_LIST', '')}")
print(f"flash_attn_cuda_archs={os.environ.get('FLASH_ATTN_CUDA_ARCHS', '')}")
PY
)"
  while IFS= read -r line; do
    [[ -n "${line}" ]] && echo "flash-attn diagnostics: ${line}"
  done <<< "${torch_report}"

  if command -v nvidia-smi >/dev/null 2>&1; then
    echo "flash-attn diagnostics: nvidia-smi --query-gpu=name,driver_version,compute_cap"
    nvidia-smi --query-gpu=name,driver_version,compute_cap --format=csv,noheader || true
  else
    echo "flash-attn diagnostics: nvidia-smi not found"
  fi

  if command -v nvcc >/dev/null 2>&1; then
    echo "flash-attn diagnostics: nvcc --version"
    nvcc --version || true
  else
    echo "flash-attn diagnostics: nvcc not found in PATH"
  fi

  if command -v free >/dev/null 2>&1; then
    echo "flash-attn diagnostics: free -h"
    free -h || true
  fi

  if [[ -r /proc/meminfo ]]; then
    mem_available_kb="$(awk '/MemAvailable:/ { print $2 }' /proc/meminfo | head -n 1)"
    swap_total_kb="$(awk '/SwapTotal:/ { print $2 }' /proc/meminfo | head -n 1)"
    swap_free_kb="$(awk '/SwapFree:/ { print $2 }' /proc/meminfo | head -n 1)"
    echo "flash-attn diagnostics: /proc/meminfo snapshot"
    grep -E '^(MemTotal|MemAvailable|SwapTotal|SwapFree):' /proc/meminfo || true
  fi

  if command -v swapon >/dev/null 2>&1; then
    echo "flash-attn diagnostics: swapon --show"
    swapon --show || true
  fi

  echo "flash-attn diagnostics: ulimit -a"
  ulimit -a || true
  echo "flash-attn diagnostics end"
}

resolve_flash_attn_jobs() {
  local explicit_jobs="${NYMPHS3D_TRELLIS_FLASH_ATTN_MAX_JOBS:-}"
  local low_memory_mode="${NYMPHS3D_TRELLIS_FLASH_ATTN_LOW_MEMORY:-auto}"
  local max_auto_jobs="${NYMPHS3D_TRELLIS_FLASH_ATTN_MAX_AUTO_JOBS:-6}"
  local mem_per_job_gb="${NYMPHS3D_TRELLIS_FLASH_ATTN_MEM_PER_JOB_GB:-8}"
  local reserve_gb="${NYMPHS3D_TRELLIS_FLASH_ATTN_RESERVE_GB:-8}"
  local mem_available_kb="0"
  local mem_available_gb="0"
  local usable_gb="0"
  local cpu_count="1"
  local chosen_jobs=""

  if [[ -n "${explicit_jobs}" ]]; then
    if [[ ! "${explicit_jobs}" =~ ^[0-9]+$ || "${explicit_jobs}" -lt 1 ]]; then
      echo "Invalid NYMPHS3D_TRELLIS_FLASH_ATTN_MAX_JOBS value: ${explicit_jobs}" >&2
      return 1
    fi
    echo "${explicit_jobs}"
    return 0
  fi

  for numeric_setting in max_auto_jobs mem_per_job_gb reserve_gb; do
    local value="${!numeric_setting}"
    if [[ ! "${value}" =~ ^[0-9]+$ || "${value}" -lt 1 ]]; then
      echo "Invalid ${numeric_setting} value: ${value}" >&2
      return 1
    fi
  done

  if command -v nproc >/dev/null 2>&1; then
    cpu_count="$(nproc)"
  fi
  if [[ ! "${cpu_count}" =~ ^[0-9]+$ || "${cpu_count}" -lt 1 ]]; then
    cpu_count="1"
  fi

  if [[ -r /proc/meminfo ]]; then
    mem_available_kb="$(awk '/MemAvailable:/ { print $2 }' /proc/meminfo | head -n 1)"
  fi
  if [[ "${mem_available_kb}" =~ ^[0-9]+$ && "${mem_available_kb}" -gt 0 ]]; then
    mem_available_gb=$((mem_available_kb / 1048576))
  fi

  cap_jobs() {
    local requested="$1"
    local capped="${requested}"
    if [[ "${cpu_count}" -lt "${capped}" ]]; then
      capped="${cpu_count}"
    fi
    if [[ "${max_auto_jobs}" -lt "${capped}" ]]; then
      capped="${max_auto_jobs}"
    fi
    if [[ "${capped}" -lt 1 ]]; then
      capped=1
    fi
    echo "${capped}"
  }

  jobs_from_memory() {
    if [[ "${mem_available_gb}" -le "${reserve_gb}" ]]; then
      echo 1
      return 0
    fi

    usable_gb=$((mem_available_gb - reserve_gb))
    local jobs=$((usable_gb / mem_per_job_gb))
    if [[ "${jobs}" -lt 1 ]]; then
      jobs=1
    fi
    cap_jobs "${jobs}"
  }

  case "${low_memory_mode}" in
    yes|true|1)
      chosen_jobs=1
      ;;
    no|false|0)
      chosen_jobs="$(cap_jobs "${max_auto_jobs}")"
      ;;
    auto)
      chosen_jobs="$(jobs_from_memory)"
      ;;
    *)
      echo "Unknown NYMPHS3D_TRELLIS_FLASH_ATTN_LOW_MEMORY value: ${low_memory_mode}" >&2
      echo "Use one of: auto, yes, no" >&2
      return 1
      ;;
  esac

  echo "${chosen_jobs}"
}

map_flash_attn_cuda_arch() {
  local compute_cap="$1"
  local major="${compute_cap%%.*}"

  case "${major}" in
    8)
      echo "80"
      ;;
    9)
      echo "90"
      ;;
    10|11)
      echo "100"
      ;;
    12)
      echo "120"
      ;;
  esac
}

resolve_flash_attn_cuda_archs() {
  local detected_caps=""
  local compute_cap=""
  local arch=""
  local archs=""

  if ! command -v nvidia-smi >/dev/null 2>&1; then
    return 0
  fi

  detected_caps="$(nvidia-smi --query-gpu=compute_cap --format=csv,noheader 2>/dev/null || true)"
  while IFS= read -r compute_cap; do
    compute_cap="$(tr -d '[:space:]' <<< "${compute_cap}")"
    if [[ ! "${compute_cap}" =~ ^[0-9]+\.[0-9]+$ ]]; then
      continue
    fi

    arch="$(map_flash_attn_cuda_arch "${compute_cap}")"
    if [[ -z "${arch}" ]]; then
      continue
    fi

    case ";${archs};" in
      *";${arch};"*)
        ;;
      *)
        if [[ -z "${archs}" ]]; then
          archs="${arch}"
        else
          archs="${archs};${arch}"
        fi
        ;;
    esac
  done <<< "${detected_caps}"

  echo "${archs}"
}

configure_nymphs3d_cuda_env
configure_nymphs3d_hf_env

if [[ ! -d "${CUDA_HOME}" ]]; then
  echo "CUDA 13.0 was not found at ${CUDA_HOME}"
  echo "Install CUDA first, then rerun this script."
  exit 1
fi

if ! command -v python3.10 >/dev/null 2>&1; then
  echo "python3.10 is not installed."
  echo "Run ./scripts/install_system_deps.sh first."
  exit 1
fi

managed_repo_apply "TRELLIS.2" "${REPO_DIR}" "${REPO_URL}" "${REPO_BRANCH}"

if [[ ! -d "${REPO_DIR}/.git" ]]; then
  echo "Expected repo checkout is still missing at ${REPO_DIR}"
  echo "If the default repo location is not correct yet, override NYMPHS3D_TRELLIS_REPO_URL and/or NYMPHS3D_TRELLIS_REPO_BRANCH and rerun."
  exit 1
fi

cd "${REPO_DIR}"

echo "Initializing TRELLIS submodules required for native builds"
git submodule update --init --recursive o-voxel/third_party/eigen

ADAPTER_SOURCE_DIR="${ROOT_DIR}/scripts/trellis_adapter"
ADAPTER_TARGET_DIR="${REPO_DIR}/scripts"
ADAPTER_FILES=(
  api_server_trellis.py
  trellis_official_common.py
  run_official_image_to_3d.py
  run_official_shape_only.py
)

echo "Syncing managed TRELLIS adapter scripts"
mkdir -p "${ADAPTER_TARGET_DIR}"
for adapter_file in "${ADAPTER_FILES[@]}"; do
  if [[ ! -f "${ADAPTER_SOURCE_DIR}/${adapter_file}" ]]; then
    echo "Missing managed TRELLIS adapter file: ${ADAPTER_SOURCE_DIR}/${adapter_file}"
    exit 1
  fi
  install -m 644 "${ADAPTER_SOURCE_DIR}/${adapter_file}" "${ADAPTER_TARGET_DIR}/${adapter_file}"
done

if [[ -L ".venv" && ! -e ".venv" ]]; then
  echo "Existing TRELLIS venv symlink is broken. Recreating it."
  rm -f .venv
fi

if [[ -e ".venv" && ! -x ".venv/bin/python" ]]; then
  echo "Existing TRELLIS venv is incomplete. Recreating it."
  rm -rf .venv
fi

if [[ ! -d ".venv" ]]; then
  echo "Creating Python 3.10 TRELLIS venv"
  python3.10 -m venv .venv
fi

VENV_PYTHON="${REPO_DIR}/.venv/bin/python"
VENV_PIP="${REPO_DIR}/.venv/bin/pip"

if [[ ! -x "${VENV_PYTHON}" || ! -x "${VENV_PIP}" ]]; then
  echo "TRELLIS venv was created, but the expected executables are missing."
  exit 1
fi

export PATH="${REPO_DIR}/.venv/bin:${PATH}"

"${VENV_PYTHON}" --version
"${VENV_PIP}" install --upgrade pip setuptools wheel ninja

if ! "${VENV_PYTHON}" -c 'import torch, torchvision' >/dev/null 2>&1; then
  echo "Installing PyTorch for TRELLIS.2 runtime"
  "${VENV_PIP}" install torch==2.11.0 torchvision torchaudio --index-url https://download.pytorch.org/whl/cu130
fi

echo "Installing TRELLIS Python dependencies"
"${VENV_PIP}" install \
  imageio \
  imageio-ffmpeg \
  tqdm \
  easydict \
  opencv-python-headless \
  trimesh \
  transformers \
  gradio==6.0.1 \
  tensorboard \
  pandas \
  lpips \
  zstandard \
  kornia \
  timm \
  psutil \
  plyfile

"${VENV_PIP}" install "git+https://github.com/EasternJournalist/utils3d.git@${UTILS3D_REF}"

if [[ -n "${NYMPHS3D_TRELLIS_CUDA_ARCH_LIST:-}" ]]; then
  export TORCH_CUDA_ARCH_LIST="${NYMPHS3D_TRELLIS_CUDA_ARCH_LIST}"
  echo "Using explicit TRELLIS CUDA arch list: ${TORCH_CUDA_ARCH_LIST}"
elif command -v nvidia-smi >/dev/null 2>&1; then
  detected_cc="$(nvidia-smi --query-gpu=compute_cap --format=csv,noheader | head -n 1 | tr -d '[:space:]')"
  if [[ "${detected_cc}" =~ ^[0-9]+\.[0-9]+$ ]]; then
    export TORCH_CUDA_ARCH_LIST="${detected_cc}"
    echo "Auto-detected TRELLIS CUDA arch list: ${TORCH_CUDA_ARCH_LIST}"
  fi
fi

if [[ -n "${NYMPHS3D_TRELLIS_FLASH_ATTN_CUDA_ARCHS:-}" ]]; then
  export FLASH_ATTN_CUDA_ARCHS="${NYMPHS3D_TRELLIS_FLASH_ATTN_CUDA_ARCHS}"
  echo "Using explicit flash-attn CUDA arch list: ${FLASH_ATTN_CUDA_ARCHS}"
elif [[ -n "${FLASH_ATTN_CUDA_ARCHS:-}" ]]; then
  echo "Using existing flash-attn CUDA arch list: ${FLASH_ATTN_CUDA_ARCHS}"
else
  flash_attn_archs="$(resolve_flash_attn_cuda_archs)"
  if [[ -n "${flash_attn_archs}" ]]; then
    export FLASH_ATTN_CUDA_ARCHS="${flash_attn_archs}"
    echo "Auto-detected flash-attn CUDA arch list: ${FLASH_ATTN_CUDA_ARCHS}"
  else
    echo "Could not auto-detect flash-attn CUDA arch list; flash-attn will use its package defaults."
  fi
fi

case "${INSTALL_FLASH_ATTN}" in
  auto|yes)
    if "${VENV_PYTHON}" -c 'import flash_attn' >/dev/null 2>&1; then
      echo "flash-attn already available."
    else
      flash_attn_jobs="$(resolve_flash_attn_jobs)"
      flash_attn_nvcc_threads="${NYMPHS3D_TRELLIS_FLASH_ATTN_NVCC_THREADS:-1}"
      if [[ ! "${flash_attn_nvcc_threads}" =~ ^[0-9]+$ || "${flash_attn_nvcc_threads}" -lt 1 ]]; then
        echo "Invalid NYMPHS3D_TRELLIS_FLASH_ATTN_NVCC_THREADS value: ${flash_attn_nvcc_threads}"
        exit 1
      fi
      flash_attn_log_dir="${REPO_DIR}/logs"
      mkdir -p "${flash_attn_log_dir}"
      flash_attn_log="${flash_attn_log_dir}/flash-attn-build-$(date +%Y%m%d-%H%M%S).log"
      echo "Installing required flash-attn for TRELLIS.2"
      echo "flash-attn build log: ${flash_attn_log}"
      echo "flash-attn build parallelism: MAX_JOBS=${flash_attn_jobs} CMAKE_BUILD_PARALLEL_LEVEL=${flash_attn_jobs} NVCC_THREADS=${flash_attn_nvcc_threads}"
      echo "flash-attn tuning: set NYMPHS3D_TRELLIS_FLASH_ATTN_MAX_JOBS to force a job count, or tune MAX_AUTO_JOBS/MEM_PER_JOB_GB/RESERVE_GB."
      echo "flash-attn build marker: diagnostics start"
      print_flash_attn_diagnostics | tee -a "${flash_attn_log}"
      echo "flash-attn build marker: diagnostics complete" | tee -a "${flash_attn_log}"
      echo "flash-attn build marker: invoking pip install" | tee -a "${flash_attn_log}"
      set +e
      MAX_JOBS="${flash_attn_jobs}" \
      CMAKE_BUILD_PARALLEL_LEVEL="${flash_attn_jobs}" \
      MAKEFLAGS="-j${flash_attn_jobs}" \
      NINJAFLAGS="-j${flash_attn_jobs}" \
      NVCC_THREADS="${flash_attn_nvcc_threads}" \
      "${VENV_PIP}" install --no-build-isolation flash-attn 2>&1 | tee -a "${flash_attn_log}"
      flash_attn_status=${PIPESTATUS[0]}
      set -e
      echo "flash-attn build marker: pip install returned status ${flash_attn_status}" | tee -a "${flash_attn_log}"
      if [[ ${flash_attn_status} -ne 0 ]]; then
        echo "flash-attn install command exited with status ${flash_attn_status}."
        echo "flash-attn full log preserved at ${flash_attn_log}"
        echo "flash-attn failure tail:"
        tail -n 120 "${flash_attn_log}" || true
        echo "flash-attn installation failed. TRELLIS.2 requires flash-attn for this managed install."
        exit 1
      else
        echo "flash-attn full log preserved at ${flash_attn_log}"
        echo "flash-attn install complete."
      fi
    fi
    ;;
  *)
    echo "Unknown NYMPHS3D_TRELLIS_INSTALL_FLASH_ATTN value: ${INSTALL_FLASH_ATTN}"
    echo "Use one of: auto, yes"
    echo "flash-attn is required for this managed TRELLIS.2 install."
    exit 1
    ;;
esac

echo "Building TRELLIS native runtime extensions"
"${VENV_PIP}" install --no-build-isolation \
  git+https://github.com/JeffreyXiang/CuMesh.git \
  git+https://github.com/JeffreyXiang/FlexGEMM.git \
  git+https://github.com/NVlabs/nvdiffrast.git@v0.4.0

"${VENV_PIP}" install --no-build-isolation --no-deps ./o-voxel

echo "Running TRELLIS adapter entrypoint sanity check"
"${VENV_PYTHON}" scripts/api_server_trellis.py --help >/dev/null

echo
echo "TRELLIS.2 install complete."
