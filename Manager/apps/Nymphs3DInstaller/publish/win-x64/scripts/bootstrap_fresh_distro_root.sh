#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "This script must run as root inside WSL." >&2
  exit 1
fi

export DEBIAN_FRONTEND="${DEBIAN_FRONTEND:-noninteractive}"

HELPER_REPO_URL="${NYMPHS3D_HELPER_REPO_URL:-https://github.com/nymphnerds/NymphsCore.git}"
HELPER_REPO_BRANCH="${NYMPHS3D_HELPER_REPO_BRANCH:-main}"
INSTALL_MINIMAL_PACKAGES="${NYMPHS3D_BOOTSTRAP_INSTALL_MINIMAL_PACKAGES:-1}"
PREPARE_RUNTIME_REPOS="${NYMPHS3D_BOOTSTRAP_PREPARE_RUNTIME_REPOS:-0}"

NYMPHS3D_OPT_ROOT="${NYMPHS3D_OPT_ROOT:-/opt/nymphs3d}"
NYMPHS3D_HELPER_ROOT="${NYMPHS3D_HELPER_ROOT:-${NYMPHS3D_OPT_ROOT}/Nymphs3D}"
NYMPHS3D_HELPER_CANONICAL_ROOT="${NYMPHS3D_HELPER_CANONICAL_ROOT:-${NYMPHS3D_OPT_ROOT}/NymphsCore}"
NYMPHS3D_RUNTIME_ROOT="${NYMPHS3D_RUNTIME_ROOT:-${NYMPHS3D_OPT_ROOT}/runtime}"

echo "Bootstrapping fresh NymphsCore distro as root..."
echo "Helper repo URL: ${HELPER_REPO_URL}"
echo "Helper repo branch: ${HELPER_REPO_BRANCH}"
echo "Helper root: ${NYMPHS3D_HELPER_ROOT}"
echo "Canonical helper alias: ${NYMPHS3D_HELPER_CANONICAL_ROOT}"
echo "Runtime root: ${NYMPHS3D_RUNTIME_ROOT}"
echo "Prepare runtime repos: ${PREPARE_RUNTIME_REPOS}"

if [[ "${INSTALL_MINIMAL_PACKAGES}" == "1" ]]; then
  echo "Installing minimum base packages required before finalize..."
  apt-get update
  apt-get install -y \
    bash \
    ca-certificates \
    curl \
    git \
    python3 \
    python3-pip \
    python3-venv \
    sudo \
    tar \
    wget
fi

mkdir -p "${NYMPHS3D_OPT_ROOT}" "${NYMPHS3D_RUNTIME_ROOT}"

if [[ -d "${NYMPHS3D_HELPER_ROOT}/.git" ]]; then
  echo "Refreshing existing helper repo checkout at ${NYMPHS3D_HELPER_ROOT}"
  git -C "${NYMPHS3D_HELPER_ROOT}" remote set-url origin "${HELPER_REPO_URL}" || true
  git -C "${NYMPHS3D_HELPER_ROOT}" fetch --depth 1 origin "${HELPER_REPO_BRANCH}"
  git -C "${NYMPHS3D_HELPER_ROOT}" checkout -B "${HELPER_REPO_BRANCH}" "origin/${HELPER_REPO_BRANCH}"
else
  echo "Cloning helper repo into ${NYMPHS3D_HELPER_ROOT}"
  rm -rf "${NYMPHS3D_HELPER_ROOT}" "${NYMPHS3D_HELPER_CANONICAL_ROOT}"
  git clone --depth 1 --branch "${HELPER_REPO_BRANCH}" --single-branch "${HELPER_REPO_URL}" "${NYMPHS3D_HELPER_ROOT}"
fi

if [[ "${NYMPHS3D_HELPER_CANONICAL_ROOT}" != "${NYMPHS3D_HELPER_ROOT}" ]]; then
  ln -sfn "${NYMPHS3D_HELPER_ROOT}" "${NYMPHS3D_HELPER_CANONICAL_ROOT}"
fi

cat >/etc/profile.d/nymphscore.sh <<'EOF'
export NYMPHS3D_HELPER_ROOT=/opt/nymphs3d/Nymphs3D
export NYMPHS3D_RUNTIME_ROOT="$HOME"
export NYMPHS3D_Z_IMAGE_DIR="$HOME/Z-Image"
export NYMPHS3D_N2D2_DIR="$NYMPHS3D_Z_IMAGE_DIR"
export NYMPHS3D_TRELLIS_DIR="$HOME/TRELLIS.2"
EOF

chmod 644 /etc/profile.d/nymphscore.sh
chmod +x "${NYMPHS3D_HELPER_ROOT}/scripts/"*.sh

if [[ "${PREPARE_RUNTIME_REPOS}" == "1" ]]; then
  echo "Preparing managed runtime repo snapshots for builder/export use..."
  export NYMPHS3D_HELPER_ROOT
  export NYMPHS3D_RUNTIME_ROOT
  /bin/bash "${NYMPHS3D_HELPER_ROOT}/scripts/prepare_fresh_builder_distro.sh"
fi

echo "Fresh distro bootstrap complete."
