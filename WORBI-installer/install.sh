#!/usr/bin/env bash
set -euo pipefail

echo "============================================="
echo "  WORBI Installer"
echo "============================================="
echo ""

# Check if Node.js is installed
if command -v node &>/dev/null; then
  echo "Node.js found: $(node --version)"
else
  echo "Node.js not found. Installing to ~/.local (no sudo)..."
  ARCH="$(uname -m)"
  case "$ARCH" in
    x86_64) NODE_ARCH="x64" ;;
    aarch64|arm64) NODE_ARCH="arm64" ;;
    *) echo "ERROR: Unsupported architecture: $ARCH" >&2; exit 1 ;;
  esac
  NODE_VERSION="18.20.8"
  NODE_TAR="node-v${NODE_VERSION}-linux-${NODE_ARCH}.tar.xz"
  curl -fsSL "https://nodejs.org/dist/v${NODE_VERSION}/${NODE_TAR}" -o "/tmp/${NODE_TAR}"
  mkdir -p "$HOME/.local"
  tar -xJf "/tmp/${NODE_TAR}" -C "$HOME/.local" --strip-components=1
  rm -f "/tmp/${NODE_TAR}"
  export PATH="$HOME/.local/bin:$PATH"
  echo "Node.js installed: $(node --version)"
fi

# Set install directory
INSTALL_DIR="$HOME/worbi"

# Locate archive in same directory as this script
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ARCHIVE_PATH=""
if ls "$SCRIPT_DIR"/worbi-*.tar.gz 1>/dev/null 2>&1; then
  ARCHIVE_PATH="$(ls "$SCRIPT_DIR"/worbi-*.tar.gz | head -1)"
else
  echo "ERROR: Cannot find WORBI package archive (worbi-*.tar.gz)" >&2
  exit 1
fi

echo "Archive: $ARCHIVE_PATH"
echo "Install: $INSTALL_DIR"
echo ""

# Extract to temp directory
echo "Extracting package..."
TEMP_DIR="$(mktemp -d)"
tar -xzf "$ARCHIVE_PATH" -C "$TEMP_DIR"

# Stop existing instances
if [[ -f "$INSTALL_DIR/logs/worbi-client.pid" ]] || [[ -f "$INSTALL_DIR/logs/worbi-server.pid" ]]; then
  echo "Stopping existing WORBI..."
  if command -v worbi-stop &>/dev/null; then
    worbi-stop 2>/dev/null || true
  fi
fi

# Preserve user data if upgrading
if [[ -d "$INSTALL_DIR" ]]; then
  echo "Existing installation found. Preserving user data..."
  cp -r "$INSTALL_DIR" "${INSTALL_DIR}.backup.$(date +%Y%m%d%H%M%S)" 2>/dev/null || true
fi

# Prepare install directory
mkdir -p "$INSTALL_DIR"
rm -rf "$INSTALL_DIR/client" "$INSTALL_DIR/server" "$INSTALL_DIR/bin"

# Copy new files
cp -r "$TEMP_DIR/worbi/client" "$INSTALL_DIR/"
cp -r "$TEMP_DIR/worbi/server" "$INSTALL_DIR/"
cp -r "$TEMP_DIR/worbi/bin" "$INSTALL_DIR/"
cp -f "$TEMP_DIR/worbi/package.json" "$INSTALL_DIR/" 2>/dev/null || true

# Restore user data if upgrading
if [[ -d "${INSTALL_DIR}.backup."* ]]; then
  OLDEST_BACKUP="$(ls -d "${INSTALL_DIR}.backup."* | head -1)"
  [[ -f "$OLDEST_BACKUP/server/src/data/users.json" ]] && cp "$OLDEST_BACKUP/server/src/data/users.json" "$INSTALL_DIR/server/src/data/" 2>/dev/null || true
  [[ -d "$OLDEST_BACKUP/server/src/data/user-settings" ]] && cp -r "$OLDEST_BACKUP/server/src/data/user-settings" "$INSTALL_DIR/server/src/data/" 2>/dev/null || true
  [[ -d "$OLDEST_BACKUP/server/src/data/users" ]] && cp -r "$OLDEST_BACKUP/server/src/data/users" "$INSTALL_DIR/server/src/data/" 2>/dev/null || true
fi

# Create required directories
mkdir -p "$INSTALL_DIR/logs"
mkdir -p "$INSTALL_DIR/server/src/data"
mkdir -p "$INSTALL_DIR/server/src/data/user-settings"
mkdir -p "$INSTALL_DIR/server/src/data/users"

# Install npm dependencies
echo ""
echo "Installing server dependencies..."
(cd "$INSTALL_DIR/server" && npm install --loglevel=error) || true

echo "Installing client dependencies..."
(cd "$INSTALL_DIR/client" && npm install --loglevel=error) || true

# Install bin scripts to ~/.local/bin
mkdir -p "$HOME/.local/bin"
cp "$INSTALL_DIR/bin/worbi-start" "$HOME/.local/bin/"
cp "$INSTALL_DIR/bin/worbi-stop" "$HOME/.local/bin/"
cp "$INSTALL_DIR/bin/worbi-status" "$HOME/.local/bin/"
chmod +x "$HOME/.local/bin/worbi-start" "$HOME/.local/bin/worbi-stop" "$HOME/.local/bin/worbi-status"

# Cleanup
rm -rf "$TEMP_DIR"

echo ""
echo "============================================="
echo "  Verification"
echo "============================================="

ERRORS=0

if [[ -d "$INSTALL_DIR/client/src" ]]; then
  echo "[OK] Client source"
else
  echo "[FAIL] Client source"
  ERRORS=$((ERRORS + 1))
fi

if [[ -d "$INSTALL_DIR/server/src" ]]; then
  echo "[OK] Server source"
else
  echo "[FAIL] Server source"
  ERRORS=$((ERRORS + 1))
fi

# npm workspaces hoist dependencies to root node_modules
if [[ -d "$INSTALL_DIR/node_modules" ]]; then
  TOTAL_PKGS=$(ls "$INSTALL_DIR/node_modules" 2>/dev/null | grep -v "^\." | wc -l)
  echo "[OK] Dependencies: $TOTAL_PKGS packages (hoisted to root)"
else
  echo "[FAIL] Dependencies - node_modules missing"
  ERRORS=$((ERRORS + 1))
fi

for cmd in worbi-start worbi-stop worbi-status; do
  if [[ -f "$HOME/.local/bin/$cmd" ]]; then
    echo "[OK] Script '$cmd' installed"
  else
    echo "[FAIL] Script '$cmd' missing"
    ERRORS=$((ERRORS + 1))
  fi
done

if [[ $ERRORS -gt 0 ]]; then
  echo ""
  echo "ERROR: $ERRORS verification(s) failed!" >&2
  exit 1
fi

echo ""
echo "============================================="
echo "  WORBI installed successfully!"
echo "============================================="
echo ""
echo "  Start:  worbi-start"
echo "  Stop:   worbi-stop"
echo "  Status: worbi-status"
echo ""
echo "  Frontend: http://localhost:5173"
echo "  Backend:  http://localhost:8082"
echo ""
echo "  Logs: $INSTALL_DIR/logs/"
echo ""