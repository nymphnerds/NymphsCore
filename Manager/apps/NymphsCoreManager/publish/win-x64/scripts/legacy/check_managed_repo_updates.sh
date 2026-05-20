#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "${ROOT_DIR}/scripts/common_paths.sh"
source "${ROOT_DIR}/scripts/managed_repo_utils.sh"

updates_available=0
up_to_date_count=0
attention_needed=0

inspect_repo() {
  local repo_name="$1"
  local repo_path="$2"
  local repo_url="$3"
  local repo_branch="$4"

  echo "Checking ${repo_name}..."
  managed_repo_inspect "${repo_name}" "${repo_path}" "${repo_url}" "${repo_branch}"

  case "${MANAGED_REPO_STATE}" in
    behind_clean|missing)
      updates_available=$((updates_available + 1))
      ;;
    up_to_date|ahead_local)
      up_to_date_count=$((up_to_date_count + 1))
      ;;
    *)
      attention_needed=$((attention_needed + 1))
      ;;
  esac

  echo "- $(managed_repo_human_summary)"
  managed_repo_print_state
}

echo "Checking managed repo update state..."
echo "This can take a short while while contacting GitHub for repo status."
echo

inspect_repo "NymphsCore helper repo" "${NYMPHS3D_HELPER_ROOT}" "https://github.com/nymphnerds/NymphsCore.git" "main"
inspect_repo "Z-Image backend" "${NYMPHS3D_N2D2_DIR}" "${NYMPHS3D_N2D2_REPO_URL:-https://github.com/nymphnerds/Nymphs2D2.git}" "${NYMPHS3D_N2D2_REPO_BRANCH}"
inspect_repo "TRELLIS.2" "${NYMPHS3D_TRELLIS_DIR}" "${NYMPHS3D_TRELLIS_REPO_URL}" "${NYMPHS3D_TRELLIS_REPO_BRANCH}"

echo
echo "Update check result:"
echo "- Updates available: ${updates_available}"
echo "- Already up to date: ${up_to_date_count}"
echo "- Needs attention before updating: ${attention_needed}"

if [[ "${updates_available}" -eq 0 && "${attention_needed}" -eq 0 ]]; then
  echo "- Everything managed by this installer is already up to date."
elif [[ "${updates_available}" -gt 0 ]]; then
  echo "- One or more managed repos can be updated when you run repair/update."
fi

if [[ "${attention_needed}" -gt 0 ]]; then
  echo "- Some repos could not be auto-updated cleanly. See details above."
fi

echo
echo "Managed repo update policy:"
echo "- NymphsCore helper repo is checked here so the installer can report whether the managed helper checkout is current."
echo "- The current installer run still uses the packaged helper scripts as its execution source of truth."
echo "- Backend repos are safe to fast-forward during repair/update when they are clean and on the expected branch."
echo "- The current default branch policy is main for the managed backend repos."
echo "- The Z-Image backend uses NYMPHS3D_N2D2_REPO_URL and NYMPHS3D_N2D2_REPO_BRANCH if you need to point the installer at a different repo or branch during experimentation."
echo "- TRELLIS.2 uses NYMPHS3D_TRELLIS_REPO_URL and NYMPHS3D_TRELLIS_REPO_BRANCH if you need to point the installer at a different repo or branch during experimentation."
