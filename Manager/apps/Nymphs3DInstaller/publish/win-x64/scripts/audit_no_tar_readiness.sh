#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"

HOME_DIR="${HOME}"
OPT_HELPER_DIR="/opt/nymphs3d/Nymphs3D"
OPT_RUNTIME_ROOT="/opt/nymphs3d/runtime"
OPT_Z_IMAGE_DIR="${OPT_RUNTIME_ROOT}/Z-Image"
OPT_TRELLIS_DIR="${OPT_RUNTIME_ROOT}/TRELLIS.2"

section() {
  echo
  echo "$1"
  echo "============================================================"
}

read_file_via_sudo() {
  local path="$1"
  if command -v sudo >/dev/null 2>&1 && sudo -n test -e "$path" 2>/dev/null; then
    sudo -n cat "$path"
    return
  fi

  if [[ -r "$path" ]]; then
    cat "$path"
    return
  fi

  echo "[unreadable]"
}

print_file() {
  local path="$1"
  echo "File: ${path}"
  if command -v sudo >/dev/null 2>&1 && sudo -n test -e "$path" 2>/dev/null; then
    read_file_via_sudo "$path"
  elif [[ -e "$path" ]]; then
    read_file_via_sudo "$path"
  else
    echo "[missing]"
  fi
}

list_dir() {
  local path="$1"
  echo "Path: ${path}"
  if [[ ! -e "$path" ]]; then
    echo "[missing]"
    return
  fi
  ls -ld "$path"
  if [[ -d "$path" ]]; then
    find "$path" -mindepth 1 -maxdepth 2 -printf '%y %P\n' 2>/dev/null | sort || true
  fi
}

repo_summary() {
  local label="$1"
  local repo_path="$2"
  local expected_remote="${3:-}"
  local expected_branch="${4:-}"

  echo
  echo "${label}"
  echo "Path: ${repo_path}"

  if [[ ! -e "${repo_path}" ]]; then
    echo "State: missing"
    return
  fi

  ls -ld "${repo_path}"

  if [[ ! -d "${repo_path}/.git" ]]; then
    echo "State: present but not a git checkout"
    return
  fi

  local head branch remote tracked_changes untracked_changes
  head="$(git -C "${repo_path}" rev-parse --short HEAD 2>/dev/null || echo unknown)"
  branch="$(git -C "${repo_path}" symbolic-ref --quiet --short HEAD 2>/dev/null || echo detached)"
  remote="$(git -C "${repo_path}" remote get-url origin 2>/dev/null || echo '[missing origin]')"
  tracked_changes="$(git -C "${repo_path}" status --short --untracked-files=no 2>/dev/null || true)"
  untracked_changes="$(git -C "${repo_path}" ls-files --others --exclude-standard 2>/dev/null || true)"

  echo "HEAD: ${head}"
  echo "Branch: ${branch}"
  echo "Origin: ${remote}"
  if [[ -n "${expected_remote}" ]]; then
    echo "Expected origin: ${expected_remote}"
  fi
  if [[ -n "${expected_branch}" ]]; then
    echo "Expected branch: ${expected_branch}"
  fi

  if [[ -n "${tracked_changes}" ]]; then
    echo "Tracked changes:"
    printf '%s\n' "${tracked_changes}" | sed -n '1,40p'
  else
    echo "Tracked changes: none"
  fi

  if [[ -n "${untracked_changes}" ]]; then
    echo "Untracked files:"
    printf '%s\n' "${untracked_changes}" | sed -n '1,40p'
  else
    echo "Untracked files: none"
  fi
}

package_summary() {
  local pkg="$1"
  if dpkg-query -W -f='${Status}\t${Version}\n' "$pkg" 2>/dev/null | grep -q '^install ok installed'; then
    local version
    version="$(dpkg-query -W -f='${Version}\n' "$pkg" 2>/dev/null | head -n 1)"
    printf '%-28s %s\n' "$pkg" "$version"
  else
    printf '%-28s %s\n' "$pkg" "[not installed]"
  fi
}

command_summary() {
  local cmd="$1"
  if command -v "$cmd" >/dev/null 2>&1; then
    printf '%-28s %s\n' "$cmd" "$(command -v "$cmd")"
  else
    printf '%-28s %s\n' "$cmd" "[missing]"
  fi
}

size_summary() {
  local label="$1"
  local path="$2"
  local value="0"
  if [[ -e "$path" ]]; then
    value="$(du -sh "$path" 2>/dev/null | awk '{print $1}')"
  fi
  printf '%-28s %8s  %s\n' "$label" "$value" "$path"
}

echo "NymphsCore no-tar readiness audit"
echo "This report is intended to expose frozen state that may exist in a tar-based distro."
echo "Timestamp: $(date -Is)"

section "Identity"
echo "WSL distro: ${WSL_DISTRO_NAME:-unknown}"
echo "User: ${USER}"
echo "Home: ${HOME}"
echo "Kernel: $(uname -a)"
if command -v lsb_release >/dev/null 2>&1; then
  lsb_release -a 2>/dev/null || true
fi

section "Command Availability"
for cmd in bash git sudo python3 python3.10 python3.11 curl wget nvidia-smi; do
  command_summary "$cmd"
done

section "Key Packages"
for pkg in \
  ca-certificates \
  git \
  wget \
  curl \
  unzip \
  build-essential \
  pkg-config \
  software-properties-common \
  python3 \
  python3-venv \
  python3-pip \
  python3.10 \
  python3.10-venv \
  python3.10-dev \
  python3.11 \
  python3.11-venv \
  python3.11-dev \
  sudo; do
  package_summary "$pkg"
done

section "Key Paths"
for path in \
  "${OPT_HELPER_DIR}" \
  "${OPT_RUNTIME_ROOT}" \
  "${OPT_Z_IMAGE_DIR}" \
  "${OPT_TRELLIS_DIR}" \
  "${NYMPHS3D_HELPER_ROOT}" \
  "${NYMPHS3D_N2D2_DIR}" \
  "${NYMPHS3D_TRELLIS_DIR}" \
  "${NYMPHS3D_CUDA_HOME}"; do
  list_dir "$path"
  echo
done

section "Size Snapshot"
size_summary "Helper repo (/opt)" "${OPT_HELPER_DIR}"
size_summary "Runtime root (/opt)" "${OPT_RUNTIME_ROOT}"
size_summary "Z-Image (/opt)" "${OPT_Z_IMAGE_DIR}"
size_summary "TRELLIS.2 (/opt)" "${OPT_TRELLIS_DIR}"
size_summary "Helper root (effective)" "${NYMPHS3D_HELPER_ROOT}"
size_summary "Z-Image (effective)" "${NYMPHS3D_N2D2_DIR}"
size_summary "TRELLIS.2 (effective)" "${NYMPHS3D_TRELLIS_DIR}"
size_summary "CUDA" "${NYMPHS3D_CUDA_HOME}"

section "Repo Provenance"
repo_summary "Helper repo under /opt" "${OPT_HELPER_DIR}" "https://github.com/nymphnerds/NymphsCore.git" "main"
repo_summary "Effective helper repo" "${NYMPHS3D_HELPER_ROOT}" "https://github.com/nymphnerds/NymphsCore.git" "main"
repo_summary "Z-Image under /opt" "${OPT_Z_IMAGE_DIR}" "${NYMPHS3D_N2D2_REPO_URL:-https://github.com/nymphnerds/Nymphs2D2.git}" "${NYMPHS3D_N2D2_REPO_BRANCH:-main}"
repo_summary "Effective Z-Image repo" "${NYMPHS3D_N2D2_DIR}" "${NYMPHS3D_N2D2_REPO_URL:-https://github.com/nymphnerds/Nymphs2D2.git}" "${NYMPHS3D_N2D2_REPO_BRANCH:-main}"
repo_summary "TRELLIS.2 under /opt" "${OPT_TRELLIS_DIR}" "${NYMPHS3D_TRELLIS_REPO_URL:-https://github.com/microsoft/TRELLIS.2.git}" "${NYMPHS3D_TRELLIS_REPO_BRANCH:-main}"
repo_summary "Effective TRELLIS.2 repo" "${NYMPHS3D_TRELLIS_DIR}" "${NYMPHS3D_TRELLIS_REPO_URL:-https://github.com/microsoft/TRELLIS.2.git}" "${NYMPHS3D_TRELLIS_REPO_BRANCH:-main}"

section "Config Files"
print_file "/etc/profile.d/nymphscore.sh"
echo
print_file "/etc/wsl.conf"
echo
echo "Sudoers snippets:"
if command -v sudo >/dev/null 2>&1 && sudo -n test -d /etc/sudoers.d 2>/dev/null; then
  sudo -n find /etc/sudoers.d -maxdepth 1 -type f -name '*nymphscore*' -print | sort || true
elif [[ -d /etc/sudoers.d ]]; then
  find /etc/sudoers.d -maxdepth 1 -type f -name '*nymphscore*' -print | sort || true
else
  echo "[missing /etc/sudoers.d]"
fi

section "Potential Frozen State Hints"
echo "- /opt helper/runtime trees may indicate what the exported tar originally carried."
echo "- Effective runtime paths under HOME show what the live finalized install is actually using."
echo "- Differences between /opt repo snapshots and HOME repo snapshots are high-value no-tar audit targets."
echo "- Local git changes, remote mismatches, detached heads, and non-git directories should be treated as suspicious until explained."

section "Recommended Follow-Up Questions"
echo "1. Does /opt/nymphs3d/runtime exist, and is it still materially different from the effective HOME runtime repos?"
echo "2. Are any helper/backend repos on unexpected remotes, branches, or local commits?"
echo "3. Are there local modifications in helper/backend repos that are not recreated by scripts?"
echo "4. Are /etc/profile.d/nymphscore.sh, /etc/wsl.conf, and sudoers snippets exactly what the scripts would create today?"
echo "5. Are there required packages present here that are not explicitly installed by bootstrap/finalize scripts?"
