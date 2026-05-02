#!/usr/bin/env bash
set -euo pipefail

TRAINER_ROOT="${ZIMAGE_TRAINER_ROOT:-$HOME/ZImage-Trainer}"
REPO_DIR="${ZIMAGE_TRAINER_REPO_DIR:-$TRAINER_ROOT/ai-toolkit}"
VENV_DIR="${ZIMAGE_TRAINER_VENV:-$REPO_DIR/venv}"
DATASET_ROOT="${ZIMAGE_DATASET_ROOT:-$TRAINER_ROOT/datasets}"
LORA_ROOT="${ZIMAGE_LORA_ROOT:-$TRAINER_ROOT/loras}"
UI_DIR="$REPO_DIR/ui"
NODE_BIN="$TRAINER_ROOT/.node20/bin/node"
UI_DB_PATH="$REPO_DIR/aitk_db.db"
GRADIO_PORT="${ZIMAGE_TRAINER_GRADIO_PORT:-7861}"
PID_FILE="$TRAINER_ROOT/run/active_train.pid"

repo_exists="no"
venv_exists="no"
dataset_root_exists="no"
output_root_exists="no"
running="no"
node_exists="no"
ui_dir_exists="no"
ui_build_exists="no"
ui_db_exists="no"
ui_running="no"
queue_worker_running="no"
gradio_running="no"
active_state="idle"

[[ -d "$REPO_DIR/.git" ]] && repo_exists="yes"
[[ -x "$VENV_DIR/bin/python" ]] && venv_exists="yes"
[[ -d "$DATASET_ROOT" ]] && dataset_root_exists="yes"
[[ -d "$LORA_ROOT" ]] && output_root_exists="yes"
[[ -x "$NODE_BIN" ]] && node_exists="yes"
[[ -d "$UI_DIR" ]] && ui_dir_exists="yes"
[[ -d "$UI_DIR/.next" ]] && ui_build_exists="yes"
[[ -f "$UI_DB_PATH" ]] && ui_db_exists="yes"

if [[ -f "$UI_DB_PATH" ]]; then
  active_state="$(UI_DB_PATH="$UI_DB_PATH" python3 - <<'PYEOF'
import os
import sqlite3

db_path = os.environ["UI_DB_PATH"]
con = sqlite3.connect(db_path, timeout=5.0)
try:
    cur = con.cursor()
    cur.execute(
        "SELECT status FROM Job WHERE job_type = 'train' AND status IN ('running', 'queued') "
        "ORDER BY CASE status WHEN 'running' THEN 0 ELSE 1 END, updated_at DESC, queue_position ASC LIMIT 1"
    )
    row = cur.fetchone()
    print((row[0] if row and row[0] else "idle"), end="")
finally:
    con.close()
PYEOF
)"
fi

if [[ "$active_state" == "running" || "$active_state" == "queued" ]]; then
  running="yes"
fi

if [[ "$running" != "yes" && -f "$PID_FILE" ]]; then
  # Manager-managed direct-run jobs now publish an explicit pidfile.
  # Prefer that over fragile process-name matching.
  # shellcheck disable=SC1090
  source "$PID_FILE" || true
  if [[ -n "${TRAIN_PID:-}" ]] && kill -0 "$TRAIN_PID" 2>/dev/null; then
    running="yes"
  elif [[ -n "${SHELL_PID:-}" ]] && kill -0 "$SHELL_PID" 2>/dev/null; then
    running="yes"
  fi
fi

if [[ "$running" != "yes" ]] && pgrep -A -u "$(id -u)" -f "run.py .*ZImage-Trainer/jobs/|ztrain-run-config|ztrain_run_config.sh|python run.py" >/dev/null 2>&1; then
  running="yes"
fi

if pgrep -u "$(id -u)" -f "dist/cron/worker.js" >/dev/null 2>&1; then
  queue_worker_running="yes"
fi

if ss -ltn 2>/dev/null | grep -q ':8675 '; then
  ui_running="yes"
fi

if ss -ltn 2>/dev/null | grep -q ":${GRADIO_PORT} "; then
  gradio_running="yes"
fi

lora_count=0
if [[ -d "$LORA_ROOT" ]]; then
  lora_count="$(find "$LORA_ROOT" -type f -name '*.safetensors' | wc -l | tr -d ' ')"
fi

dataset_count=0
if [[ -d "$DATASET_ROOT" ]]; then
  dataset_count="$(find "$DATASET_ROOT" -mindepth 1 -maxdepth 1 -type d | wc -l | tr -d ' ')"
fi

install_state="missing"
if [[ "$repo_exists" == "yes" && "$venv_exists" == "yes" && "$node_exists" == "yes" && "$ui_build_exists" == "yes" && "$ui_db_exists" == "yes" ]]; then
  install_state="installed"
elif [[ "$repo_exists" == "yes" || "$venv_exists" == "yes" || "$node_exists" == "yes" || "$ui_build_exists" == "yes" || "$ui_db_exists" == "yes" ]]; then
  install_state="partial"
fi

echo "ZIMAGE_TRAINER_INSTALLED=$install_state"
echo "ZIMAGE_TRAINER_REPO=$repo_exists"
echo "ZIMAGE_TRAINER_VENV=$venv_exists"
echo "ZIMAGE_TRAINER_DATASET_ROOT=$dataset_root_exists"
echo "ZIMAGE_TRAINER_OUTPUT_ROOT=$output_root_exists"
echo "ZIMAGE_TRAINER_RUNNING=$running"
echo "ZIMAGE_TRAINER_ACTIVE_STATE=$active_state"
echo "ZIMAGE_TRAINER_LORAS_FOUND=$lora_count"
echo "ZIMAGE_TRAINER_DATASETS_FOUND=$dataset_count"
echo "ZIMAGE_TRAINER_UI_DIR=$ui_dir_exists"
echo "ZIMAGE_TRAINER_UI_NODE=$node_exists"
echo "ZIMAGE_TRAINER_UI_BUILD=$ui_build_exists"
echo "ZIMAGE_TRAINER_UI_DB=$ui_db_exists"
echo "ZIMAGE_TRAINER_UI_RUNNING=$ui_running"
echo "ZIMAGE_TRAINER_QUEUE_WORKER_RUNNING=$queue_worker_running"
echo "ZIMAGE_TRAINER_GRADIO_RUNNING=$gradio_running"
