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
MCP_DIR="${INSTALL_ROOT}/mcp"
MCP_CONFIG_DIR="${MCP_DIR}/config"
MCP_DATA_DIR="${MCP_DIR}/data"
MCP_LOG_DIR="${MCP_DIR}/logs"
OPEN_WEBUI_VENV_DIR="${INSTALL_ROOT}/open-webui-venv"
OPEN_WEBUI_DATA_DIR="${INSTALL_ROOT}/open-webui-data"
OPEN_WEBUI_LOG_DIR="${OPEN_WEBUI_DATA_DIR}/logs"
SECRET_DIR="${INSTALL_ROOT}/secrets"
CONFIG_DIR="${INSTALL_ROOT}/config"
OPEN_WEBUI_HOST="${NYMPHS_BRAIN_OPEN_WEBUI_HOST:-127.0.0.1}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-8081}"
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-127.0.0.1}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-8100}}"
LMSTUDIO_API_BASE_URL="${NYMPHS_BRAIN_LMSTUDIO_API_BASE_URL:-http://127.0.0.1:1234/v1}"

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

mkdir -p \
  "${BIN_DIR}" \
  "${VENV_DIR}" \
  "${NPM_GLOBAL}" \
  "${LOCAL_TOOLS_DIR}" \
  "${LOCAL_BIN_DIR}" \
  "${MCP_CONFIG_DIR}" \
  "${MCP_DATA_DIR}" \
  "${MCP_LOG_DIR}" \
  "${OPEN_WEBUI_DATA_DIR}" \
  "${OPEN_WEBUI_LOG_DIR}" \
  "${SECRET_DIR}"

if [[ "${MODEL_ID}" == "auto" && "${DOWNLOAD_MODEL}" == "1" ]]; then
  VRAM_MB="$(detect_vram_mb)"
  choose_auto_model "${VRAM_MB:-0}"
  echo "Auto model selected from detected VRAM (${VRAM_MB:-0} MB): ${MODEL_ID}"
elif [[ "${MODEL_ID}" == "auto" ]]; then
  MODEL_ID=""
  echo "No Brain model configured during install."
  echo "Use the Manager Brain page 'Manage Models' action after install to download/select Act and Plan models."
fi

DL_TARGET=""
if [[ -n "${MODEL_ID}" ]]; then
  DL_TARGET="${MODEL_ID}@${QUANTIZATION}"
fi

export DEBIAN_FRONTEND=noninteractive
MISSING_CRITICAL=()
for dep in curl tar; do
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
"${OPEN_WEBUI_VENV_DIR}/bin/pip" install --upgrade pip open-webui aiosqlite

export npm_config_prefix="${NPM_GLOBAL}"

echo "Installing/updating LM Studio CLI in the user profile."
curl -fsSL https://lmstudio.ai/install.sh | bash
add_lmstudio_paths

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

if [[ "${DOWNLOAD_MODEL}" == "1" && -n "${DL_TARGET}" ]]; then
  if ! command -v lms >/dev/null 2>&1; then
    echo "LM Studio CLI command 'lms' was not found after install." >&2
    echo "Open a new shell or check the LM Studio CLI install output, then rerun this script." >&2
    exit 1
  fi
  echo "Downloading model: ${DL_TARGET}"
  lms get "${DL_TARGET}" --yes
elif [[ "${DOWNLOAD_MODEL}" == "1" ]]; then
  echo "Skipping model download because no Brain model was selected during install."
  echo "Use the Manager Brain page 'Manage Models' action after install to download/select a model."
else
  echo "Skipping model download during install."
  echo "Use the Manager Brain page 'Manage Models' action after install to download/select a model."
fi

cat > "${INSTALL_ROOT}/nymph-agent.py" <<'PYEOF'
import requests

URL = "http://127.0.0.1:1234/v1/chat/completions"
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

sed -i "s|__MODEL_ID__|${MODEL_ID}|g" "${INSTALL_ROOT}/nymph-agent.py"

mkdir -p "${CONFIG_DIR}"
cat > "${CONFIG_DIR}/lms-model-profiles.env" <<EOF
PLAN_MODEL_ID="${MODEL_ID}"
PLAN_CONTEXT_LENGTH="${CONTEXT_LENGTH}"
ACT_MODEL_ID=""
ACT_CONTEXT_LENGTH=""
PRIMARY_MODEL_ROLE="plan"
EOF

cat > "${BIN_DIR}/lms-start" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
PROFILE_CONFIG_FILE="${INSTALL_ROOT}/config/lms-model-profiles.env"
export PATH="${INSTALL_ROOT}/bin:${INSTALL_ROOT}/local-tools/bin:${INSTALL_ROOT}/local-tools/node/bin:${INSTALL_ROOT}/npm-global/bin:${PATH}"
for candidate in "${HOME}/.lmstudio/bin" "${HOME}/.cache/lm-studio/bin" "${HOME}/.local/bin"; do
  if [[ -d "${candidate}" ]]; then
    export PATH="${candidate}:${PATH}"
  fi
done

PLAN_MODEL_ID="__MODEL_ID__"
PLAN_CONTEXT_LENGTH="__CONTEXT_LENGTH__"
ACT_MODEL_ID=""
ACT_CONTEXT_LENGTH=""

if [[ -f "${PROFILE_CONFIG_FILE}" ]]; then
  # shellcheck disable=SC1090
  source "${PROFILE_CONFIG_FILE}"
fi

declare -A LOADED_MODEL_KEYS=()

json_model_keys() {
  if [[ -x "${INSTALL_ROOT}/venv/bin/python3" ]]; then
    "${INSTALL_ROOT}/venv/bin/python3" -c '
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
  else
    python3 -c '
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
  fi
}

lms_model_keys() {
  local model_json
  model_json="$(lms ls --llm --json 2>/dev/null || printf '[]')"
  printf '%s' "${model_json}" | json_model_keys
}

model_is_downloaded() {
  local model_id="$1"
  local downloaded_model

  while IFS= read -r downloaded_model; do
    if [[ "${downloaded_model}" == "${model_id}" ]]; then
      return 0
    fi
  done < <(lms_model_keys)

  return 1
}

load_profile() {
  local role="$1"
  local model_id="$2"
  local context_length="$3"
  local model_key

  [[ -n "${model_id}" ]] || return 0
  context_length="${context_length:-16384}"
  model_key="${model_id}|${context_length}"

  if [[ -n "${LOADED_MODEL_KEYS[${model_key}]:-}" ]]; then
    echo "Skipping ${role} model because it matches ${LOADED_MODEL_KEYS[${model_key}]}. (${model_id}, context ${context_length})"
    return 0
  fi

  if ! model_is_downloaded "${model_id}"; then
    echo "Cannot load ${role} model because it is not downloaded in LM Studio: ${model_id}" >&2
    echo "Use the Manager Brain page 'Manage Models' flow to download/select a model, then start Brain again." >&2
    echo "Downloaded LM Studio model keys:" >&2
    lms_model_keys | sed 's/^/- /' >&2 || true
    return 2
  fi

  echo "Loading ${role} model: ${model_id} (context ${context_length})"
  if ! timeout --foreground 300s lms load "${model_id}" --gpu "max" --context-length "${context_length}" -y < /dev/null; then
    echo "LM Studio could not load the ${role} model non-interactively: ${model_id}" >&2
    echo "Use Manage Models to confirm the downloaded model key, then start Brain again." >&2
    return 2
  fi
  LOADED_MODEL_KEYS["${model_key}"]="${role}"
}

lms server stop >/dev/null 2>&1 || true
lms server start >/dev/null 2>&1 &
for _ in $(seq 1 90); do
  if curl -fsS http://localhost:1234/v1/models >/dev/null 2>&1; then
    break
  fi
  sleep 1
done
if ! curl -fsS http://localhost:1234/v1/models >/dev/null 2>&1; then
  echo "LM Studio server did not become ready before the timeout." >&2
  exit 2
fi
load_profile "act" "${ACT_MODEL_ID}" "${ACT_CONTEXT_LENGTH}"
load_profile "plan" "${PLAN_MODEL_ID}" "${PLAN_CONTEXT_LENGTH}"
WRAPEOF

cat > "${BIN_DIR}/lms-stop" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
export PATH="${INSTALL_ROOT}/bin:${INSTALL_ROOT}/local-tools/bin:${INSTALL_ROOT}/local-tools/node/bin:${INSTALL_ROOT}/npm-global/bin:${PATH}"
for candidate in "${HOME}/.lmstudio/bin" "${HOME}/.cache/lm-studio/bin" "${HOME}/.local/bin"; do
  if [[ -d "${candidate}" ]]; then
    export PATH="${candidate}:${PATH}"
  fi
done

echo "Unloading all LM Studio models..."
lms unload --all || true

echo "Stopping LM Studio server..."
lms server stop || true

echo "Shutting down LM Studio daemon..."
lms daemon down || true

echo "LM Studio has been stopped."
WRAPEOF

cat > "${BIN_DIR}/lms-update" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
export PATH="${INSTALL_ROOT}/bin:${INSTALL_ROOT}/local-tools/bin:${INSTALL_ROOT}/local-tools/node/bin:${INSTALL_ROOT}/npm-global/bin:${PATH}"
for candidate in "${HOME}/.lmstudio/bin" "${HOME}/.cache/lm-studio/bin" "${HOME}/.local/bin"; do
  if [[ -d "${candidate}" ]]; then
    export PATH="${candidate}:${PATH}"
  fi
done

was_running=0
if curl -fsS http://127.0.0.1:1234/v1/models >/dev/null 2>&1; then
  was_running=1
  echo "Stopping LM Studio before update..."
  "${SCRIPT_DIR}/lms-stop"
fi

echo "Updating LM Studio CLI in the Linux user profile..."
curl -fsSL https://lmstudio.ai/install.sh | bash

for candidate in "${HOME}/.lmstudio/bin" "${HOME}/.cache/lm-studio/bin" "${HOME}/.local/bin"; do
  if [[ -d "${candidate}" ]]; then
    export PATH="${candidate}:${PATH}"
  fi
done

if ! command -v lms >/dev/null 2>&1; then
  echo "LM Studio CLI command 'lms' was not found after update." >&2
  exit 1
fi

echo "LM Studio CLI update completed."

if [[ "${was_running}" == "1" ]]; then
  echo "Restarting LM Studio server and selected model..."
  "${SCRIPT_DIR}/lms-start"
else
  echo "LM Studio remains stopped. Use lms-start when ready."
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
SELECTED_ROLE="act"

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

start_server() {
  echo "Stopping any existing LM Studio server..."
  "${LMS_BIN}" server stop >/dev/null 2>&1 || true

  echo "Starting LM Studio server..."
  "${LMS_BIN}" server start >/dev/null 2>&1 &

  echo "Waiting for server to be ready..."
  until curl -fsS http://localhost:1234/v1/models >/dev/null 2>&1; do
    sleep 1
    printf "."
  done
  echo " ready."
}

unload_models() {
  echo "Unloading currently loaded models..."
  "${LMS_BIN}" unload --all 2>/dev/null || true
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

select_role() {
  local choice
  local default_role="${1:-act}"

  while true; do
    echo
    echo "Assign this model to which role?"
    echo "  1) Act"
    echo "  2) Plan"
    echo
    read -rp "Enter your choice (1-2) [default: ${default_role}]: " choice

    case "${choice}" in
      1)
        SELECTED_ROLE="act"
        return 0
        ;;
      2)
        SELECTED_ROLE="plan"
        return 0
        ;;
      "")
        SELECTED_ROLE="${default_role}"
        return 0
        ;;
      *)
        echo "Invalid choice. Please enter 1 or 2."
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
  local role="$1"
  local model_key="$2"
  local context_size="$3"
  local profile_config_path="${INSTALL_ROOT}/config/lms-model-profiles.env"
  local plan_model_id=""
  local plan_context_length=""
  local act_model_id=""
  local act_context_length=""
  local primary_model_role="act"

  if [[ -f "${profile_config_path}" ]]; then
    # shellcheck disable=SC1090
    source "${profile_config_path}"
  fi

  if [[ "${role}" == "plan" ]]; then
    plan_model_id="${model_key}"
    plan_context_length="${context_size}"
  else
    act_model_id="${model_key}"
    act_context_length="${context_size}"
  fi

  mkdir -p "$(dirname "${profile_config_path}")"
  cat > "${profile_config_path}" <<EOF
PLAN_MODEL_ID="${plan_model_id}"
PLAN_CONTEXT_LENGTH="${plan_context_length}"
ACT_MODEL_ID="${act_model_id}"
ACT_CONTEXT_LENGTH="${act_context_length}"
PRIMARY_MODEL_ROLE="${primary_model_role}"
EOF
  echo "Updated ${role} model profile."
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

finalize_selected_model() {
  local role="${1:-act}"

  select_context_size

  echo
  echo "Saving model profile:"
  echo "  role: ${role}"
  echo "  model: ${SELECTED_MODEL_KEY}"
  echo "  context: ${SELECTED_CONTEXT_SIZE}"

  update_lms_start_script "${role}" "${SELECTED_MODEL_KEY}" "${SELECTED_CONTEXT_SIZE}"
  if [[ "${role}" == "act" ]]; then
    update_agent_script "${SELECTED_MODEL_KEY}"
  fi
  echo "Model profile saved for ${role}."
  echo "Restart Nymphs-Brain LLM to apply the updated plan/act model set."
}

use_downloaded_model_menu() {
  local role="${1:-act}"
  local retry=true

  echo
  echo "Use Downloaded Model (${role^})"

  while "${retry}"; do
    SELECTED_ROLE="${role}"
    if ! choose_downloaded_model "Select a downloaded model to use:"; then
      return
    fi
    if ! confirm_model; then
      retry=true
      continue
    fi
    retry=false
  done

  finalize_selected_model "${role}"
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
  local role="$1"
  local search_query="$2"
  local retry=true

  echo
  echo "Download New Model (${role^})"

  while "${retry}"; do
    SELECTED_ROLE="${role}"
    capture_selected_model "${search_query}"
    if ! confirm_model; then
      retry=true
      continue
    fi
    retry=false
  done

  finalize_selected_model "${role}"
}

clear_plan_model() {
  "${INSTALL_ROOT}/bin/lms-set-profile" plan clear
  echo "Plan model cleared."
}

clear_act_model() {
  "${INSTALL_ROOT}/bin/lms-set-profile" act clear
  echo "Act model cleared."
}

main() {
  ensure_lms

  if [[ "$#" -gt 0 ]]; then
    run_add_change_model "plan" "$*"
    return
  fi

  while true; do
    echo
    echo "LM Studio Model Manager"
    echo "1) Set Plan Model From Downloaded"
    echo "2) Set Act Model From Downloaded"
    echo "3) Download New Model For Plan"
    echo "4) Download New Model For Act"
    echo "5) Clear Plan Model"
    echo "6) Clear Act Model"
    echo "7) Remove Models"
    echo "8) Exit"
    echo
    read -rp "Enter your choice (1-8): " choice

    case "${choice}" in
      1) use_downloaded_model_menu "plan" ;;
      2) use_downloaded_model_menu "act" ;;
      3) run_add_change_model "plan" "" ;;
      4) run_add_change_model "act" "" ;;
      5) clear_plan_model ;;
      6) clear_act_model ;;
      7) remove_models_menu ;;
      8) return ;;
      *) echo "Invalid choice. Please try again." ;;
    esac
  done
}

main "$@"
WRAPEOF

cat > "${BIN_DIR}/lms-get-profile" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
PROFILE_CONFIG_FILE="${INSTALL_ROOT}/config/lms-model-profiles.env"

PLAN_MODEL_ID=""
PLAN_CONTEXT_LENGTH=""
ACT_MODEL_ID=""
ACT_CONTEXT_LENGTH=""

if [[ -f "${PROFILE_CONFIG_FILE}" ]]; then
  # shellcheck disable=SC1090
  source "${PROFILE_CONFIG_FILE}"
fi

role="${1:-all}"
case "${role}" in
  plan)
    printf '%s\n' "${PLAN_MODEL_ID}"
    ;;
  act)
    printf '%s\n' "${ACT_MODEL_ID}"
    ;;
  all)
    printf 'plan: %s (context %s)\n' "${PLAN_MODEL_ID:-none}" "${PLAN_CONTEXT_LENGTH:-none}"
    printf 'act: %s (context %s)\n' "${ACT_MODEL_ID:-none}" "${ACT_CONTEXT_LENGTH:-none}"
    ;;
  *)
    echo "Usage: lms-get-profile [plan|act]" >&2
    exit 1
    ;;
esac
WRAPEOF

cat > "${BIN_DIR}/lms-set-profile" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: lms-set-profile ROLE MODEL_KEY [CONTEXT_LENGTH]" >&2
  echo "Use MODEL_KEY=clear to clear a role." >&2
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
PROFILE_CONFIG_FILE="${INSTALL_ROOT}/config/lms-model-profiles.env"
ROLE="${1,,}"
MODEL_KEY="$2"
CONTEXT_LENGTH="${3:-}"

python_json() {
  if [[ -x "${INSTALL_ROOT}/venv/bin/python3" ]]; then
    "${INSTALL_ROOT}/venv/bin/python3" "$@"
  else
    python3 "$@"
  fi
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
}

PLAN_MODEL_ID=""
PLAN_CONTEXT_LENGTH=""
ACT_MODEL_ID=""
ACT_CONTEXT_LENGTH=""
PRIMARY_MODEL_ROLE="plan"

if [[ -f "${PROFILE_CONFIG_FILE}" ]]; then
  # shellcheck disable=SC1090
  source "${PROFILE_CONFIG_FILE}"
fi

case "${ROLE}" in
  plan|act) ;;
  *)
    echo "Role must be 'plan' or 'act'." >&2
    exit 1
    ;;
esac

if [[ "${MODEL_KEY}" == "clear" || "${MODEL_KEY}" == "none" || "${MODEL_KEY}" == "-" ]]; then
  MODEL_KEY=""
fi

if [[ -z "${CONTEXT_LENGTH}" ]]; then
  if [[ "${ROLE}" == "plan" ]]; then
    CONTEXT_LENGTH="${PLAN_CONTEXT_LENGTH:-16384}"
  else
    CONTEXT_LENGTH="${ACT_CONTEXT_LENGTH:-16384}"
  fi
fi

if [[ "${ROLE}" == "plan" ]]; then
  PLAN_MODEL_ID="${MODEL_KEY}"
  PLAN_CONTEXT_LENGTH="${MODEL_KEY:+${CONTEXT_LENGTH}}"
else
  ACT_MODEL_ID="${MODEL_KEY}"
  ACT_CONTEXT_LENGTH="${MODEL_KEY:+${CONTEXT_LENGTH}}"
fi

mkdir -p "$(dirname "${PROFILE_CONFIG_FILE}")"
cat > "${PROFILE_CONFIG_FILE}" <<EOF
PLAN_MODEL_ID="${PLAN_MODEL_ID}"
PLAN_CONTEXT_LENGTH="${PLAN_CONTEXT_LENGTH}"
ACT_MODEL_ID="${ACT_MODEL_ID}"
ACT_CONTEXT_LENGTH="${ACT_CONTEXT_LENGTH}"
PRIMARY_MODEL_ROLE="${PRIMARY_MODEL_ROLE}"
EOF

if [[ "${ROLE}" == "act" && -n "${ACT_MODEL_ID}" ]]; then
  update_agent_script "${ACT_MODEL_ID}"
fi

if [[ -n "${MODEL_KEY}" ]]; then
  echo "Updated ${ROLE} profile: ${MODEL_KEY} (context ${CONTEXT_LENGTH})"
else
  echo "Cleared ${ROLE} profile."
fi
WRAPEOF

cat > "${BIN_DIR}/lms-get-selected" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail
"$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lms-get-profile" plan
WRAPEOF

cat > "${BIN_DIR}/lms-set-selected" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail
"$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lms-set-profile" plan "$@"
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
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-__MCP_HOST__}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-__MCP_PORT__}}"
PID_FILE="${INSTALL_ROOT}/mcp/logs/mcp-proxy.pid"

stop_pid() {
  local pid="$1"

  [[ -n "${pid}" ]] || return 0

  if ! kill -0 "${pid}" >/dev/null 2>&1; then
    return 0
  fi

  kill "${pid}" >/dev/null 2>&1 || true

  for _ in $(seq 1 10); do
    if ! kill -0 "${pid}" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done

  kill -9 "${pid}" >/dev/null 2>&1 || true
}

port_pids() {
  ss -ltnp 2>/dev/null | awk -v target=":${MCP_PORT}" '
    index($4, target) {
      if (match($0, /pid=[0-9]+/)) {
        print substr($0, RSTART + 4, RLENGTH - 4)
      }
    }
  ' | sort -u
}

echo "Stopping Nymphs-Brain MCP gateway..."

stopped_any=0

if [[ -f "${PID_FILE}" ]]; then
  stop_pid "$(cat "${PID_FILE}" 2>/dev/null || true)"
  stopped_any=1
fi

while read -r pid; do
  [[ -n "${pid}" ]] || continue
  stop_pid "${pid}"
  stopped_any=1
done < <(port_pids)

rm -f "${PID_FILE}"

if curl -fsS "http://${MCP_HOST}:${MCP_PORT}/status" >/dev/null 2>&1; then
  echo "MCP gateway still appears to be running on port ${MCP_PORT}." >&2
  exit 1
fi

if [[ "${stopped_any}" -eq 1 ]]; then
  echo "Nymphs-Brain MCP gateway stopped."
else
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
LMSTUDIO_API_BASE_URL="${NYMPHS_BRAIN_LMSTUDIO_API_BASE_URL:-__LMSTUDIO_API_BASE_URL__}"
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
  OPENAI_API_BASE_URL="${LMSTUDIO_API_BASE_URL}" \
  OPENAI_API_KEY="lm-studio" \
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

cat > "${BIN_DIR}/open-webui-update" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
OPEN_WEBUI_VENV_DIR="${INSTALL_ROOT}/open-webui-venv"
OPEN_WEBUI_DATA_DIR="${INSTALL_ROOT}/open-webui-data"
OPEN_WEBUI_LOG_DIR="${OPEN_WEBUI_DATA_DIR}/logs"
PID_FILE="${OPEN_WEBUI_LOG_DIR}/open-webui.pid"
PYTHON_BIN="${OPEN_WEBUI_VENV_DIR}/bin/python3"
PIP_BIN="${OPEN_WEBUI_VENV_DIR}/bin/pip"

was_running=0
if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1; then
  was_running=1
  echo "Stopping Open WebUI before update..."
  "${SCRIPT_DIR}/open-webui-stop"
fi

if [[ ! -x "${PYTHON_BIN}" || ! -x "${PIP_BIN}" ]]; then
  echo "Open WebUI virtual environment is missing at ${OPEN_WEBUI_VENV_DIR}. Rerun install_nymphs_brain.sh." >&2
  exit 1
fi

echo "Updating pip..."
"${PYTHON_BIN}" -m pip install --upgrade pip
echo "Updating Open WebUI..."
"${PIP_BIN}" install --upgrade open-webui aiosqlite
echo "Open WebUI packages updated."

if [[ "${was_running}" == "1" ]]; then
  echo "Restarting Open WebUI..."
  "${SCRIPT_DIR}/open-webui-start"
else
  echo "Open WebUI remains stopped. Use open-webui-start when ready."
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
PROFILE_CONFIG_FILE="${INSTALL_ROOT}/config/lms-model-profiles.env"
OPEN_WEBUI_HOST="${NYMPHS_BRAIN_OPEN_WEBUI_HOST:-__OPEN_WEBUI_HOST__}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-__OPEN_WEBUI_PORT__}"
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-__MCP_HOST__}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-__MCP_PORT__}}"
CURL_CHECK_ARGS=(--silent --show-error --fail --connect-timeout 2 --max-time 5)

PLAN_MODEL_ID="__MODEL_ID__"
PLAN_CONTEXT_LENGTH="__CONTEXT_LENGTH__"
ACT_MODEL_ID=""
ACT_CONTEXT_LENGTH=""

if [[ -f "${PROFILE_CONFIG_FILE}" ]]; then
  # shellcheck disable=SC1090
  source "${PROFILE_CONFIG_FILE}"
fi

echo "Brain install: $([[ -x "${SCRIPT_DIR}/lms-start" ]] && echo installed || echo missing)"

if curl "${CURL_CHECK_ARGS[@]}" "http://127.0.0.1:1234/v1/models" >/tmp/nymphs-brain-models.json 2>/dev/null; then
  echo "LLM server: running"
  MODEL_OUTPUT="$(timeout --foreground 5s lms ps --json 2>/tmp/nymphs-brain-models.err | "${INSTALL_ROOT}/venv/bin/python3" -c '
import json
import sys

def extract_model_keys(payload):
    if isinstance(payload, dict):
        for key in ("models", "llms", "data"):
            value = payload.get(key)
            if isinstance(value, list):
                payload = value
                break
        else:
            payload = []
    if not isinstance(payload, list):
        return []

    keys = []
    for item in payload:
        if not isinstance(item, dict):
            continue
        model = item.get("model")
        key = (
            item.get("modelKey")
            or item.get("key")
            or item.get("id")
            or (model.get("modelKey") if isinstance(model, dict) else None)
            or (model.get("key") if isinstance(model, dict) else None)
            or (model.get("id") if isinstance(model, dict) else None)
        )
        if key:
            keys.append(str(key))
    return keys

try:
    payload = json.load(sys.stdin)
except Exception:
    payload = []

loaded = extract_model_keys(payload)
print(", ".join(loaded) if loaded else "none reported")
' 2>/dev/null || "${INSTALL_ROOT}/venv/bin/python3" -c "import json; from pathlib import Path; data=json.loads(Path('/tmp/nymphs-brain-models.json').read_text(encoding='utf-8')); models=data.get('data', []); loaded=[item.get('id') for item in models if isinstance(item, dict) and item.get('id')]; print(', '.join(loaded) if loaded else 'none reported')" 2>/dev/null || echo unknown)"
  echo "Model loaded: ${MODEL_OUTPUT}"
else
  echo "LLM server: stopped"
  echo "Model loaded: none"
fi

echo "Act model: ${ACT_MODEL_ID:-none} (context ${ACT_CONTEXT_LENGTH:-none})"
echo "Plan model: ${PLAN_MODEL_ID:-none} (context ${PLAN_CONTEXT_LENGTH:-none})"

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

sed -i "s|__MODEL_ID__|${MODEL_ID}|g" "${BIN_DIR}/lms-start"
sed -i "s|__CONTEXT_LENGTH__|${CONTEXT_LENGTH}|g" "${BIN_DIR}/lms-start"
sed -i "s|__MODEL_ID__|${MODEL_ID}|g" "${BIN_DIR}/brain-status"
sed -i "s|__CONTEXT_LENGTH__|${CONTEXT_LENGTH}|g" "${BIN_DIR}/brain-status"
sed -i "s|__MCP_HOST__|${MCP_HOST}|g" "${BIN_DIR}/mcp-start" "${BIN_DIR}/mcp-status" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/brain-status"
sed -i "s|__MCP_PORT__|${MCP_PORT}|g" "${BIN_DIR}/mcp-start" "${BIN_DIR}/mcp-status" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/brain-status"
sed -i "s|__OPEN_WEBUI_HOST__|${OPEN_WEBUI_HOST}|g" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/open-webui-status" "${BIN_DIR}/brain-status"
sed -i "s|__OPEN_WEBUI_PORT__|${OPEN_WEBUI_PORT}|g" "${BIN_DIR}/mcp-start" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/open-webui-status" "${BIN_DIR}/brain-status"
sed -i "s|__LMSTUDIO_API_BASE_URL__|${LMSTUDIO_API_BASE_URL}|g" "${BIN_DIR}/open-webui-start"
chmod +x \
  "${BIN_DIR}/lms-start" \
  "${BIN_DIR}/lms-model" \
  "${BIN_DIR}/lms-get-profile" \
  "${BIN_DIR}/lms-set-profile" \
  "${BIN_DIR}/lms-get-selected" \
  "${BIN_DIR}/lms-set-selected" \
  "${BIN_DIR}/lms-update" \
  "${BIN_DIR}/lms-stop" \
  "${BIN_DIR}/mcp-start" \
  "${BIN_DIR}/mcp-stop" \
  "${BIN_DIR}/mcp-status" \
  "${BIN_DIR}/open-webui-start" \
  "${BIN_DIR}/open-webui-stop" \
  "${BIN_DIR}/open-webui-update" \
  "${BIN_DIR}/open-webui-status" \
  "${BIN_DIR}/brain-status" \
  "${BIN_DIR}/nymph-chat" \
  "${BIN_DIR}/brain-env"

cat > "${INSTALL_ROOT}/install-summary.txt" <<EOF
Nymphs-Brain experimental local LLM stack
Install root: ${INSTALL_ROOT}
Plan model: ${MODEL_ID}
Act model: not configured
Quantization: ${QUANTIZATION}
Context length: ${CONTEXT_LENGTH}
Model download during install: ${DOWNLOAD_MODEL}
LM Studio CLI location: user profile managed by LM Studio
Commands:
- ${BIN_DIR}/lms-start
- ${BIN_DIR}/lms-model
- ${BIN_DIR}/lms-get-profile
- ${BIN_DIR}/lms-set-profile
- ${BIN_DIR}/lms-update
- ${BIN_DIR}/lms-stop
- ${BIN_DIR}/nymph-chat
- ${BIN_DIR}/mcp-start
- ${BIN_DIR}/mcp-status
- ${BIN_DIR}/mcp-stop
- ${BIN_DIR}/open-webui-start
- ${BIN_DIR}/open-webui-status
- ${BIN_DIR}/open-webui-stop
- ${BIN_DIR}/open-webui-update
- ${BIN_DIR}/brain-status
Open WebUI URL: http://localhost:${OPEN_WEBUI_PORT}
MCP gateway URL: http://localhost:${MCP_PORT}
Primary Streamable HTTP endpoints:
- http://localhost:${MCP_PORT}/servers/filesystem/mcp
- http://localhost:${MCP_PORT}/servers/memory/mcp
- http://localhost:${MCP_PORT}/servers/web-forager/mcp
EOF

echo "Nymphs-Brain setup complete."
echo "Start LM Studio model server: ${BIN_DIR}/lms-start"
echo "Change/download/remove LM Studio models: ${BIN_DIR}/lms-model"
echo "Set plan/act model profiles: ${BIN_DIR}/lms-set-profile"
echo "Update LM Studio CLI/runtime: ${BIN_DIR}/lms-update"
echo "Stop LM Studio cleanly: ${BIN_DIR}/lms-stop"
echo "Start MCP proxy: ${BIN_DIR}/mcp-start"
echo "Start Open WebUI: ${BIN_DIR}/open-webui-start"
echo "Update Open WebUI: ${BIN_DIR}/open-webui-update"
echo "Run chat wrapper: ${BIN_DIR}/nymph-chat"
