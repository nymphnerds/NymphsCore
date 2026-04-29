#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: ztrain_run_config.sh /path/to/job.sh" >&2
  exit 2
fi

JOB="$1"
if [[ ! -f "$JOB" ]]; then
  echo "Training job not found: $JOB" >&2
  exit 1
fi

if [[ -z "${HOME:-}" || ! -d "${HOME}" || "${HOME}" == "/root" ]]; then
  DETECTED_HOME="$(getent passwd "$(id -un)" | cut -d: -f6 || true)"
  if [[ -n "${DETECTED_HOME}" && -d "${DETECTED_HOME}" ]]; then
    export HOME="${DETECTED_HOME}"
  fi
fi

export USER="${USER:-$(id -un)}"
export LOGNAME="${LOGNAME:-${USER}}"

LOG_ROOT="${ZIMAGE_TRAINER_LOG_ROOT:-$HOME/ZImage-Trainer/logs}"
mkdir -p "$LOG_ROOT"
bash "$JOB" 2>&1 | tee "$LOG_ROOT/$(basename "$JOB" .sh)-$(date +%Y%m%d-%H%M%S).log"
