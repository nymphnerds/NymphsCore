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
MCP_VENV_DIR="${INSTALL_ROOT}/mcp-venv"
MCP_DIR="${INSTALL_ROOT}/mcp"
MCP_CONFIG_DIR="${MCP_DIR}/config"
MCP_DATA_DIR="${MCP_DIR}/data"
MCP_LOG_DIR="${MCP_DIR}/logs"
OPEN_WEBUI_VENV_DIR="${INSTALL_ROOT}/open-webui-venv"
OPEN_WEBUI_DATA_DIR="${INSTALL_ROOT}/open-webui-data"
OPEN_WEBUI_LOG_DIR="${OPEN_WEBUI_DATA_DIR}/logs"
SECRET_DIR="${INSTALL_ROOT}/secrets"
OPEN_WEBUI_HOST="${NYMPHS_BRAIN_OPEN_WEBUI_HOST:-127.0.0.1}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-8080}"
MCPO_HOST="${NYMPHS_BRAIN_MCPO_HOST:-127.0.0.1}"
MCPO_PORT="${NYMPHS_BRAIN_MCPO_PORT:-8100}"
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

model_served_name() {
  basename "$1"
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
  "${CACHE_DIR}" \
  "${LOCAL_TOOLS_DIR}" \
  "${LOCAL_BIN_DIR}" \
  "${MCP_CONFIG_DIR}" \
  "${MCP_DATA_DIR}" \
  "${MCP_LOG_DIR}" \
  "${OPEN_WEBUI_DATA_DIR}" \
  "${OPEN_WEBUI_LOG_DIR}" \
  "${SECRET_DIR}"

if [[ "${MODEL_ID}" == "auto" ]]; then
  VRAM_MB="$(detect_vram_mb)"
  choose_auto_model "${VRAM_MB:-0}"
  echo "Auto model selected from detected VRAM (${VRAM_MB:-0} MB): ${MODEL_ID}"
fi

SERVED_NAME="$(model_served_name "${MODEL_ID}")"
DL_TARGET="${MODEL_ID}@${QUANTIZATION}"

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
"${MCP_VENV_DIR}/bin/pip" install --upgrade pip mcpo web-forager

ensure_python_venv "${OPEN_WEBUI_VENV_DIR}" "Open WebUI"
"${OPEN_WEBUI_VENV_DIR}/bin/pip" install --upgrade pip open-webui

export npm_config_prefix="${NPM_GLOBAL}"
export LMSTUDIO_MODEL_PATH="${CACHE_DIR}"

echo "Installing/updating LM Studio CLI in the user profile."
curl -fsSL https://lmstudio.ai/install.sh | bash
add_lmstudio_paths

echo "Installing MCP helper packages into ${NPM_GLOBAL}"
npm install -g --prefix="${NPM_GLOBAL}" \
  @modelcontextprotocol/server-filesystem \
  @modelcontextprotocol/server-memory

ensure_secret_file "${SECRET_DIR}/webui-secret-key" 32
ensure_secret_file "${SECRET_DIR}/mcpo-api-key" 24
WEBUI_SECRET_KEY="$(tr -d '\r\n' < "${SECRET_DIR}/webui-secret-key")"
MCPO_API_KEY="$(tr -d '\r\n' < "${SECRET_DIR}/mcpo-api-key")"

cat > "${MCP_CONFIG_DIR}/mcpo.json" <<EOF
{
  "mcpServers": {
    "filesystem": {
      "command": "${NPM_GLOBAL}/bin/mcp-server-filesystem",
      "args": [
        "${HOME}/NymphsCore",
        "${INSTALL_ROOT}"
      ]
    },
    "memory": {
      "command": "${NPM_GLOBAL}/bin/mcp-server-memory",
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

cat > "${MCP_CONFIG_DIR}/open-webui-tool-connections.json" <<EOF
[
  {
    "type": "openapi",
    "url": "http://${MCPO_HOST}:${MCPO_PORT}/filesystem",
    "spec_type": "url",
    "spec": "",
    "path": "openapi.json",
    "auth_type": "bearer",
    "key": "${MCPO_API_KEY}",
    "config": { "enable": true },
    "info": {
      "id": "nymphs-brain-filesystem",
      "name": "Nymphs-Brain Filesystem",
      "description": "Restricted filesystem tools for the NymphsCore and Nymphs-Brain folders."
    }
  },
  {
    "type": "openapi",
    "url": "http://${MCPO_HOST}:${MCPO_PORT}/memory",
    "spec_type": "url",
    "spec": "",
    "path": "openapi.json",
    "auth_type": "bearer",
    "key": "${MCPO_API_KEY}",
    "config": { "enable": true },
    "info": {
      "id": "nymphs-brain-memory",
      "name": "Nymphs-Brain Memory",
      "description": "Persistent local memory tools for Nymphs-Brain."
    }
  },
  {
    "type": "openapi",
    "url": "http://${MCPO_HOST}:${MCPO_PORT}/web-forager",
    "spec_type": "url",
    "spec": "",
    "path": "openapi.json",
    "auth_type": "bearer",
    "key": "${MCPO_API_KEY}",
    "config": { "enable": true },
    "info": {
      "id": "nymphs-brain-web-forager",
      "name": "Nymphs-Brain Web Forager",
      "description": "DuckDuckGo search and URL fetch tools exposed through mcpo."
    }
  }
]
EOF

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
export PATH="${INSTALL_ROOT}/bin:${INSTALL_ROOT}/local-tools/bin:${INSTALL_ROOT}/local-tools/node/bin:${INSTALL_ROOT}/npm-global/bin:${PATH}"
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

cat > "${BIN_DIR}/lms-model" <<'WRAPEOF'
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

export LMSTUDIO_MODEL_PATH="${INSTALL_ROOT}/models"

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

python_json() {
  if [[ -x "${INSTALL_ROOT}/venv/bin/python3" ]]; then
    "${INSTALL_ROOT}/venv/bin/python3" "$@"
  else
    python3 "$@"
  fi
}

json_model_keys() {
  python_json - <<'PYEOF'
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
PYEOF
}

lms_model_keys() {
  local model_json
  model_json="$(lms ls --llm --json 2>/dev/null || printf '[]')"
  printf '%s' "${model_json}" | json_model_keys
}

ensure_lms() {
  if ! command -v lms >/dev/null 2>&1; then
    echo "LM Studio CLI command 'lms' was not found." >&2
    echo "Rerun install_nymphs_brain.sh or check the LM Studio CLI installation." >&2
    exit 1
  fi
}

start_server() {
  echo "Stopping any existing LM Studio server..."
  lms server stop >/dev/null 2>&1 || true

  echo "Starting LM Studio server..."
  lms server start >/dev/null 2>&1 &

  echo "Waiting for server to be ready..."
  until curl -fsS http://localhost:1234/v1/models >/dev/null 2>&1; do
    sleep 1
    printf "."
  done
  echo " ready."
}

unload_models() {
  echo "Unloading currently loaded models..."
  lms unload --all 2>/dev/null || true
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
    lms get "${search_query}" --select
  else
    echo "Showing available models..."
    lms get --select
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
  local new_load_line

  new_load_line=$(printf 'lms load "%s" --gpu "max" --context-length "%s"' "${model_key}" "${context_size}")

  if [[ ! -f "${lms_start_path}" ]]; then
    echo "Warning: lms-start was not found at ${lms_start_path}."
    return 0
  fi

  awk -v new_line="${new_load_line}" '
    /^lms load / { print new_line; next }
    { print }
  ' "${lms_start_path}" > "${lms_start_path}.tmp"
  mv "${lms_start_path}.tmp" "${lms_start_path}"
  chmod +x "${lms_start_path}"
  echo "Updated lms-start with the selected model and context size."
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
          lms rm "${model_choice}"
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
  echo "LM Studio Interactive Model Selector"
  start_server

  while "${retry}"; do
    capture_selected_model "${search_query}"
    if ! confirm_model; then
      retry=true
      continue
    fi
    retry=false
  done

  select_context_size
  unload_models

  echo
  echo "Loading model:"
  echo "  model: ${SELECTED_MODEL_KEY}"
  echo "  context: ${SELECTED_CONTEXT_SIZE}"

  lms load --yes "${SELECTED_MODEL_KEY}" --gpu "max" --context-length "${SELECTED_CONTEXT_SIZE}"

  update_lms_start_script "${SELECTED_MODEL_KEY}" "${SELECTED_CONTEXT_SIZE}"
  update_agent_script "${SELECTED_MODEL_KEY}"
  echo "Model loaded successfully."
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
    echo "1) Add/Change Model"
    echo "2) Remove Models"
    echo "3) Exit"
    echo
    read -rp "Enter your choice (1-3): " choice

    case "${choice}" in
      1) run_add_change_model "" ;;
      2) remove_models_menu ;;
      3) return ;;
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
MCPO_HOST="${NYMPHS_BRAIN_MCPO_HOST:-__MCPO_HOST__}"
MCPO_PORT="${NYMPHS_BRAIN_MCPO_PORT:-__MCPO_PORT__}"
PID_FILE="${MCP_LOG_DIR}/mcpo.pid"
LOG_FILE="${MCP_LOG_DIR}/mcpo.log"
KEY_FILE="${SECRET_DIR}/mcpo-api-key"

is_running() {
  [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1
}

mkdir -p "${MCP_LOG_DIR}"

if is_running; then
  echo "Nymphs-Brain MCP proxy is already running at http://${MCPO_HOST}:${MCPO_PORT}"
  exit 0
fi

if [[ ! -x "${MCP_VENV_DIR}/bin/mcpo" ]]; then
  echo "mcpo is not installed at ${MCP_VENV_DIR}/bin/mcpo. Rerun install_nymphs_brain.sh." >&2
  exit 1
fi

if [[ ! -s "${KEY_FILE}" ]]; then
  echo "MCP API key file is missing at ${KEY_FILE}. Rerun install_nymphs_brain.sh." >&2
  exit 1
fi

MCPO_API_KEY="$(tr -d '\r\n' < "${KEY_FILE}")"

echo "Starting Nymphs-Brain MCP proxy at http://${MCPO_HOST}:${MCPO_PORT}"
nohup "${MCP_VENV_DIR}/bin/mcpo" \
  --host "${MCPO_HOST}" \
  --port "${MCPO_PORT}" \
  --api-key "${MCPO_API_KEY}" \
  --config "${MCP_CONFIG_DIR}/mcpo.json" \
  --hot-reload \
  > "${LOG_FILE}" 2>&1 &

echo "$!" > "${PID_FILE}"

for _ in $(seq 1 60); do
  if curl -fsS "http://${MCPO_HOST}:${MCPO_PORT}/docs" >/dev/null 2>&1; then
    echo "Nymphs-Brain MCP proxy is ready."
    exit 0
  fi
  sleep 1
done

echo "MCP proxy did not become ready in time. See ${LOG_FILE}" >&2
exit 1
WRAPEOF

cat > "${BIN_DIR}/mcp-stop" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
PID_FILE="${INSTALL_ROOT}/mcp/logs/mcpo.pid"

if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1; then
  echo "Stopping Nymphs-Brain MCP proxy..."
  kill "$(cat "${PID_FILE}")" >/dev/null 2>&1 || true
  rm -f "${PID_FILE}"
  echo "Nymphs-Brain MCP proxy stopped."
else
  rm -f "${PID_FILE}"
  echo "Nymphs-Brain MCP proxy is not running."
fi
WRAPEOF

cat > "${BIN_DIR}/mcp-status" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
MCPO_HOST="${NYMPHS_BRAIN_MCPO_HOST:-__MCPO_HOST__}"
MCPO_PORT="${NYMPHS_BRAIN_MCPO_PORT:-__MCPO_PORT__}"
PID_FILE="${INSTALL_ROOT}/mcp/logs/mcpo.pid"

if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1; then
  echo "MCP proxy: running"
else
  echo "MCP proxy: stopped"
fi

echo "MCP URL: http://${MCPO_HOST}:${MCPO_PORT}"
echo "MCP docs:"
echo "- filesystem: http://${MCPO_HOST}:${MCPO_PORT}/filesystem/docs"
echo "- memory: http://${MCPO_HOST}:${MCPO_PORT}/memory/docs"
echo "- web-forager: http://${MCPO_HOST}:${MCPO_PORT}/web-forager/docs"
echo "MCP config: ${INSTALL_ROOT}/mcp/config/mcpo.json"
echo "MCP API key file: ${INSTALL_ROOT}/secrets/mcpo-api-key"
WRAPEOF

cat > "${BIN_DIR}/open-webui-start" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
OPEN_WEBUI_VENV_DIR="${INSTALL_ROOT}/open-webui-venv"
OPEN_WEBUI_DATA_DIR="${INSTALL_ROOT}/open-webui-data"
OPEN_WEBUI_LOG_DIR="${OPEN_WEBUI_DATA_DIR}/logs"
MCP_CONFIG_DIR="${INSTALL_ROOT}/mcp/config"
SECRET_DIR="${INSTALL_ROOT}/secrets"
OPEN_WEBUI_HOST="${NYMPHS_BRAIN_OPEN_WEBUI_HOST:-__OPEN_WEBUI_HOST__}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-__OPEN_WEBUI_PORT__}"
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
TOOL_SERVER_CONNECTIONS="$("${OPEN_WEBUI_VENV_DIR}/bin/python3" - "${MCP_CONFIG_DIR}/open-webui-tool-connections.json" <<'PYEOF'
import json
import sys
from pathlib import Path

print(json.dumps(json.loads(Path(sys.argv[1]).read_text(encoding="utf-8")), separators=(",", ":")))
PYEOF
)"

echo "Starting Open WebUI at http://${OPEN_WEBUI_HOST}:${OPEN_WEBUI_PORT}"
nohup env \
  DATA_DIR="${OPEN_WEBUI_DATA_DIR}" \
  WEBUI_SECRET_KEY="${WEBUI_SECRET_KEY}" \
  OPENAI_API_BASE_URL="${LMSTUDIO_API_BASE_URL}" \
  OPENAI_API_KEY="lm-studio" \
  ENABLE_DIRECT_CONNECTIONS="True" \
  TOOL_SERVER_CONNECTIONS="${TOOL_SERVER_CONNECTIONS}" \
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
WRAPEOF

cat > "${BIN_DIR}/brain-status" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
OPEN_WEBUI_HOST="${NYMPHS_BRAIN_OPEN_WEBUI_HOST:-__OPEN_WEBUI_HOST__}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-__OPEN_WEBUI_PORT__}"
MCPO_HOST="${NYMPHS_BRAIN_MCPO_HOST:-__MCPO_HOST__}"
MCPO_PORT="${NYMPHS_BRAIN_MCPO_PORT:-__MCPO_PORT__}"

echo "Brain install: $([[ -x "${SCRIPT_DIR}/lms-start" ]] && echo installed || echo missing)"

if curl -fsS "http://127.0.0.1:1234/v1/models" >/tmp/nymphs-brain-models.json 2>/dev/null; then
  echo "LLM server: running"
  "${INSTALL_ROOT}/venv/bin/python3" - <<'PYEOF' || true
import json
from pathlib import Path

try:
    data = json.loads(Path("/tmp/nymphs-brain-models.json").read_text(encoding="utf-8"))
    models = data.get("data", [])
    loaded = [item.get("id") for item in models if isinstance(item, dict) and item.get("id")]
    print("Model loaded: " + (", ".join(loaded) if loaded else "none reported"))
except Exception:
    print("Model loaded: unknown")
PYEOF
else
  echo "LLM server: stopped"
  model="$(awk '/^lms load / { gsub(/"/, "", $3); print $3; exit }' "${SCRIPT_DIR}/lms-start" 2>/dev/null || true)"
  echo "Model loaded: ${model:-none}"
fi

if curl -fsS "http://${MCPO_HOST}:${MCPO_PORT}/docs" >/dev/null 2>&1; then
  echo "MCP proxy: running"
else
  echo "MCP proxy: stopped"
fi

if curl -fsS "http://${OPEN_WEBUI_HOST}:${OPEN_WEBUI_PORT}" >/dev/null 2>&1; then
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
export LMSTUDIO_MODEL_PATH="${CACHE_DIR}"
export NYMPHS_BRAIN_OPEN_WEBUI_URL="http://localhost:${OPEN_WEBUI_PORT}"
export NYMPHS_BRAIN_MCPO_URL="http://localhost:${MCPO_PORT}"
export PATH="${BIN_DIR}:${LOCAL_BIN_DIR}:${LOCAL_NODE_DIR}/bin:${NPM_GLOBAL}/bin:\${PATH}"
WRAPEOF

sed -i "s|__SERVED_NAME__|${SERVED_NAME}|g" "${BIN_DIR}/lms-start"
sed -i "s|__CONTEXT_LENGTH__|${CONTEXT_LENGTH}|g" "${BIN_DIR}/lms-start"
sed -i "s|__MCPO_HOST__|${MCPO_HOST}|g" "${BIN_DIR}/mcp-start" "${BIN_DIR}/mcp-status" "${BIN_DIR}/brain-status"
sed -i "s|__MCPO_PORT__|${MCPO_PORT}|g" "${BIN_DIR}/mcp-start" "${BIN_DIR}/mcp-status" "${BIN_DIR}/brain-status"
sed -i "s|__OPEN_WEBUI_HOST__|${OPEN_WEBUI_HOST}|g" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/open-webui-status" "${BIN_DIR}/brain-status"
sed -i "s|__OPEN_WEBUI_PORT__|${OPEN_WEBUI_PORT}|g" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/open-webui-status" "${BIN_DIR}/brain-status"
sed -i "s|__LMSTUDIO_API_BASE_URL__|${LMSTUDIO_API_BASE_URL}|g" "${BIN_DIR}/open-webui-start"
chmod +x \
  "${BIN_DIR}/lms-start" \
  "${BIN_DIR}/lms-model" \
  "${BIN_DIR}/lms-stop" \
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
Nymphs-Brain experimental local LLM stack
Install root: ${INSTALL_ROOT}
Model: ${MODEL_ID}
Quantization: ${QUANTIZATION}
Context length: ${CONTEXT_LENGTH}
Model download during install: ${DOWNLOAD_MODEL}
LM Studio CLI location: user profile managed by LM Studio
Commands:
- ${BIN_DIR}/lms-start
- ${BIN_DIR}/lms-model
- ${BIN_DIR}/lms-stop
- ${BIN_DIR}/nymph-chat
- ${BIN_DIR}/mcp-start
- ${BIN_DIR}/mcp-status
- ${BIN_DIR}/mcp-stop
- ${BIN_DIR}/open-webui-start
- ${BIN_DIR}/open-webui-status
- ${BIN_DIR}/open-webui-stop
- ${BIN_DIR}/brain-status
Open WebUI URL: http://localhost:${OPEN_WEBUI_PORT}
MCP proxy URL: http://localhost:${MCPO_PORT}
EOF

echo "Nymphs-Brain setup complete."
echo "Start LM Studio model server: ${BIN_DIR}/lms-start"
echo "Change/download/remove LM Studio models: ${BIN_DIR}/lms-model"
echo "Stop LM Studio cleanly: ${BIN_DIR}/lms-stop"
echo "Start MCP proxy: ${BIN_DIR}/mcp-start"
echo "Start Open WebUI: ${BIN_DIR}/open-webui-start"
echo "Run chat wrapper: ${BIN_DIR}/nymph-chat"
