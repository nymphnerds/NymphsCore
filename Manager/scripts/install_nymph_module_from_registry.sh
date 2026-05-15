#!/usr/bin/env bash
set -euo pipefail

DEFAULT_REGISTRY_URL="https://raw.githubusercontent.com/nymphnerds/nymphs-registry/main/nymphs.json"

usage() {
  cat <<'EOF'
Usage: install_nymph_module_from_registry.sh --module <id> [--registry-url <url>] [--action install|update] [--dry-run]

Installs a trusted Nymph module by reading the public Nymphs registry, fetching
that module's nymph.json, cloning the module repo, and running its install
entrypoint.
EOF
}

MODULE_ID=""
REGISTRY_URL="${NYMPHS_REGISTRY_URL:-${DEFAULT_REGISTRY_URL}}"
ACTION="install"
DRY_RUN=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --module)
      MODULE_ID="${2:-}"
      shift 2
      ;;
    --registry-url)
      REGISTRY_URL="${2:-}"
      shift 2
      ;;
    --action)
      ACTION="${2:-}"
      shift 2
      ;;
    --dry-run)
      DRY_RUN=1
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
ACTION="$(printf '%s' "${ACTION}" | tr '[:upper:]' '[:lower:]')"
if [[ "${ACTION}" != "install" && "${ACTION}" != "update" ]]; then
  echo "Unsupported --action: ${ACTION}" >&2
  exit 2
fi
WORK_ROOT="${NYMPHS_MODULE_WORK_ROOT:-${HOME}/.cache/nymphs-modules}"
REPO_ROOT="${WORK_ROOT}/repos/${MODULE_ID}"
REGISTRY_FILE="${WORK_ROOT}/registry.json"
MANIFEST_FILE="${WORK_ROOT}/${MODULE_ID}.nymph.json"
ACTION_ROOT="${WORK_ROOT}/actions"
ACTION_STATE_FILE="${ACTION_ROOT}/${MODULE_ID}.state"

mkdir -p "${WORK_ROOT}/repos" "${ACTION_ROOT}"

write_action_state() {
  local status="$1"
  local detail="$2"
cat > "${ACTION_STATE_FILE}" <<EOF
module=${MODULE_ID}
action=${ACTION}
status=${status}
pid=$$
started_at=$(date -Is)
detail=${detail}
EOF
}

clear_action_state() {
  rm -f "${ACTION_STATE_FILE}"
}

require_tool() {
  local tool="$1"
  if ! command -v "${tool}" >/dev/null 2>&1; then
    echo "Required tool missing: ${tool}" >&2
    exit 3
  fi
}

require_tool curl
require_tool git
require_tool python3

echo "module=${MODULE_ID}"
echo "registry_url=${REGISTRY_URL}"

curl -fsSL "${REGISTRY_URL}" -o "${REGISTRY_FILE}"

read_manifest_url() {
  python3 - "$REGISTRY_FILE" "$MODULE_ID" <<'PY'
import json
import sys

registry_path, module_id = sys.argv[1], sys.argv[2]
with open(registry_path, "r", encoding="utf-8") as handle:
    registry = json.load(handle)

for module in registry.get("modules", []):
    if str(module.get("id", "")).lower() != module_id:
        continue
    if not module.get("trusted", False):
        raise SystemExit(f"registry entry for {module_id} is not trusted")
    manifest_url = str(module.get("manifest_url", "")).strip()
    if not manifest_url:
        raise SystemExit(f"registry entry for {module_id} has no manifest_url")
    print(manifest_url)
    raise SystemExit(0)

raise SystemExit(f"module not found in registry: {module_id}")
PY
}

MANIFEST_URL="$(read_manifest_url)"
echo "manifest_url=${MANIFEST_URL}"

if [[ "${MANIFEST_URL}" =~ ^https://raw\.githubusercontent\.com/nymphnerds/([^/]+)/([^/]+)/nymph\.json$ ]]; then
  REPO_NAME="${BASH_REMATCH[1]}"
  REPO_BRANCH="${BASH_REMATCH[2]}"
  REPO_URL="https://github.com/nymphnerds/${REPO_NAME}.git"
else
  echo "Unsupported manifest URL. Only trusted nymphnerds raw GitHub manifests are allowed." >&2
  exit 4
fi

echo "repo_url=${REPO_URL}"
echo "repo_branch=${REPO_BRANCH}"

if [[ "${DRY_RUN}" -eq 1 ]]; then
  echo "dry_run=yes"
  echo "would_clone_or_update=${REPO_ROOT}"
  echo "would_read_manifest=${MANIFEST_FILE}"
  exit 0
fi

write_action_state "running" "${ACTION^} ${MODULE_ID} from the Nymphs registry."
trap clear_action_state EXIT

if [[ -d "${REPO_ROOT}/.git" ]]; then
  git -C "${REPO_ROOT}" remote set-url origin "${REPO_URL}"
  git -C "${REPO_ROOT}" fetch --depth 1 origin "${REPO_BRANCH}"
  git -C "${REPO_ROOT}" reset -q --hard FETCH_HEAD
  git -C "${REPO_ROOT}" clean -q -fd
else
  rm -rf "${REPO_ROOT}"
  git clone --depth 1 --branch "${REPO_BRANCH}" "${REPO_URL}" "${REPO_ROOT}"
fi

cp "${REPO_ROOT}/nymph.json" "${MANIFEST_FILE}"

INSTALL_ENTRYPOINT="$(
  python3 - "$MANIFEST_FILE" "$ACTION" <<'PY'
import json
import sys

manifest_path, action = sys.argv[1], sys.argv[2]
with open(manifest_path, "r", encoding="utf-8") as handle:
    manifest = json.load(handle)

entrypoints = manifest.get("entrypoints", {})
entrypoint = ""
if action == "update":
    entrypoint = str(entrypoints.get("update", "")).strip()
entrypoint = entrypoint or str(entrypoints.get("install", "")).strip()
if not entrypoint:
    raise SystemExit("module manifest has no install entrypoint")
if entrypoint.startswith("/") or ".." in entrypoint.split("/"):
    raise SystemExit("module entrypoint is not a safe relative path")
print(entrypoint)
PY
)"

INSTALL_SCRIPT="${REPO_ROOT}/${INSTALL_ENTRYPOINT}"
if [[ ! -f "${INSTALL_SCRIPT}" ]]; then
  echo "Module entrypoint is missing: ${INSTALL_SCRIPT}" >&2
  exit 5
fi

chmod +x "${INSTALL_SCRIPT}"
echo "${ACTION}_entrypoint=${INSTALL_ENTRYPOINT}"
echo "${ACTION^} ${MODULE_ID}..."
set +e
"${INSTALL_SCRIPT}"
INSTALL_STATUS=$?
set -e
if [[ "${INSTALL_STATUS}" -ne 0 ]]; then
  echo "ERROR: ${ACTION} entrypoint failed for ${MODULE_ID} with exit code ${INSTALL_STATUS}." >&2
  exit "${INSTALL_STATUS}"
fi
echo "${ACTION^}d ${MODULE_ID}."
exit 0
