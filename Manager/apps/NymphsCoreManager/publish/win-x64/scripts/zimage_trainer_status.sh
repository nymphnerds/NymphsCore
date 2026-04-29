#!/usr/bin/env bash
set -euo pipefail

TRAINER_ROOT="${ZIMAGE_TRAINER_ROOT:-$HOME/ZImage-Trainer}"
REPO_DIR="${ZIMAGE_TRAINER_REPO_DIR:-$TRAINER_ROOT/DiffSynth-Studio}"
VENV_DIR="${ZIMAGE_TRAINER_VENV:-$TRAINER_ROOT/.venv-ztrain}"
DATASET_ROOT="${ZIMAGE_DATASET_ROOT:-$TRAINER_ROOT/datasets}"
LORA_ROOT="${ZIMAGE_LORA_ROOT:-$TRAINER_ROOT/loras}"

repo_exists="no"
venv_exists="no"
dataset_root_exists="no"
output_root_exists="no"
running="no"

[[ -d "$REPO_DIR/.git" ]] && repo_exists="yes"
[[ -x "$VENV_DIR/bin/python" ]] && venv_exists="yes"
[[ -d "$DATASET_ROOT" ]] && dataset_root_exists="yes"
[[ -d "$LORA_ROOT" ]] && output_root_exists="yes"

if pgrep -u "$(id -u)" -f "examples/z_image/model_training/train.py|ztrain_run_config.sh|DiffSynth-Studio.*train.py" >/dev/null 2>&1; then
  running="yes"
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
if [[ "$repo_exists" == "yes" && "$venv_exists" == "yes" ]]; then
  install_state="installed"
elif [[ "$repo_exists" == "yes" || "$venv_exists" == "yes" ]]; then
  install_state="partial"
fi

echo "ZIMAGE_TRAINER_INSTALLED=$install_state"
echo "ZIMAGE_TRAINER_REPO=$repo_exists"
echo "ZIMAGE_TRAINER_VENV=$venv_exists"
echo "ZIMAGE_TRAINER_DATASET_ROOT=$dataset_root_exists"
echo "ZIMAGE_TRAINER_OUTPUT_ROOT=$output_root_exists"
echo "ZIMAGE_TRAINER_RUNNING=$running"
echo "ZIMAGE_TRAINER_LORAS_FOUND=$lora_count"
echo "ZIMAGE_TRAINER_DATASETS_FOUND=$dataset_count"
