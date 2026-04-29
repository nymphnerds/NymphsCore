#!/usr/bin/env bash
set -euo pipefail

INSTALL_ROOT="${BRAIN_INSTALL_ROOT:-${HOME}/Nymphs-Brain}"
DATASET_DIR="${ZIMAGE_DATASET_DIR:-}"
METADATA_PATH="${ZIMAGE_METADATA_PATH:-}"
CAPTION_MODE="${ZIMAGE_CAPTION_MODE:-fill_blanks}"
TRAINING_FOCUS="${ZIMAGE_TRAINING_FOCUS:-character}"
LLMS_START_PATH="${INSTALL_ROOT}/bin/lms-start"
LMS_STOP_PATH="${INSTALL_ROOT}/bin/lms-stop"
PYTHON_HELPER="${HOME}/ZImage-Trainer/bin/zimage-caption-brain.py"
MODELS_DIR="${INSTALL_ROOT}/models"

if [[ -z "${DATASET_DIR}" || -z "${METADATA_PATH}" ]]; then
  echo "Caption Brain is missing dataset or metadata paths." >&2
  exit 1
fi

if [[ ! -x "${LLMS_START_PATH}" ]]; then
  echo "Caption Brain could not find ${LLMS_START_PATH}. Install or repair Nymphs-Brain first." >&2
  exit 1
fi

if [[ ! -x "${LMS_STOP_PATH}" ]]; then
  echo "Caption Brain could not find ${LMS_STOP_PATH}. Install or repair Nymphs-Brain first." >&2
  exit 1
fi

if [[ ! -f "${PYTHON_HELPER}" ]]; then
  echo "Caption Brain helper is missing at ${PYTHON_HELPER}. Repair the trainer first." >&2
  exit 1
fi

brain_api_ready() {
  curl --silent --show-error --fail --connect-timeout 2 --max-time 5 "http://127.0.0.1:8000/v1/models" >/dev/null 2>&1
}

read_model_key() {
  sed -n 's/^MODEL_KEY="\([^"]*\)".*/\1/p' "${LLMS_START_PATH}" | head -n 1
}

model_is_probably_vision() {
  local model_key="$1"
  local normalized
  normalized="$(printf '%s' "${model_key}" | tr '[:upper:]' '[:lower:]')"

  case "${normalized}" in
    *-vl-*|*vision*|*llava*|*minicpmv*|*internvl*|*cogvlm*|*pixtral*|*glm4v*|*gemma4v*|*qwen2vl*|*qwen3vl*|*molmo*|*smolvlm*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

find_model_dir_for_key() {
  local model_key="$1"
  local model_name="${model_key##*/}"
  local model_slug
  model_slug="$(printf '%s' "${model_name}" | tr '[:upper:]' '[:lower:]' | sed 's/-gguf$//; s/[^a-z0-9]//g')"

  if [[ ! -d "${MODELS_DIR}" ]]; then
    return 1
  fi

  find "${MODELS_DIR}" -mindepth 2 -maxdepth 2 -type d 2>/dev/null | \
    awk -v model_slug="${model_slug}" '
      {
        path = tolower($0)
        comparable = path
        gsub(/-gguf/, "", comparable)
        gsub(/[^a-z0-9]/, "", comparable)
        if (index(comparable, model_slug) > 0) {
          print
          exit
        }
      }'
}

model_dir_is_vision_ready() {
  local model_dir="$1"

  [[ -n "${model_dir}" && -d "${model_dir}" ]] || return 1
  find "${model_dir}" -maxdepth 1 -type f -iname '*.gguf' ! -iname '*mmproj*' | grep -q .
  find "${model_dir}" -maxdepth 1 -type f -iname '*mmproj*' | grep -q .
}

find_compatible_vision_model_key() {
  if [[ ! -d "${MODELS_DIR}" ]]; then
    return 1
  fi

  while IFS= read -r model_dir; do
    [[ -n "${model_dir}" ]] || continue
    local folder_name
    local vendor_name
    local candidate_key
    folder_name="$(basename "${model_dir}")"
    vendor_name="$(basename "$(dirname "${model_dir}")")"
    candidate_key="${vendor_name}/${folder_name}"
    if model_is_probably_vision "${candidate_key}" && model_dir_is_vision_ready "${model_dir}"; then
      printf '%s\n' "${candidate_key}"
      return 0
    fi
  done < <(find "${MODELS_DIR}" -mindepth 2 -maxdepth 2 -type d | sort)

  return 1
}

update_model_key() {
  local model_key="$1"
  awk -v model="${model_key}" '
    BEGIN { RS=ORS="\n" }
    {
      gsub(/\r$/, "")
      if (/^MODEL_KEY=/) {
        print "MODEL_KEY=\"" model "\""
        next
      }
      print
    }
  ' "${LLMS_START_PATH}" > "${LLMS_START_PATH}.tmp"

  mv "${LLMS_START_PATH}.tmp" "${LLMS_START_PATH}"
  chmod +x "${LLMS_START_PATH}"
}

ORIGINAL_MODEL_KEY="$(read_model_key)"
SELECTED_MODEL_KEY="${ORIGINAL_MODEL_KEY}"
ORIGINAL_SERVER_RUNNING=0
STARTED_TEMP_SERVER=0
TEMP_SCRIPT_BACKUP=""

cleanup() {
  local exit_code=$?

  if [[ -n "${TEMP_SCRIPT_BACKUP}" && -f "${TEMP_SCRIPT_BACKUP}" ]]; then
    cp "${TEMP_SCRIPT_BACKUP}" "${LLMS_START_PATH}" 2>/dev/null || true
    chmod +x "${LLMS_START_PATH}" 2>/dev/null || true
    rm -f "${TEMP_SCRIPT_BACKUP}" 2>/dev/null || true
  fi

  if [[ ${STARTED_TEMP_SERVER} -eq 1 ]]; then
    "${LMS_STOP_PATH}" >/dev/null 2>&1 || true
  fi

  if [[ ${ORIGINAL_SERVER_RUNNING} -eq 1 ]]; then
    if ! brain_api_ready; then
      echo "Restoring the original Brain model..." >&2
      "${LLMS_START_PATH}" >/dev/null 2>&1 || true
    fi
  fi

  exit "${exit_code}"
}

trap cleanup EXIT

if brain_api_ready; then
  ORIGINAL_SERVER_RUNNING=1
  echo "Caption Brain: existing Brain server detected."
fi

if model_is_probably_vision "${ORIGINAL_MODEL_KEY}"; then
  original_model_dir="$(find_model_dir_for_key "${ORIGINAL_MODEL_KEY}" || true)"
  if model_dir_is_vision_ready "${original_model_dir}"; then
    SELECTED_MODEL_KEY="${ORIGINAL_MODEL_KEY}"
  fi
fi

if [[ -z "${SELECTED_MODEL_KEY}" ]] || ! model_is_probably_vision "${SELECTED_MODEL_KEY}"; then
  SELECTED_MODEL_KEY="$(find_compatible_vision_model_key || true)"
fi

if [[ -z "${SELECTED_MODEL_KEY}" ]]; then
  echo "Caption Brain could not find a downloaded vision model with a matching mmproj file under ${MODELS_DIR}." >&2
  echo "Download a Brain vision model such as Qwen2.5-VL and keep the mmproj file beside the GGUF." >&2
  exit 1
fi

echo "Caption Brain will use model: ${SELECTED_MODEL_KEY}"

if [[ ${ORIGINAL_SERVER_RUNNING} -eq 0 ]]; then
  TEMP_SCRIPT_BACKUP="$(mktemp)"
  cp "${LLMS_START_PATH}" "${TEMP_SCRIPT_BACKUP}"
  update_model_key "${SELECTED_MODEL_KEY}"
  echo "Caption Brain: starting temporary Brain vision server..."
  "${LLMS_START_PATH}"
  STARTED_TEMP_SERVER=1
elif [[ "${SELECTED_MODEL_KEY}" != "${ORIGINAL_MODEL_KEY}" ]]; then
  TEMP_SCRIPT_BACKUP="$(mktemp)"
  cp "${LLMS_START_PATH}" "${TEMP_SCRIPT_BACKUP}"
  echo "Caption Brain: stopping the current Brain model so a vision model can be loaded..."
  "${LMS_STOP_PATH}" >/dev/null 2>&1 || true
  update_model_key "${SELECTED_MODEL_KEY}"
  echo "Caption Brain: starting temporary Brain vision server..."
  "${LLMS_START_PATH}"
  STARTED_TEMP_SERVER=1
else
  echo "Caption Brain: reusing the already configured Brain vision model."
fi

python3 "${PYTHON_HELPER}" \
  --dataset-dir "${DATASET_DIR}" \
  --metadata-path "${METADATA_PATH}" \
  --mode "${CAPTION_MODE}" \
  --training-focus "${TRAINING_FOCUS}" \
  --endpoint "http://127.0.0.1:8000/v1/chat/completions"

echo "Caption Brain finished drafting captions."
