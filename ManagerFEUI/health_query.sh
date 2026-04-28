#!/bin/bash
# Health check script for llama-server
# Output: TSV format
# Fields: state | pid | queue | tps | etime

# Find llama-server PID with fallback
PID=$(pgrep -f llama-server | head -1)
if [ -z "$PID" ]; then
  PID=$(ss -tlnp 'sport = :8080' 2>/dev/null | grep -oP 'pid=\K\d+' | head -1)
fi
if [ -z "$PID" ]; then
  PID=$(lsof -ti:8080 2>/dev/null | head -1)
fi

if [ -n "$PID" ]; then
  echo -n "RUNNING"
  printf '\t'
  echo -n "$PID"
  printf '\t'

  # Queue depth
  QUEUE=$(curl -s --max-time 3 http://localhost:8080/v1/queue.info 2>/dev/null | jq -r '.size // 0' 2>/dev/null)
  echo -n "${QUEUE:-0}"
  printf '\t'

  # Decode TPS
  D_TPS=$(curl -s --max-time 3 http://localhost:8080/metrics 2>/dev/null | grep avg_decode_tps | tail -1 | awk '{print $2}')
  echo -n "${D_TPS:-0}"
  printf '\t'

  # Process elapsed time (uptime)
  ETIME=$(ps -p "$PID" -o etime= 2>/dev/null | head -1 | tr -d ' ')
  echo -n "${ETIME:-0:00}"
else
  echo -n "STOPPED"
fi

echo ""