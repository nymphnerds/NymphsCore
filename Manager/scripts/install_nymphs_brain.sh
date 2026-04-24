#!/usr/bin/env bash
set -euo pipefail

INSTALL_ROOT="${HOME}/Nymphs-Brain"
MODEL_ID="auto"
QUANTIZATION="q4_k_m"
CONTEXT_LENGTH="16384"
CONTEXT_SET="0"
DOWNLOAD_MODEL="0"
QUIET="0"
LLM_WRAPPER_MODEL="${NYMPHS_BRAIN_LLM_WRAPPER_MODEL:-openai/gpt-4o-mini}"
OPENROUTER_API_KEY="${NYMPHS_BRAIN_OPENROUTER_API_KEY:-}"

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
  --llm-wrapper-model ID  Default OpenRouter model for delegated llm-wrapper calls
  --openrouter-api-key K  Seed/update the llm-wrapper OpenRouter API key
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
    --llm-wrapper-model)
      LLM_WRAPPER_MODEL="${2:?Missing value for --llm-wrapper-model}"
      shift 2
      ;;
    --openrouter-api-key)
      OPENROUTER_API_KEY="${2:?Missing value for --openrouter-api-key}"
      shift 2
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
MCPO_OPENAPI_PORT="${NYMPHS_BRAIN_MCPO_OPENAPI_PORT:-8099}"
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

ensure_llm_wrapper_secret_file() {
  local path="$1"

  mkdir -p "$(dirname "${path}")"

  if [[ -n "${OPENROUTER_API_KEY}" ]]; then
    cat > "${path}" <<EOF
# Nymphs-Brain llm-wrapper configuration
OPENROUTER_API_KEY=${OPENROUTER_API_KEY}
EOF
    chmod 600 "${path}" || true
    return 0
  fi

  if [[ ! -f "${path}" ]]; then
    cat > "${path}" <<'EOF'
# Nymphs-Brain llm-wrapper configuration
# Add your OpenRouter key, then restart the MCP stack.
OPENROUTER_API_KEY=
EOF
    chmod 600 "${path}" || true
  fi
}

secret_file_has_openrouter_key() {
  local path="$1"

  [[ -f "${path}" ]] || return 1

  awk -F '=' '
    $1 == "OPENROUTER_API_KEY" {
      value = $2
      sub(/^[[:space:]]+/, "", value)
      sub(/[[:space:]]+$/, "", value)
      if (value != "") {
        found = 1
      }
    }
    END { exit(found ? 0 : 1) }
  ' "${path}"
}

install_remote_llm_bundle() {
  local bundle_source_dir
  local bundle_target_dir="${LOCAL_TOOLS_DIR}/remote_llm_mcp"

  bundle_source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/remote_llm_mcp"

  if [[ ! -d "${bundle_source_dir}" ]]; then
    echo "Bundled remote_llm_mcp directory is missing at ${bundle_source_dir}" >&2
    exit 1
  fi

  mkdir -p "${bundle_target_dir}"
  cp "${bundle_source_dir}/"* "${bundle_target_dir}/"
  chmod +x \
    "${bundle_target_dir}/cached_llm_mcp_server.py" \
    "${bundle_target_dir}/install-llm-wrapper.sh" \
    "${bundle_target_dir}/uninstall-llm-wrapper.sh"
  "${MCP_VENV_DIR}/bin/python3" -m py_compile "${bundle_target_dir}/cached_llm_mcp_server.py"
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

PROFILE_CONFIG_FILE="${CONFIG_DIR}/lms-model-profiles.env"
EXISTING_PLAN_MODEL_ID=""
EXISTING_PLAN_CONTEXT_LENGTH=""
EXISTING_ACT_MODEL_ID=""
EXISTING_ACT_CONTEXT_LENGTH=""
EXISTING_LLM_WRAPPER_MODEL=""
EXISTING_PRIMARY_MODEL_ROLE="plan"

if [[ -f "${PROFILE_CONFIG_FILE}" ]]; then
  # shellcheck disable=SC1090
  source "${PROFILE_CONFIG_FILE}"
  EXISTING_PLAN_MODEL_ID="${PLAN_MODEL_ID:-}"
  EXISTING_PLAN_CONTEXT_LENGTH="${PLAN_CONTEXT_LENGTH:-}"
  EXISTING_ACT_MODEL_ID="${ACT_MODEL_ID:-}"
  EXISTING_ACT_CONTEXT_LENGTH="${ACT_CONTEXT_LENGTH:-}"
  EXISTING_LLM_WRAPPER_MODEL="${LLM_WRAPPER_MODEL:-}"
  EXISTING_PRIMARY_MODEL_ROLE="${PRIMARY_MODEL_ROLE:-plan}"
fi

if [[ "${MODEL_ID}" == "auto" && "${DOWNLOAD_MODEL}" == "1" ]]; then
  VRAM_MB="$(detect_vram_mb)"
  choose_auto_model "${VRAM_MB:-0}"
  echo "Auto model selected from detected VRAM (${VRAM_MB:-0} MB): ${MODEL_ID}"
elif [[ "${MODEL_ID}" == "auto" ]]; then
  MODEL_ID=""
  echo "No Brain model configured during install."
  echo "Use the Manager Brain page 'Manage Models' action after install to choose local Plan/Act models and the optional remote llm-wrapper model."
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
"${MCP_VENV_DIR}/bin/pip" install --upgrade pip mcp-proxy web-forager mcpo requests
install_remote_llm_bundle

ensure_python_venv "${OPEN_WEBUI_VENV_DIR}" "Open WebUI"
"${OPEN_WEBUI_VENV_DIR}/bin/pip" install --upgrade pip open-webui aiosqlite

export npm_config_prefix="${NPM_GLOBAL}"

echo "Installing/updating LM Studio CLI in the user profile."
curl -fsSL https://lmstudio.ai/install.sh | bash
add_lmstudio_paths

echo "Installing MCP helper packages into ${NPM_GLOBAL}"
npm install -g --prefix="${NPM_GLOBAL}" \
  @modelcontextprotocol/server-filesystem \
  @modelcontextprotocol/server-memory \
  @upstash/context7-mcp

ensure_secret_file "${SECRET_DIR}/webui-secret-key" 32
ensure_llm_wrapper_secret_file "${SECRET_DIR}/llm-wrapper.env"
WEBUI_SECRET_KEY="$(tr -d '\r\n' < "${SECRET_DIR}/webui-secret-key")"
LLM_WRAPPER_ENABLED="0"

if secret_file_has_openrouter_key "${SECRET_DIR}/llm-wrapper.env"; then
  LLM_WRAPPER_ENABLED="1"
fi

cat > "${MCP_CONFIG_DIR}/mcp-proxy-servers.json" <<EOF
{
  "mcpServers": {
    "filesystem": {
      "command": "${LOCAL_NODE_DIR}/bin/node",
      "args": [
        "${NPM_GLOBAL}/lib/node_modules/@modelcontextprotocol/server-filesystem/dist/index.js",
        "${HOME}",
        "${INSTALL_ROOT}",
        "/opt/nymphs3d/NymphsCore"
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
    },
    "context7": {
      "command": "${LOCAL_NODE_DIR}/bin/node",
      "args": [
        "${NPM_GLOBAL}/lib/node_modules/@upstash/context7-mcp/dist/index.js"
      ]
    },
    "llm-wrapper": {
      "command": "bash",
      "args": [
        "-lc",
        "if [[ -f \"\${LLM_WRAPPER_SECRET_FILE}\" ]]; then set -a; source \"\${LLM_WRAPPER_SECRET_FILE}\"; set +a; fi; exec \"\${MCP_VENV_DIR}/bin/python3\" \"\${CACHED_LLM_SERVER_PATH}\" --model \"\${LLM_WRAPPER_MODEL}\" --timeout 120 --cache-ttl 3600 --cache-dir \"\${LLM_WRAPPER_CACHE_DIR}\" --limit-user-prompt-length 2000"
      ],
      "env": {
        "MCP_VENV_DIR": "${MCP_VENV_DIR}",
        "CACHED_LLM_SERVER_PATH": "${LOCAL_TOOLS_DIR}/remote_llm_mcp/cached_llm_mcp_server.py",
        "LLM_WRAPPER_CACHE_DIR": "${MCP_DATA_DIR}/llm_cache",
        "LLM_WRAPPER_SECRET_FILE": "${SECRET_DIR}/llm-wrapper.env",
        "LLM_WRAPPER_MODEL": "${LLM_WRAPPER_MODEL}",
        "LLM_API_BASE_URL": "https://openrouter.ai/api/v1"
      }
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
    },
    "context7": {
      "url": "http://${MCP_HOST}:${MCP_PORT}/servers/context7/mcp",
      "type": "streamableHttp",
      "disabled": false,
      "timeout": 60
    },
    "llm-wrapper": {
      "url": "http://${MCP_HOST}:${MCP_PORT}/servers/llm-wrapper/mcp",
      "type": "streamableHttp",
      "disabled": false,
      "timeout": 60
    }
  }
}
EOF

CLINE_GLOBAL_SETTINGS="${HOME}/.config/Code/User/globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json"
if [[ -f "${CLINE_GLOBAL_SETTINGS}" ]]; then
  "${PYTHON_BIN}" - "${CLINE_GLOBAL_SETTINGS}" "${MCP_PORT}" <<'PYEOF'
import json
import sys
from pathlib import Path

settings_path = Path(sys.argv[1])
port = sys.argv[2]
settings = json.loads(settings_path.read_text(encoding="utf-8"))
servers = settings.setdefault("mcpServers", {})

servers.update(
    {
        "filesystem": {
            "url": f"http://127.0.0.1:{port}/servers/filesystem/mcp",
            "type": "streamableHttp",
            "disabled": False,
            "timeout": 60,
        },
        "memory": {
            "url": f"http://127.0.0.1:{port}/servers/memory/mcp",
            "type": "streamableHttp",
            "disabled": False,
            "timeout": 60,
        },
        "web-forager": {
            "url": f"http://127.0.0.1:{port}/servers/web-forager/mcp",
            "type": "streamableHttp",
            "disabled": False,
            "timeout": 60,
        },
        "context7": {
            "url": f"http://127.0.0.1:{port}/servers/context7/mcp",
            "type": "streamableHttp",
            "disabled": False,
            "timeout": 60,
        },
        "llm-wrapper": {
            "url": f"http://127.0.0.1:{port}/servers/llm-wrapper/mcp",
            "type": "streamableHttp",
            "disabled": False,
            "timeout": 60,
        },
    }
)

settings_path.parent.mkdir(parents=True, exist_ok=True)
settings_path.write_text(json.dumps(settings, indent=2) + "\n", encoding="utf-8")
PYEOF
fi

cat > "${MCP_CONFIG_DIR}/mcpo-servers.json" <<EOF
{
  "mcpServers": {
    "filesystem": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:${MCP_PORT}/servers/filesystem/mcp"
    },
    "memory": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:${MCP_PORT}/servers/memory/mcp"
    },
    "web-forager": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:${MCP_PORT}/servers/web-forager/mcp"
    },
    "context7": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:${MCP_PORT}/servers/context7/mcp"
    },
    "llm-wrapper": {
      "type": "streamable-http",
      "url": "http://127.0.0.1:${MCP_PORT}/servers/llm-wrapper/mcp"
    }
  }
}
EOF

cat > "${MCP_CONFIG_DIR}/open-webui-tool-servers.json" <<EOF
[
  {
    "type": "openapi",
    "url": "http://127.0.0.1:${MCPO_OPENAPI_PORT}/filesystem",
    "path": "openapi.json",
    "spec_type": "url",
    "spec": "",
    "auth_type": "none",
    "key": "",
    "config": { "enable": true },
    "info": {
      "id": "nymphs-brain-filesystem",
      "name": "Nymphs Brain Filesystem",
      "description": "Filesystem tools exposed by Nymphs-Brain mcpo."
    }
  },
  {
    "type": "openapi",
    "url": "http://127.0.0.1:${MCPO_OPENAPI_PORT}/memory",
    "path": "openapi.json",
    "spec_type": "url",
    "spec": "",
    "auth_type": "none",
    "key": "",
    "config": { "enable": true },
    "info": {
      "id": "nymphs-brain-memory",
      "name": "Nymphs Brain Memory",
      "description": "Memory tools exposed by Nymphs-Brain mcpo."
    }
  },
  {
    "type": "openapi",
    "url": "http://127.0.0.1:${MCPO_OPENAPI_PORT}/web-forager",
    "path": "openapi.json",
    "spec_type": "url",
    "spec": "",
    "auth_type": "none",
    "key": "",
    "config": { "enable": true },
    "info": {
      "id": "nymphs-brain-web-forager",
      "name": "Nymphs Brain Web Forager",
      "description": "Web Forager tools exposed by Nymphs-Brain mcpo."
    }
  },
  {
    "type": "openapi",
    "url": "http://127.0.0.1:${MCPO_OPENAPI_PORT}/context7",
    "path": "openapi.json",
    "spec_type": "url",
    "spec": "",
    "auth_type": "none",
    "key": "",
    "config": { "enable": true },
    "info": {
      "id": "nymphs-brain-context7",
      "name": "Nymphs Brain Context7",
      "description": "Context7 documentation tools exposed by Nymphs-Brain mcpo."
    }
  },
  {
    "type": "openapi",
    "url": "http://127.0.0.1:${MCPO_OPENAPI_PORT}/llm-wrapper",
    "path": "openapi.json",
    "spec_type": "url",
    "spec": "",
    "auth_type": "none",
    "key": "",
    "config": { "enable": true },
    "info": {
      "id": "nymphs-brain-llm-wrapper",
      "name": "Nymphs Brain LLM Wrapper",
      "description": "Remote model delegation tools exposed by Nymphs-Brain mcpo."
    }
  }
]
EOF

cat > "${MCP_CONFIG_DIR}/open-webui-mcp-servers.md" <<EOF
Nymphs-Brain MCP servers for Open WebUI

Open WebUI launch now seeds Brain tool connections automatically from:

- ${MCP_CONFIG_DIR}/open-webui-tool-servers.json

Those seeded OpenAPI tool server entries point at mcpo routes:

- Filesystem: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/filesystem
- Memory: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/memory
- Web Forager: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/web-forager
- Context7: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/context7
- LLM Wrapper: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/llm-wrapper

If you prefer Open WebUI native MCP instead of OpenAPI, add these manually in Admin Settings -> External Tools:

Type: MCP (Streamable HTTP)
Auth: None

- Filesystem: http://${MCP_HOST}:${MCP_PORT}/servers/filesystem/mcp
- Memory: http://${MCP_HOST}:${MCP_PORT}/servers/memory/mcp
- Web Forager: http://${MCP_HOST}:${MCP_PORT}/servers/web-forager/mcp
- Context7: http://${MCP_HOST}:${MCP_PORT}/servers/context7/mcp
- LLM Wrapper: http://${MCP_HOST}:${MCP_PORT}/servers/llm-wrapper/mcp

Notes
- Open WebUI default URL: http://localhost:${OPEN_WEBUI_PORT}
- mcpo default URL: http://localhost:${MCPO_OPENAPI_PORT}
- Cline uses the direct MCP endpoints with transport type streamableHttp.
- llm-wrapper requires a valid OpenRouter key in ${SECRET_DIR}/llm-wrapper.env.
EOF

if [[ "${LLM_WRAPPER_ENABLED}" != "1" ]]; then
  "${PYTHON_BIN}" - "${MCP_CONFIG_DIR}" "${CLINE_GLOBAL_SETTINGS}" <<'PYEOF'
import json
import sys
from pathlib import Path

config_dir = Path(sys.argv[1])
cline_global_settings = Path(sys.argv[2])

for name in [
    "mcp-proxy-servers.json",
    "cline-mcp-settings.json",
    "mcpo-servers.json",
    "open-webui-tool-servers.json",
]:
    path = config_dir / name
    if not path.exists():
        continue

    data = json.loads(path.read_text(encoding="utf-8"))

    if name == "open-webui-tool-servers.json":
      data = [
          item
          for item in data
          if item.get("info", {}).get("id") != "nymphs-brain-llm-wrapper"
      ]
    else:
      servers = data.get("mcpServers", {})
      servers.pop("llm-wrapper", None)

    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")

if cline_global_settings.exists():
    data = json.loads(cline_global_settings.read_text(encoding="utf-8"))
    data.setdefault("mcpServers", {}).pop("llm-wrapper", None)
    cline_global_settings.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PYEOF
fi

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
INITIAL_PLAN_MODEL_ID="${EXISTING_PLAN_MODEL_ID}"
INITIAL_PLAN_CONTEXT_LENGTH="${EXISTING_PLAN_CONTEXT_LENGTH}"
INITIAL_ACT_MODEL_ID="${EXISTING_ACT_MODEL_ID}"
INITIAL_ACT_CONTEXT_LENGTH="${EXISTING_ACT_CONTEXT_LENGTH}"
INITIAL_LLM_WRAPPER_MODEL="${EXISTING_LLM_WRAPPER_MODEL:-${LLM_WRAPPER_MODEL}}"
INITIAL_PRIMARY_MODEL_ROLE="${EXISTING_PRIMARY_MODEL_ROLE:-plan}"

if [[ -n "${MODEL_ID}" ]]; then
  INITIAL_PLAN_MODEL_ID="${MODEL_ID}"
  INITIAL_PLAN_CONTEXT_LENGTH="${CONTEXT_LENGTH}"
fi

cat > "${PROFILE_CONFIG_FILE}" <<EOF
PLAN_MODEL_ID="${INITIAL_PLAN_MODEL_ID}"
PLAN_CONTEXT_LENGTH="${INITIAL_PLAN_CONTEXT_LENGTH}"
ACT_MODEL_ID="${INITIAL_ACT_MODEL_ID}"
ACT_CONTEXT_LENGTH="${INITIAL_ACT_CONTEXT_LENGTH}"
LLM_WRAPPER_MODEL="${INITIAL_LLM_WRAPPER_MODEL}"
PRIMARY_MODEL_ROLE="${INITIAL_PRIMARY_MODEL_ROLE}"
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
LLM_WRAPPER_MODEL=""

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
load_profile "plan" "${PLAN_MODEL_ID}" "${PLAN_CONTEXT_LENGTH}"
load_profile "act" "${ACT_MODEL_ID}" "${ACT_CONTEXT_LENGTH}"
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

cat > "${BIN_DIR}/brain-refresh" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
INSTALLER_COPY="${INSTALL_ROOT}/brain-installer.sh"

if [[ ! -x "${INSTALLER_COPY}" ]]; then
  echo "Brain refresh script is missing at ${INSTALLER_COPY}. Rerun install_nymphs_brain.sh." >&2
  exit 1
fi

exec "${INSTALLER_COPY}" --install-root "${INSTALL_ROOT}" --quiet
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
  local llm_wrapper_model=""
  local primary_model_role="plan"

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
LLM_WRAPPER_MODEL="${llm_wrapper_model:-${LLM_WRAPPER_MODEL}}"
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
  local models_root="${HOME}/.lmstudio/models"

  resolve_model_dir() {
    local model_key="$1"
    python_json - "${models_root}" "${model_key}" <<'PYEOF'
import re
import sys
from pathlib import Path

root = Path(sys.argv[1]).expanduser()
model_key = sys.argv[2]

def norm(value: str) -> str:
    return re.sub(r"[^a-z0-9]+", "", value.lower())

if not root.exists():
    raise SystemExit(1)

key_parts = [part for part in model_key.split("/") if part]
candidates = [model_key]
if key_parts:
    candidates.append(key_parts[-1])

normalized_candidates = [norm(item) for item in candidates if item]
direct = root / model_key
if direct.exists() and direct.is_dir():
    print(direct)
    raise SystemExit(0)

for path in root.glob("*/*"):
    if not path.is_dir():
        continue
    haystack = norm("/".join(path.parts[-2:]))
    if any(candidate and candidate in haystack for candidate in normalized_candidates):
        print(path)
        raise SystemExit(0)

raise SystemExit(1)
PYEOF
  }

  clear_removed_profile_refs() {
    local removed_key="$1"
    local plan_key=""
    local act_key=""

    plan_key="$("${INSTALL_ROOT}/bin/lms-get-profile" plan 2>/dev/null || true)"
    act_key="$("${INSTALL_ROOT}/bin/lms-get-profile" act 2>/dev/null || true)"

    if [[ "${plan_key}" == "${removed_key}" ]]; then
      "${INSTALL_ROOT}/bin/lms-set-profile" plan clear
    fi

    if [[ "${act_key}" == "${removed_key}" ]]; then
      "${INSTALL_ROOT}/bin/lms-set-profile" act clear
    fi
  }

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
          model_dir="$(resolve_model_dir "${model_choice}" || true)"
          if [[ -z "${model_dir}" ]]; then
            echo "Could not find a local LM Studio folder for '${model_choice}' under ${models_root}." >&2
            echo "Open ${models_root} and remove the model folder manually if needed." >&2
            break
          fi

          "${LMS_BIN}" unload "${model_choice}" >/dev/null 2>&1 || true
          rm -rf -- "${model_dir}"
          clear_removed_profile_refs "${model_choice}"
          echo "Removed ${model_choice} from ${model_dir}."
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

set_remote_llm_wrapper_model() {
  local current_remote_model=""
  local role="remote"
  local selected_remote_model=""
  local selected_choice=""

  current_remote_model="$("${INSTALL_ROOT}/bin/lms-get-profile" remote 2>/dev/null || true)"

  echo
  echo "Set Remote llm-wrapper Model"
  echo "Current remote model: ${current_remote_model:-${LLM_WRAPPER_MODEL:-none}}"
  echo
  echo "1) openai/gpt-4o-mini"
  echo "2) anthropic/claude-3.5-sonnet"
  echo "3) openai/gpt-4o"
  echo "4) google/gemini-flash-1.5"
  echo "5) deepseek/deepseek-chat"
  echo "6) nvidia/nemotron-3-super-120b-a12b:free"
  echo "7) anthropic/claude-3-haiku"
  echo "8) Enter custom OpenRouter model"
  echo "9) Clear remote model override"
  echo "10) Back"
  echo

  read -rp "Enter your choice (1-10): " selected_choice

  case "${selected_choice}" in
    1) selected_remote_model="openai/gpt-4o-mini" ;;
    2) selected_remote_model="anthropic/claude-3.5-sonnet" ;;
    3) selected_remote_model="openai/gpt-4o" ;;
    4) selected_remote_model="google/gemini-flash-1.5" ;;
    5) selected_remote_model="deepseek/deepseek-chat" ;;
    6) selected_remote_model="nvidia/nemotron-3-super-120b-a12b:free" ;;
    7) selected_remote_model="anthropic/claude-3-haiku" ;;
    8)
      read -rp "Enter custom OpenRouter model id (provider/model): " selected_remote_model
      selected_remote_model="${selected_remote_model//[$'\r\n']/}"
      selected_remote_model="${selected_remote_model#"${selected_remote_model%%[![:space:]]*}"}"
      selected_remote_model="${selected_remote_model%"${selected_remote_model##*[![:space:]]}"}"
      if [[ -z "${selected_remote_model}" ]]; then
        echo "Custom remote model cannot be empty."
        return
      fi
      ;;
    9)
      "${INSTALL_ROOT}/bin/lms-set-profile" "${role}" clear
      return
      ;;
    10) return ;;
    *)
      echo "Invalid choice. Please try again."
      return
      ;;
  esac

  "${INSTALL_ROOT}/bin/lms-set-profile" "${role}" "${selected_remote_model}"
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
    echo "7) Set Remote llm-wrapper Model"
    echo "8) Remove Models"
    echo "9) Exit"
    echo
    read -rp "Enter your choice (1-9): " choice

    case "${choice}" in
      1) use_downloaded_model_menu "plan" ;;
      2) use_downloaded_model_menu "act" ;;
      3) run_add_change_model "plan" "" ;;
      4) run_add_change_model "act" "" ;;
      5) clear_plan_model ;;
      6) clear_act_model ;;
      7) set_remote_llm_wrapper_model ;;
      8) remove_models_menu ;;
      9) return ;;
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
LLM_WRAPPER_MODEL=""

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
  remote|llm-wrapper|wrapper)
    printf '%s\n' "${LLM_WRAPPER_MODEL}"
    ;;
  all)
    printf 'plan: %s (context %s)\n' "${PLAN_MODEL_ID:-none}" "${PLAN_CONTEXT_LENGTH:-none}"
    printf 'act: %s (context %s)\n' "${ACT_MODEL_ID:-none}" "${ACT_CONTEXT_LENGTH:-none}"
    printf 'remote llm-wrapper: %s\n' "${LLM_WRAPPER_MODEL:-none}"
    ;;
  *)
    echo "Usage: lms-get-profile [plan|act|remote]" >&2
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
LLM_WRAPPER_MODEL=""
PRIMARY_MODEL_ROLE="plan"

if [[ -f "${PROFILE_CONFIG_FILE}" ]]; then
  # shellcheck disable=SC1090
  source "${PROFILE_CONFIG_FILE}"
fi

case "${ROLE}" in
  plan|act|remote|llm-wrapper|wrapper) ;;
  *)
    echo "Role must be 'plan', 'act', or 'remote'." >&2
    exit 1
    ;;
esac

if [[ "${MODEL_KEY}" == "clear" || "${MODEL_KEY}" == "none" || "${MODEL_KEY}" == "-" ]]; then
  MODEL_KEY=""
fi

if [[ "${ROLE}" == "remote" || "${ROLE}" == "llm-wrapper" || "${ROLE}" == "wrapper" ]]; then
  CONTEXT_LENGTH=""
elif [[ -z "${CONTEXT_LENGTH}" ]]; then
  if [[ "${ROLE}" == "plan" ]]; then
    CONTEXT_LENGTH="${PLAN_CONTEXT_LENGTH:-16384}"
  else
    CONTEXT_LENGTH="${ACT_CONTEXT_LENGTH:-16384}"
  fi
fi

if [[ "${ROLE}" == "plan" ]]; then
  PLAN_MODEL_ID="${MODEL_KEY}"
  PLAN_CONTEXT_LENGTH="${MODEL_KEY:+${CONTEXT_LENGTH}}"
elif [[ "${ROLE}" == "act" ]]; then
  ACT_MODEL_ID="${MODEL_KEY}"
  ACT_CONTEXT_LENGTH="${MODEL_KEY:+${CONTEXT_LENGTH}}"
else
  LLM_WRAPPER_MODEL="${MODEL_KEY}"
fi

mkdir -p "$(dirname "${PROFILE_CONFIG_FILE}")"
cat > "${PROFILE_CONFIG_FILE}" <<EOF
PLAN_MODEL_ID="${PLAN_MODEL_ID}"
PLAN_CONTEXT_LENGTH="${PLAN_CONTEXT_LENGTH}"
ACT_MODEL_ID="${ACT_MODEL_ID}"
ACT_CONTEXT_LENGTH="${ACT_CONTEXT_LENGTH}"
LLM_WRAPPER_MODEL="${LLM_WRAPPER_MODEL}"
PRIMARY_MODEL_ROLE="${PRIMARY_MODEL_ROLE}"
EOF

if [[ "${ROLE}" == "act" && -n "${ACT_MODEL_ID}" ]]; then
  update_agent_script "${ACT_MODEL_ID}"
fi

if [[ -n "${MODEL_KEY}" ]]; then
  if [[ "${ROLE}" == "remote" || "${ROLE}" == "llm-wrapper" || "${ROLE}" == "wrapper" ]]; then
    echo "Updated remote llm-wrapper profile: ${MODEL_KEY}"
  else
    echo "Updated ${ROLE} profile: ${MODEL_KEY} (context ${CONTEXT_LENGTH})"
  fi
else
  if [[ "${ROLE}" == "remote" || "${ROLE}" == "llm-wrapper" || "${ROLE}" == "wrapper" ]]; then
    echo "Cleared remote llm-wrapper profile."
  else
    echo "Cleared ${ROLE} profile."
  fi
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
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-__MCP_HOST__}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-__MCP_PORT__}}"
OPEN_WEBUI_PORT="${NYMPHS_BRAIN_OPEN_WEBUI_PORT:-__OPEN_WEBUI_PORT__}"
MCPO_OPENAPI_PORT="${NYMPHS_BRAIN_MCPO_OPENAPI_PORT:-__MCPO_OPENAPI_PORT__}"
MCP_PID_FILE="${MCP_LOG_DIR}/mcp-proxy.pid"
MCP_LOG_FILE="${MCP_LOG_DIR}/mcp-proxy.log"
MCPO_PID_FILE="${MCP_LOG_DIR}/mcpo.pid"
MCPO_LOG_FILE="${MCP_LOG_DIR}/mcpo.log"
MCP_CONFIG_FILE="${MCP_CONFIG_DIR}/mcp-proxy-servers.json"
MCPO_CONFIG_FILE="${MCP_CONFIG_DIR}/mcpo-servers.json"

is_mcp_running() {
  [[ -f "${MCP_PID_FILE}" ]] && kill -0 "$(cat "${MCP_PID_FILE}")" >/dev/null 2>&1
}

is_mcpo_running() {
  [[ -f "${MCPO_PID_FILE}" ]] && kill -0 "$(cat "${MCPO_PID_FILE}")" >/dev/null 2>&1
}

mcpo_probe_url() {
  echo "http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/filesystem/openapi.json"
}

wait_for_url() {
  local url="$1"
  local attempts="$2"

  for _ in $(seq 1 "${attempts}"); do
    if curl -fsS "${url}" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done

  return 1
}

has_llm_wrapper() {
  [[ -f "${MCP_CONFIG_FILE}" ]] && grep -q '"llm-wrapper"' "${MCP_CONFIG_FILE}"
}

mkdir -p "${MCP_LOG_DIR}"

if is_mcp_running; then
  echo "Nymphs-Brain MCP gateway is already running at http://${MCP_HOST}:${MCP_PORT}"
else
  if [[ ! -x "${MCP_VENV_DIR}/bin/mcp-proxy" ]]; then
    echo "mcp-proxy is not installed at ${MCP_VENV_DIR}/bin/mcp-proxy. Rerun install_nymphs_brain.sh." >&2
    exit 1
  fi

  if [[ ! -f "${MCP_CONFIG_FILE}" ]]; then
    echo "MCP config is missing at ${MCP_CONFIG_FILE}. Rerun install_nymphs_brain.sh." >&2
    exit 1
  fi

  echo "Starting Nymphs-Brain MCP gateway at http://${MCP_HOST}:${MCP_PORT}"
  nohup "${MCP_VENV_DIR}/bin/mcp-proxy" \
    --host "${MCP_HOST}" \
    --port "${MCP_PORT}" \
    --allow-origin "http://localhost:${OPEN_WEBUI_PORT}" \
    --allow-origin "http://127.0.0.1:${OPEN_WEBUI_PORT}" \
    --named-server-config "${MCP_CONFIG_FILE}" \
    > "${MCP_LOG_FILE}" 2>&1 &

  echo "$!" > "${MCP_PID_FILE}"
fi

if ! wait_for_url "http://${MCP_HOST}:${MCP_PORT}/status" 60; then
  echo "MCP gateway did not become ready in time. See ${MCP_LOG_FILE}" >&2
  exit 1
fi

if is_mcpo_running; then
  echo "Nymphs-Brain mcpo OpenAPI bridge is already running at http://${MCP_HOST}:${MCPO_OPENAPI_PORT}"
else
  if [[ ! -x "${MCP_VENV_DIR}/bin/mcpo" ]]; then
    echo "mcpo is not installed at ${MCP_VENV_DIR}/bin/mcpo. Rerun install_nymphs_brain.sh." >&2
    exit 1
  fi

  if [[ ! -f "${MCPO_CONFIG_FILE}" ]]; then
    echo "mcpo config is missing at ${MCPO_CONFIG_FILE}. Rerun install_nymphs_brain.sh." >&2
    exit 1
  fi

  echo "Starting Nymphs-Brain mcpo OpenAPI bridge at http://${MCP_HOST}:${MCPO_OPENAPI_PORT}"
  nohup "${MCP_VENV_DIR}/bin/mcpo" \
    --host "${MCP_HOST}" \
    --port "${MCPO_OPENAPI_PORT}" \
    --config "${MCPO_CONFIG_FILE}" \
    > "${MCPO_LOG_FILE}" 2>&1 &

  echo "$!" > "${MCPO_PID_FILE}"
fi

if ! wait_for_url "$(mcpo_probe_url)" 60; then
  echo "mcpo did not become ready in time. See ${MCPO_LOG_FILE}" >&2
  exit 1
fi

echo ""
echo "=========================================="
echo "Nymphs-Brain MCP stack is running:"
echo "  MCP gateway:    http://${MCP_HOST}:${MCP_PORT}"
echo "  OpenAPI bridge: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}"
echo "  Filesystem API: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/filesystem"
echo "  Memory API:     http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/memory"
echo "  Web Forager:    http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/web-forager"
echo "  Context7 API:   http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/context7"
if has_llm_wrapper; then
  echo "  LLM Wrapper:    http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/llm-wrapper"
fi
echo "=========================================="
WRAPEOF

cat > "${BIN_DIR}/mcp-stop" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-__MCP_HOST__}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-__MCP_PORT__}}"
MCPO_OPENAPI_PORT="${NYMPHS_BRAIN_MCPO_OPENAPI_PORT:-__MCPO_OPENAPI_PORT__}"
MCP_PID_FILE="${INSTALL_ROOT}/mcp/logs/mcp-proxy.pid"
MCPO_PID_FILE="${INSTALL_ROOT}/mcp/logs/mcpo.pid"
MCP_CONFIG_FILE="${INSTALL_ROOT}/mcp/config/mcp-proxy-servers.json"

has_llm_wrapper() {
  [[ -f "${MCP_CONFIG_FILE}" ]] && grep -q '"llm-wrapper"' "${MCP_CONFIG_FILE}"
}

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
  local target_port="$1"
  ss -ltnp 2>/dev/null | awk -v target=":${target_port}" '
    index($4, target) {
      if (match($0, /pid=[0-9]+/)) {
        print substr($0, RSTART + 4, RLENGTH - 4)
      }
    }
  ' | sort -u
}

echo "Stopping Nymphs-Brain MCP stack..."

stop_service() {
  local label="$1"
  local pid_file="$2"
  local port="$3"
  local probe_url="$4"
  local stopped_any=0

  if [[ -f "${pid_file}" ]]; then
    stop_pid "$(cat "${pid_file}" 2>/dev/null || true)"
    stopped_any=1
  fi

  while read -r pid; do
    [[ -n "${pid}" ]] || continue
    stop_pid "${pid}"
    stopped_any=1
  done < <(port_pids "${port}")

  rm -f "${pid_file}"

  if curl -fsS "${probe_url}" >/dev/null 2>&1; then
    echo "${label} still appears to be running on port ${port}." >&2
    exit 1
  fi

  if [[ "${stopped_any}" -eq 1 ]]; then
    echo "${label} stopped."
  else
    echo "${label} is not running."
  fi
}

stop_service "Nymphs-Brain mcpo OpenAPI bridge" "${MCPO_PID_FILE}" "${MCPO_OPENAPI_PORT}" "http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/filesystem/openapi.json"
stop_service "Nymphs-Brain MCP gateway" "${MCP_PID_FILE}" "${MCP_PORT}" "http://${MCP_HOST}:${MCP_PORT}/status"
WRAPEOF

cat > "${BIN_DIR}/mcp-status" <<'WRAPEOF'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
MCP_HOST="${NYMPHS_BRAIN_MCP_HOST:-${NYMPHS_BRAIN_MCPO_HOST:-__MCP_HOST__}}"
MCP_PORT="${NYMPHS_BRAIN_MCP_PORT:-${NYMPHS_BRAIN_MCPO_PORT:-__MCP_PORT__}}"
MCPO_OPENAPI_PORT="${NYMPHS_BRAIN_MCPO_OPENAPI_PORT:-__MCPO_OPENAPI_PORT__}"
MCP_PID_FILE="${INSTALL_ROOT}/mcp/logs/mcp-proxy.pid"
MCPO_PID_FILE="${INSTALL_ROOT}/mcp/logs/mcpo.pid"

if [[ -f "${MCP_PID_FILE}" ]] && kill -0 "$(cat "${MCP_PID_FILE}")" >/dev/null 2>&1; then
  echo "MCP proxy: running (pid $(cat "${MCP_PID_FILE}"))"
else
  echo "MCP proxy: stopped"
fi

if [[ -f "${MCPO_PID_FILE}" ]] && kill -0 "$(cat "${MCPO_PID_FILE}")" >/dev/null 2>&1; then
  echo "mcpo OpenAPI: running (pid $(cat "${MCPO_PID_FILE}"))"
else
  echo "mcpo OpenAPI: stopped"
fi

echo ""
echo "MCP gateway URL:    http://${MCP_HOST}:${MCP_PORT}"
echo "MCP status URL:     http://${MCP_HOST}:${MCP_PORT}/status"
echo "OpenAPI bridge URL: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}"
echo "OpenAPI probe URL:  http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/filesystem/openapi.json"
echo "OpenAPI docs:"
echo "- filesystem:  http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/filesystem/docs"
echo "- memory:      http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/memory/docs"
echo "- web-forager: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/web-forager/docs"
echo "- context7:    http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/context7/docs"
if has_llm_wrapper; then
  echo "- llm-wrapper: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/llm-wrapper/docs"
fi
echo ""
echo "Streamable HTTP endpoints:"
echo "- filesystem: http://${MCP_HOST}:${MCP_PORT}/servers/filesystem/mcp"
echo "- memory: http://${MCP_HOST}:${MCP_PORT}/servers/memory/mcp"
echo "- web-forager: http://${MCP_HOST}:${MCP_PORT}/servers/web-forager/mcp"
echo "- context7: http://${MCP_HOST}:${MCP_PORT}/servers/context7/mcp"
if has_llm_wrapper; then
  echo "- llm-wrapper: http://${MCP_HOST}:${MCP_PORT}/servers/llm-wrapper/mcp"
fi
echo "Legacy SSE endpoints:"
echo "- filesystem: http://${MCP_HOST}:${MCP_PORT}/servers/filesystem/sse"
echo "- memory: http://${MCP_HOST}:${MCP_PORT}/servers/memory/sse"
echo "- web-forager: http://${MCP_HOST}:${MCP_PORT}/servers/web-forager/sse"
echo "- context7: http://${MCP_HOST}:${MCP_PORT}/servers/context7/sse"
if has_llm_wrapper; then
  echo "- llm-wrapper: http://${MCP_HOST}:${MCP_PORT}/servers/llm-wrapper/sse"
fi
echo "MCP config: ${INSTALL_ROOT}/mcp/config/mcp-proxy-servers.json"
echo "mcpo config: ${INSTALL_ROOT}/mcp/config/mcpo-servers.json"
echo "Cline config template: ${INSTALL_ROOT}/mcp/config/cline-mcp-settings.json"
echo "Open WebUI tool seed: ${INSTALL_ROOT}/mcp/config/open-webui-tool-servers.json"
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
MCPO_OPENAPI_PORT="${NYMPHS_BRAIN_MCPO_OPENAPI_PORT:-__MCPO_OPENAPI_PORT__}"
LMSTUDIO_API_BASE_URL="${NYMPHS_BRAIN_LMSTUDIO_API_BASE_URL:-__LMSTUDIO_API_BASE_URL__}"
PID_FILE="${OPEN_WEBUI_LOG_DIR}/open-webui.pid"
LOG_FILE="${OPEN_WEBUI_LOG_DIR}/open-webui.log"
WEBUI_SECRET_KEY_FILE="${SECRET_DIR}/webui-secret-key"
TOOL_SERVER_CONNECTIONS_FILE="${INSTALL_ROOT}/mcp/config/open-webui-tool-servers.json"

is_running() {
  [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1
}

seed_tool_server_connections() {
  if [[ ! -f "${TOOL_SERVER_CONNECTIONS_FILE}" ]]; then
    echo "Open WebUI tool server seed file is missing at ${TOOL_SERVER_CONNECTIONS_FILE}. Rerun install_nymphs_brain.sh." >&2
    return 1
  fi

  DATA_DIR="${OPEN_WEBUI_DATA_DIR}" TOOL_SERVER_CONNECTIONS_FILE="${TOOL_SERVER_CONNECTIONS_FILE}" \
    "${OPEN_WEBUI_VENV_DIR}/bin/python3" - <<'PYEOF'
import json
import os
from pathlib import Path

payload = json.loads(Path(os.environ["TOOL_SERVER_CONNECTIONS_FILE"]).read_text(encoding="utf-8"))

from open_webui.config import ENABLE_DIRECT_CONNECTIONS, TOOL_SERVER_CONNECTIONS

managed_ids = {
    item.get("info", {}).get("id")
    for item in payload
    if item.get("info", {}).get("id")
}

current = list(TOOL_SERVER_CONNECTIONS.value or [])
preserved = [
    item
    for item in current
    if item.get("info", {}).get("id") not in managed_ids
]
merged = preserved + payload

if current != merged:
    TOOL_SERVER_CONNECTIONS.value = merged
    TOOL_SERVER_CONNECTIONS.save()

if ENABLE_DIRECT_CONNECTIONS.value is not True:
    ENABLE_DIRECT_CONNECTIONS.value = True
    ENABLE_DIRECT_CONNECTIONS.save()
PYEOF
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
seed_tool_server_connections

WEBUI_SECRET_KEY="$(tr -d '\r\n' < "${WEBUI_SECRET_KEY_FILE}")"

echo "Starting Open WebUI at http://${OPEN_WEBUI_HOST}:${OPEN_WEBUI_PORT}"
nohup env \
  DATA_DIR="${OPEN_WEBUI_DATA_DIR}" \
  WEBUI_SECRET_KEY="${WEBUI_SECRET_KEY}" \
  ENABLE_DIRECT_CONNECTIONS="True" \
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
    echo "Brain OpenAPI tool servers were seeded automatically from:"
    echo "${TOOL_SERVER_CONNECTIONS_FILE}"
    echo "mcpo base URL: http://${MCP_HOST}:${MCPO_OPENAPI_PORT}"
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
MCPO_OPENAPI_PORT="${NYMPHS_BRAIN_MCPO_OPENAPI_PORT:-__MCPO_OPENAPI_PORT__}"
PID_FILE="${INSTALL_ROOT}/open-webui-data/logs/open-webui.pid"

if [[ -f "${PID_FILE}" ]] && kill -0 "$(cat "${PID_FILE}")" >/dev/null 2>&1; then
  echo "Open WebUI: running"
else
  echo "Open WebUI: stopped"
fi

echo "Open WebUI URL: http://${OPEN_WEBUI_HOST}:${OPEN_WEBUI_PORT}"
echo "Windows URL: http://localhost:${OPEN_WEBUI_PORT}"
echo "mcpo OpenAPI bridge: http://127.0.0.1:${MCPO_OPENAPI_PORT}"
echo "Open WebUI data: ${INSTALL_ROOT}/open-webui-data"
echo "Open WebUI log: ${INSTALL_ROOT}/open-webui-data/logs/open-webui.log"
echo "Open WebUI tool server seed: ${INSTALL_ROOT}/mcp/config/open-webui-tool-servers.json"
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
MCPO_OPENAPI_PORT="${NYMPHS_BRAIN_MCPO_OPENAPI_PORT:-__MCPO_OPENAPI_PORT__}"
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
if not loaded:
    try:
        with open("/tmp/nymphs-brain-models.json", "r", encoding="utf-8") as handle:
            api_payload = json.load(handle)
    except Exception:
        api_payload = []
    loaded = extract_model_keys(api_payload)

print(", ".join(loaded) if loaded else "none reported")
' 2>/dev/null || "${INSTALL_ROOT}/venv/bin/python3" -c "import json; from pathlib import Path; data=json.loads(Path('/tmp/nymphs-brain-models.json').read_text(encoding='utf-8')); models=data.get('data', []); loaded=[item.get('id') for item in models if isinstance(item, dict) and item.get('id')]; print(', '.join(loaded) if loaded else 'none reported')" 2>/dev/null || echo unknown)"
  echo "Model loaded: ${MODEL_OUTPUT}"
else
  echo "LLM server: stopped"
  echo "Model loaded: none"
fi

echo "Act model: ${ACT_MODEL_ID:-none} (context ${ACT_CONTEXT_LENGTH:-none})"
echo "Plan model: ${PLAN_MODEL_ID:-none} (context ${PLAN_CONTEXT_LENGTH:-none})"
echo "Remote llm-wrapper model: ${LLM_WRAPPER_MODEL:-none}"

if curl "${CURL_CHECK_ARGS[@]}" "http://${MCP_HOST}:${MCP_PORT}/status" >/dev/null 2>&1; then
  echo "MCP proxy: running"
else
  echo "MCP proxy: stopped"
fi

if curl "${CURL_CHECK_ARGS[@]}" "http://${MCP_HOST}:${MCPO_OPENAPI_PORT}/filesystem/openapi.json" >/dev/null 2>&1; then
  echo "OpenAPI proxy: running"
else
  echo "OpenAPI proxy: stopped"
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
sed -i "s|__MCP_HOST__|${MCP_HOST}|g" "${BIN_DIR}/mcp-start" "${BIN_DIR}/mcp-stop" "${BIN_DIR}/mcp-status" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/brain-status"
sed -i "s|__MCP_PORT__|${MCP_PORT}|g" "${BIN_DIR}/mcp-start" "${BIN_DIR}/mcp-stop" "${BIN_DIR}/mcp-status" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/brain-status"
sed -i "s|__MCPO_OPENAPI_PORT__|${MCPO_OPENAPI_PORT}|g" "${BIN_DIR}/mcp-start" "${BIN_DIR}/mcp-stop" "${BIN_DIR}/mcp-status" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/open-webui-status" "${BIN_DIR}/brain-status"
sed -i "s|__OPEN_WEBUI_HOST__|${OPEN_WEBUI_HOST}|g" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/open-webui-status" "${BIN_DIR}/brain-status"
sed -i "s|__OPEN_WEBUI_PORT__|${OPEN_WEBUI_PORT}|g" "${BIN_DIR}/mcp-start" "${BIN_DIR}/open-webui-start" "${BIN_DIR}/open-webui-status" "${BIN_DIR}/brain-status"
sed -i "s|__LMSTUDIO_API_BASE_URL__|${LMSTUDIO_API_BASE_URL}|g" "${BIN_DIR}/open-webui-start"
if [[ "$(readlink -f "$0")" != "$(readlink -f "${INSTALL_ROOT}/brain-installer.sh")" ]]; then
  cp "$0" "${INSTALL_ROOT}/brain-installer.sh"
fi
chmod +x \
  "${INSTALL_ROOT}/brain-installer.sh" \
  "${BIN_DIR}/brain-refresh" \
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
Plan model: ${INITIAL_PLAN_MODEL_ID:-none}
Act model: ${INITIAL_ACT_MODEL_ID:-none}
Quantization: ${QUANTIZATION}
Plan context length: ${INITIAL_PLAN_CONTEXT_LENGTH:-none}
Act context length: ${INITIAL_ACT_CONTEXT_LENGTH:-none}
Model download during install: ${DOWNLOAD_MODEL}
llm-wrapper default model: ${LLM_WRAPPER_MODEL}
LM Studio CLI location: user profile managed by LM Studio
Commands:
- ${BIN_DIR}/brain-refresh
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
mcpo OpenAPI bridge: http://localhost:${MCPO_OPENAPI_PORT}
MCP gateway URL: http://localhost:${MCP_PORT}
Primary Streamable HTTP endpoints:
- http://localhost:${MCP_PORT}/servers/filesystem/mcp
- http://localhost:${MCP_PORT}/servers/memory/mcp
- http://localhost:${MCP_PORT}/servers/web-forager/mcp
- http://localhost:${MCP_PORT}/servers/context7/mcp
$(if [[ "${LLM_WRAPPER_ENABLED}" == "1" ]]; then echo "- http://localhost:${MCP_PORT}/servers/llm-wrapper/mcp"; fi)
Open WebUI seeded tool config: ${MCP_CONFIG_DIR}/open-webui-tool-servers.json
EOF

echo "Nymphs-Brain setup complete."
echo "Refresh local Brain wrappers: ${BIN_DIR}/brain-refresh"
echo "Start LM Studio model server: ${BIN_DIR}/lms-start"
echo "Change/download/remove LM Studio models: ${BIN_DIR}/lms-model"
echo "Set plan/act model profiles: ${BIN_DIR}/lms-set-profile"
echo "Update LM Studio CLI/runtime: ${BIN_DIR}/lms-update"
echo "Stop LM Studio cleanly: ${BIN_DIR}/lms-stop"
echo "Start MCP proxy: ${BIN_DIR}/mcp-start"
echo "mcpo OpenAPI bridge: http://localhost:${MCPO_OPENAPI_PORT}"
echo "Start Open WebUI: ${BIN_DIR}/open-webui-start"
echo "Update Open WebUI: ${BIN_DIR}/open-webui-update"
echo "Open WebUI seeded tool config: ${MCP_CONFIG_DIR}/open-webui-tool-servers.json"
echo "Run chat wrapper: ${BIN_DIR}/nymph-chat"
