#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "Usage: ztrain_run_config.sh /path/to/config.yaml" >&2
  exit 2
fi

CONFIG_PATH="$1"
if [[ ! -f "$CONFIG_PATH" ]]; then
  echo "Training config not found: $CONFIG_PATH" >&2
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

TRAINER_ROOT="${ZIMAGE_TRAINER_ROOT:-$HOME/ZImage-Trainer}"
LOG_ROOT="${ZIMAGE_TRAINER_LOG_ROOT:-$TRAINER_ROOT/logs}"
RUN_STATE_DIR="${ZIMAGE_TRAINER_RUN_STATE_DIR:-$TRAINER_ROOT/run}"
PID_FILE="$RUN_STATE_DIR/active_train.pid"
mkdir -p "$LOG_ROOT" "$RUN_STATE_DIR"

cd "$TRAINER_ROOT/ai-toolkit"
source "$TRAINER_ROOT/ai-toolkit/venv/bin/activate"

readarray -t TRAIN_INFO < <(python3 - <<'PYINFO' "$CONFIG_PATH"
from pathlib import Path
import sys
import yaml

config_path = Path(sys.argv[1])
data = yaml.safe_load(config_path.read_text(encoding="utf-8"))
config = data.get("config", {})
processes = config.get("process", []) or []
proc = processes[0] if processes else {}
name = str(config.get("name") or "training").strip()
steps = int(proc.get("train", {}).get("steps", 0) or 0)
training_folder = str(proc.get("training_folder") or "").strip()
db_path = ""
if training_folder and name:
    db_path = f"{training_folder.rstrip('/')}/{name}/loss_log.db"
print(name)
print(steps)
print(db_path)
PYINFO
)

RUN_NAME="${TRAIN_INFO[0]:-training}"
TOTAL_STEPS="${TRAIN_INFO[1]:-0}"
PROGRESS_DB="${TRAIN_INFO[2]:-}"
RUN_JOB_ID="${AITK_JOB_ID:-}"
LOG_FILE="$LOG_ROOT/${RUN_NAME}-$(date +%Y%m%d-%H%M%S).log"

cleanup() {
  rm -f "$PID_FILE"
}

trap cleanup EXIT

if [[ "$TOTAL_STEPS" =~ ^[0-9]+$ && "$TOTAL_STEPS" -gt 0 ]]; then
  echo "TRAIN_PROGRESS current=0 total=$TOTAL_STEPS"
fi

python run.py "$CONFIG_PATH" > >(tee "$LOG_FILE") 2>&1 &
TRAIN_PID=$!

PROGRESS_PID=""
if [[ -n "$PROGRESS_DB" && "$TOTAL_STEPS" =~ ^[0-9]+$ && "$TOTAL_STEPS" -gt 0 ]]; then
  python3 -u - <<'PYPROG' "$PROGRESS_DB" "$TOTAL_STEPS" "$TRAIN_PID" &
import os
import sqlite3
import sys
import time
from pathlib import Path

db_path = Path(sys.argv[1])
total = int(sys.argv[2])
pid = int(sys.argv[3])
last = -1

def alive(process_id: int) -> bool:
    try:
        os.kill(process_id, 0)
        return True
    except OSError:
        return False

def read_current() -> int | None:
    if not db_path.exists():
        return None
    try:
        con = sqlite3.connect(f"file:{db_path}?mode=ro", uri=True, timeout=1.0)
        try:
            row = con.execute("SELECT MAX(step) FROM steps").fetchone()
        finally:
            con.close()
        if not row or row[0] is None:
            return None
        return min(int(row[0]) + 1, total)
    except Exception:
        return None

while alive(pid):
    current = read_current()
    if current is not None and current != last:
        print(f"TRAIN_PROGRESS current={current} total={total}", flush=True)
        last = current
    time.sleep(1.0)

current = read_current()
if current is not None and current != last:
    print(f"TRAIN_PROGRESS current={current} total={total}", flush=True)
PYPROG
  PROGRESS_PID=$!
fi

cat > "$PID_FILE" <<EOF
SHELL_PID=$$
TRAIN_PID=$TRAIN_PID
PROGRESS_PID=${PROGRESS_PID:-}
CONFIG_PATH=$CONFIG_PATH
RUN_NAME=$RUN_NAME
AITK_JOB_ID=${RUN_JOB_ID:-}
LOG_FILE=$LOG_FILE
EOF

set +e
wait "$TRAIN_PID"
TRAIN_EXIT=$?
set -e

if [[ -n "$PROGRESS_PID" ]]; then
  wait "$PROGRESS_PID" || true
fi

if [[ "$TRAIN_EXIT" -eq 0 && "$TOTAL_STEPS" =~ ^[0-9]+$ && "$TOTAL_STEPS" -gt 0 ]]; then
  echo "TRAIN_PROGRESS current=$TOTAL_STEPS total=$TOTAL_STEPS"
fi

exit "$TRAIN_EXIT"
