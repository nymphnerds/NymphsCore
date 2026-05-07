#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: uninstall_nymph_module.sh --module <id> [--dry-run] [--yes] [--purge]

Modules: brain, zimage, trellis, lora, worbi

Default uninstall removes the module install folder and moves known user data
to ~/NymphsModuleBackups/<module>-<timestamp>. Use --purge to delete the whole
module folder including module data.
EOF
}

MODULE_ID=""
DRY_RUN=0
YES=0
PURGE=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --module)
      MODULE_ID="${2:-}"
      shift 2
      ;;
    --dry-run)
      DRY_RUN=1
      shift
      ;;
    --yes)
      YES=1
      shift
      ;;
    --purge)
      PURGE=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "${MODULE_ID}" ]]; then
  echo "Missing --module." >&2
  usage >&2
  exit 2
fi

MODULE_ID="$(printf '%s' "${MODULE_ID}" | tr '[:upper:]' '[:lower:]')"
INSTALL_ROOT=""
PRESERVE_NAMES=()
WORK_ROOT="${NYMPHS_MODULE_WORK_ROOT:-${HOME}/.cache/nymphs-modules}"

case "${MODULE_ID}" in
  brain)
    INSTALL_ROOT="${BRAIN_INSTALL_ROOT:-${HOME}/Nymphs-Brain}"
    PRESERVE_NAMES=("models" "open-webui-data" "mcp" "secrets" "logs")
    ;;
  zimage)
    INSTALL_ROOT="${NYMPHS3D_Z_IMAGE_DIR:-${HOME}/Z-Image}"
    PRESERVE_NAMES=("outputs" "logs")
    ;;
  trellis)
    INSTALL_ROOT="${NYMPHS3D_TRELLIS_DIR:-${HOME}/TRELLIS.2}"
    PRESERVE_NAMES=("outputs" "logs")
    ;;
  lora|ai-toolkit)
    MODULE_ID="lora"
    INSTALL_ROOT="${ZIMAGE_TRAINER_ROOT:-${HOME}/ZImage-Trainer}"
    PRESERVE_NAMES=("datasets" "loras" "jobs" "config" "logs")
    ;;
  worbi)
    INSTALL_ROOT="${WORBI_ROOT:-${HOME}/worbi}"
    PRESERVE_NAMES=("data" "projects" "config" "logs")
    ;;
  *)
    echo "Unsupported module id: ${MODULE_ID}" >&2
    usage >&2
    exit 2
    ;;
esac

echo "module=${MODULE_ID}"
echo "install_root=${INSTALL_ROOT}"
echo "purge=${PURGE}"

run_module_owned_uninstall_if_available() {
  local repo_root="${WORK_ROOT}/repos/${MODULE_ID}"
  local manifest_file="${repo_root}/nymph.json"

  [[ -f "${manifest_file}" ]] || return 1
  command -v python3 >/dev/null 2>&1 || return 1

  local uninstall_entrypoint
  uninstall_entrypoint="$(
    python3 - "${manifest_file}" <<'PY'
import json
import sys

with open(sys.argv[1], "r", encoding="utf-8") as handle:
    manifest = json.load(handle)

entrypoint = str(manifest.get("entrypoints", {}).get("uninstall", "")).strip()
if not entrypoint:
    raise SystemExit(1)
if entrypoint.startswith("/") or ".." in entrypoint.split("/"):
    raise SystemExit("module uninstall entrypoint is not a safe relative path")
print(entrypoint)
PY
  )" || return 1

  local uninstall_script="${repo_root}/${uninstall_entrypoint}"
  [[ -f "${uninstall_script}" ]] || return 1

  chmod +x "${uninstall_script}"
  echo "module_uninstall_entrypoint=${uninstall_entrypoint}"
  if [[ "${DRY_RUN}" -eq 1 ]]; then
    if [[ "${PURGE}" -eq 1 ]]; then
      "${uninstall_script}" --dry-run --purge
    else
      "${uninstall_script}" --dry-run
    fi
  elif [[ "${PURGE}" -eq 1 ]]; then
    "${uninstall_script}" --yes --purge
  else
    "${uninstall_script}" --yes
  fi

  return 0
}

if run_module_owned_uninstall_if_available; then
  echo "Uninstall handled by ${MODULE_ID} module contract."
  exit 0
fi

if [[ ! -e "${INSTALL_ROOT}" ]]; then
  echo "Module install root is already absent."
  exit 0
fi

if [[ "${DRY_RUN}" -eq 1 ]]; then
  echo "dry_run=yes"
  if [[ "${PURGE}" -eq 1 ]]; then
    echo "would_delete=${INSTALL_ROOT}"
  else
    echo "would_backup_known_data=${PRESERVE_NAMES[*]}"
    echo "would_delete=${INSTALL_ROOT}"
  fi
  exit 0
fi

if [[ "${YES}" -ne 1 ]]; then
  echo "Refusing to uninstall without --yes. Re-run with --dry-run to preview." >&2
  exit 3
fi

stop_known_processes() {
  case "${MODULE_ID}" in
    brain)
      if [[ -x "${INSTALL_ROOT}/bin/lms-stop" ]]; then
        "${INSTALL_ROOT}/bin/lms-stop" || true
      fi
      ;;
    lora)
      pkill -f "${INSTALL_ROOT}/ai-toolkit" >/dev/null 2>&1 || true
      ;;
    worbi)
      if [[ -x "${INSTALL_ROOT}/bin/worbi-stop" ]]; then
        "${INSTALL_ROOT}/bin/worbi-stop" || true
      elif [[ -x "${HOME}/.local/bin/worbi-stop" ]]; then
        "${HOME}/.local/bin/worbi-stop" || true
      fi
      pkill -f "${INSTALL_ROOT}" >/dev/null 2>&1 || true
      ;;
  esac
}

stop_known_processes

if [[ "${PURGE}" -eq 1 ]]; then
  rm -rf -- "${INSTALL_ROOT}" || true
  if [[ ! -e "${INSTALL_ROOT}" ]]; then
    echo "Deleted ${INSTALL_ROOT}."
    exit 0
  fi

  echo "ERROR: Failed to delete ${INSTALL_ROOT}." >&2
  exit 1
fi

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_ROOT="${NYMPHS_MODULE_BACKUP_ROOT:-${HOME}/NymphsModuleBackups}/${MODULE_ID}-${TIMESTAMP}"
mkdir -p -- "${BACKUP_ROOT}"

for name in "${PRESERVE_NAMES[@]}"; do
  if [[ -e "${INSTALL_ROOT}/${name}" ]]; then
    mv -- "${INSTALL_ROOT}/${name}" "${BACKUP_ROOT}/${name}"
    echo "Saved ${name} to ${BACKUP_ROOT}/${name}."
  fi
done

rm -rf -- "${INSTALL_ROOT}" || true

if [[ -z "$(find "${BACKUP_ROOT}" -mindepth 1 -maxdepth 1 -print -quit)" ]]; then
  rmdir -- "${BACKUP_ROOT}"
  echo "No known user data was found to preserve."
else
  echo "Preserved known user data in ${BACKUP_ROOT}."
fi

echo "Uninstalled ${MODULE_ID}."
if [[ ! -e "${INSTALL_ROOT}" ]]; then
  exit 0
fi

echo "ERROR: Failed to remove ${INSTALL_ROOT}." >&2
exit 1
