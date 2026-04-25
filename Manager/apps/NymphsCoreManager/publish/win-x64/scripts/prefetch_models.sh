#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

N2D2_DIR="${NYMPHS3D_N2D2_DIR}"
TRELLIS_DIR="${NYMPHS3D_TRELLIS_DIR}"
HF_TOKEN="${NYMPHS3D_HF_TOKEN:-}"
HF_CACHE_DIR="${NYMPHS3D_HF_CACHE_DIR}"
export HF_HUB_DISABLE_PROGRESS_BARS=1
configure_nymphs3d_hf_env

BACKEND="all"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --backend)
      if [[ $# -lt 2 ]]; then
        echo "--backend requires one of: all, zimage, trellis" >&2
        exit 2
      fi
      BACKEND="$2"
      shift 2
      ;;
    --backend=*)
      BACKEND="${1#*=}"
      shift
      ;;
    --help|-h)
      cat <<'EOF'
Usage: prefetch_models.sh [--backend all|zimage|trellis]

Downloads cached model weights for the selected backend. The default is all.
EOF
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      exit 2
      ;;
  esac
done

case "${BACKEND}" in
  all|zimage|trellis) ;;
  *)
    echo "Unknown backend '${BACKEND}'. Expected one of: all, zimage, trellis" >&2
    exit 2
    ;;
esac

cache_size_bytes() {
  local path="$1"
  if [[ ! -d "${path}" ]]; then
    echo 0
    return
  fi
  find "${path}" -type f -printf '%s\n' 2>/dev/null | awk '{ total += $1 } END { printf "%.0f\n", total }'
}

format_bytes() {
  local size_bytes="${1:-0}"
  awk -v bytes="${size_bytes}" 'BEGIN {
    split("B KiB MiB GiB TiB", units, " ");
    value = bytes + 0;
    unit_index = 1;
    while (value >= 1024 && unit_index < 5) {
      value = value / 1024;
      unit_index++;
    }
    if (unit_index == 1) {
      printf "%d %s", value, units[unit_index];
    } else {
      printf "%.2f %s", value, units[unit_index];
    }
  }'
}

hf_repo_cache_dir() {
  local repo_id="$1"
  local repo_path="${repo_id//\//--}"
  echo "${HF_CACHE_DIR}/models--${repo_path}"
}

hf_repo_blob_bytes() {
  local repo_id="$1"
  local repo_dir
  repo_dir="$(hf_repo_cache_dir "${repo_id}")"
  cache_size_bytes "${repo_dir}/blobs"
}

hf_repo_incomplete_count() {
  local repo_id="$1"
  local repo_dir
  repo_dir="$(hf_repo_cache_dir "${repo_id}")"
  if [[ ! -d "${repo_dir}/blobs" ]]; then
    echo 0
    return
  fi
  find "${repo_dir}/blobs" -type f -name '*.incomplete' 2>/dev/null | wc -l | tr -d ' '
}

print_hf_download_progress() {
  local label="$1"
  local repo_id="$2"
  local start_cache_bytes="$3"
  local current_cache_bytes
  local repo_bytes
  local incomplete_count
  local cache_delta

  current_cache_bytes="$(cache_size_bytes "${HF_CACHE_DIR}")"
  repo_bytes="$(hf_repo_blob_bytes "${repo_id}")"
  incomplete_count="$(hf_repo_incomplete_count "${repo_id}")"
  cache_delta=$(( current_cache_bytes - start_cache_bytes ))
  if [[ "${cache_delta}" -lt 0 ]]; then
    cache_delta=0
  fi

  echo "${label}: still downloading ${repo_id}..."
  if [[ -n "${NYMPHS3D_PREFETCH_COMPONENT_HINT:-}" ]]; then
    echo "- waiting on: ${NYMPHS3D_PREFETCH_COMPONENT_HINT}"
  fi
  echo "- shared HF cache: $(format_bytes "${current_cache_bytes}") (+$(format_bytes "${cache_delta}") during this step)"
  echo "- repo cache blobs: $(format_bytes "${repo_bytes}") (${incomplete_count} active partial files)"
}

run_with_hf_download_progress() {
  local label="$1"
  local repo_id="$2"
  shift 2

  local interval="${NYMPHS3D_PREFETCH_PROGRESS_INTERVAL:-10}"
  local start_cache_bytes
  local marker
  local pid
  local status

  if [[ ! "${interval}" =~ ^[0-9]+$ || "${interval}" -lt 1 ]]; then
    interval=10
  fi

  start_cache_bytes="$(cache_size_bytes "${HF_CACHE_DIR}")"
  marker="$(mktemp "${TMPDIR:-/tmp}/nymphscore-prefetch.XXXXXX.status")"
  rm -f "${marker}"

  echo "${label}: download started. Progress will update every ${interval}s while Hugging Face is busy."
  (
    set +e
    "$@"
    status=$?
    printf '%s\n' "${status}" > "${marker}"
    exit "${status}"
  ) &
  pid=$!

  while [[ ! -f "${marker}" ]]; do
    sleep "${interval}"
    if [[ -f "${marker}" ]]; then
      break
    fi
    print_hf_download_progress "${label}" "${repo_id}" "${start_cache_bytes}"
  done

  wait "${pid}" || true
  status="$(cat "${marker}" 2>/dev/null || echo 1)"
  rm -f "${marker}"

  if [[ "${status}" -eq 0 ]]; then
    print_hf_download_progress "${label}" "${repo_id}" "${start_cache_bytes}"
    echo "${label}: download step complete."
  else
    echo "${label}: download step failed with status ${status}."
  fi

  return "${status}"
}

prefetch_zimage_nunchaku_weights() {
  python - <<'PY'
import os
from huggingface_hub import hf_hub_download

repo_id = os.getenv("Z_IMAGE_NUNCHAKU_MODEL_REPO") or "nunchaku-ai/nunchaku-z-image-turbo"
rank = os.getenv("Z_IMAGE_NUNCHAKU_RANK") or os.getenv("NYMPHS2D2_NUNCHAKU_RANK") or "32"
precision = (os.getenv("Z_IMAGE_NUNCHAKU_PRECISION") or os.getenv("NYMPHS2D2_NUNCHAKU_PRECISION") or "auto").strip().lower()
precisions = ["int4", "fp4"] if precision == "auto" else [precision]
cache_dir = os.getenv("NYMPHS3D_HF_CACHE_DIR") or None
token = os.getenv("NYMPHS3D_HF_TOKEN") or None

for item in precisions:
    filename = f"svdq-{item}_r{rank}-z-image-turbo.safetensors"
    print(f"Z-Image Turbo Nunchaku weight prefetch: {repo_id}/{filename}", flush=True)
    path = hf_hub_download(
        repo_id=repo_id,
        filename=filename,
        cache_dir=cache_dir,
        token=token,
    )
    print(f"Z-Image Turbo Nunchaku weight ready: {path}", flush=True)
PY
}

prefetch_nymphs2d2_model() {
  (
    cd "${N2D2_DIR}"
    source .venv-nunchaku/bin/activate
    configure_nymphs3d_hf_env
    export NYMPHS3D_HF_CACHE_DIR="${HF_CACHE_DIR}"
    export Z_IMAGE_RUNTIME="nunchaku"
    export Z_IMAGE_NUNCHAKU_MODEL_REPO="${Z_IMAGE_NUNCHAKU_MODEL_REPO:-nunchaku-ai/nunchaku-z-image-turbo}"
    export Z_IMAGE_NUNCHAKU_RANK="${Z_IMAGE_NUNCHAKU_RANK:-32}"
    export Z_IMAGE_NUNCHAKU_PRECISION="${Z_IMAGE_NUNCHAKU_PRECISION:-auto}"
    export NYMPHS3D_PREFETCH_COMPONENT_HINT="base Z-Image files: scheduler, text encoder, tokenizer, transformer, and VAE"
    if [[ -n "${HF_TOKEN}" ]]; then
      export NYMPHS3D_HF_TOKEN="${HF_TOKEN}"
    fi
    export HF_HUB_DISABLE_PROGRESS_BARS=1
    run_with_hf_download_progress \
      "Z-Image Turbo model prefetch" \
      "Tongyi-MAI/Z-Image-Turbo" \
      python scripts/prefetch_model.py
    unset NYMPHS3D_PREFETCH_COMPONENT_HINT
    export NYMPHS3D_PREFETCH_COMPONENT_HINT="Nunchaku rank weights for the selected Z-Image preset"
    run_with_hf_download_progress \
      "Z-Image Turbo Nunchaku weight prefetch" \
      "${Z_IMAGE_NUNCHAKU_MODEL_REPO}" \
      prefetch_zimage_nunchaku_weights
    unset NYMPHS3D_PREFETCH_COMPONENT_HINT
  )
}

prefetch_trellis_gguf_model_bundle() {
  (
    cd "${TRELLIS_DIR}"
    source .venv/bin/activate
    configure_nymphs3d_hf_env
    export HF_HUB_DISABLE_PROGRESS_BARS=1
    python - <<'PY' "${HF_TOKEN}" "${HF_CACHE_DIR}"
import os
import sys
import threading
from pathlib import Path

token = sys.argv[1] or None
cache_dir = sys.argv[2]
sys.path.insert(0, str(Path.cwd() / "scripts"))
from trellis_gguf_common import (
    DEFAULT_GGUF_QUANT,
    GGUF_MODEL_REPO_ID,
    TRELLIS_SUPPORT_MODEL_REPO_ID,
    ensure_required_support_models,
    resolve_gguf_model_root,
)

if token:
    os.environ["HF_TOKEN"] = token
    os.environ["HUGGING_FACE_HUB_TOKEN"] = token

repo_id = GGUF_MODEL_REPO_ID
quant = os.getenv("TRELLIS_GGUF_QUANT") or DEFAULT_GGUF_QUANT
stop_event = threading.Event()

def cache_size_bytes(path: str) -> int:
    total = 0
    if not path or not os.path.isdir(path):
        return total
    for root, _, files in os.walk(path):
        for file_name in files:
            try:
                total += os.path.getsize(os.path.join(root, file_name))
            except OSError:
                pass
    return total

def format_gib(size_bytes: int) -> str:
    return f"{size_bytes / (1024 ** 3):.2f} GiB"

def heartbeat(start_size: int) -> None:
    last_size = start_size
    while not stop_event.wait(10):
        current_size = cache_size_bytes(cache_dir)
        delta = current_size - start_size
        rate = current_size - last_size
        print(
            f"Still downloading {repo_id}: HF cache +{format_gib(max(delta, 0))}, recent activity {format_gib(max(rate, 0))}/10s",
            flush=True,
        )
        last_size = current_size

start_size = cache_size_bytes(cache_dir)
print(f"Prefetching {repo_id} ({quant}) into shared HF cache", flush=True)
thread = threading.Thread(target=heartbeat, args=(start_size,), daemon=True)
thread.start()
try:
    resolve_gguf_model_root(local_files_only=False, quant=quant, include_texture=True)
    print(f"Prefetching TRELLIS GGUF support checkpoints from {TRELLIS_SUPPORT_MODEL_REPO_ID}", flush=True)
    for config_file, model_file in ensure_required_support_models(local_files_only=False):
        print(f"Support checkpoint ready: {config_file}", flush=True)
        print(f"Support checkpoint ready: {model_file}", flush=True)
finally:
    stop_event.set()
    thread.join(timeout=1)

end_size = cache_size_bytes(cache_dir)
print(
    f"Finished {repo_id}: HF cache increased by {format_gib(max(end_size - start_size, 0))}, total cache size now {format_gib(end_size)}",
    flush=True,
)
PY
  )
}

prefetch_rembg_u2net() {
  local u2net_cache_dir="${HOME}/.u2net"
  local u2net_file="${u2net_cache_dir}/u2net.onnx"
  local u2net_url="https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net.onnx"

  if [[ -f "${u2net_file}" ]]; then
    echo "rembg u2net model already present at ${u2net_file}"
  else
    echo "Prefetching rembg u2net model..."
    mkdir -p "${u2net_cache_dir}"
    curl -L -o "${u2net_file}.tmp" "${u2net_url}"
    mv "${u2net_file}.tmp" "${u2net_file}"
    echo "rembg u2net model downloaded to ${u2net_file}"
  fi
}

echo "Prefetching core backend model weights (${BACKEND})..."

if [[ "${BACKEND}" == "all" || "${BACKEND}" == "zimage" ]]; then
  echo "Prefetching Z-Image Turbo default model..."
  prefetch_nymphs2d2_model
fi

if [[ "${BACKEND}" == "all" || "${BACKEND}" == "trellis" ]]; then
  echo "Prefetching TRELLIS.2 GGUF model bundle..."
  prefetch_trellis_gguf_model_bundle
fi

if [[ "${BACKEND}" == "all" || "${BACKEND}" == "trellis" ]]; then
  echo "Prefetching rembg u2net model..."
  prefetch_rembg_u2net
fi

echo
echo "Core backend model prefetch complete."
