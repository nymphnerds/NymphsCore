#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

configure_nymphs3d_cuda_env

cd "${NYMPHS3D_H2_DIR}"
source .venv/bin/activate
python gradio_app.py \
  --model_path tencent/Hunyuan3D-2mv \
  --subfolder hunyuan3d-dit-v2-mv-turbo \
  --texgen_model_path tencent/Hunyuan3D-2
