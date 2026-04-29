#!/usr/bin/env bash
set -euo pipefail

TRAINER_ROOT="${ZIMAGE_TRAINER_ROOT:-$HOME/ZImage-Trainer}"
REPO_DIR="${ZIMAGE_TRAINER_REPO_DIR:-$TRAINER_ROOT/DiffSynth-Studio}"
VENV_DIR="${ZIMAGE_TRAINER_VENV:-$TRAINER_ROOT/.venv-ztrain}"
DATASET_ROOT="${ZIMAGE_DATASET_ROOT:-$TRAINER_ROOT/datasets}"
LORA_ROOT="${ZIMAGE_LORA_ROOT:-$TRAINER_ROOT/loras}"
LOG_ROOT="$TRAINER_ROOT/logs"
JOB_ROOT="$TRAINER_ROOT/jobs"
CONFIG_ROOT="$TRAINER_ROOT/config"
BIN_ROOT="$TRAINER_ROOT/bin"
ADAPTER_ROOT="$TRAINER_ROOT/adapters"
ADAPTER_REPO_DIR="$ADAPTER_ROOT/zimage_turbo_training_adapter"
ADAPTER_PATH_FILE="$ADAPTER_REPO_DIR/selected_adapter_path.txt"
TRAINING_MODEL_ROOT="$REPO_DIR/models/Tongyi-MAI/Z-Image-Turbo"

echo "Z-Image Trainer: preparing isolated DiffSynth-Studio sidecar."
mkdir -p "$TRAINER_ROOT" "$DATASET_ROOT" "$LORA_ROOT" "$LOG_ROOT" "$JOB_ROOT" "$CONFIG_ROOT" "$BIN_ROOT" "$ADAPTER_REPO_DIR"

if ! command -v git >/dev/null 2>&1; then
  echo "Installing git..."
  sudo apt-get update
  sudo apt-get install -y git
fi

if ! command -v python3 >/dev/null 2>&1; then
  echo "Installing Python..."
  sudo apt-get update
  sudo apt-get install -y python3 python3-venv python3-pip
fi

if [[ -d "$REPO_DIR/.git" ]]; then
  echo "DiffSynth-Studio repo found. Updating..."
  git -C "$REPO_DIR" pull --ff-only
else
  echo "Cloning DiffSynth-Studio into $REPO_DIR..."
  git clone https://github.com/modelscope/DiffSynth-Studio.git "$REPO_DIR"
fi

if [[ ! -x "$VENV_DIR/bin/python" ]]; then
  echo "Creating trainer venv at $VENV_DIR..."
  python3 -m venv "$VENV_DIR"
fi

echo "Installing trainer Python dependencies..."
"$VENV_DIR/bin/python" -m pip install --upgrade pip wheel setuptools
"$VENV_DIR/bin/python" -m pip install -e "$REPO_DIR"
"$VENV_DIR/bin/python" -m pip install accelerate peft safetensors modelscope huggingface_hub torchaudio

echo "Prefetching Z-Image Turbo training model bundle..."
TRAINING_MODEL_ROOT="$TRAINING_MODEL_ROOT" "$VENV_DIR/bin/python" - <<'PYEOF'
import os
from pathlib import Path

from huggingface_hub import snapshot_download

model_root = Path(os.environ["TRAINING_MODEL_ROOT"])
model_root.mkdir(parents=True, exist_ok=True)

snapshot_download(
    repo_id="Tongyi-MAI/Z-Image-Turbo",
    local_dir=str(model_root),
    allow_patterns=[
        "transformer/*",
        "text_encoder/*",
        "vae/*",
        "tokenizer/*",
        "*.json",
        "*.txt",
        "*.model",
    ],
)

required_paths = [
    model_root / "transformer",
    model_root / "text_encoder",
    model_root / "vae" / "diffusion_pytorch_model.safetensors",
    model_root / "tokenizer",
]

missing = [str(path) for path in required_paths if not path.exists()]
if missing:
    raise SystemExit(
        "Trainer model prefetch for Tongyi-MAI/Z-Image-Turbo is incomplete. Missing: "
        + ", ".join(missing)
    )

print(f"Z-Image Turbo trainer model bundle ready: {model_root}", flush=True)
PYEOF

echo "Prefetching Turbo training adapter..."
ADAPTER_REPO_DIR="$ADAPTER_REPO_DIR" ADAPTER_PATH_FILE="$ADAPTER_PATH_FILE" "$VENV_DIR/bin/python" - <<'PYEOF'
import os
from pathlib import Path

from huggingface_hub import snapshot_download

adapter_dir = Path(os.environ["ADAPTER_REPO_DIR"])
path_file = Path(os.environ["ADAPTER_PATH_FILE"])
adapter_dir.mkdir(parents=True, exist_ok=True)

snapshot_download(
    repo_id="ostris/zimage_turbo_training_adapter",
    local_dir=str(adapter_dir),
    allow_patterns=["*.safetensors", "*.bin", "*.pt", "*.pth", "*.ckpt"],
)

candidates = sorted(
    [
        path for path in adapter_dir.rglob("*")
        if path.is_file() and path.suffix.lower() in {".safetensors", ".bin", ".pt", ".pth", ".ckpt"}
    ],
    key=lambda path: (
        0 if path.suffix.lower() == ".safetensors" else 1,
        0 if path.parent == adapter_dir else 1,
        len(path.name),
        str(path).lower(),
    ),
)

if not candidates:
    raise SystemExit("No adapter weight file was downloaded for ostris/zimage_turbo_training_adapter")

selected = candidates[0].resolve()
path_file.write_text(str(selected) + "\n", encoding="utf-8")
print(f"Turbo training adapter ready: {selected}", flush=True)
PYEOF

cat > "$CONFIG_ROOT/zimage-turbo-differential-lora.template.sh" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

# Copy this file into ../jobs/<name>.sh and edit the dataset/output settings.
# Default method: Z-Image Turbo Differential LoRA with the Turbo training adapter.

DATASET_BASE_PATH="${DATASET_BASE_PATH:-$HOME/ZImage-Trainer/datasets/example}"
DATASET_METADATA_PATH="${DATASET_METADATA_PATH:-$DATASET_BASE_PATH/metadata.csv}"
OUTPUT_PATH="${OUTPUT_PATH:-$HOME/ZImage-Trainer/loras/example_zimage_turbo_lora}"
PRESET_LORA_PATH=""
if [[ -f "$HOME/ZImage-Trainer/adapters/zimage_turbo_training_adapter/selected_adapter_path.txt" ]]; then
  IFS= read -r PRESET_LORA_PATH < "$HOME/ZImage-Trainer/adapters/zimage_turbo_training_adapter/selected_adapter_path.txt" || true
  PRESET_LORA_PATH="${PRESET_LORA_PATH%$'\r'}"
fi
if [[ -z "$PRESET_LORA_PATH" || ! -f "$PRESET_LORA_PATH" ]]; then
  for candidate in \
    "$HOME/ZImage-Trainer/adapters/zimage_turbo_training_adapter"/*.safetensors \
    "$HOME/ZImage-Trainer/adapters/zimage_turbo_training_adapter"/*.bin \
    "$HOME/ZImage-Trainer/adapters/zimage_turbo_training_adapter"/*.pt \
    "$HOME/ZImage-Trainer/adapters/zimage_turbo_training_adapter"/*.pth \
    "$HOME/ZImage-Trainer/adapters/zimage_turbo_training_adapter"/*.ckpt; do
    if [[ -f "$candidate" ]]; then
      PRESET_LORA_PATH="$candidate"
      printf '%s\n' "$PRESET_LORA_PATH" > "$HOME/ZImage-Trainer/adapters/zimage_turbo_training_adapter/selected_adapter_path.txt"
      break
    fi
  done
fi
if [[ -z "$PRESET_LORA_PATH" || ! -f "$PRESET_LORA_PATH" ]]; then
  echo "Turbo training adapter file missing: $PRESET_LORA_PATH" >&2
  ls -l "$HOME/ZImage-Trainer/adapters/zimage_turbo_training_adapter" >&2 || true
  exit 1
fi

cd "$HOME/ZImage-Trainer/DiffSynth-Studio"
source "$HOME/ZImage-Trainer/.venv-ztrain/bin/activate"

if [[ ! -f "$HOME/ZImage-Trainer/bin/ztrain-zimage.py" ]]; then
  echo "Managed trainer entrypoint missing: $HOME/ZImage-Trainer/bin/ztrain-zimage.py" >&2
  exit 1
fi

accelerate launch "$HOME/ZImage-Trainer/bin/ztrain-zimage.py" \
  --dataset_base_path "$DATASET_BASE_PATH" \
  --dataset_metadata_path "$DATASET_METADATA_PATH" \
  --data_file_keys "image" \
  --max_pixels 1048576 \
  --dataset_repeat 50 \
  --model_id_with_origin_paths "Tongyi-MAI/Z-Image-Turbo:transformer/*.safetensors,Tongyi-MAI/Z-Image-Turbo:text_encoder/*.safetensors,Tongyi-MAI/Z-Image-Turbo:vae/diffusion_pytorch_model.safetensors" \
  --learning_rate 1e-4 \
  --num_epochs 5 \
  --remove_prefix_in_ckpt "pipe.dit." \
  --output_path "$OUTPUT_PATH" \
  --lora_base_model "dit" \
  --lora_target_modules "to_q,to_k,to_v,to_out.0,w1,w2,w3" \
  --lora_rank 32 \
  --preset_lora_path "$PRESET_LORA_PATH" \
  --preset_lora_model "dit" \
  --min_timestep_boundary 0.0 \
  --max_timestep_boundary 1.0 \
  --use_gradient_checkpointing \
  --dataset_num_workers 8
EOF

cat > "$BIN_ROOT/ztrain-list-loras" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
LORA_ROOT="${ZIMAGE_LORA_ROOT:-$HOME/ZImage-Trainer/loras}"
mkdir -p "$LORA_ROOT"
find "$LORA_ROOT" -type f -name '*.safetensors' -printf '%P\n' | sort
EOF

cat > "$BIN_ROOT/ztrain-run-config" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 1 ]]; then
  echo "Usage: ztrain-run-config /path/to/job.sh" >&2
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
EOF

chmod +x "$CONFIG_ROOT/zimage-turbo-differential-lora.template.sh" "$BIN_ROOT/ztrain-list-loras" "$BIN_ROOT/ztrain-run-config"

echo "Z-Image Trainer installed."
echo "Trainer root: $TRAINER_ROOT"
echo "Datasets: $DATASET_ROOT"
echo "LoRA outputs: $LORA_ROOT"
echo "Trainer model cache: $TRAINING_MODEL_ROOT"
echo "Default method: Z-Image Turbo Differential LoRA with ostris/zimage_turbo_training_adapter."
