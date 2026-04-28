#!/bin/bash
# llama-server monitoring script
# Output: TSV format
# Fields: reachable | pid | mem_used_kb | mem_total_kb | vram_used_mb | vram_total_mb | queue | prompt_tps | decode_tps

# Check if server is reachable on port 8080
if nc -z localhost 8080 2>/dev/null; then
  REACHABLE=1
else
  REACHABLE=0
fi
echo -n "$REACHABLE"
printf '\t'

# Find llama-server PID with fallback to port-based discovery
PID=$(pgrep -f llama-server | head -1)
if [ -z "$PID" ]; then
  PID=$(ss -tlnp 'sport = :8080' 2>/dev/null | grep -oP 'pid=\K\d+' | head -1)
fi
if [ -z "$PID" ]; then
  PID=$(lsof -ti:8080 2>/dev/null | head -1)
fi
echo -n "${PID:-}"
printf '\t'

# WSL-wide RAM: used memory in KB (MemTotal - MemAvailable)
MEM_USED_KB=$(awk '/^MemTotal:/{total=$2} /^MemAvailable:/{avail=$2} END {print total-avail}' /proc/meminfo 2>/dev/null)
echo -n "${MEM_USED_KB:-0}"
printf '\t'

# Total system RAM in KB
TOTAL_MEM_KB=$(awk '/^MemTotal:/{print $2}' /proc/meminfo 2>/dev/null)
echo -n "${TOTAL_MEM_KB:-0}"
printf '\t'

# VRAM used (MB) - handle multi-GPU by summing
VRAM_USED=$(nvidia-smi --query-gpu=memory.used --format=csv,noheader,nounits 2>/dev/null | awk '{s+=$1} END {print s+0}')
echo -n "${VRAM_USED:-0}"
printf '\t'

# VRAM total (MB) - handle multi-GPU by summing
VRAM_TOTAL=$(nvidia-smi --query-gpu=memory.total --format=csv,noheader,nounits 2>/dev/null | awk '{s+=$1} END {print s+0}')
echo -n "${VRAM_TOTAL:-0}"
printf '\t'

# Queue depth from API
QUEUE=$(curl -s --max-time 3 http://localhost:8080/v1/queue.info 2>/dev/null | jq -r '.size // 0' 2>/dev/null)
echo -n "${QUEUE:-0}"
printf '\t'

# Token gen: prompt TPS
P_TPS=$(curl -s --max-time 3 http://localhost:8080/metrics 2>/dev/null | grep avg_prompt_tps | tail -1 | awk '{print $2}')
echo -n "${P_TPS:-0}"
printf '\t'

# Token gen: decode TPS
D_TPS=$(curl -s --max-time 3 http://localhost:8080/metrics 2>/dev/null | grep avg_decode_tps | tail -1 | awk '{print $2}')
echo -n "${D_TPS:-0}"

echo ""