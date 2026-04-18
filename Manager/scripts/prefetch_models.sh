#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

H2_DIR="${NYMPHS3D_H2_DIR}"
N2D2_DIR="${NYMPHS3D_N2D2_DIR}"
TRELLIS_DIR="${NYMPHS3D_TRELLIS_DIR}"
HF_TOKEN="${NYMPHS3D_HF_TOKEN:-}"
HF_CACHE_DIR="${NYMPHS3D_HF_CACHE_DIR}"
export HF_HUB_DISABLE_PROGRESS_BARS=1
configure_nymphs3d_hf_env

prefetch_repo_models() {
  local repo_dir="$1"
  shift
  local spec_json="$1"

  (
    cd "${repo_dir}"
    source .venv/bin/activate
    python - <<'PY' "${spec_json}" "${HF_TOKEN}" "${HF_CACHE_DIR}"
import json
import os
import sys
import threading
from huggingface_hub import snapshot_download

specs = json.loads(sys.argv[1])
token = sys.argv[2] or None
cache_dir = sys.argv[3]

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

if token:
    print(f"Using provided Hugging Face token for installer downloads ({len(token)} chars).", flush=True)
else:
    print("No Hugging Face token provided. Downloads will use anonymous access and may be slower.", flush=True)

print("Hugging Face progress bars disabled for installer logging; installer heartbeat lines will be used instead.", flush=True)

for spec in specs:
    repo_id = spec["repo_id"]
    patterns = spec["allow_patterns"]
    print(f"Prefetching {repo_id}: {', '.join(patterns)}", flush=True)
    print(
        f"Starting snapshot download for {repo_id}. Large shards can appear to sit before the first cache jump.",
        flush=True,
    )
    start_size = cache_size_bytes(cache_dir)
    stop_event = threading.Event()

    def heartbeat() -> None:
        last_size = start_size
        while not stop_event.wait(10):
            current_size = cache_size_bytes(cache_dir)
            delta = current_size - start_size
            rate = current_size - last_size
            print(
                f"Still downloading {repo_id}: cache +{format_gib(max(delta, 0))} this repo, total HF cache {format_gib(current_size)}, recent activity {format_gib(max(rate, 0))}/10s",
                flush=True,
            )
            last_size = current_size

    thread = threading.Thread(target=heartbeat, daemon=True)
    thread.start()
    try:
        snapshot_download(repo_id=repo_id, allow_patterns=patterns, token=token)
    finally:
        stop_event.set()
        thread.join(timeout=1)

    end_size = cache_size_bytes(cache_dir)
    print(
        f"Finished {repo_id}: cache increased by {format_gib(max(end_size - start_size, 0))}, total HF cache now {format_gib(end_size)}",
        flush=True,
    )
PY
  )
}

prefetch_nymphs2d2_model() {
  (
    cd "${N2D2_DIR}"
    source .venv-nunchaku/bin/activate
    configure_nymphs3d_hf_env
    export NYMPHS3D_HF_CACHE_DIR="${HF_CACHE_DIR}"
    export Z_IMAGE_RUNTIME="nunchaku"
    if [[ -n "${HF_TOKEN}" ]]; then
      export NYMPHS3D_HF_TOKEN="${HF_TOKEN}"
    fi
    export HF_HUB_DISABLE_PROGRESS_BARS=1
    python scripts/prefetch_model.py
  )
}

prefetch_trellis_model_bundle() {
  (
    cd "${TRELLIS_DIR}"
    source .venv/bin/activate
    configure_nymphs3d_hf_env
    export HF_HUB_DISABLE_PROGRESS_BARS=1
    python - <<'PY' "${HF_TOKEN}" "${HF_CACHE_DIR}"
import os
import sys
import threading
from huggingface_hub import snapshot_download

token = sys.argv[1] or None
cache_dir = sys.argv[2]
repo_id = "microsoft/TRELLIS.2-4B"
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
print(f"Prefetching {repo_id} into shared HF cache", flush=True)
thread = threading.Thread(target=heartbeat, args=(start_size,), daemon=True)
thread.start()
try:
    snapshot_download(repo_id=repo_id, token=token)
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

echo "Prefetching core backend model weights..."

prefetch_repo_models "${H2_DIR}" '[
  {
    "repo_id": "tencent/Hunyuan3D-2mv",
    "allow_patterns": [
      "hunyuan3d-dit-v2-mv/*",
      "hunyuan3d-dit-v2-mv-turbo/*"
    ]
  },
  {
    "repo_id": "tencent/Hunyuan3D-2",
    "allow_patterns": [
      "hunyuan3d-vae-v2-0/*",
      "hunyuan3d-vae-v2-0-turbo/*",
      "hunyuan3d-delight-v2-0/*",
      "hunyuan3d-paint-v2-0-turbo/*"
    ]
  }
]'

echo "Prefetching Z-Image Turbo default model..."
prefetch_nymphs2d2_model

echo "Prefetching TRELLIS.2 model bundle..."
prefetch_trellis_model_bundle

echo "Prefetching rembg u2net model..."
prefetch_rembg_u2net

echo
echo "Core backend model prefetch complete."
