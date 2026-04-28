# Image Mode Handoff: `--mmproj` Support in `lms-start`

## Status: Needs Fix

The `lms-start` script in `Nymphs-Brain/bin/lms-start` currently always passes `--mmproj` to `llama-server`, even when the model is text-only and has no multimodal projector file. This causes the server to hang or crash on startup.

---

## The Problem

**Current code (lines 75–101 of `lms-start`):**

```bash
GGUF_DIR="$(dirname "${GGUF_PATH}")"
MULTI_PROJ_PATH=$(find "${GGUF_DIR}" -maxdepth 1 -name "mmproj-*.gguf" -type f | head -n1)

# ... later ...

LLAMA_ARGS=(
    --mmproj "${MULTI_PROJ_PATH}"   # <-- Always added, even if MULTI_PROJ_PATH is empty
    -m "${GGUF_PATH}"
    # ...
)
```

When no `mmproj-*.gguf` file exists in the model directory (i.e., the model is text-only), `MULTI_PROJ_PATH` is an empty string. Passing `--mmproj ""` to `llama-server` causes it to fail silently or hang during startup.

---

## Background: What Is `--mmproj`?

The `--mmproj` flag tells `llama-server` (llama.cpp) to load a **multimodal projector** file. This is required for models that support vision or audio inputs, such as:

- **Qwen2.5-VL** series (vision + language)
- **LLaVA** series (vision + language)
- **Gemma-3** multimodal variants
- **BakLLaVA**, **MiniCPM-V**, etc.

These models ship with two files:
1. The main model GGUF (e.g., `qwen2.5-vl-7b.Q4_K_M.gguf`)
2. The multimodal projector GGUF (e.g., `mmproj-qwen2.5-vl-7b-Q4_K_M.gguf`)

**Text-only models** (Llama-3, Mistral, Phi, Qwen text-only, etc.) do NOT have an mmproj file and must NOT have the `--mmproj` flag passed.

Reference: https://github.com/ggml-org/llama.cpp/blob/master/docs/multimodal.md

---

## The Fix

Replace the unconditional `--mmproj` with a conditional check. The corrected section should look like this:

```bash
# Multimodal projector path
GGUF_DIR="$(dirname "${GGUF_PATH}")"
MULTI_PROJ_PATH=$(find "${GGUF_DIR}" -maxdepth 1 -name "mmproj-*.gguf" -type f 2>/dev/null | head -n1)

echo "Starting llama-server..."
echo "  Model: ${MODEL_KEY}"
echo "  GGUF Path: ${GGUF_PATH}"
echo "  Context Length: ${CONTEXT_LENGTH}"

# Ensure shared libraries can be found (fallback if RPATH was not fixed by patchelf)
export LD_LIBRARY_PATH="$(dirname "${LLAMA_SERVER}"):${LD_LIBRARY_PATH:-}"

# Build the command arguments (without --mmproj by default)
LLAMA_ARGS=(
    -m "${GGUF_PATH}"
    -c "${CONTEXT_LENGTH}"
    -ngl 9999
    --port 8000
    --host "127.0.0.1"
    --flash-attn on
    --parallel 4
    -ctk q8_0
    -ctv q8_0
)

# Only add --mmproj if a valid projector file exists
if [[ -n "${MULTI_PROJ_PATH}" && -f "${MULTI_PROJ_PATH}" ]]; then
  echo "  Multi Projector: ${MULTI_PROJ_PATH}"
  LLAMA_ARGS+=(--mmproj "${MULTI_PROJ_PATH}")
else
  echo "  Multi Projector: (none - text-only model)"
fi
echo ""
```

### Key Changes

| Before | After |
|--------|-------|
| `--mmproj` always in `LLAMA_ARGS` | `--mmproj` appended only when mmproj file exists |
| No feedback about image mode support | Logs whether mmproj was found or not |
| `find` doesn't suppress errors | Added `2>/dev/null` to suppress find errors on missing dirs |

### What the `if` check does

```bash
if [[ -n "${MULTI_PROJ_PATH}" && -f "${MULTI_PROJ_PATH}" ]]; then
```

- **`-n "${MULTI_PROJ_PATH}"`** — ensures the variable is not empty (i.e., `find` returned at least one result).
- **`-f "${MULTI_PROJ_PATH}"`** — ensures the path points to an actual regular file (not a directory or broken path).
- Only if **both** conditions are true is `--mmproj` appended to the args array.

---

## File to Modify

**Path:** `Nymphs-Brain/bin/lms-start`

**Lines affected:** 75–101 (the mmproj discovery, echo block, and `LLAMA_ARGS` array).

---

## Testing Checklist

- [ ] Start server with a **multimodal model** (has mmproj file) — verify `--mmproj` appears in the log and server starts correctly.
- [ ] Start server with a **text-only model** (no mmproj file) — verify it starts without the `--mmproj` flag and health check passes.
- [ ] Check the log output shows `(none - text-only model)` when no mmproj is present.
- [ ] Check the log output shows the full mmproj path when one is present.

---

## References

- llama.cpp multimodal docs: https://github.com/ggml-org/llama.cpp/blob/master/docs/multimodal.md
- llama.cpp CLI help: run `llama-server --help` and search for `--mmproj`