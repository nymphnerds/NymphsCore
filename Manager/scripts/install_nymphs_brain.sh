#!/usr/bin/env bash
set -euo pipefail

INSTALL_ROOT="${HOME}/Nymphs-Brain"
MODEL_ID="auto"
QUANTIZATION="q4_k_m"
CONTEXT_LENGTH="16384"
CONTEXT_SET="0"
DOWNLOAD_MODEL="0"
QUIET="0"

usage() {
  cat <<'EOF'
Usage: install_nymphs_brain.sh [options]

Experimental optional local LLM stack for NymphsCore.

Options:
  --install-root PATH     Install root. Default: $HOME/Nymphs-Brain
  --model MODEL_ID        LM Studio model id, or "auto". Default: auto
  --quant QUANT           Quantization suffix. Default: q4_k_m
  --context N             Context length. Default: 16384
  --download-model        Download the selected model now
  --quiet                 Reduce chatter where possible
  -h, --help              Show this help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --install-root)
      INSTALL_ROOT="${2:?Missing value for --install-root}"
      shift 2
      ;;
    --model)
      MODEL_ID="${2:?Missing value for --model}"
      shift 2
      ;;
    --quant)
      QUANTIZATION="${2:?Missing value for --quant}"
      shift 2
      ;;
    --context)
      CONTEXT_LENGTH="${2:?Missing value for --context}"
      CONTEXT_SET="1"
      shift 2
      ;;
    --download-model)
      DOWNLOAD_MODEL="1"
      shift
      ;;
    --quiet)
      QUIET="1"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ ! "${CONTEXT_LENGTH}" =~ ^[0-9]+$ ]] || [[ "${CONTEXT_LENGTH}" -lt 1024 ]]; then
  echo "Context length must be a number >= 1024." >&2
  exit 2
fi

BIN_DIR="${INSTALL_ROOT}/bin"
VENV_DIR="${INSTALL_ROOT}/venv"
NPM_GLOBAL="${INSTALL_ROOT}/npm-global"
LOCAL_TOOLS_DIR="${INSTALL_ROOT}/local-tools"
LOCAL_BIN_DIR="${LOCAL_TOOLS_DIR}/bin"
LOCAL_NODE_DIR="${LOCAL_TOOLS_DIR}/node"
MCP_VENV_DIR="${INSTALL_ROOT}/mcp-venv"
MODELS_DIR="${INSTALL_ROOT}/models"
LMSTUDIO_DIR="${INSTALL_ROOT}/lmstudio"
MCP_DIR="${INSTALL_ROOT}/mcp"
MCP_CONFIG_DIR="${MCP_DIR}/config"
MCP_DATA_DIR="${MCP_DIR}/data"
MCP_LOG_DIR="${MCP_DIR}/logs"
OPEN_WEBUI_VENV_DIR="${INSTALL_ROOT}/open-webui-venv"
OPEN_WEBUI_DATA_DIR="${INSTALL_ROOT}/open-webui-data"
OPEN_WEBUI_LOG_DIR="${OPEN_WEBUI_DATA_DIR}/logs"
SECRET_DIR="${INSTALL_ROOT}/secrets"
OPEN_WEBUI_HOST="${NYMPHS_BRAIN_OPEN_WEBUI_HOST:-127.0.0.1}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-8081}"
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-127.0.0.1}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-8100}}"
LLM_API_BASE_URL="${NYMPHS_BRAIN_LLM_API_BASE_URL:-http://127.0.0.1:8000/v1}"

export PATH="${BIN_DIR}:${LOCAL_BIN_DIR}:${LOCAL_NODE_DIR}/bin:${NPM_GLOBAL}/bin:${PATH}"

log() {
  if [[ "${QUIET}" != "1" ]]; then
    echo "$@"
  fi
}

python_has_venv() {
  local python_bin="$1"
  "${python_bin}" - <<'PYEOF' >/dev/null 2>&1
import venv
PYEOF
}

select_python_bin() {
  local candidate
  for candidate in python3.11 python3.10 python3; do
    if command -v "${candidate}" >/dev/null 2>&1 && python_has_venv "${candidate}"; then
      command -v "${candidate}"
      return 0
    fi
  done

  return 1
}

add_lmstudio_paths() {
  local candidates=(
    "${LOCAL_NODE_DIR}/bin"
    "${LOCAL_BIN_DIR}"
    "${HOME}/.lmstudio/bin"
    "${HOME}/.cache/lm-studio/bin"
    "${HOME}/.local/bin"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -d "${candidate}" ]]; then
      export PATH="${candidate}:${PATH}"
    fi
  done
}

detect_node_arch() {
  case "$(uname -m)" in
    x86_64) echo "linux-x64" ;;
    aarch64|arm64) echo "linux-arm64" ;;
    *)
      echo "Unsupported architecture for local Node bootstrap: $(uname -m)" >&2
      return 1
      ;;
  esac
}

ensure_local_node() {
  if command -v node >/dev/null 2>&1 && command -v npm >/dev/null 2>&1; then
    return 0
  fi

  local node_arch
  local node_version="20.19.0"
  local node_archive=""
  local node_url=""
  local temp_extract_dir=""
  local tarball=""

  node_arch="$(detect_node_arch)"
  node_archive="node-v${node_version}-${node_arch}"
  node_url="https://nodejs.org/dist/v${node_version}/${node_archive}.tar.xz"
  temp_extract_dir="$(mktemp -d)"
  tarball="${temp_extract_dir}/${node_archive}.tar.xz"

  echo "Installing local Node.js runtime (${node_version}, ${node_arch})..."
  curl -fsSL "${node_url}" -o "${tarball}"
  rm -rf "${LOCAL_NODE_DIR}"
  mkdir -p "${LOCAL_TOOLS_DIR}"
  tar -xf "${tarball}" -C "${temp_extract_dir}"
  mv "${temp_extract_dir}/${node_archive}" "${LOCAL_NODE_DIR}"
  rm -rf "${temp_extract_dir}"
  export PATH="${LOCAL_NODE_DIR}/bin:${PATH}"
}

detect_installation_type() {
  local lms_start="${INSTALL_ROOT}/bin/lms-start"
  
  if [[ ! -f "$lms_start" ]]; then
    echo "new"
    return
  fi
  
  # Check if it's using LM Studio server (port 1234 or "lms serve")
  if grep -qE "(localhost:1234|127\.0\.0\.1:1234|lms\s+server\s+(start|stop))" "$lms_start"; then
    echo "lmstudio-migrate"
  elif grep -q "llama-server.*--port 8000" "$lms_start"; then
    echo "existing-llama"
  elif grep -q "curl.*127\.0\.0\.1:8000" "$lms_start"; then
    echo "existing-llama"
  else
    echo "unknown"
  fi
}

migrate_from_lmstudio() {
  echo ""
  echo "============================================================"
  echo "DETECTED EXISTING LM STUDIO INSTALLATION (port 1234)"
  echo "============================================================"
  echo ""
  echo "Migrating to llama-server hybrid architecture..."
  echo ""
  
  # Stop any running LM Studio server first
  if [[ -f "${INSTALL_ROOT}/logs/lms.pid" ]]; then
    local old_pid
    old_pid=$(cat "${INSTALL_ROOT}/logs/lms.pid" 2>/dev/null || true)
    if [[ -n "$old_pid" ]] && kill -0 "$old_pid" >/dev/null 2>&1; then
      echo "Stopping existing LM Studio server (PID ${old_pid})..."
      kill "$old_pid" >/dev/null 2>&1 || true
      sleep 2
    fi
  fi
  
  # Also try pkill for LM Studio processes
  pkill -f "lms.*server" 2>/dev/null || true
  
  # Backup old scripts
  local backup_dir="${INSTALL_ROOT}/bin/backup-lmstudio-$(date +%Y%m%d-%H%M%S)"
  mkdir -p "${backup_dir}" 2>/dev/null || true
  
  for script in lms-start lms-stop brain-status open-webui-start nymph-agent.py; do
    if [[ -f "${BIN_DIR}/${script}" ]]; then
      cp "${BIN_DIR}/${script}" "${backup_dir}/" 2>/dev/null || true
    fi
  done
  
  if [[ -d "${backup_dir}" ]] && [[ "$(ls -A "${backup_dir}" 2>/dev/null)" ]]; then
    echo "Backed up old LM Studio scripts to: ${backup_dir}"
  fi
  
  # Update nymph-agent.py if it exists and references port 1234
  local agent_path="${INSTALL_ROOT}/nymph-agent.py"
  if [[ -f "$agent_path" ]]; then
    if grep -q "127.0.0.1:1234\|localhost:1234" "$agent_path"; then
      echo "Updating nymph-agent.py to use llama-server (port 8000)..."
      sed -i 's|http://127\.0\.0\.1:1234|http://127.0.0.1:8000|g' "$agent_path"
      sed -i 's|http://localhost:1234|http://localhost:8000|g' "$agent_path"
    fi
  fi
  
  echo ""
  echo "Migration preparation complete. Installing llama-server scripts..."
  echo ""
}

detect_vram_mb() {
  # Check if Manager passed the actual GPU VRAM from Windows
  local vram_env="${NYMPHS3D_GPU_VRAM_MB:-0}"
  if [[ -n "${vram_env}" && "${vram_env}" =~ ^[0-9]+$ ]]; then
    echo "${vram_env}"
    return
  fi

  # Fall back to local nvidia-smi detection (may be limited by WSL memory config)
  local smi_cmd=""
  if command -v nvidia-smi >/dev/null 2>&1; then
    smi_cmd="nvidia-smi"
  elif command -v nvidia-smi.exe >/dev/null 2>&1; then
    smi_cmd="nvidia-smi.exe"
  elif [[ -x "/mnt/c/Windows/System32/nvidia-smi.exe" ]]; then
    smi_cmd="/mnt/c/Windows/System32/nvidia-smi.exe"
  fi

  if [[ -z "${smi_cmd}" ]]; then
    echo 0
    return
  fi

  "${smi_cmd}" --query-gpu=memory.total --format=csv,noheader,nounits 2>/dev/null |
    head -n 1 |
    tr -d '\r ' |
    awk '{print int($1)}'
}

choose_auto_model() {
  local vram_mb="$1"
  if [[ "${vram_mb}" -ge 32000 ]]; then
    MODEL_ID="qwen/qwen2.5-coder-32b"
    if [[ "${CONTEXT_SET}" != "1" ]]; then CONTEXT_LENGTH="32768"; fi
  elif [[ "${vram_mb}" -ge 20000 ]]; then
    MODEL_ID="qwen/qwen3-30b-a3b"
    if [[ "${CONTEXT_SET}" != "1" ]]; then CONTEXT_LENGTH="32768"; fi
  elif [[ "${vram_mb}" -ge 14000 ]]; then
    MODEL_ID="qwen/qwen2.5-coder-14b"
    if [[ "${CONTEXT_SET}" != "1" ]]; then CONTEXT_LENGTH="16384"; fi
  else
    MODEL_ID="qwen/qwen3-1.7b"
    if [[ "${CONTEXT_SET}" != "1" ]]; then CONTEXT_LENGTH="8192"; fi
  fi
}

ensure_secret_file() {
  local path="$1"
  local bytes="$2"

  if [[ -s "${path}" ]]; then
    chmod 600 "${path}" || true
    return 0
  fi

  "${PYTHON_BIN}" - "${path}" "${bytes}" <<'PYEOF'
import secrets
import sys
from pathlib import Path

path = Path(sys.argv[1])
bytes_len = int(sys.argv[2])
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(secrets.token_urlsafe(bytes_len) + "\n", encoding="utf-8")
PYEOF
  chmod 600 "${path}" || true
}

ensure_python_venv() {
  local venv_dir="$1"
  local label="$2"

  if [[ ! -x "${venv_dir}/bin/python3" ]] || [[ ! -x "${venv_dir}/bin/pip" ]]; then
    if [[ -d "${venv_dir}" ]]; then
      echo "Removing incomplete ${label} venv at ${venv_dir}"
      rm -rf "${venv_dir}"
    fi
    echo "Creating ${label} venv at ${venv_dir}"
    "${PYTHON_BIN}" -m venv "${venv_dir}"
  fi
}

echo "Nymphs-Brain experimental installer"
echo "Install root: ${INSTALL_ROOT}"

# Create directories including new models and lmstudio directories
mkdir -p \
  "${BIN_DIR}" \
  "${VENV_DIR}" \
  "${NPM_GLOBAL}" \
  "${LOCAL_TOOLS_DIR}" \
  "${LOCAL_BIN_DIR}" \
  "${MODELS_DIR}" \
  "${LMSTUDIO_DIR}" \
  "${MCP_CONFIG_DIR}" \
  "${MCP_DATA_DIR}" \
  "${MCP_LOG_DIR}" \
  "${OPEN_WEBUI_DATA_DIR}" \
  "${OPEN_WEBUI_LOG_DIR}" \
  "${SECRET_DIR}"

# Migrate existing models from old locations to new centralized models directory
# Note: We do NOT remove .lmstudio here as it will be a symlink created later
migrate_existing_models() {
  local source_paths=(
    "${HOME}/.cache/lm-studio/models"
  )
  
  for src in "${source_paths[@]}"; do
    if [[ -d "$src" ]] && [[ "$(ls -A "$src" 2>/dev/null)" ]]; then
      echo "Migrating existing models from ${src} to ${MODELS_DIR}..."
      cp -r "$src/"* "${MODELS_DIR}/" 2>/dev/null || true
    fi
  done
  
  # Only remove .cache/lm-studio if it's not our symlink target and contains only models
  if [[ -d "${HOME}/.cache/lm-studio" && ! -L "${HOME}/.lmstudio" ]]; then
    local cache_content
    cache_content="$(ls -A "${HOME}/.cache/lm-studio" 2>/dev/null || true)"
    if [[ "$cache_content" == "models" ]]; then
      echo "Cleaning up old .cache/lm-studio directory..."
      rm -rf "${HOME}/.cache/lm-studio"
    fi
  fi
  
  # Remove any existing lms CLI from user's local bin (will be reinstalled via ~/.lmstudio symlink)
  if [[ -x "${HOME}/.local/bin/lms" ]]; then
    echo "Removing old lms CLI from ~/.local/bin..."
    rm -f "${HOME}/.local/bin/lms"
  fi
}

migrate_existing_models

if [[ "${MODEL_ID}" == "auto" ]]; then
  VRAM_MB="$(detect_vram_mb)"
  choose_auto_model "${VRAM_MB:-0}"
  echo "Auto model selected from detected VRAM (${VRAM_MB:-0} MB): ${MODEL_ID}"
fi

DL_TARGET="${MODEL_ID}@${QUANTIZATION}"

export DEBIAN_FRONTEND=noninteractive
MISSING_CRITICAL=()
for dep in curl tar unzip; do
  if ! command -v "${dep}" >/dev/null 2>&1; then
    MISSING_CRITICAL+=("${dep}")
  fi
done
PYTHON_BIN=""
if ! PYTHON_BIN="$(select_python_bin)"; then
  MISSING_CRITICAL+=("python3.11-venv or python3.10-venv or python3-venv")
fi
if [[ "${#MISSING_CRITICAL[@]}" -gt 0 ]]; then
  echo "ERROR: Missing critical dependencies: ${MISSING_CRITICAL[*]}"
  echo "These must be in the base distro."
  exit 1
fi

ensure_local_node

log "Zero-sudo dependency check complete."

ensure_python_venv "${VENV_DIR}" "Nymphs-Brain"

"${VENV_DIR}/bin/pip" install --upgrade pip requests huggingface_hub

ensure_python_venv "${MCP_VENV_DIR}" "Nymphs-Brain MCP"
"${MCP_VENV_DIR}/bin/pip" install --upgrade pip mcp-proxy web-forager

ensure_python_venv "${OPEN_WEBUI_VENV_DIR}" "Open WebUI"
"${OPEN_WEBUI_VENV_DIR}/bin/pip" install --upgrade pip open-webui

  export npm_config_prefix="${NPM_GLOBAL}"
  
  # Set up LM Studio to use our custom directory
  export LMSTUDIO_HOME="${LMSTUDIO_DIR}"
  
  # Create symlink from ~/.lmstudio to our install directory
  # This ensures the lms CLI stores data in ${INSTALL_ROOT}/lmstudio
  if [[ -L "$HOME/.lmstudio" ]]; then
    # Symlink already exists, remove and recreate to ensure it points to correct location
    rm "$HOME/.lmstudio"
  elif [[ -e "$HOME/.lmstudio" ]]; then
    # Regular file or directory exists, remove it
    rm -rf "$HOME/.lmstudio"
  fi
  
  mkdir -p "${LMSTUDIO_DIR}"
  ln -s "${LMSTUDIO_DIR}" "$HOME/.lmstudio"
  
  echo "Installing/updating LM Studio CLI into ${LMSTUDIO_DIR}..."
  curl -fsSL https://lmstudio.ai/install.sh | bash
  
  # Create symlink so LM Studio sees models in our centralized directory
  # ~/.lmstudio/models → ~/Nymphs-Brain/models
  # This way 'lms get' downloads to our centralized models directory
  mkdir -p "${MODELS_DIR}"
  if [[ -e "$HOME/.lmstudio/models" ]] || [[ -L "$HOME/.lmstudio/models" ]]; then
    rm -rf "$HOME/.lmstudio/models"
  fi
  ln -s "${MODELS_DIR}" "$HOME/.lmstudio/models"
  echo "Linked LM Studio models directory: ~/.lmstudio/models → ${MODELS_DIR}"

# Add our lmstudio bin to PATH
export PATH="${LMSTUDIO_DIR}/bin:${PATH}"

# Detect installation type and handle migration if needed
INSTALL_TYPE=$(detect_installation_type)

case "${INSTALL_TYPE}" in
  "lmstudio-migrate")
    migrate_from_lmstudio
    ;;
  "existing-llama")
    echo ""
    echo "Detected existing llama-server installation, updating..."
    echo ""
    ;;
  "new")
    echo ""
    echo "New installation detected."
    echo ""
    ;;
  "unknown")
    echo ""
    echo "Unknown installation type detected, proceeding with update..."
    echo ""
    ;;
esac

# Install llama-server with CUDA support
echo "Setting up llama-server with CUDA support..."

# Check if already installed from previous build (skip rebuild if binary AND all .so libs present)
if [[ -x "${LOCAL_BIN_DIR}/llama-server" ]] && [[ -f "${LOCAL_BIN_DIR}/libllama-common.so.0" ]] && [[ -f "${LOCAL_BIN_DIR}/libggml-cuda.so.0" ]] && [[ -f "${LOCAL_BIN_DIR}/libggml.so.0" ]]; then
    echo "llama-server already installed at ${LOCAL_BIN_DIR}/llama-server"
else
    if [[ -x "${LOCAL_BIN_DIR}/llama-server" ]]; then
        echo "llama-server binary exists but shared libraries are incomplete, rebuilding..."
    fi
    
    # Need to build from source
    if ! command -v cmake >/dev/null 2>&1; then
        echo "ERROR: cmake is required to build llama-server but not found." >&2
        echo "Please install cmake and try again:" >&2
        echo "  sudo apt-get install cmake" >&2
        exit 1
    fi
    
    LLAMA_CPP_TEMP=""

cleanup_llama_build() {
    if [[ -n "${LLAMA_CPP_TEMP}" && -d "${LLAMA_CPP_TEMP}" ]]; then
        rm -rf "${LLAMA_CPP_TEMP}" 2>/dev/null || true
    fi
}
trap cleanup_llama_build EXIT
    
    echo "cmake detected, building llama-server from source..."
    
    # Only use native Linux CUDA installations at /usr/local/cuda-* (not Windows-mounted /mnt/c paths)
    CUDA_TOOLKIT_ROOT_DIR=""
    
    # Check native Ubuntu/WSL CUDA paths in version order
    for cuda_path in "/usr/local/cuda-13.0" "/usr/local/cuda-12.8" "/usr/local/cuda-12.6" "/usr/local/cuda-12.5" \
                     "/usr/local/cuda-12" "/usr/local/cuda-11.8" "/usr/local/cuda-11" "/usr/local/cuda"; do
        if [[ -d "${cuda_path}" && -x "${cuda_path}/bin/nvcc" ]]; then
            # Verify it's a native ELF binary (not Windows exe via /mnt)
            if file "${cuda_path}/bin/nvcc" 2>/dev/null | grep -q "ELF"; then
                CUDA_TOOLKIT_ROOT_DIR="${cuda_path}"
                break
            fi
        fi
    done
    
    if [[ -n "${CUDA_TOOLKIT_ROOT_DIR}" ]]; then
        echo "Found native CUDA toolkit at: ${CUDA_TOOLKIT_ROOT_DIR}"
        export CUDA_TOOLKIT_ROOT_DIR
        
        NVCC_PATH="${CUDA_TOOLKIT_ROOT_DIR}/bin/nvcc"
        
        # Export the full path to cudart.so (required by cmake)
        if [[ -f "${CUDA_TOOLKIT_ROOT_DIR}/lib64/libcudart.so" ]]; then
            export CUDA_CUDART_LIBRARY="${CUDA_TOOLKIT_ROOT_DIR}/lib64/libcudart.so"
            export CUDA_CUDA_LIBRARY="${CUDA_TOOLKIT_ROOT_DIR}/lib64/libcudart.so"
        else
            echo "WARNING: libcudart.so not found in ${CUDA_TOOLKIT_ROOT_DIR}/lib64"
            CUDA_TOOLKIT_ROOT_DIR=""
        fi
    else
        echo "No native CUDA toolkit found at /usr/local/cuda-*, using bundled archive..."
    fi
    
    if [[ -n "${CUDA_TOOLKIT_ROOT_DIR}" && -d "${CUDA_TOOLKIT_ROOT_DIR}" ]]; then
        # Create temp directory for build
        LLAMA_CPP_TEMP=$(mktemp -d)
        cd "${LLAMA_CPP_TEMP}"
        
        echo "Cloning llama.cpp repository..."
        git clone --depth 1 https://github.com/ggml-org/llama.cpp.git .
        
        echo "Configuring build with CUDA (using GGML_CUDA)..."
        
        # Set environment variables to force cmake to use native CUDA only
        export CMAKE_PREFIX_PATH="${CUDA_TOOLKIT_ROOT_DIR}"
        export CMAKE_LIBRARY_PATH="${CUDA_TOOLKIT_ROOT_DIR}/lib64"
        export LD_LIBRARY_PATH="${CUDA_TOOLKIT_ROOT_DIR}/lib64:${LD_LIBRARY_PATH:-}"
        export CUDA_PATH="${CUDA_TOOLKIT_ROOT_DIR}"
        export NVCC="$(dirname "${NVCC_PATH}")/nvcc"
        
        # Create a CMake toolchain file to override autodetection
        cat > "${LLAMA_CPP_TEMP}/toolchain.cmake" <<TOOLCHEOF
# Force native Linux CUDA only, block Windows paths in WSL
set(CMAKE_SYSTEM_NAME Linux)
set(CUDA_TOOLKIT_ROOT_DIR "${CUDA_TOOLKIT_ROOT_DIR}" CACHE PATH "CUDA Toolkit Root" FORCE)
set(CUDA_PATH "${CUDA_TOOLKIT_ROOT_DIR}" CACHE PATH "CUDA Path" FORCE)
set(CMAKE_CUDA_COMPILER "${NVCC_PATH}" CACHE FILEPATH "CUDA Compiler" FORCE)
set(CUDA_CUDART_LIBRARY "${CUDA_CUDART_LIBRARY}" CACHE FILEPATH "CUDA Runtime Library" FORCE)
set(CUDA_CUDA_LIBRARY "${CUDA_CUDART_LIBRARY}" CACHE FILEPATH "CUDA Library" FORCE)
set(CUDA_LIBRARIES "${CUDA_CUDART_LIBRARY}" CACHE FILEPATH "CUDA Libraries" FORCE)
set(CUDA_INCLUDE_DIRS "${CUDA_TOOLKIT_ROOT_DIR}/include" CACHE PATH "CUDA Include Dirs" FORCE)
# Set search modes to only find in specified paths, not elsewhere
set(CMAKE_FIND_ROOT_PATH "${CUDA_TOOLKIT_ROOT_DIR}")
set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
TOOLCHEOF
        
        echo "cmake configuration with native CUDA ${CUDA_TOOLKIT_ROOT_DIR}..."
        
        cmake -B build \
            -DCMAKE_TOOLCHAIN_FILE:FILEPATH="${LLAMA_CPP_TEMP}/toolchain.cmake" \
            -DGGML_CUDA=ON \
            -DLLAMA_CUDA=OFF \
            -DGGML_NATIVE=ON \
            -DGPU_ARCHS="70;75;80;86;89;90" \
            -DCMAKE_BUILD_TYPE=Release 2>&1 | tee "${LLAMA_CPP_TEMP}/cmake_output.log"
        
        CMAKE_EXIT_CODE=${PIPESTATUS[0]}
        
        # Verify cmake found our native CUDA
        if [[ -f build/CMakeCache.txt ]]; then
            echo ""
            echo "Checking CMakeCache.txt for CUDA configuration..."
            grep -E "(CUDA_TOOLKIT_ROOT_DIR|CMAKE_CUDA_COMPILER|CUDA_CUDART_LIBRARY)" build/CMakeCache.txt || true
        fi
        
        # Check if we used the correct native CUDA
        if grep -q '/usr/local/cuda-13.0' build/CMakeCache.txt 2>/dev/null; then
            echo "SUCCESS: Using native CUDA from /usr/local/cuda-13.0"
        else
            echo "WARNING: Native CUDA may not have been used correctly."
            echo "cmake output:"
            cat "${LLAMA_CPP_TEMP}/cmake_output.log" 2>/dev/null | tail -20 || true
        fi
        
        echo "Building llama-server (this may take a few minutes)..."
        cmake --build build --config Release -j$(nproc)
        BUILD_RESULT=$?
        
        if [[ ${BUILD_RESULT} -ne 0 ]] || [[ ! -f "build/bin/llama-server" ]]; then
            echo "ERROR: Failed to build llama-server from source." >&2
            exit 1
        fi
        
        mkdir -p "${LOCAL_BIN_DIR}"
        cp build/bin/llama-server "${LOCAL_BIN_DIR}/llama-server"
        chmod +x "${LOCAL_BIN_DIR}/llama-server"
        
        # Copy all required shared libraries (use wildcard to catch all .so files)
        for lib in build/bin/*.so*; do
            if [[ -f "${lib}" ]]; then
                cp "${lib}" "${LOCAL_BIN_DIR}/"
                echo "  Copied: $(basename "${lib}")"
            fi
        done
        
        # Fix RPATH on all binaries and shared libraries so they find each other
        # at runtime regardless of where the temp build dir was
        if ! command -v patchelf >/dev/null 2>&1; then
            echo "patchelf not found, installing..."
            sudo apt-get install -y patchelf >/dev/null 2>&1 || true
        fi
        
        if command -v patchelf >/dev/null 2>&1; then
            for lib in "${LOCAL_BIN_DIR}"/*.so* "${LOCAL_BIN_DIR}"/llama-server; do
                if [[ -f "${lib}" ]]; then
                    patchelf --set-rpath "\$ORIGIN" "${lib}" 2>/dev/null || true
                fi
            done
        fi
        
        echo "llama-server built and installed to ${LOCAL_BIN_DIR}/llama-server"
        
        # Cleanup build directory
        LLAMA_CPP_TEMP=""
        trap - EXIT
        cd - >/dev/null || true
    else
        echo "ERROR: No native CUDA toolkit found at /usr/local/cuda-*" >&2
        echo "Please install NVIDIA CUDA Toolkit and try again." >&2
        exit 1
    fi
fi

echo "Installing MCP helper packages into ${NPM_GLOBAL}"
npm install -g --prefix="${NPM_GLOBAL}" \
  @modelcontextprotocol/server-filesystem \
  @modelcontextprotocol/server-memory

ensure_secret_file "${SECRET_DIR}/webui-secret-key" 32
WEBUI_SECRET_KEY="$(tr -d '\r\n' < "${SECRET_DIR}/webui-secret-key")"

cat > "${MCP_CONFIG_DIR}/mcp-proxy-servers.json" <<EOF
{
  "mcpServers": {
    "filesystem": {
      "command": "${LOCAL_NODE_DIR}/bin/node",
      "args": [
        "${NPM_GLOBAL}/lib/node_modules/@modelcontextprotocol/server-filesystem/dist/index.js",
        "${HOME}",
        "${INSTALL_ROOT}",
        "/opt/nymphs3d/Nymphs3D"
      ]
    },
    "memory": {
      "command": "${LOCAL_NODE_DIR}/bin/node",
      "args": [
        "${NPM_GLOBAL}/lib/node_modules/@modelcontextprotocol/server-memory/dist/index.js"
      ],
      "env": {
        "MEMORY_FILE_PATH": "${MCP_DATA_DIR}/memory.jsonl"
      }
    },
    "web-forager": {
      "command": "${MCP_VENV_DIR}/bin/web-forager",
      "args": ["serve"]
    }
  }
}
EOF

cat > "${MCP_CONFIG_DIR}/cline-mcp-settings.json" <<EOF
{
  "mcpServers": {
    "filesystem": {
      "url": "http://${MCP_HOST}:${MCP_PORT}/servers/filesystem/mcp",
      "type": "streamableHttp",
      "disabled": false,
      "timeout": 60
    },
    "memory": {
      "url": "http://${MCP_HOST}:${MCP_PORT}/servers/memory/mcp",
      "type": "streamableHttp",
      "disabled": false,
      "timeout": 60
    },
    "web-forager": {
      "url": "http://${MCP_HOST}:${MCP_PORT}/servers/web-forager/mcp",
      "type": "streamableHttp",
      "disabled": false,
      "timeout": 60
    }
  }
}
EOF

cat > "${MCP_CONFIG_DIR}/open-webui-mcp-servers.md" <<EOF
Nymphs-Brain MCP servers for Open WebUI

Add these in Admin Settings -> External Tools
Type: MCP (Streamable HTTP)
Auth: None

- Filesystem: http://${MCP_HOST}:${MCP_PORT}/servers/filesystem/mcp
- Memory: http://${MCP_HOST}:${MCP_PORT}/servers/memory/mcp
- Web Forager: http://${MCP_HOST}:${MCP_PORT}/servers/web-forager/mcp

Notes
- Open WebUI default URL: http://localhost:${OPEN_WEBUI_PORT}
- These endpoints bind to localhost only.
- Cline can use the same endpoints with transport type streamableHttp.
EOF

if [[ "${DOWNLOAD_MODEL}" == "1" ]]; then
  if ! command -v lms >/dev/null 2>&1; then
    echo "LM Studio CLI command 'lms' was not found after install." >&2
    echo "Open a new shell or check the LM Studio CLI install output, then rerun this script." >&2
    exit 1
  fi
  echo "Downloading model: ${DL_TARGET}"
  lms get "${DL_TARGET}" --yes
  
  # Stop any LMS server/daemon that may have started during download using proper CLI commands
  echo ""
  echo "Stopping any running LMS daemons..."
  lms server stop 2>/dev/null || true
  lms daemon down 2>/dev/null || true
  
  echo ""
  echo "Model downloaded successfully."
else
  echo "Skipping model download. The wrapper is configured for ${DL_TARGET}."
fi

cat > "${INSTALL_ROOT}/nymph-agent.py" <<'PYEOF'
import requests

URL = "http://127.0.0.1:8000/v1/chat/completions"
MODEL = "__MODEL_ID__"


def call(messages):
    try:
        response = requests.post(
            URL,
            json={"model": MODEL, "messages": messages},
            timeout=120,
        )
        response.raise_for_status()
        return response.json()["choices"][0]["message"]["content"]
    except Exception as exc:
        return f"Error: {exc}"


history = [
    {
        "role": "system",
        "content": "You are Nymphs-Brain, an experimental local assistant for NymphsCore (llama-server backend).",
    }
]

print("\033[92mNymphs-Brain Agent Active (llama-server on port 8000).\033[0m")
while True:
    try:
        user = input("task> ")
        if user.lower() in {"exit", "quit"}:
            break
        history.append({"role": "user", "content": user})
        output = call(history)
        print(f"AI: {output}")
        history.append({"role": "assistant", "content": output})
    except KeyboardInterrupt:
        break
PYEOF

cat > "${BIN_DIR}/lms-start" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
export PATH="${INSTALL_ROOT}/bin:${INSTALL_ROOT}/local-tools/bin:${INSTALL_ROOT}/local-tools/node/bin:${INSTALL_ROOT}/npm-global/bin:${PATH}"

# Add LM Studio paths for model management tools
for candidate in "${HOME}/.lmstudio/bin" "${HOME}/.cache/lm-studio/bin" "${HOME}/.local/bin"; do
  if [[ -d "${candidate}" ]]; then
    export PATH="${candidate}:${PATH}"
  fi
done

# llama-server path
LLAMA_SERVER="${INSTALL_ROOT}/local-tools/bin/llama-server"

# Model configuration (updated by lms-model script)
MODEL_KEY="__MODEL_ID__"
CONTEXT_LENGTH="__CONTEXT_LENGTH__"

PID_FILE="${INSTALL_ROOT}/logs/lms.pid"
LOG_FILE="${INSTALL_ROOT}/logs/lms.log"

mkdir -p "${INSTALL_ROOT}/logs"

# Stop any existing llama-server instance
if [[ -f "${PID_FILE}" ]]; then
  old_pid="$(cat "${PID_FILE}")" 2>/dev/null || true
  if kill -0 "$old_pid" >/dev/null 2>&1; then
    echo "Stopping existing llama-server (PID ${old_pid})..."
    kill "$old_pid" >/dev/null 2>&1 || true
    sleep 2
  fi
fi

# Find the actual .gguf file path from our centralized models directory
LMSTUDIO_DIR="${INSTALL_ROOT}/lmstudio"
MODELS_DIR="${INSTALL_ROOT}/models"

find_gguf_path() {
  local model_key="$1"
  
  # Check all possible model locations in priority order
  local search_paths=(
    "${MODELS_DIR}"                    # Centralized models directory
    "${LMSTUDIO_DIR}/models"           # LM Studio's default location
    "${HOME}/.cache/lm-studio/models"  # Legacy location
    "${HOME}/.lmstudio/models"         # Another legacy location
  )
  
  for base_path in "${search_paths[@]}"; do
    if [[ -d "$base_path" ]]; then
      gguf_file=$(find "$base_path" -name "*.gguf" -type f 2>/dev/null | \
                  grep -i "$model_key" | head -n1)
      if [[ -n "$gguf_file" && -f "$gguf_file" ]]; then
        echo "$gguf_file"
        return 0
      fi
    fi
  done
  
  echo ""
}

GGUF_PATH="$(find_gguf_path "${MODEL_KEY}")"

if [[ -z "${GGUF_PATH}" || ! -f "${GGUF_PATH}" ]]; then
  echo "ERROR: Could not find GGUF file for model '${MODEL_KEY}'" >&2
  echo "Use 'lms-model' to download/manage models via LM Studio:" >&2
  echo "  ${SCRIPT_DIR}/lms-model" >&2
  exit 1
fi

echo "Starting llama-server..."
echo "  Model: ${MODEL_KEY}"
echo "  GGUF Path: ${GGUF_PATH}"
echo "  Context Length: ${CONTEXT_LENGTH}"
echo ""

# Ensure shared libraries can be found (fallback if RPATH was not fixed by patchelf)
export LD_LIBRARY_PATH="$(dirname "${LLAMA_SERVER}"):${LD_LIBRARY_PATH:-}"

# Start llama-server with CUDA acceleration
nohup "${LLAMA_SERVER}" \
    -m "${GGUF_PATH}" \
    -c "${CONTEXT_LENGTH}" \
    -ngl 9999 \
    --port 8000 \
    --host "127.0.0.1" \
    --flash-attn on \
    --parallel 4 \
    -ctk q8_0 \
    -ctv q8_0 \
    > "${LOG_FILE}" 2>&1 &

echo "$!" > "${PID_FILE}"

# Health check
echo "Waiting for llama-server to be ready..."
for i in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:8000/v1/models" >/dev/null 2>&1; then
    echo "llama-server is ready on port 8000"
    echo ""
    echo "API endpoints:"
    echo "  Models:     http://localhost:8000/v1/models"
    echo "  Chat API:   http://localhost:8000/v1/chat/completions"
    echo ""
    echo "Log file: ${LOG_FILE}"
    exit 0
  fi
  sleep 1
done

echo "ERROR: llama-server did not become ready in time." >&2
echo "Check log file: ${LOG_FILE}" >&2
exit 1
WRAPEOF

cat > "${BIN_DIR}/lms-stop" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"

PID_FILE="${INSTALL_ROOT}/logs/lms.pid"

echo "Stopping llama-server..."

if [[ -f "${PID_FILE}" ]]; then
  pid="$(cat "${PID_FILE}")" 2>/dev/null || true
  if [[ -n "$pid" ]] && kill -0 "$pid" >/dev/null 2>&1; then
    echo "  Killing process ${pid}..."
    kill "$pid" >/dev/null 2>&1 || true
    for i in $(seq 1 10); do
      if ! kill -0 "$pid" >/dev/null 2>&1; then
        break
      fi
      sleep 1
    done
    if kill -0 "$pid" >/dev/null 2>&1; then
      echo "  Force killing process ${pid}..."
      kill -9 "$pid" >/dev/null 2>&1 || true
    fi
  fi
  rm -f "${PID_FILE}"
else
  pkill -f "llama-server.*--port 8000" 2>/dev/null || true
fi

echo "llama-server stopped."
echo "LM Studio remains available for model management via: ${SCRIPT_DIR}/lms-model"
WRAPEOF

cat > "${BIN_DIR}/lms-status" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
PID_FILE="${INSTALL_ROOT}/logs/lms.pid"
LOG_FILE="${INSTALL_ROOT}/logs/lms.log"

echo "llama-server status:"

if [[ -f "${PID_FILE}" ]]; then
  pid="$(cat "${PID_FILE}")" 2>/dev/null || echo "unknown"
  if kill -0 "$pid" >/dev/null 2>&1; then
    echo "  Process: running (PID ${pid})"
  else
    echo "  Process: stopped (stale PID file)"
  fi
else
  echo "  Process: no PID file found"
fi

if curl -fsS "http://127.0.0.1:8000/v1/models" >/dev/null 2>&1; then
  echo "  API: responding on port 8000"
  
  MODEL_INFO=$(curl -fsS "http://127.0.0.1:8000/v1/models" 2>/dev/null | \
               python3 -c "import sys,json; d=json.load(sys.stdin); models=d.get('data',[]); print(models[0]['id'] if models else 'none')" 2>/dev/null || echo "unknown")
  echo "  Model loaded: ${MODEL_INFO}"
else
  echo "  API: not responding"
fi

echo ""
echo "Log file: ${LOG_FILE}"
echo "Last 5 lines of log:"
if [[ -f "${LOG_FILE}" ]]; then
  tail -n 5 "${LOG_FILE}" | sed 's/^/  /'
else
  echo "  (no log file found)"
fi
WRAPEOF

cat > "${BIN_DIR}/lms-model" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"

python_json() {
  if [[ -x "${INSTALL_ROOT}/venv/bin/python3" ]]; then
    "${INSTALL_ROOT}/venv/bin/python3" "$@"
  else
    python3 "$@"
  fi
}

export PATH="${INSTALL_ROOT}/bin:${INSTALL_ROOT}/local-tools/bin:${INSTALL_ROOT}/local-tools/node/bin:${INSTALL_ROOT}/npm-global/bin:${PATH}"
for candidate in "${HOME}/.lmstudio/bin" "${HOME}/.cache/lm-studio/bin" "${HOME}/.local/bin"; do
  if [[ -d "${candidate}" ]]; then
    export PATH="${candidate}:${PATH}"
  fi
done

LMS_BIN="$(command -v lms || true)"

declare -A CONTEXT_SIZES=(
  ["1"]="4096"
  ["2"]="8192"
  ["3"]="16384"
  ["4"]="32768"
  ["5"]="65536"
  ["6"]="128000"
)

CONTEXT_LABELS=(
  "4k   (4096)"
  "8k   (8192)"
  "16k  (16384)"
  "32k  (32768)"
  "64k  (65536)"
  "128k (128000)"
)

SELECTED_CONTEXT_SIZE=""
SELECTED_MODEL_KEY=""

json_model_keys() {
  python_json -c '
import json
import sys

try:
    data = json.load(sys.stdin)
except Exception:
    data = []

if isinstance(data, dict):
    for key in ("models", "llms", "data"):
        if isinstance(data.get(key), list):
            data = data[key]
            break
    else:
        data = []

for item in data if isinstance(data, list) else []:
    if isinstance(item, dict):
        model_key = item.get("modelKey") or item.get("key") or item.get("id")
        if model_key:
            print(model_key)
'
}

lms_model_keys() {
  local model_json
  model_json="$("${LMS_BIN}" ls --llm --json 2>/dev/null || printf '[]')"
  printf '%s' "${model_json}" | json_model_keys
}

ensure_lms() {
  if [[ -z "${LMS_BIN:-}" ]] || [[ ! -x "${LMS_BIN}" ]]; then
    echo "LM Studio CLI command 'lms' was not found." >&2
    echo "Rerun install_nymphs_brain.sh or check the LM Studio CLI installation." >&2
    exit 1
  fi
}

check_model_downloaded() {
  local model_key="$1"
  # The lms ls command will only show downloaded models, so if we find it, it's ready
  local model_list
  model_list="$("${LMS_BIN}" ls --llm --json 2>/dev/null || printf '[]')"
  echo "$model_list" | python_json -c "
import json, sys
data = json.load(sys.stdin)
target = '${1}'
if isinstance(data, dict):
    for key in ('models', 'llms', 'data'):
        if isinstance(data.get(key), list):
            data = data[key]
            break
else:
    data = []
found = any(
    isinstance(item, dict) and str(item.get('modelKey') or item.get('key') or item.get('id')) == target
    for item in data if isinstance(data, list)
)
sys.exit(0 if found else 1)
" 2>/dev/null
}

select_context_size() {
  local choice

  while true; do
    echo
    echo "Select context size:"
    for i in "${!CONTEXT_LABELS[@]}"; do
      printf "  %d) %s\n" "$((i + 1))" "${CONTEXT_LABELS[i]}"
    done
    echo
    read -rp "Enter your choice (1-6): " choice

    if [[ -n "${CONTEXT_SIZES[$choice]:-}" ]]; then
      SELECTED_CONTEXT_SIZE="${CONTEXT_SIZES[$choice]}"
      echo "Selected context size: ${SELECTED_CONTEXT_SIZE} tokens"
      return 0
    fi

    echo "Invalid choice. Please enter a number from 1 to 6."
  done
}

confirm_model() {
  local response

  while true; do
    read -rp "Use this model? (y/n/change): " response
    case "${response}" in
      y|Y|yes|Yes)
        echo
        return 0
        ;;
      n|N|no|No)
        echo "Exiting without loading a model."
        exit 0
        ;;
      c|C|change|Change)
        echo "Returning to model selection..."
        return 1
        ;;
      *)
        echo "Please enter 'y', 'n', or 'change'."
        ;;
    esac
  done
}

choose_downloaded_model() {
  local prompt="$1"
  mapfile -t model_keys < <(lms_model_keys)

  if [[ "${#model_keys[@]}" -eq 0 ]]; then
    echo "No downloaded models were found."
    return 1
  fi

  echo "${prompt}"
  select model_choice in "${model_keys[@]}"; do
    if [[ -n "${model_choice}" ]]; then
      SELECTED_MODEL_KEY="${model_choice}"
      return 0
    fi
    echo "Invalid selection. Please try again."
  done
}

capture_selected_model() {
  local search_query="$1"
  local before_keys
  local after_keys
  local new_model_key

  before_keys="$(lms_model_keys | sort || true)"

  if [[ -n "${search_query}" ]]; then
    echo "Searching for models matching: ${search_query}"
    "${LMS_BIN}" get "${search_query}" --select
  else
    echo "Showing available models..."
    "${LMS_BIN}" get --select
  fi

  after_keys="$(lms_model_keys | sort || true)"
  new_model_key="$(comm -13 <(printf '%s\n' "${before_keys}") <(printf '%s\n' "${after_keys}") || true)"
  new_model_key="${new_model_key%%$'\n'*}"

  SELECTED_MODEL_KEY=""
  if [[ -n "${new_model_key}" ]]; then
    SELECTED_MODEL_KEY="${new_model_key}"
    echo "Selected new model: ${SELECTED_MODEL_KEY}"
    return 0
  fi

  choose_downloaded_model "Select the downloaded model to use:"
}

update_lms_start_script() {
  local model_key="$1"
  local context_size="$2"
  local lms_start_path="${INSTALL_ROOT}/bin/lms-start"

  if [[ ! -f "${lms_start_path}" ]]; then
    echo "Warning: lms-start was not found at ${lms_start_path}."
    return 0
  fi

  # Use awk for robust replacement that handles CRLF line endings
  awk -v model="${model_key}" \
      -v context="${context_size}" '
    BEGIN { RS=ORS="\n" }
    {
      gsub(/\r$/, "")  # Strip CRLF if present
      if (/^MODEL_KEY=/) {
        print "MODEL_KEY=\"" model "\""
        next
      }
      if (/^CONTEXT_LENGTH=/) {
        print "CONTEXT_LENGTH=" context
        next
      }
      print
    }
    ' "${lms_start_path}" > "${lms_start_path}.tmp"

  mv "${lms_start_path}.tmp" "${lms_start_path}"
  chmod +x "${lms_start_path}"
  echo "Updated lms-start with model: ${model_key} and context: ${context_size} for llama-server."
}

update_agent_script() {
  local model_key="$1"
  local agent_path="${INSTALL_ROOT}/nymph-agent.py"
  local model_literal

  if [[ ! -f "${agent_path}" ]]; then
    return 0
  fi

  model_literal="$(python_json -c 'import json, sys; print(json.dumps(sys.argv[1]))' "${model_key}")"
  awk -v model_literal="${model_literal}" '
    /^MODEL = / { print "MODEL = " model_literal; next }
    { print }
  ' "${agent_path}" > "${agent_path}.tmp"
  mv "${agent_path}.tmp" "${agent_path}"
  echo "Updated nymph-chat to request the selected model."
}

stop_lmstudio_daemon() {
  echo "Stopping LM Studio daemon..."
  lms server stop 2>/dev/null || true
  lms daemon down 2>/dev/null || true
  # Fallback: kill any remaining LM Studio server/daemon processes
  pkill -f "lms.*server" 2>/dev/null || true
  pkill -f "lms.*daemon" 2>/dev/null || true
  sleep 1
  echo "LM Studio daemon stopped."
}

finalize_selected_model() {
  select_context_size

  echo
  echo "Updating lms-start configuration:"
  echo "  model: ${SELECTED_MODEL_KEY}"
  echo "  context: ${SELECTED_CONTEXT_SIZE}"

  update_lms_start_script "${SELECTED_MODEL_KEY}" "${SELECTED_CONTEXT_SIZE}"
  update_agent_script "${SELECTED_MODEL_KEY}"
  
  echo ""
  echo "Configuration updated successfully."
  
  # Stop the LM Studio daemon that was started for model selection
  stop_lmstudio_daemon
  
  echo ""
  echo "To start the server with this model, run:"
  echo "  ${INSTALL_ROOT}/bin/lms-start"
}

use_downloaded_model_menu() {
  local retry=true

  echo
  echo "Use Downloaded Model"

  while "${retry}"; do
    if ! choose_downloaded_model "Select a downloaded model to use:"; then
      return
    fi
    if ! confirm_model; then
      retry=true
      continue
    fi
    retry=false
  done

  finalize_selected_model
}

remove_models_menu() {
  while true; do
    echo
    echo "Remove Models"
    mapfile -t model_keys < <(lms_model_keys)

    if [[ "${#model_keys[@]}" -eq 0 ]]; then
      echo "No models to remove."
      return
    fi

    select model_choice in "${model_keys[@]}" "Back to Main Menu"; do
      if [[ "${model_choice}" == "Back to Main Menu" ]]; then
        return
      fi

      if [[ -n "${model_choice}" ]]; then
        read -rp "Remove '${model_choice}'? (y/n): " response
        if [[ "${response}" =~ ^[Yy]$ ]]; then
          "${LMS_BIN}" rm "${model_choice}"
          echo "Removed ${model_choice}."
        fi
        break
      fi

      echo "Invalid selection. Please try again."
    done
  done
}

run_add_change_model() {
  local search_query="$1"
  local retry=true

  echo
  echo "Download New Model"

  while "${retry}"; do
    capture_selected_model "${search_query}"
    if ! confirm_model; then
      retry=true
      continue
    fi
    retry=false
  done

  finalize_selected_model
}

main() {
  ensure_lms

  if [[ "$#" -gt 0 ]]; then
    run_add_change_model "$*"
    return
  fi

  while true; do
    echo
    echo "LM Studio Model Manager"
    echo "1) Use Downloaded Model"
    echo "2) Download New Model"
    echo "3) Remove Models"
    echo "4) Exit"
    echo
    read -rp "Enter your choice (1-4): " choice

    case "${choice}" in
      1) use_downloaded_model_menu ;;
      2) run_add_change_model "" ;;
      3) remove_models_menu ;;
      4) return ;;
      *) echo "Invalid choice. Please try again." ;;
    esac
  done
}

main "$@"
WRAPEOF

cat > "${BIN_DIR}/mcp-start" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
MCP_VENV_DIR="${INSTALL_ROOT}/mcp-venv"
MCP_CONFIG_DIR="${INSTALL_ROOT}/mcp/config"
MCP_LOG_DIR="${INSTALL_ROOT}/mcp/logs"
SECRET_DIR="${INSTALL_ROOT}/secrets"
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-__MCP_HOST__}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-__MCP_PORT__}}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-__OPEN_WEBUI_PORT__}"
PID_FILE="${MCP_LOG_DIR}/mcp-proxy.pid"
LOG_FILE="${MCP_LOG_DIR}/mcp-proxy.log"

is_running() {
  [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1
}

mkdir -p "${MCP_LOG_DIR}"

if is_running; then
  echo "Nymphs-Brain MCP gateway is already running at http://${MCP_HOST}:${MCP_PORT}"
  exit 0
fi

if [[ ! -x "${MCP_VENV_DIR}/bin/mcp-proxy" ]]; then
  echo "mcp-proxy is not installed at ${MCP_VENV_DIR}/bin/mcp-proxy. Rerun install_nymphs_brain.sh." >&2
  exit 1
fi

echo "Starting Nymphs-Brain MCP gateway at http://${MCP_HOST}:${MCP_PORT}"
nohup "${MCP_VENV_DIR}/bin/mcp-proxy" \
  --host "${MCP_HOST}" \
  --port "${MCP_PORT}" \
  --allow-origin "http://localhost:${OPEN_WEBUI_PORT}" \
  --allow-origin "http://127.0.0.1:${OPEN_WEBUI_PORT}" \
  --named-server-config "${MCP_CONFIG_DIR}/mcp-proxy-servers.json" \
  > "${LOG_FILE}" 2>&1 &

echo "$!" > "${PID_FILE}"

for _ in $(seq 1 60); do
  if curl -fsS "http://${MCP_HOST}:${MCP_PORT}/status" >/dev/null 2>&1; then
    echo "Nymphs-Brain MCP gateway is ready."
    exit 0
  fi
  sleep 1
done

echo "MCP gateway did not become ready in time. See ${LOG_FILE}" >&2
exit 1
WRAPEOF

cat > "${BIN_DIR}/mcp-stop" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
PID_FILE="${INSTALL_ROOT}/mcp/logs/mcp-proxy.pid"

if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1; then
  echo "Stopping Nymphs-Brain MCP gateway..."
  kill "$(cat "${PID_FILE}")" >/dev/null 2>&1 || true
  rm -f "${PID_FILE}"
  echo "Nymphs-Brain MCP gateway stopped."
else
  rm -f "${PID_FILE}"
  echo "Nymphs-Brain MCP gateway is not running."
fi
WRAPEOF

cat > "${BIN_DIR}/mcp-status" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-__MCP_HOST__}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-__MCP_PORT__}}"
PID_FILE="${INSTALL_ROOT}/mcp/logs/mcp-proxy.pid"

if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1; then
  echo "MCP proxy: running"
else
  echo "MCP proxy: stopped"
fi

echo "MCP gateway URL: http://${MCP_HOST}:${MCP_PORT}"
echo "MCP status URL: http://${MCP_HOST}:${MCP_PORT}/status"
echo "Streamable HTTP endpoints:"
echo "- filesystem: http://${MCP_HOST}:${MCP_PORT}/servers/filesystem/mcp"
echo "- memory: http://${MCP_HOST}:${MCP_PORT}/servers/memory/mcp"
echo "- web-forager: http://${MCP_HOST}:${MCP_PORT}/servers/web-forager/mcp"
echo "Legacy SSE endpoints:"
echo "- filesystem: http://${MCP_HOST}:${MCP_PORT}/servers/filesystem/sse"
echo "- memory: http://${MCP_HOST}:${MCP_PORT}/servers/memory/sse"
echo "- web-forager: http://${MCP_HOST}:${MCP_PORT}/servers/web-forager/sse"
echo "MCP config: ${INSTALL_ROOT}/mcp/config/mcp-proxy-servers.json"
echo "Cline config template: ${INSTALL_ROOT}/mcp/config/cline-mcp-settings.json"
echo "Open WebUI setup note: ${INSTALL_ROOT}/mcp/config/open-webui-mcp-servers.md"
WRAPEOF

cat > "${BIN_DIR}/open-webui-start" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
OPEN_WEBUI_VENV_DIR="${INSTALL_ROOT}/open-webui-venv"
OPEN_WEBUI_DATA_DIR="${INSTALL_ROOT}/open-webui-data"
OPEN_WEBUI_LOG_DIR="${OPEN_WEBUI_DATA_DIR}/logs"
SECRET_DIR="${INSTALL_ROOT}/secrets"
OPEN_WEBUI_HOST="${NYMPHS_BRAIN_OPEN_WEBUI_HOST:-__OPEN_WEBUI_HOST__}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-__OPEN_WEBUI_PORT__}"
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-__MCP_HOST__}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-__MCP_PORT__}}"
LLM_API_BASE_URL="${NYMPHS_BRAIN_LLM_API_BASE_URL:-http://127.0.0.1:8000/v1}"
PID_FILE="${OPEN_WEBUI_LOG_DIR}/open-webui.pid"
LOG_FILE="${OPEN_WEBUI_LOG_DIR}/open-webui.log"
WEBUI_SECRET_KEY_FILE="${SECRET_DIR}/webui-secret-key"

is_running() {
  [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1
}

mkdir -p "${OPEN_WEBUI_LOG_DIR}"

if is_running; then
  echo "Open WebUI is already running at http://${OPEN_WEBUI_HOST}:${OPEN_WEBUI_PORT}"
  exit 0
fi

if [[ ! -x "${OPEN_WEBUI_VENV_DIR}/bin/open-webui" ]]; then
  echo "Open WebUI is not installed at ${OPEN_WEBUI_VENV_DIR}/bin/open-webui. Rerun install_nymphs_brain.sh." >&2
  exit 1
fi

if [[ ! -s "${WEBUI_SECRET_KEY_FILE}" ]]; then
  echo "Open WebUI secret key is missing at ${WEBUI_SECRET_KEY_FILE}. Rerun install_nymphs_brain.sh." >&2
  exit 1
fi

"${SCRIPT_DIR}/mcp-start"

WEBUI_SECRET_KEY="$(tr -d '\r\n' < "${WEBUI_SECRET_KEY_FILE}")"

echo "Starting Open WebUI at http://${OPEN_WEBUI_HOST}:${OPEN_WEBUI_PORT}"
nohup env \
  DATA_DIR="${OPEN_WEBUI_DATA_DIR}" \
  WEBUI_SECRET_KEY="${WEBUI_SECRET_KEY}" \
  OPENAI_API_BASE_URL="${LLM_API_BASE_URL}" \
  UVICORN_WORKERS="1" \
  "${OPEN_WEBUI_VENV_DIR}/bin/open-webui" serve \
    --host "${OPEN_WEBUI_HOST}" \
    --port "${OPEN_WEBUI_PORT}" \
  > "${LOG_FILE}" 2>&1 &

echo "$!" > "${PID_FILE}"

for _ in $(seq 1 90); do
  if curl -fsS "http://${OPEN_WEBUI_HOST}:${OPEN_WEBUI_PORT}" >/dev/null 2>&1; then
    echo "Open WebUI is ready."
    echo "Open this URL from Windows: http://localhost:${OPEN_WEBUI_PORT}"
    echo "Then add MCP (Streamable HTTP) servers from:"
    echo "${INSTALL_ROOT}/mcp/config/open-webui-mcp-servers.md"
    echo "Recommended first URL: http://${MCP_HOST}:${MCP_PORT}/servers/filesystem/mcp"
    exit 0
  fi
  sleep 1
done

echo "Open WebUI did not become ready in time. See ${LOG_FILE}" >&2
exit 1
WRAPEOF

cat > "${BIN_DIR}/open-webui-stop" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
PID_FILE="${INSTALL_ROOT}/open-webui-data/logs/open-webui.pid"

if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1; then
  echo "Stopping Open WebUI..."
  kill "$(cat "${PID_FILE}")" >/dev/null 2>&1 || true
  rm -f "${PID_FILE}"
  echo "Open WebUI stopped."
else
  rm -f "${PID_FILE}"
  echo "Open WebUI is not running."
fi
WRAPEOF

cat > "${BIN_DIR}/open-webui-status" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
OPEN_WEBUI_HOST="${NYMPHS_BRAIN_OPEN_WEBUI_HOST:-__OPEN_WEBUI_HOST__}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-__OPEN_WEBUI_PORT__}"
PID_FILE="${INSTALL_ROOT}/open-webui-data/logs/open-webui.pid"

if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1; then
  echo "Open WebUI: running"
else
  echo "Open WebUI: stopped"
fi

echo "Open WebUI URL: http://${OPEN_WEBUI_HOST}:${OPEN_WEBUI_PORT}"
echo "Windows URL: http://localhost:${OPEN_WEBUI_PORT}"
echo "Open WebUI data: ${INSTALL_ROOT}/open-webui-data"
echo "Open WebUI log: ${INSTALL_ROOT}/open-webui-data/logs/open-webui.log"
echo "Open WebUI MCP setup note: ${INSTALL_ROOT}/mcp/config/open-webui-mcp-servers.md"
WRAPEOF

cat > "${BIN_DIR}/brain-status" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
OPEN_WEBUI_HOST="${NYMPHS_BRAIN_OPEN_WEBUI_HOST:-__OPEN_WEBUI_HOST__}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-__OPEN_WEBUI_PORT__}"
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-__MCP_HOST__}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-__MCP_PORT__}}"
CURL_CHECK_ARGS=(--silent --show-error --fail --connect-timeout 2 --max-time 5)

echo "Brain install: $([[ -x "${SCRIPT_DIR}/lms-start" ]] && echo installed || echo missing)"

# Check llama-server on port 8000
if curl "${CURL_CHECK_ARGS[@]}" "http://127.0.0.1:8000/v1/models" >/tmp/nymphs-brain-models.json 2>/dev/null; then
  echo "llama-server: running on port 8000"
  MODEL_OUTPUT="$("${INSTALL_ROOT}/venv/bin/python3" -c "import json; from pathlib import Path; data=json.loads(Path('/tmp/nymphs-brain-models.json').read_text(encoding='utf-8')); models=data.get('data', []); loaded=[item.get('id') for item in models if isinstance(item, dict) and item.get('id')]; print(', '.join(loaded) if loaded else 'none reported')" 2>/dev/null || echo unknown)"
  echo "Model loaded: ${MODEL_OUTPUT}"
else
  echo "llama-server: stopped"
  MODEL_NAME="$(sed -n 's/^MODEL_KEY="\([^"]*\)".*/\1/p' "${SCRIPT_DIR}/lms-start" | head -n 1)"
  echo "Model configured: ${MODEL_NAME:-none}"
fi

if curl "${CURL_CHECK_ARGS[@]}" "http://${MCP_HOST}:${MCP_PORT}/status" >/dev/null 2>&1; then
  echo "MCP proxy: running"
else
  echo "MCP proxy: stopped"
fi

if curl "${CURL_CHECK_ARGS[@]}" "http://${OPEN_WEBUI_HOST}:${OPEN_WEBUI_PORT}" >/dev/null 2>&1; then
  echo "Open WebUI: running"
else
  echo "Open WebUI: stopped"
fi
WRAPEOF

cat > "${BIN_DIR}/nymph-chat" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
export PATH="${INSTALL_ROOT}/bin:${INSTALL_ROOT}/local-tools/bin:${INSTALL_ROOT}/local-tools/node/bin:${INSTALL_ROOT}/npm-global/bin:${PATH}"
"${INSTALL_ROOT}/venv/bin/python3" "${INSTALL_ROOT}/nymph-agent.py"
WRAPEOF

cat > "${BIN_DIR}/brain-env" <<WRAPEOF
#!/usr/bin/env bash
export NYMPHS_BRAIN_ROOT="${INSTALL_ROOT}"
export NYMPHS_BRAIN_OPEN_WEBUI_URL="http://localhost:${OPEN_WEBUI_PORT}"
export NYMPHS_BRAIN_MCP_URL="http://localhost:${MCP_PORT}"
export PATH="${BIN_DIR}:${LOCAL_BIN_DIR}:${LOCAL_NODE_DIR}/bin:${NPM_GLOBAL}/bin:\${PATH}"
WRAPEOF

# Robust template replacement that handles CRLF line endings (from Windows installer)
# This awk-based approach is more reliable than sed for cross-platform scenarios

replace_template_vars() {
    local file="$1"
    awk -v MODEL_ID="${MODEL_ID}" \
        -v CONTEXT_LENGTH="${CONTEXT_LENGTH}" \
        -v MCP_HOST="${MCP_HOST}" \
        -v MCP_PORT="${MCP_PORT}" \
        -v OPEN_WEBUI_HOST="${OPEN_WEBUI_HOST}" \
        -v OPEN_WEBUI_PORT="${OPEN_WEBUI_PORT}" '
    BEGIN { RS=ORS="\n" }
    {
        # Strip CRLF to handle Windows line endings
        gsub(/\r$/, "")
        # Replace template placeholders
        gsub(/__MODEL_ID__/, MODEL_ID)
        gsub(/__CONTEXT_LENGTH__/, CONTEXT_LENGTH)
        gsub(/__MCP_HOST__/, MCP_HOST)
        gsub(/__MCP_PORT__/, MCP_PORT)
        gsub(/__OPEN_WEBUI_HOST__/, OPEN_WEBUI_HOST)
        gsub(/__OPEN_WEBUI_PORT__/, OPEN_WEBUI_PORT)
        print
    }
    ' "$file" > "${file}.tmp" && mv -f "${file}.tmp" "$file"
}

# Apply template variable replacements to all generated scripts
replace_template_vars "${BIN_DIR}/lms-start"
replace_template_vars "${BIN_DIR}/mcp-start"
replace_template_vars "${BIN_DIR}/mcp-status"
replace_template_vars "${BIN_DIR}/open-webui-start"
replace_template_vars "${BIN_DIR}/open-webui-status"
replace_template_vars "${BIN_DIR}/brain-status"

# Also update nymph-agent.py with the same robust replacement
awk -v MODEL_ID="${MODEL_ID}" '
BEGIN { RS=ORS="\n" }
{
    gsub(/\r$/, "")
    gsub(/__MODEL_ID__/, MODEL_ID)
    print
}
' "${INSTALL_ROOT}/nymph-agent.py" > "${INSTALL_ROOT}/nymph-agent.py.tmp" && mv -f "${INSTALL_ROOT}/nymph-agent.py.tmp" "${INSTALL_ROOT}/nymph-agent.py"
chmod +x \
  "${BIN_DIR}/lms-start" \
  "${BIN_DIR}/lms-model" \
  "${BIN_DIR}/lms-stop" \
  "${BIN_DIR}/lms-status" \
  "${BIN_DIR}/mcp-start" \
  "${BIN_DIR}/mcp-stop" \
  "${BIN_DIR}/mcp-status" \
  "${BIN_DIR}/open-webui-start" \
  "${BIN_DIR}/open-webui-stop" \
  "${BIN_DIR}/open-webui-status" \
  "${BIN_DIR}/brain-status" \
  "${BIN_DIR}/nymph-chat" \
  "${BIN_DIR}/brain-env"

cat > "${INSTALL_ROOT}/install-summary.txt" <<EOF
Nymphs-Brain experimental local LLM stack (hybrid architecture)
Install root: ${INSTALL_ROOT}
Model: ${MODEL_ID}
Quantization: ${QUANTIZATION}
Context length: ${CONTEXT_LENGTH}
Model download during install: ${DOWNLOAD_MODEL}

Architecture:
- LM Studio: Used for model download/management only (lms-model, lms commands)
- llama-server: Serves LLM on port 8000 (CUDA accelerated via llama.cpp)

Commands:
  LLM Server:
  - Start server:   ${BIN_DIR}/lms-start
  - Stop server:    ${BIN_DIR}/lms-stop
  - Check status:   ${BIN_DIR}/lms-status
  - Manage models:  ${BIN_DIR}/lms-model (uses LM Studio CLI for downloads)

  Chat & UI:
  - Run chat wrapper: ${BIN_DIR}/nymph-chat
  - Start Open WebUI: ${BIN_DIR}/open-webui-start
  - Stop Open WebUI:  ${BIN_DIR}/open-webui-stop
  - Check Open WebUI: ${BIN_DIR}/open-webui-status

  MCP Gateway:
  - Start MCP:    ${BIN_DIR}/mcp-start
  - Stop MCP:     ${BIN_DIR}/mcp-stop
  - Check MCP:    ${BIN_DIR}/mcp-status

  Overall Status:
  - ${BIN_DIR}/brain-status

API Endpoints:
- llama-server API: http://localhost:8000/v1/chat/completions
- Open WebUI:       http://localhost:${OPEN_WEBUI_PORT}
- MCP gateway:      http://localhost:${MCP_PORT}

Streamable HTTP MCP endpoints:
- http://localhost:${MCP_PORT}/servers/filesystem/mcp
- http://localhost:${MCP_PORT}/servers/memory/mcp
- http://localhost:${MCP_PORT}/servers/web-forager/mcp
EOF

echo "============================================================"
echo "Nymphs-Brain setup complete (hybrid LM Studio + llama-server)"
echo "============================================================"
echo ""
echo "Architecture summary:"
echo "  - Use 'lms-model' to download/manage models via LM Studio"
echo "  - Use 'lms-start' to launch llama-server (port 8000) with CUDA"
echo "  - llama-server provides OpenAI-compatible API on port 8000"
echo ""
echo "Quick start:"
echo "  ${BIN_DIR}/lms-model   # Download/configure a model first"
echo "  ${BIN_DIR}/lms-start   # Start the LLM server (port 8000)"
echo "  ${BIN_DIR}/mcp-start   # Start MCP gateway"
echo "  ${BIN_DIR}/open-webui-start  # Start Open WebUI"