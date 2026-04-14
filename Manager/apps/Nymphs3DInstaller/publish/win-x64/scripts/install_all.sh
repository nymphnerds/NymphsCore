#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "Starting full Nymphs backend setup from ${ROOT_DIR}"

"${ROOT_DIR}/scripts/preflight_wsl.sh"
"${ROOT_DIR}/scripts/install_system_deps.sh"
"${ROOT_DIR}/scripts/prune_legacy_runtime.sh"
"${ROOT_DIR}/scripts/install_cuda_13_wsl.sh"
"${ROOT_DIR}/scripts/install_hunyuan_2.sh"
"${ROOT_DIR}/scripts/install_nymphs2d2.sh"
"${ROOT_DIR}/scripts/install_trellis.sh"
if [[ "${NYMPHS3D_INSTALL_PARTS:-0}" == "1" ]]; then
  "${ROOT_DIR}/scripts/install_hunyuan_parts.sh"
fi
"${ROOT_DIR}/scripts/prefetch_models.sh"
"${ROOT_DIR}/scripts/verify_install.sh"

if [[ -n "${NYMPHS3D_SMOKE_TEST:-}" ]]; then
  "${ROOT_DIR}/scripts/verify_install.sh" --smoke-test "${NYMPHS3D_SMOKE_TEST}"
fi

echo
echo "Full backend setup complete."
echo "Next useful checks:"
echo "- ${ROOT_DIR}/scripts/run_hunyuan_2_mv_api.sh"
echo "- Z-Image backend: Z_IMAGE_RUNTIME=nunchaku ~/Z-Image/.venv-nunchaku/bin/python ~/Z-Image/api_server.py --host 127.0.0.1 --port 8090"
echo "- TRELLIS.2 backend: ~/TRELLIS.2/.venv/bin/python ~/TRELLIS.2/scripts/api_server_trellis.py --host 127.0.0.1 --port 8094"
echo "- ${ROOT_DIR}/scripts/verify_install.sh --smoke-test 2mv"
echo "- ${ROOT_DIR}/scripts/verify_install.sh --smoke-test trellis"
