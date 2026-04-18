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
CACHE_DIR="${INSTALL_ROOT}/models"
LOCAL_TOOLS_DIR="${INSTALL_ROOT}/local-tools"
LOCAL_BIN_DIR="${LOCAL_TOOLS_DIR}/bin"
LOCAL_NODE_DIR="${LOCAL_TOOLS_DIR}/node"

export PATH="${BIN_DIR}:${LOCAL_BIN_DIR}:${LOCAL_NODE_DIR}/bin:${NPM_GLOBAL}/bin:${PATH}"

log() {
  if [[ "${QUIET}" != "1" ]]; then
    echo "$@"
  fi
}

have_python_venv() {
  python3 - <<'PYEOF' >/dev/null 2>&1
import venv
PYEOF
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

detect_vram_mb() {
  # Check if Manager passed the actual GPU VRAM from Windows
  if [[ -n "${NYMPHS3D_GPU_VRAM_MB}" && "${NYMPHS3D_GPU_VRAM_MB}" =~ ^[0-9]+$ ]]; then
    echo "${NYMPHS3D_GPU_VRAM_MB}"
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

model_served_name() {
  basename "$1"
}

echo "Nymphs-Brain experimental installer"
echo "Install root: ${INSTALL_ROOT}"

mkdir -p "${BIN_DIR}" "${VENV_DIR}" "${NPM_GLOBAL}" "${CACHE_DIR}" "${LOCAL_TOOLS_DIR}" "${LOCAL_BIN_DIR}"

if [[ "${MODEL_ID}" == "auto" ]]; then
  VRAM_MB="$(detect_vram_mb)"
  choose_auto_model "${VRAM_MB:-0}"
  echo "Auto model selected from detected VRAM (${VRAM_MB:-0} MB): ${MODEL_ID}"
fi

SERVED_NAME="$(model_served_name "${MODEL_ID}")"
DL_TARGET="${MODEL_ID}@${QUANTIZATION}"

export DEBIAN_FRONTEND=noninteractive
MISSING_CRITICAL=()
for dep in python3 curl tar; do
  if ! command -v "${dep}" >/dev/null 2>&1; then
    MISSING_CRITICAL+=("${dep}")
  fi
done
if ! have_python_venv; then
  MISSING_CRITICAL+=(python3-venv)
fi
if [[ "${#MISSING_CRITICAL[@]}" -gt 0 ]]; then
  echo "ERROR: Missing critical dependencies: ${MISSING_CRITICAL[*]}"
  echo "These must be in the base distro."
  exit 1
fi

ensure_local_node

log "Zero-sudo dependency check complete."

if [[ ! -x "${VENV_DIR}/bin/python3" ]]; then
  echo "Creating Python venv at ${VENV_DIR}"
  python3 -m venv "${VENV_DIR}"
fi

"${VENV_DIR}/bin/pip" install --upgrade pip requests huggingface_hub

export npm_config_prefix="${NPM_GLOBAL}"
export LMSTUDIO_MODEL_PATH="${CACHE_DIR}"

echo "Installing/updating LM Studio CLI in the user profile."
curl -fsSL https://lmstudio.ai/install.sh | bash
add_lmstudio_paths

echo "Installing MCP helper packages into ${NPM_GLOBAL}"
npm install -g --prefix="${NPM_GLOBAL}" \
  @modelcontextprotocol/server-filesystem \
  @modelcontextprotocol/server-memory

if [[ "${DOWNLOAD_MODEL}" == "1" ]]; then
  if ! command -v lms >/dev/null 2>&1; then
    echo "LM Studio CLI command 'lms' was not found after install." >&2
    echo "Open a new shell or check the LM Studio CLI install output, then rerun this script." >&2
    exit 1
  fi
  echo "Downloading model: ${DL_TARGET}"
  lms get "${DL_TARGET}" --yes
else
  echo "Skipping model download. The wrapper is configured for ${DL_TARGET}."
fi

cat > "${INSTALL_ROOT}/nymph-agent.py" <<'PYEOF'
import requests

URL = "http://127.0.0.1:1234/v1/chat/completions"
MODEL = "__SERVED_NAME__"


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
        "content": "You are Nymphs-Brain, an experimental local assistant for NymphsCore.",
    }
]

print("\033[92mNymphs-Brain Agent Active.\033[0m")
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

sed -i "s|__SERVED_NAME__|${SERVED_NAME}|g" "${INSTALL_ROOT}/nymph-agent.py"

cat > "${BIN_DIR}/lms-start" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
export PATH="${INSTALL_ROOT}/npm-global/bin:${PATH}"
for candidate in "${HOME}/.lmstudio/bin" "${HOME}/.cache/lm-studio/bin" "${HOME}/.local/bin"; do
  if [[ -d "${candidate}" ]]; then
    export PATH="${candidate}:${PATH}"
  fi
done
export LMSTUDIO_MODEL_PATH="${INSTALL_ROOT}/models"
lms server stop >/dev/null 2>&1 || true
lms server start >/dev/null 2>&1 &
until curl -fsS http://localhost:1234/v1/models >/dev/null 2>&1; do
  sleep 1
done
lms load "__SERVED_NAME__" --gpu "max" --context-length "__CONTEXT_LENGTH__"
WRAPEOF

cat > "${BIN_DIR}/nymph-chat" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
export PATH="${INSTALL_ROOT}/npm-global/bin:${PATH}"
"${INSTALL_ROOT}/venv/bin/python3" "${INSTALL_ROOT}/nymph-agent.py"
WRAPEOF

cat > "${BIN_DIR}/brain-env" <<WRAPEOF
#!/usr/bin/env bash
export NYMPHS_BRAIN_ROOT="${INSTALL_ROOT}"
export LMSTUDIO_MODEL_PATH="${CACHE_DIR}"
export PATH="${NPM_GLOBAL}/bin:\${PATH}"
WRAPEOF

sed -i "s|__SERVED_NAME__|${SERVED_NAME}|g" "${BIN_DIR}/lms-start"
sed -i "s|__CONTEXT_LENGTH__|${CONTEXT_LENGTH}|g" "${BIN_DIR}/lms-start"
chmod +x "${BIN_DIR}/lms-start" "${BIN_DIR}/nymph-chat" "${BIN_DIR}/brain-env"

cat > "${INSTALL_ROOT}/install-summary.txt" <<EOF
Nymphs-Brain experimental local LLM stack
Install root: ${INSTALL_ROOT}
Model: ${MODEL_ID}
Quantization: ${QUANTIZATION}
Context length: ${CONTEXT_LENGTH}
Model download during install: ${DOWNLOAD_MODEL}
LM Studio CLI location: user profile managed by LM Studio
EOF

echo "Nymphs-Brain setup complete."
echo "Start LM Studio model server: ${BIN_DIR}/lms-start"
echo "Run chat wrapper: ${BIN_DIR}/nymph-chat"
