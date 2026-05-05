# WORBI Installer

Pre-built package for installing WORBI on a Linux machine.

## Contents

| File | Description |
|------|-------------|
| `install.sh` | Installer script (run this) |
| `worbi-6.2.18.tar.gz` | Compressed WORBI source (no `node_modules/`) |
| `README.md` | This file |

## Requirements

- **Linux** (x86_64 or ARM64)
- **curl** (pre-installed on most distributions)
- **Node.js 18+** - auto-installed to `~/.local/` if missing (no sudo required)

## Installation

```bash
# Copy entire Package folder to target machine, then:
cd /path/to/Package
chmod +x install.sh
./install.sh
```

The installer will:
1. Install Node.js 18.x to `~/.local/` if not found (no sudo)
2. Extract WORBI source to `~/worbi/`
3. Run `npm install` for server and client dependencies
4. Install `worbi-start`, `worbi-stop`, `worbi-status` to `~/.local/bin/`
5. Verify installation

**Note:** If `~/.local/bin` is not in your shell `PATH`, add it:
```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
source ~/.bashrc
```

## Usage

| Command | Description |
|---------|-------------|
| `worbi-start` | Start backend + frontend |
| `worbi-stop` | Stop both servers |
| `worbi-status` | Check server status and health |

- **Frontend:** http://localhost:5173
- **Backend:** http://localhost:8082
- **Logs:** `~/worbi/logs/`

## Caveats & Fixes

### 1. npm `.bin/` wrappers broken on fresh installs (WSL/Node 18)
**Symptom:** `SyntaxError: Cannot use import statement outside a module` when starting frontend.
**Root cause:** `node_modules/.bin/vite` wrapper created incorrectly on some Node 18 + npm combinations in WSL environments.
**Fix:** Bypass the `.bin/` wrapper entirely - call Vite source directly:
```bash
node --experimental-vm-modules "$INSTALL_DIR/node_modules/vite/bin/vite.js"
```

### 2. npm workspace dependency hoisting
**Symptom:** Frontend fails with `Cannot find module '/home/user/worbi/client/node_modules/vite/bin/vite.js'`.
**Root cause:** WORBI uses npm workspaces. Dependencies are hoisted to root `~/worbi/node_modules/`, not to `~/worbi/client/node_modules/`.
**Fix:** Reference vite via `$INSTALL_DIR/node_modules/vite/bin/vite.js` (root path), not `$(pwd)/node_modules/...` (which resolves to client subdirectory after `cd`).

### 3. `npm install` pipefail crash
**Symptom:** Installer exits with error, `node_modules/` never created, verification fails.
**Root cause:** `npm install 2>&1 | tail -3` with `set -euo pipefail` - npm returns non-zero for audit warnings, causing pipefail to abort the entire script.
**Fix:** Run npm in subshell with `|| true`:
```bash
(cd "$INSTALL_DIR/client" && npm install --loglevel=error) || true
```

### 4. Node.js not in PATH after user-space install
**Symptom:** `worbi-start` fails with `command not found: node`.
**Root cause:** Node.js installed to `~/.local/bin/` by install.sh, but `worbi-start` runs in a new shell that doesn't inherit the updated PATH.
**Fix:** Add `export PATH="$HOME/.local/bin:$PATH"` at the top of all bin scripts.

### 5. Verification checking wrong `node_modules/` paths
**Symptom:** Installer shows `[FAIL] Client dependencies` and `[FAIL] Server dependencies` even though npm install succeeded.
**Root cause:** Verification checked `~/worbi/client/node_modules/` and `~/worbi/server/node_modules/` which don't exist with workspace hoisting.
**Fix:** Verify `~/worbi/node_modules/` (root) instead.

## Uninstall

```bash
worbi-stop
rm -rf ~/worbi
rm -f ~/.local/bin/worbi-{start,stop,status}
```

To also remove the user-space Node.js (if it was installed by this installer):
```bash
rm -rf ~/.local
```
