#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

BACKEND="zimage"
TIMEOUT_SECONDS="${NYMPHS3D_SMOKE_TEST_TIMEOUT:-900}"
PORT=""
WORK_DIR=""
LOG_PATH=""
SERVER_PID=""
VENV_ACTIVATE=""

usage() {
  cat <<'EOF'
Usage: smoke_test_server.sh [--backend zimage|trellis] [--port PORT] [--timeout SECONDS]

Starts a local API server, waits for /server_info, and then shuts it down.
EOF
}

cleanup() {
  if [[ -n "${SERVER_PID}" ]] && kill -0 "${SERVER_PID}" >/dev/null 2>&1; then
    kill "${SERVER_PID}" >/dev/null 2>&1 || true
    wait "${SERVER_PID}" >/dev/null 2>&1 || true
  fi
  if [[ -n "${WORK_DIR}" && -d "${WORK_DIR}" ]]; then
    rm -rf "${WORK_DIR}"
  fi
}

configure_cuda_env() {
  configure_nymphs3d_cuda_env
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --backend)
      shift
      BACKEND="${1:-}"
      ;;
    --port)
      shift
      PORT="${1:-}"
      ;;
    --timeout)
      shift
      TIMEOUT_SECONDS="${1:-}"
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1"
      usage
      exit 1
      ;;
  esac
  shift
done

case "${BACKEND}" in
  zimage)
    REPO_DIR="${NYMPHS3D_Z_IMAGE_DIR}"
    VENV_ACTIVATE="${REPO_DIR}/.venv-nunchaku/bin/activate"
    PORT="${PORT:-8090}"
    EXPECTED_BACKEND="Z-Image"
    EXPECTED_BACKEND_ALIASES="Z-Image,Nymphs2D2"
    EXPECTED_MODEL="Tongyi-MAI/Z-Image-Turbo"
    EXPECTED_SUBFOLDER="nunchaku"
    SERVER_CMD=(
      env
      Z_IMAGE_RUNTIME=nunchaku
      NYMPHS2D2_RUNTIME=nunchaku
      Z_IMAGE_MODEL_ID="${EXPECTED_MODEL}"
      NYMPHS2D2_MODEL_ID="${EXPECTED_MODEL}"
      Z_IMAGE_PORT="${PORT}"
      NYMPHS2D2_PORT="${PORT}"
      python -u api_server.py
      --host 127.0.0.1
      --port "${PORT}"
    )
    ;;
  trellis)
    REPO_DIR="${NYMPHS3D_TRELLIS_DIR}"
    VENV_ACTIVATE="${REPO_DIR}/.venv/bin/activate"
    PORT="${PORT:-8094}"
    EXPECTED_BACKEND="TRELLIS.2-GGUF"
    EXPECTED_MODEL="Aero-Ex/Trellis2-GGUF"
    EXPECTED_SUBFOLDER="gguf/${TRELLIS_GGUF_QUANT:-Q5_K_M}"
    SERVER_CMD=(
      python -u scripts/api_server_trellis_gguf.py
      --host 127.0.0.1
      --port "${PORT}"
      --python-path "${REPO_DIR}/.venv/bin/python"
      --gguf-quant "${TRELLIS_GGUF_QUANT:-Q5_K_M}"
    )
    ;;
  *)
    echo "Unknown backend: ${BACKEND}"
    usage
    exit 1
    ;;
esac

trap cleanup EXIT
WORK_DIR="$(mktemp -d)"
LOG_PATH="${WORK_DIR}/${BACKEND}.log"

echo "Smoke-testing ${BACKEND} on port ${PORT}..."

cd "${REPO_DIR}"
source "${VENV_ACTIVATE}"
configure_cuda_env

echo "Backend log: ${LOG_PATH}"
"${SERVER_CMD[@]}" > >(tee "${LOG_PATH}") 2>&1 &
SERVER_PID="$!"

if ! python - <<'PY' "${PORT}" "${TIMEOUT_SECONDS}" "${EXPECTED_BACKEND}" "${EXPECTED_MODEL}" "${EXPECTED_SUBFOLDER}" "${EXPECTED_BACKEND_ALIASES:-}"
import json
import sys
import time
import urllib.error
import urllib.request

port = sys.argv[1]
timeout_seconds = int(sys.argv[2])
expected_backend = sys.argv[3]
expected_model = sys.argv[4]
expected_subfolder = sys.argv[5]
expected_backend_aliases = {
    item.strip() for item in (sys.argv[6] if len(sys.argv) > 6 else "").split(",") if item.strip()
}
if expected_backend:
    expected_backend_aliases.add(expected_backend)
deadline = time.time() + timeout_seconds
last_error = "server did not become ready"
url = f"http://127.0.0.1:{port}/server_info"

while time.time() < deadline:
    try:
        with urllib.request.urlopen(url, timeout=5) as response:
            payload = json.load(response)
        payload_backend = payload.get("backend")
        payload_model = payload.get("model_path") or payload.get("configured_model_id")
        payload_subfolder = payload.get("subfolder")
        payload_extra = payload.get("extra") if isinstance(payload.get("extra"), dict) else {}
        payload_runtime = payload_extra.get("runtime")
        payload_configured_runtime = payload_extra.get("configured_runtime")
        payload_supported_modes = payload.get("supported_modes") if isinstance(payload.get("supported_modes"), list) else []
        if payload_backend in expected_backend_aliases and payload_model == expected_model:
            if expected_backend == "Z-Image":
                if payload_runtime == expected_subfolder or payload_configured_runtime == expected_subfolder:
                    print(f"Smoke test passed for {expected_backend}.")
                    sys.exit(0)
                if "txt2img" in payload_supported_modes:
                    print(
                        f"Smoke test passed for {expected_backend} using legacy backend identity "
                        f"{payload_backend} (runtime={payload_runtime or payload_configured_runtime or 'unknown'})."
                    )
                    sys.exit(0)
            if expected_backend != "Z-Image" and payload.get("status") in {"ok", "ready"} and payload_subfolder == expected_subfolder:
                print(f"Smoke test passed for {expected_backend}.")
                sys.exit(0)
        last_error = f"Unexpected server_info payload: {payload}"
    except Exception as exc:
        last_error = str(exc)
    print(f"Waiting for {expected_backend} /server_info on port {port}: {last_error}", flush=True)
    time.sleep(5)

print(last_error, file=sys.stderr)
sys.exit(1)
PY
then
  echo "Smoke test failed. Server log follows:"
  tail -n 80 "${LOG_PATH}" || true
  exit 1
fi

echo "Smoke test completed successfully."
