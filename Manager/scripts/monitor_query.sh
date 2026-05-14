#!/usr/bin/env bash
# Monitor helper script - called from Windows via wsl.exe
# Usage: monitor_query.sh <query>

set -euo pipefail

LOG_FILE="$HOME/Nymphs-Brain/logs/lms.log"
LMS_START="$HOME/Nymphs-Brain/bin/lms-start"

case "${1:-}" in
    pid)
        ss -ltnp 2>/dev/null | awk '
            index($4, ":8000") && match($0, /pid=[0-9]+/) {
                print substr($0, RSTART + 4, RLENGTH - 4)
                exit
            }'
        ;;

    model)
        model=$(grep 'general.name str' "$LOG_FILE" 2>/dev/null | tail -1 | sed 's/.*= //' || true)
        if [ -n "$model" ]; then
            echo "$model"
        else
            echo "-"
        fi
        ;;

    local-model)
        model=$(sed -n 's/^MODEL_KEY="\([^"]*\)".*/\1/p' "$LMS_START" 2>/dev/null | head -1 || true)
        if [ -n "$model" ]; then
            echo "$model"
        else
            echo "-"
        fi
        ;;

    remote-model)
        model=$(sed -n 's/^REMOTE_LLM_MODEL=//p' "$HOME/Nymphs-Brain/secrets/llm-wrapper.env" 2>/dev/null | tail -1 || true)
        if [ -n "$model" ]; then
            echo "$model"
        else
            echo "-"
        fi
        ;;

    context)
        ctx=$(grep -oP 'CONTEXT_LENGTH="?\K[0-9]+' "$LMS_START" 2>/dev/null | head -1 || true)
        if [ -n "$ctx" ]; then
            echo "$ctx" | sed ':a;s/\B[0-9]\{3\}\>/,&/;ta'
        else
            echo "-"
        fi
        ;;

    gpu-vram)
        read -r used total < <(nvidia-smi --query-gpu=memory.used,memory.total --format=csv,noheader,nounits 2>/dev/null | head -1 | tr ',' ' ') || true
        used=$(echo "${used:-}" | tr -d ' ')
        total=$(echo "${total:-}" | tr -d ' ')
        if [[ "$used" =~ ^[0-9]+([.][0-9]+)?$ ]] && [[ "$total" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
            awk "BEGIN {printf \"%.0f GB/%.0f GB\\n\", $used/1024, $total/1024}"
        else
            echo "-"
        fi
        ;;

    gpu-temp)
        temp=$(nvidia-smi --query-gpu=temperature.gpu --format=csv,noheader 2>/dev/null | head -1 | tr -d ' ' || true)
        if [[ "$temp" =~ ^[0-9]+([.][0-9]+)?$ ]]; then
            echo "${temp}C"
        else
            echo "-"
        fi
        ;;

    tps)
        tps=$(grep 'eval time' "$LOG_FILE" 2>/dev/null | grep -v 'prompt eval time' | tail -1 \
            | grep -oP '[0-9.]+\s*tokens per second' | grep -oP '^[0-9.]+' || true)
        if [ -n "$tps" ]; then
            echo "$tps"
        else
            echo "Waiting"
        fi
        ;;

    *)
        echo "Usage: monitor_query.sh {pid|model|local-model|remote-model|context|gpu-vram|gpu-temp|tps}"
        exit 1
        ;;
esac
