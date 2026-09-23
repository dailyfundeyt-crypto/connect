#!/usr/bin/env bash
# Build Connect.exe (Windows) that copies the project into Downloads\Connect.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${1:-$HOME/Downloads}"
PACK="$ROOT/packaging"
STAGE="$(mktemp -d)"
ZIP="$PACK/connect-src.zip"

cleanup() { rm -rf "$STAGE"; }
trap cleanup EXIT

mkdir -p "$OUT_DIR" "$PACK"

echo "[Connect] Packe Projekt (ohne node_modules / .env)…"
# Copy tree into stage so zip root = project files (START.sh at top).
mkdir -p "$STAGE/Connect"
if command -v rsync >/dev/null 2>&1; then
  rsync -a \
    --exclude 'node_modules' \
    --exclude '*/node_modules' \
    --exclude '.git' \
    --exclude '.logs' \
    --exclude '.env' \
    --exclude 'desktop/src-tauri/target' \
    --exclude 'packaging/connect-src.zip' \
    --exclude 'packaging/*.exe' \
    --exclude '*.exe' \
    "$ROOT/" "$STAGE/Connect/"
else
  tar -C "$ROOT" \
    --exclude='node_modules' \
    --exclude='.git' \
    --exclude='.logs' \
    --exclude='.env' \
    --exclude='desktop/src-tauri/target' \
    --exclude='packaging/connect-src.zip' \
    --exclude='*.exe' \
    -cf - . | tar -C "$STAGE/Connect" -xf -
fi

# Zip with Connect/ as top folder OR flat? Launcher extracts INTO Downloads\Connect,
# so contents should be flat (START.sh at zip root).
rm -f "$ZIP"
(
  cd "$STAGE/Connect"
  # Prefer zip(1); fall back to python
  if command -v zip >/dev/null 2>&1; then
    zip -qr "$ZIP" .
  else
    python3 - <<PY
import zipfile, os
from pathlib import Path
root = Path(".")
with zipfile.ZipFile("$ZIP", "w", zipfile.ZIP_DEFLATED) as z:
    for p in root.rglob("*"):
        if p.is_file():
            z.write(p, p.as_posix())
print("wrote", "$ZIP")
PY
  fi
)

ls -lh "$ZIP"
export PATH="${BUN_INSTALL:-$HOME/.bun}/bin:$PATH"

echo "[Connect] Kompiliere Windows .exe…"
cd "$PACK"
bun build \
  --compile \
  --target=bun-windows-x64 \
  --asset=./connect-src.zip \
  --outfile="$OUT_DIR/Connect.exe" \
  ./connect-launcher.ts

ls -lh "$OUT_DIR/Connect.exe"
file "$OUT_DIR/Connect.exe"
echo "[Connect] Fertig: $OUT_DIR/Connect.exe"
echo "  Auf Windows: Doppelklick → kopiert nach Downloads\\Connect → startet → Enter stoppt."
