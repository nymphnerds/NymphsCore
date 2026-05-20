#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage: uninstall_nymph_module.sh --module <id> [--install-root <path>] [--dry-run] [--yes] [--purge] [--data-only]

Default uninstall removes runtime files while preserving known module data when
the module supports preservation. Use --purge to delete the whole module folder
including module data. Use --data-only to delete known data folders while
keeping the installed runtime.
EOF
}

MODULE_ID=""
INSTALL_ROOT_OVERRIDE=""
DRY_RUN=0
YES=0
PURGE=0
DATA_ONLY=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --module)
      MODULE_ID="${2:-}"
      shift 2
      ;;
    --install-root)
      INSTALL_ROOT_OVERRIDE="${2:-}"
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
    --data-only)
      DATA_ONLY=1
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

if [[ "${PURGE}" -eq 1 && "${DATA_ONLY}" -eq 1 ]]; then
  echo "Choose only one of --purge or --data-only." >&2
  usage >&2
  exit 2
fi

MODULE_ID="$(printf '%s' "${MODULE_ID}" | tr '[:upper:]' '[:lower:]')"
WORK_ROOT="${NYMPHS_MODULE_WORK_ROOT:-${HOME}/.cache/nymphs-modules}"
ACTION_ROOT="${WORK_ROOT}/actions"
ACTION_STATE_FILE=""

read_manifest_install_root() {
  local manifest_file="${WORK_ROOT}/repos/${MODULE_ID}/nymph.json"
  [[ -f "${manifest_file}" ]] || return 1
  command -v python3 >/dev/null 2>&1 || return 1

  python3 - "${manifest_file}" "${HOME}" <<'PY'
import json
import sys

manifest_path = sys.argv[1]
home = sys.argv[2].rstrip("/")

with open(manifest_path, "r", encoding="utf-8") as handle:
    manifest = json.load(handle)

root = ""
install = manifest.get("install")
if isinstance(install, dict):
    root = str(install.get("root") or install.get("path") or "").strip()
runtime = manifest.get("runtime")
if not root and isinstance(runtime, dict):
    root = str(runtime.get("install_root") or "").strip()

root = root.replace("\\", "/").rstrip("/")
parts = [part for part in root.split("/") if part]
if not root.startswith(home + "/") or ".." in parts:
    raise SystemExit(1)
print(root)
PY
}

normalize_install_root_override() {
  local root="${INSTALL_ROOT_OVERRIDE}"
  [[ -n "${root}" ]] || return 1

  root="${root//\\//}"
  root="${root%/}"

  case "${root}" in
    "\$HOME") root="${HOME}" ;;
    "\$HOME/"*) root="${HOME}/${root#\$HOME/}" ;;
    "~") root="${HOME}" ;;
    "~/"*) root="${HOME}/${root#~/}" ;;
  esac

  local parts
  IFS='/' read -r -a parts <<< "${root}"
  for part in "${parts[@]}"; do
    [[ "${part}" != ".." ]] || return 1
  done

  [[ "${root}" == "${HOME}" || "${root}" == "${HOME}/"* ]] || return 1
  printf '%s\n' "${root}"
}

if override_install_root="$(normalize_install_root_override 2>/dev/null)"; then
  INSTALL_ROOT="${override_install_root}"
elif manifest_install_root="$(read_manifest_install_root 2>/dev/null)"; then
  INSTALL_ROOT="${manifest_install_root}"
else
  INSTALL_ROOT="${HOME}/${MODULE_ID}"
fi
PRESERVE_NAMES=("data" "projects" "config" "logs" "outputs" "models" "datasets" "loras" "jobs" "secrets")
DATA_NAMES=("data" "projects" "config" "logs" "outputs" "models" "datasets" "loras" "jobs" "secrets")

echo "module=${MODULE_ID}"
echo "install_root=${INSTALL_ROOT}"
echo "purge=${PURGE}"
echo "data_only=${DATA_ONLY}"

mkdir -p "${ACTION_ROOT}"
ACTION_STATE_FILE="${ACTION_ROOT}/${MODULE_ID}.state"

write_action_state() {
  local action="$1"
  local status="$2"
  local detail="$3"
  cat > "${ACTION_STATE_FILE}" <<EOF
module=${MODULE_ID}
action=${action}
status=${status}
pid=$$
started_at=$(date -Is)
detail=${detail}
EOF
}

clear_action_state() {
  rm -f "${ACTION_STATE_FILE}"
}

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
    if [[ "${DATA_ONLY}" -eq 1 ]]; then
      "${uninstall_script}" --dry-run --data-only
    elif [[ "${PURGE}" -eq 1 ]]; then
      "${uninstall_script}" --dry-run --purge
    else
      "${uninstall_script}" --dry-run
    fi
  elif [[ "${DATA_ONLY}" -eq 1 ]]; then
    "${uninstall_script}" --yes --data-only
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
  if [[ "${DATA_ONLY}" -eq 1 ]]; then
    echo "would_delete_known_data=${DATA_NAMES[*]}"
  elif [[ "${PURGE}" -eq 1 ]]; then
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

if [[ "${DATA_ONLY}" -eq 1 ]]; then
  write_action_state "delete-data" "running" "Deleting ${MODULE_ID} data from the managed WSL distro."
elif [[ "${PURGE}" -eq 1 ]]; then
  write_action_state "delete" "running" "Deleting ${MODULE_ID} from the managed WSL distro."
else
  write_action_state "uninstall" "running" "Uninstalling ${MODULE_ID} from the managed WSL distro."
fi
trap clear_action_state EXIT

stop_known_processes() {
  if [[ -n "${INSTALL_ROOT}" ]]; then
    pkill -f "${INSTALL_ROOT}" >/dev/null 2>&1 || true
  fi
}

if [[ "${DATA_ONLY}" -ne 1 ]]; then
  stop_known_processes
fi

if [[ "${DATA_ONLY}" -eq 1 ]]; then
  deleted=0
  for name in "${DATA_NAMES[@]}"; do
    if [[ -e "${INSTALL_ROOT}/${name}" ]]; then
      rm -rf -- "${INSTALL_ROOT:?}/${name}"
      echo "Deleted data ${INSTALL_ROOT}/${name}."
      deleted=1
    fi
  done

  if [[ "${deleted}" -eq 0 ]]; then
    echo "No known module data was found to delete."
  fi

  echo "Deleted data for ${MODULE_ID}."
  exit 0
fi

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
