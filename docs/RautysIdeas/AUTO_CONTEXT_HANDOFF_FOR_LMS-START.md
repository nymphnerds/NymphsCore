# Auto Context Handoff — lms-start & Monitor Interaction

> **Location:** `~/Nymphs-Brain/bin/` (inside NymphsCore WSL distro)  
> **Author:** Rauty  
> **Purpose:** Document how `lms-start` launches llama-server with automatic VRAM-based context calculation, how `lms-model` configures it, and how the Windows Monitor observes the running server.

---

## 1. Overview

The **`lms-start`** script is the heart of the Nymphs-Brain server lifecycle. It is responsible for:

1. Locating the selected GGUF model file
2. Calculating the optimal context length based on available GPU VRAM (or using a user-specified value)
3. Launching `llama-server` with the correct arguments (CUDA, flash attention, KV cache settings)
4. Writing a PID file and log file for lifecycle management
5. Performing a health check to confirm the server is ready on port 8000

The **Windows Monitor** (`NymphsCore/Monitor/`) is an observational companion that reads the same data sources `lms-start` produces (log file, process table, GPU queries) to display real-time metrics. The two projects **do not communicate directly** — they share data sources.

---

## 2. The lms-start Script — Complete Flow

### 2.1 Entry & Environment Setup

```bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INSTALL_ROOT="$(dirname "${SCRIPT_DIR}")"
```

- Resolves `INSTALL_ROOT` to `~/Nymphs-Brain/`
- Prepends Nymphs-Brain binary directories to `PATH`
- Scans for LM Studio CLI binaries (`~/.lmstudio/bin`, `~/.cache/lm-studio/bin`, `~/.local/bin`)

### 2.2 Model Configuration (Updated by lms-model)

Two variables at the top of `lms-start` are the configuration bridge from `lms-model`:

```bash
MODEL_KEY="qwen3.6-27b"      # Set by lms-model when user selects a model
CONTEXT_LENGTH=0              # 0 = auto-calculate from VRAM, or a fixed value
```

### 2.3 Stop Existing Instance

Before starting a new server, `lms-start` checks for a running instance:

```bash
PID_FILE="${INSTALL_ROOT}/logs/lms.pid"
if [[ -f "${PID_FILE}" ]]; then
  old_pid="$(cat "${PID_FILE}")"
  if kill -0 "$old_pid" >/dev/null 2>&1; then
    kill "$old_pid"
    sleep 2
  fi
fi
```

### 2.4 Locate GGUF Model File

The `find_gguf_path()` function searches multiple directories in priority order:

| Priority | Path | Description |
|---|---|---|
| 1 | `~/Nymphs-Brain/models/` | Centralized models directory |
| 2 | `~/Nymphs-Brain/lmstudio/models/` | LM Studio default location |
| 3 | `~/.cache/lm-studio/models/` | Legacy location |
| 4 | `~/.lmstudio/models/` | Another legacy location |

Uses `find -name "*.gguf"` + `grep -i "$model_name"` to match the model key against file paths.

### 2.5 Detect Multimodal Projector

Scans the model's directory for `.mmproj` files (vision-language models). If found, appends `--mmproj` flag to the llama-server arguments.

### 2.6 Auto Context Calculation (CONTEXT_LENGTH=0)

**This is the core intelligence of the system.** When `CONTEXT_LENGTH=0`, the script dynamically calculates the optimal context window based on available VRAM.

#### Step-by-Step Calculation Flow

```
┌──────────────────────────────────────────────────────────────────┐
│              calculate_context_from_vram()                       │
│                                                                  │
│  1. Query nvidia-smi → total VRAM, used VRAM                    │
│     └─ Fallback: if nvidia-smi unavailable → use native context │
│                                                                  │
│  2. Calculate free VRAM budget                                   │
│     free_mb = total_mb - used_mb                                │
│     budget = free_mb × 0.88  (88% of free VRAM)                 │
│     └─ Fallback: if no free VRAM → minimum context (2048)       │
│                                                                  │
│  3. Parse GGUF metadata (Python + gguf library)                 │
│     ├─ Extract: n_layers, n_heads, n_kv_heads, n_embd           │
│     ├─ Extract: key_len, val_len (for MLA models)               │
│     ├─ Get file size (model weight VRAM estimate)               │
│     └─ Determine KV cache formula:                              │
│        ├─ MLA:    layers × (key_len + val_len) per token        │
│        └─ Trad:   2 × layers × kv_heads × head_dim per token    │
│        └─ Fallback: if unknown → use native context             │
│                                                                  │
│  4. Estimate model VRAM footprint                                │
│     model_vram = file_size × 1.05  (5% overhead)                │
│                                                                  │
│  5. Calculate KV cache budget                                    │
│     kv_budget = budget_bytes - model_vram_bytes                 │
│     (minimum 1 GB reserved for KV cache)                        │
│                                                                  │
│  6. Calculate max tokens                                         │
│     max_tokens = (kv_budget / bytes_per_token) × 2              │
│     (2x multiplier: quantized weights use less VRAM than        │
│      file size suggests)                                         │
│                                                                  │
│  7. Round & Clamp                                                │
│     ├─ Round DOWN to nearest 8192 multiple                      │
│     ├─ Clamp to native context (model's trained max)            │
│     └─ Minimum: 2048 tokens                                     │
│                                                                  │
│  Output: Final context length (e.g., 32768 = 32k)               │
└──────────────────────────────────────────────────────────────────┘
```

#### KV Cache Formulas

**Traditional (most models):**
```
KV elements per token = 2 × n_layers × n_kv_heads × (n_embd / n_heads)
bytes_per_token = KV_elements × 1 (q8_0) × 2 (parallel sequences)
```

**MLA (Qwen3+, DeepSeek — latent attention):**
```
KV elements per token = n_layers × (key_len + val_len)
bytes_per_token = KV_elements × 1 (q8_0) × 2 (parallel sequences)
```

#### Example Calculation Output

```
=== VRAM-Based Context Calculation ===
GPU: 49152 MB total, 512 MB used
Free: 48640 MB → Target (88%): 42803 MB available
Model: 18.5 GB weights (est. GPU footprint)
KV cache budget: 24.3 GB for context
Formula: MLA (key=128, val=128, layers=48)
Bytes per token (q8_0, parallel=2): 24576
Raw max context: 204800 tokens
Final context: 196608 (192k)
```

### 2.7 Launch llama-server

```bash
LLAMA_ARGS=(
    -m "${GGUF_PATH}"         # Model file
    -c "${CONTEXT_LENGTH}"    # Context length (auto or manual)
    -ngl 9999                 # Offload all layers to GPU
    --port 8000
    --host "127.0.0.1"
    --flash-attn on           # Flash attention optimization
    --parallel 2              # 2 parallel sequences
    --rope-scaling linear     # RoPE scaling for extended context
    -ctk q8_0                 # KV cache type: key = q8_0
    -ctv q8_0                 # KV cache type: value = q8_0
)
# Append --mmproj if multimodal projector detected

nohup "${LLAMA_SERVER}" "${LLAMA_ARGS[@]}" > "${LOG_FILE}" 2>&1 &
echo "$!" > "${PID_FILE}"
```

### 2.8 Health Check

```bash
for i in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:8000/v1/models" >/dev/null 2>&1; then
    echo "llama-server is ready on port 8000"
    exit 0
  fi
  sleep 1
done
echo "ERROR: llama-server did not become ready in time." >&2
exit 1
```

Waits up to 60 seconds for the OpenAI-compatible API endpoint to respond.

---

## 3. The lms-model Script — Model Manager

### 3.1 Purpose

`lms-model` is an interactive CLI tool for managing models via the LM Studio CLI (`lms`). It provides:

- **Use Downloaded Model** — Select from already-downloaded models
- **Download New Model** — Search and download from LM Studio's model registry
- **Remove Models** — Delete downloaded models
- **Context Size Selection** — Choose Auto (VRAM-based) or one of 11 preset sizes

### 3.2 Context Size Presets

| Option | Size | Label |
|---|---|---|
| 1 | `0` | Auto (VRAM-based calculation) |
| 2 | 2,048 | 2k |
| 3 | 4,096 | 4k |
| 4 | 8,192 | 8k |
| 5 | 16,384 | 16k |
| 6 | 32,768 | 32k |
| 7 | 49,152 | 48k |
| 8 | 65,536 | 64k |
| 9 | 98,304 | 96k |
| 10 | 131,072 | 128k |
| 11 | 262,144 | 256k |
| 12 | Custom | User input (min 512) |

### 3.3 Configuration Updates

When a model is selected, `lms-model` updates TWO files:

**1. `lms-start` (server launcher):**
```bash
awk -v model="${model_key}" -v context="${context_size}" '
  /^MODEL_KEY=/    { print "MODEL_KEY=\"" model "\""; next }
  /^CONTEXT_LENGTH=/ { print "CONTEXT_LENGTH=" context; next }
  { print }
' lms-start > lms-start.tmp
```

**2. `nymph-agent.py` (agent/chat integration):**
```bash
awk -v model_literal="${json_model_key}" '
  /^MODEL = / { print "MODEL = " model_literal; next }
  { print }
' nymph-agent.py > nymph-agent.py.tmp
```

### 3.4 LM Studio Daemon Lifecycle

`lms-model` briefly starts the LM Studio daemon to list/download models, then stops it:

```bash
stop_lmstudio_daemon() {
  lms server stop 2>/dev/null || true
  lms daemon down 2>/dev/null || true
  pkill -f "lms.*server" 2>/dev/null || true
  pkill -f "lms.*daemon" 2>/dev/null || true
}
```

This ensures the LM Studio daemon does not conflict with the llama-server launched by `lms-start`.

---

## 4. How lms-start and the Monitor Interact

### 4.1 Relationship Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    CONFIGURATION CHAIN                          │
│                                                                 │
│  User runs lms-model  ──▶  Updates lms-start config             │
│   (selects model,           MODEL_KEY="qwen3.6-27b"             │
│    context size)          CONTEXT_LENGTH=0                      │
│                                                                 │
│                       ▼                                         │
│  User runs lms-start  ──▶  Reads config, calculates context     │
│   (launches server)       Launches llama-server -c 196608 ...   │
│                                                                 │
│                       ▼                                         │
│              llama-server running                                │
│              ├─ Writes to ~/Nymphs-Brain/logs/lms.log            │
│              ├─ Listening on 127.0.0.1:8000                      │
│              └─ Process visible in `ps aux`                      │
│                                                                 │
│  ┌──────────────────────────────────────────────────────┐       │
│  │              Windows Monitor (observational)           │       │
│  │                                                       │       │
│  │  monitor_query.sh reads:                              │       │
│  │  ├─ lms.log → model name, TPS                         │       │
│  │  ├─ ps aux → PID detection, context (-c flag)          │       │
│  │  └─ nvidia-smi → GPU VRAM, GPU temp                   │       │
│  │                                                       │       │
│  │  Displays metrics in WinForms UI                       │       │
│  └──────────────────────────────────────────────────────┘       │
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 Shared Data Sources

| Data Source | Written By | Read By Monitor |
|---|---|---|
| `~/Nymphs-Brain/logs/lms.log` | `lms-start` (llama-server stdout/stderr) | `monitor_query.sh` — model name, TPS |
| Process table (`ps aux`) | `lms-start` (launches llama-server) | `monitor_query.sh` — PID, context length |
| GPU (`nvidia-smi`) | `lms-start` (context calculation) + llama-server (inference) | `monitor_query.sh` — VRAM, temperature |
| `~/Nymphs-Brain/logs/lms.pid` | `lms-start` (writes PID) | Not used by Monitor (Monitor uses `ps aux` instead) |

### 4.3 No Direct Communication

**Critical design point:** The Monitor and lms-start do NOT talk to each other. There is:
- No socket connection
- No shared memory
- No API calls from Monitor to llama-server
- No file locking or signaling

The Monitor is **purely observational**. It reads the same data sources that lms-start produces, making it a passive dashboard that has zero impact on server performance.

### 4.4 Monitor Queries Mapped to lms-start Outputs

| Monitor Query | How lms-start Produces This Data |
|---|---|
| `pid` | `lms-start` launches `llama-server` in background; Monitor finds it via `ps aux \| grep llama-server` |
| `model` | llama-server writes `general.name str = ...` to `lms.log`; Monitor greps this line |
| `context` | `lms-start` passes `-c 196608` as argument; Monitor extracts the `-c` value from process arguments via `grep -oP -- '-c\s+\K[0-9]+'` |
| `gpu-vram` | Both independently query `nvidia-smi` (lms-start for calculation, Monitor for display) |
| `gpu-temp` | Monitor queries `nvidia-smi` independently |
| `tps` | llama-server writes `eval time = ... X tokens per second` to `lms.log`; Monitor greps this line |

### 4.5 Timing Relationship

```
Time →
│
│  lms-model (user configures model, one-time)
│  └── Updates MODEL_KEY + CONTEXT_LENGTH in lms-start
│
│  lms-start (user launches server)
│  ├── Reads config from lms-start
│  ├── Auto-calculates context (if CONTEXT_LENGTH=0)
│  ├── Launches llama-server
│  ├── Writes PID to lms.pid
│  ├── llama-server writes to lms.log
│  └── Health check confirms port 8000 ready
│
│  Monitor (running continuously on Windows)
│  ├── Detects llama-server process (pid query)
│  ├── Reads lms.log for model name
│  ├── Extracts -c from process args for context
│  ├── Queries nvidia-smi for GPU metrics
│  ├── Parses lms.log for TPS
│  └── Refreshes every 5 seconds
│
│  (User runs lms-model again to switch models)
│  └── Updates lms-start config
│  └── User runs lms-start again → restarts with new model
│  └── Monitor automatically picks up new metrics
```

---

## 5. Key File Paths Reference

| Path | Purpose | Created By |
|---|---|---|
| `~/Nymphs-Brain/bin/lms-start` | Server launcher script | Installation |
| `~/Nymphs-Brain/bin/lms-model` | Model manager script | Installation |
| `~/Nymphs-Brain/local-tools/bin/llama-server` | llama.cpp server binary | Installation |
| `~/Nymphs-Brain/logs/lms.log` | Server log file | `lms-start` at runtime |
| `~/Nymphs-Brain/logs/lms.pid` | Server PID file | `lms-start` at runtime |
| `~/Nymphs-Brain/models/` | Centralized model storage | `lms-model` downloads |
| `~/Nymphs-Brain/nymph-agent.py` | Agent/chat integration | Installation (updated by `lms-model`) |
| `~/Nymphs-Brain/scripts/monitor_query.sh` | Monitor helper script | Monitor auto-deployment |

---

## 6. VRAM-Based Auto Context — Deep Dive

### 6.1 Why Auto Context?

Fixed context lengths are inefficient: they either waste VRAM (context larger than needed) or cause OOM errors (context larger than available VRAM). The auto-context system dynamically sizes the context window to fit the available GPU memory.

### 6.2 The 88% Budget Rule

The system uses 88% of free VRAM as the budget (not 100%), reserving 12% as a safety margin for:
- OS/driver overhead
- CUDA allocator fragmentation
- Other GPU workloads
- KV cache growth during generation (not just prompting)

### 6.3 The 2x Multiplier

```bash
max_tokens = (kv_budget / bytes_per_token) × 2
```

The 2x multiplier accounts for the fact that quantized model weights (e.g., Q4_K_M) use significantly less VRAM than the raw file size suggests. The file size on disk is the quantized size, but when loaded to GPU, the weights are dequantized on-the-fly per-layer, not all at once. This means the actual GPU footprint of the model weights is often less than the file size, leaving more room for the KV cache.

### 6.4 Rounding to 8192

Context lengths are rounded DOWN to the nearest 8192-token multiple. This ensures:
- Clean alignment with common RoPE scaling boundaries
- Predictable performance characteristics
- Compatibility with model training context windows

### 6.5 Clamping to Native Context

The calculated context is never allowed to exceed the model's native (trained) context length. A model trained on 32k context will produce poor quality output beyond that range, regardless of available VRAM.

### 6.6 Edge Cases

| Scenario | Behavior |
|---|---|
| nvidia-smi unavailable | Falls back to native context length |
| nvidia-smi query fails | Falls back to native context length |
| No free VRAM | Minimum context (2048) |
| GGUF metadata parse fails | Falls back to native context length |
| KV formula unknown | Falls back to native context length |
| Calculated < 2048 | Clamped to 2048 |
| Calculated > native | Clamped to native |

---

## 7. Troubleshooting

### 7.1 Monitor Shows Wrong Context

**Problem:** Monitor displays a different context value than expected.

**Cause:** `lms-model` was run to change the model but `lms-start` was not re-run.

**Fix:** Always re-run `lms-start` after changing models with `lms-model` to restart the server with the new configuration.

### 7.2 Auto Context Falls Back to Native

**Problem:** Context is set to the model's native value instead of a VRAM-optimized value.

**Check:**
1. Verify `CONTEXT_LENGTH=0` in `lms-start`
2. Verify `nvidia-smi` works in the WSL distro
3. Check `lms.log` for "WARNING" messages during context calculation
4. Verify the `gguf` Python package is available in the Nymphs-Brain venv

### 7.3 Monitor Shows "—" for All Metrics

**Problem:** Server is not running.

**Fix:** Run `~/Nymphs-Brain/bin/lms-start` and check the output for errors.

### 7.4 Server Fails Health Check

**Problem:** `lms-start` reports "llama-server did not become ready in time."

**Causes:**
- Model file not found (check `find_gguf_path` output)
- VRAM exhausted (check `lms.log` for OOM errors)
- Port 8000 already in use (kill existing process)
- llama-server binary missing or incompatible

**Fix:** Check `~/Nymphs-Brain/logs/lms.log` for error details.

### 7.5 Monitor Shows GPU VRAM but Server is CPU-Only

**Problem:** nvidia-smi reports VRAM from other applications, not the server.

**Cause:** Server is running without GPU offload (CUDA not available).

**Fix:** Verify CUDA is installed in the WSL distro and `-ngl 9999` is in the llama-server arguments.

---

## 8. Summary

The `lms-start` / `lms-model` / Monitor ecosystem follows a clean separation of concerns:

| Component | Responsibility |
|---|---|
| `lms-model` | Model selection and configuration (user-facing, interactive) |
| `lms-start` | Server lifecycle management with intelligent auto-context (CLI launcher) |
| llama-server | The actual inference engine (runs in background) |
| Monitor | Passive observation and display of runtime metrics (Windows app) |

The auto-context system in `lms-start` eliminates the need for users to manually tune context lengths, while the Monitor provides real-time visibility into server health without any performance overhead.