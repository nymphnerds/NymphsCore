#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCRIPT_DIR="${ROOT_DIR}/scripts"
LOCK_FILE="${SCRIPT_DIR}/runtime-deps.lock.json"
ZIMAGE_OVERLAY_DIR="${SCRIPT_DIR}/zimage_backend_overlay"

source "${SCRIPT_DIR}/common_paths.sh"

MODE="pinned"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --mode)
      if [[ $# -lt 2 ]]; then
        echo "--mode requires one of: pinned, latest" >&2
        exit 2
      fi
      MODE="$2"
      shift 2
      ;;
    --mode=*)
      MODE="${1#*=}"
      shift
      ;;
    --help|-h)
      cat <<'EOF'
Usage: apply_runtime_dependency_mode.sh --mode pinned|latest

Applies pinned or latest external runtime dependencies to the existing dev runtime.
EOF
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      exit 2
      ;;
  esac
done

case "${MODE}" in
  pinned|latest) ;;
  *)
    echo "Unknown mode '${MODE}'. Expected pinned or latest." >&2
    exit 2
    ;;
esac

if [[ ! -f "${LOCK_FILE}" ]]; then
  echo "Missing runtime dependency lock file: ${LOCK_FILE}" >&2
  exit 1
fi

eval "$(
  python3 - "${LOCK_FILE}" "${MODE}" <<'PY'
import json
import shlex
import sys

lock_path, mode = sys.argv[1:3]
data = json.load(open(lock_path, encoding="utf-8"))
deps = data["dependencies"]

if mode == "latest":
    zimage_ref = deps["Nymphs2D2"].get("tracking_ref") or "HEAD"
    trellis_source_ref = deps["TRELLIS.2"].get("tracking_ref") or "HEAD"
    if deps["nunchaku"]["kind"] == "git":
        nunchaku_ref = deps["nunchaku"].get("tracking_ref") or "HEAD"
        nunchaku_spec = f"git+{deps['nunchaku']['repo']}@{nunchaku_ref}"
        nunchaku_index_url = ""
    else:
        nunchaku_spec = deps["nunchaku"]["package"]
        nunchaku_index_url = deps["nunchaku"]["index_url"]
    diffusers_spec = deps["diffusers"]["package"]
    trellis_ref = deps["ComfyUI-Trellis2-GGUF"].get("tracking_ref") or "HEAD"
    comfy_ref = deps["ComfyUI-GGUF"].get("tracking_ref") or "HEAD"
else:
    zimage_ref = deps["Nymphs2D2"]["pinned"]
    trellis_source_ref = deps["TRELLIS.2"]["pinned"]
    if deps["nunchaku"]["kind"] == "git":
        nunchaku_ref = deps["nunchaku"]["pinned"]
        nunchaku_spec = f"git+{deps['nunchaku']['repo']}@{nunchaku_ref}"
        nunchaku_index_url = ""
    else:
        nunchaku_spec = deps["nunchaku"]["install_spec"]
        nunchaku_index_url = deps["nunchaku"]["index_url"]
    diffusers_spec = deps["diffusers"]["install_spec"]
    trellis_ref = deps["ComfyUI-Trellis2-GGUF"]["pinned"]
    comfy_ref = deps["ComfyUI-GGUF"]["pinned"]

values = {
    "ZIMAGE_REPO_URL": deps["Nymphs2D2"]["repo"],
    "ZIMAGE_REPO_REF": zimage_ref,
    "TRELLIS_SOURCE_REPO_URL": deps["TRELLIS.2"]["repo"],
    "TRELLIS_SOURCE_REPO_REF": trellis_source_ref,
    "NUNCHAKU_INDEX_URL": nunchaku_index_url,
    "NUNCHAKU_SPEC": nunchaku_spec,
    "DIFFUSERS_SPEC": diffusers_spec,
    "TRELLIS2_GGUF_REPO_URL": deps["ComfyUI-Trellis2-GGUF"]["repo"],
    "TRELLIS2_GGUF_REPO_REF": trellis_ref,
    "COMFYUI_GGUF_REPO_URL": deps["ComfyUI-GGUF"]["repo"],
    "COMFYUI_GGUF_REPO_REF": comfy_ref,
}

for key, value in values.items():
    print(f"{key}={shlex.quote(str(value))}")
PY
)"

sync_git_ref() {
  local name="$1"
  local repo_url="$2"
  local repo_ref="$3"
  local repo_path="$4"

  if [[ ! -d "${repo_path}/.git" ]]; then
    rm -rf "${repo_path}"
    echo "Cloning ${name}"
    GIT_TERMINAL_PROMPT=0 git clone --filter=blob:none --no-checkout "${repo_url}" "${repo_path}"
  fi

  echo "Syncing ${name} to ${repo_ref}"
  GIT_TERMINAL_PROMPT=0 git -C "${repo_path}" fetch --depth 1 origin "${repo_ref}"
  git -C "${repo_path}" checkout --detach FETCH_HEAD
  echo "${name}: active commit $(git -C "${repo_path}" rev-parse --short HEAD)"
}

apply_zimage_backend_overlay() {
  if [[ ! -d "${ZIMAGE_OVERLAY_DIR}" ]]; then
    echo "Expected Z-Image backend overlay directory is missing: ${ZIMAGE_OVERLAY_DIR}" >&2
    exit 1
  fi

  local overlay_file
  for overlay_file in api_server.py model_manager.py schemas.py; do
    if [[ ! -f "${ZIMAGE_OVERLAY_DIR}/${overlay_file}" ]]; then
      echo "Expected Z-Image backend overlay file is missing: ${ZIMAGE_OVERLAY_DIR}/${overlay_file}" >&2
      exit 1
    fi
  done

  echo "Applying managed Z-Image backend overlay"
  cp "${ZIMAGE_OVERLAY_DIR}/api_server.py" "${NYMPHS3D_N2D2_DIR}/api_server.py"
  cp "${ZIMAGE_OVERLAY_DIR}/model_manager.py" "${NYMPHS3D_N2D2_DIR}/model_manager.py"
  cp "${ZIMAGE_OVERLAY_DIR}/schemas.py" "${NYMPHS3D_N2D2_DIR}/schemas.py"
}

site_packages_dir() {
  "${NYMPHS3D_TRELLIS_DIR}/.venv/bin/python" - <<'PY'
import site
paths = [p for p in site.getsitepackages() if p.endswith("site-packages")]
if not paths:
    raise SystemExit("Could not resolve site-packages for the TRELLIS venv.")
print(paths[0])
PY
}

apply_zimage_diffusers() {
  local venv_python="${NYMPHS3D_N2D2_DIR}/.venv-nunchaku/bin/python"
  local pip_args=()

  if [[ ! -x "${venv_python}" ]]; then
    echo "Z-Image Nunchaku venv is missing. Run Runtime Tools repair/install first." >&2
    exit 1
  fi

  "${venv_python}" - <<'PY'
try:
    import diffusers
except Exception as exc:
    print(f"diffusers_before=unavailable ({exc})")
else:
    print(f"diffusers_before={diffusers.__version__}")
PY

  pip_args=(install --no-deps --force-reinstall)
  if [[ "${MODE}" == "latest" ]]; then
    pip_args+=(--upgrade)
  fi
  echo "Installing Z-Image diffusers dependency: ${DIFFUSERS_SPEC}"
  "${venv_python}" -m pip "${pip_args[@]}" "${DIFFUSERS_SPEC}"
  "${venv_python}" - <<'PY'
import diffusers
from diffusers.pipelines.z_image.pipeline_z_image import ZImagePipeline
from diffusers.pipelines.z_image.pipeline_z_image_img2img import ZImageImg2ImgPipeline

print(f"diffusers_after={diffusers.__version__}")
print(f"zimage_pipeline={ZImagePipeline.__name__}")
print(f"zimage_img2img_pipeline={ZImageImg2ImgPipeline.__name__}")
PY
}

apply_zimage_runtime_code() {
  local venv_python="${NYMPHS3D_N2D2_DIR}/.venv-nunchaku/bin/python"
  local pip_args=()

  sync_git_ref "Z-Image backend" "${ZIMAGE_REPO_URL}" "${ZIMAGE_REPO_REF}" "${NYMPHS3D_N2D2_DIR}"
  apply_zimage_backend_overlay

  if [[ ! -x "${venv_python}" ]]; then
    echo "Z-Image Nunchaku venv is missing. Run Runtime Tools repair/install first." >&2
    exit 1
  fi

  "${venv_python}" - <<'PY'
try:
    import importlib.metadata as metadata
    print(f"nunchaku_before={metadata.version('nunchaku')}")
except Exception as exc:
    print(f"nunchaku_before=unavailable ({exc})")
PY

  pip_args=(install --no-deps --pre --force-reinstall)
  if [[ -n "${NUNCHAKU_INDEX_URL}" ]]; then
    pip_args+=(--index-url "${NUNCHAKU_INDEX_URL}")
  fi
  if [[ "${MODE}" == "latest" ]]; then
    pip_args+=(--upgrade)
  fi
  echo "Installing Z-Image Nunchaku dependency: ${NUNCHAKU_SPEC}"
  "${venv_python}" -m pip "${pip_args[@]}" "${NUNCHAKU_SPEC}"
  "${venv_python}" - <<'PY'
import importlib.metadata as metadata
from nunchaku import NunchakuZImageTransformer2DModel

print(f"nunchaku_after={metadata.version('nunchaku')}")
print(f"nunchaku_transformer={NunchakuZImageTransformer2DModel.__name__}")
PY
}

apply_trellis_gguf_helpers() {
  local package_dir="${NYMPHS3D_TRELLIS_DIR}/.cache/trellis-gguf-runtime/ComfyUI-Trellis2-GGUF"
  local loader_dir="${NYMPHS3D_TRELLIS_DIR}/.cache/trellis-gguf-runtime/ComfyUI-GGUF"
  local site_packages
  local loader_target

  if [[ ! -x "${NYMPHS3D_TRELLIS_DIR}/.venv/bin/python" ]]; then
    echo "TRELLIS venv is missing. Run Runtime Tools repair/install first." >&2
    exit 1
  fi

  sync_git_ref "TRELLIS.2" "${TRELLIS_SOURCE_REPO_URL}" "${TRELLIS_SOURCE_REPO_REF}" "${NYMPHS3D_TRELLIS_DIR}"

  mkdir -p "${NYMPHS3D_TRELLIS_DIR}/.cache/trellis-gguf-runtime"
  sync_git_ref "ComfyUI-Trellis2-GGUF" "${TRELLIS2_GGUF_REPO_URL}" "${TRELLIS2_GGUF_REPO_REF}" "${package_dir}"
  sync_git_ref "ComfyUI-GGUF" "${COMFYUI_GGUF_REPO_URL}" "${COMFYUI_GGUF_REPO_REF}" "${loader_dir}"

  if [[ ! -d "${package_dir}/trellis2_gguf" ]]; then
    echo "Expected trellis2_gguf package is missing from ${package_dir}" >&2
    exit 1
  fi
  if [[ ! -f "${loader_dir}/ops.py" || ! -f "${loader_dir}/dequant.py" || ! -f "${loader_dir}/loader.py" ]]; then
    echo "Expected ComfyUI-GGUF loader files are missing from ${loader_dir}" >&2
    exit 1
  fi

  site_packages="$(site_packages_dir)"
  rm -rf "${site_packages}/trellis2_gguf"
  cp -a "${package_dir}/trellis2_gguf" "${site_packages}/trellis2_gguf"

  loader_target="${site_packages}/ComfyUI-GGUF"
  mkdir -p "${loader_target}"
  cp -a "${loader_dir}/ops.py" "${loader_dir}/dequant.py" "${loader_dir}/loader.py" "${loader_target}/"

  "${NYMPHS3D_TRELLIS_DIR}/.venv/bin/python" - <<'PY'
import importlib
importlib.import_module("trellis2_gguf")
print("trellis2_gguf_import=ok")
PY
}

configure_nymphs3d_hf_env

echo "Applying runtime dependency mode: ${MODE}"
apply_zimage_runtime_code
apply_zimage_diffusers
apply_trellis_gguf_helpers
echo "Runtime dependency mode '${MODE}' applied."
