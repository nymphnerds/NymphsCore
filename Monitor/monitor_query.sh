#!/bin/bash
# Monitor helper script - called from Windows via wsl.exe
# Usage: monitor_query.sh <query> [arg]

set -euo pipefail

LOG_FILE="$HOME/Nymphs-Brain/logs/lms.log"
LMS_START="$HOME/Nymphs-Brain/bin/lms-start"

case "${1:-}" in
    pid)
        # Return llama-server PID, or empty if not running
        ps aux | grep '[l]lama-server' | grep -v grep | awk '{print $2}' | head -1
        ;;

    model)
        # Extract model name/size from lms.log (e.g., "Qwen3.6 27B")
        model=$(grep 'general.name str' "$LOG_FILE" 2>/dev/null | tail -1 | sed 's/.*= //')
        if [ -n "$model" ]; then
            echo "$model"
        else
            echo "—"
        fi
        ;;

    context)
        # Parse CONTEXT_LENGTH from lms-start script (the actual runtime context)
        ctx=$(grep -oP 'CONTEXT_LENGTH=\K[0-9]+' "$LMS_START" 2>/dev/null | head -1)
        if [ -n "$ctx" ]; then
            # Format with commas
            echo "$ctx" | sed ':a;s/\B[0-9]\{3\}\>/,&/;ta'
        else
            echo "—"
        fi
        ;;

    gpu-vram)
        # Get GPU VRAM usage as used/total in GB
        read -r used total < <(nvidia-smi --query-gpu=memory.used,memory.total --format=csv,noheader,nounits 2>/dev/null | head -1 | tr ',' ' ')
        used=$(echo "$used" | tr -d ' ')
        total=$(echo "$total" | tr -d ' ')
        if [ -n "$used" ] && [ "$used" != "N/A" ] && [ -n "$total" ] && [ "$total" != "N/A" ]; then
            awk "BEGIN {printf \"%.0f GB/%.0f GB\", $used/1024, $total/1024}"
        else
            echo "—"
        fi
        ;;

    gpu-temp)
        # Get GPU temperature
        temp=$(nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader 2>/dev/null | head -1 | tr -d ' ')
        if [ -n "$temp" ] && [ "$temp" != "N/A" ]; then
            echo "${temp}C"
        else
            echo "—"
        fi
        ;;

    tps)
        # Parse the "tokens per second" value from the last "eval time" line (not prompt eval) from lms.log
        # Example: eval time =    4888.81 ms /   276 tokens (   17.71 ms per token,    56.46 tokens per second)
        tps=$(grep 'eval time' "$LOG_FILE" 2>/dev/null | grep -v 'prompt eval time' | tail -1 \
            | grep -oP '[\d.]+\s*tokens per second' | grep -oP '^[\d.]+' || true)
        if [ -n "$tps" ]; then
            echo "$tps"
        else
            echo "—"
        fi
        ;;

    *)
        echo "Usage: monitor_query.sh {pid|model|context|gpu-vram|gpu-temp|tps}"
        exit 1
        ;;
esac