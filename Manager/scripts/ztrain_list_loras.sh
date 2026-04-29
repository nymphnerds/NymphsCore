#!/usr/bin/env bash
set -euo pipefail

LORA_ROOT="${ZIMAGE_LORA_ROOT:-$HOME/ZImage-Trainer/loras}"
mkdir -p "$LORA_ROOT"
find "$LORA_ROOT" -type f -name '*.safetensors' -printf '%P\n' | sort
