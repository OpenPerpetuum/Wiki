#!/usr/bin/env bash
# Fetch the game layer data the zone maps need into <dest>/custom-layers
# (dest defaults to .assets):
#
#   1. the original zones' .bin layers from the Perpetuum Dedicated Server
#      installer (Steam app 693060, anonymous login — the same flow the
#      PerpetuumServer2 CI uses in its setup-test-data action)
#   2. the latest custom/gamma zone .bin layers from the public Google
#      Drive archive the PerpetuumServer2 CI uses (GAMMA_LAYERS_NEW)
#
# Zones whose layers only ship in the game CLIENT (Perpetuum.gbf — not
# anonymously downloadable) are NOT fetchable here; the zone map tool
# keeps the committed derived PNGs from static/zonemaps-fallback/ for
# those instead.
#
# Usage: bash tools/fetch_zone_layers.sh [dest]
# Each source is fetched independently and skipped when its marker file
# is already present (CI cache hit or a partial local run).
set -euo pipefail

DEST="${1:-.assets}"
CL="$DEST/custom-layers"
DRIVE_URL="https://drive.google.com/uc?id=1Xp0T1K57Pv-vjgmpXMG8Iea_ec0bWYR4" # gamma_layers.rar
mkdir -p "$CL"
command -v 7z >/dev/null 2>&1 || { echo "error: 7z is required (e.g. 'sudo apt-get install p7zip-full')"; exit 1; }

# --- 1. Dedicated Server installer (Steam) -> original zone .bin layers ---
if [ ! -f "$CL/altitude.0000.bin" ]; then
    echo "downloading the Dedicated Server installer (Steam app 693060)..."
    SC="$(mktemp -d)"
    curl -sqL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" | tar xzC "$SC"
    "$SC/steamcmd.sh" +@sSteamCmdForcePlatformType windows +login anonymous +app_info_update 1 +quit || true
    ok=0
    for attempt in 1 2 3; do
        echo "SteamCMD download attempt $attempt..."
        if "$SC/steamcmd.sh" +@sSteamCmdForcePlatformType windows +force_install_dir "$SC/server" +login anonymous +app_update 693060 +quit; then
            ok=1; break
        fi
        sleep 5
    done
    [ "$ok" = 1 ] || { echo "error: SteamCMD download failed"; rm -rf "$SC"; exit 1; }
    if [ -f "$SC/server/perpetuumserver_setup.exe" ]; then
        7z x "$SC/server/perpetuumserver_setup.exe" -o"$SC/data" -y >/dev/null
        LAYERS="$SC/data/data/layers"
    else
        LAYERS="$SC/server/data/layers"
    fi
    [ -f "$LAYERS/altitude.0000.bin" ] || { echo "error: installer layers not found under $LAYERS"; rm -rf "$SC"; exit 1; }
    cp -f "$LAYERS"/altitude.*.bin "$LAYERS"/blocks.*.bin "$LAYERS"/control.*.bin "$CL/" 2>/dev/null || \
        cp -f "$LAYERS"/*.bin "$CL/"
    rm -rf "$SC"
fi

# --- 2. gamma/custom layers from Google Drive -> .bin layers ---
if [ ! -f "$CL/altitude.0100.bin" ]; then
    python3 -m venv /tmp/wiki-gdown-env
    /tmp/wiki-gdown-env/bin/pip install -q gdown
    W="$(mktemp -d)"
    echo "downloading gamma_layers.rar from Google Drive..."
    /tmp/wiki-gdown-env/bin/gdown "$DRIVE_URL" -O "$W/gamma_layers.rar"
    [ -s "$W/gamma_layers.rar" ] || { echo "error: gamma_layers.rar download failed"; rm -rf "$W"; exit 1; }
    7z x -y "$W/gamma_layers.rar" -o"$W/x" >/dev/null
    SRC="$W/x"
    [ -d "$W/x/GAMMA_LAYERS_NEW" ] && SRC="$W/x/GAMMA_LAYERS_NEW"
    [ -d "$W/x/custom-layers" ] && SRC="$W/x/custom-layers"
    ls "$SRC"/*.bin >/dev/null 2>&1 || { echo "error: no .bin files after extraction"; rm -rf "$W"; exit 1; }
    cp -f "$SRC"/*.bin "$CL/"
    rm -rf "$W"
fi

echo "layer data ready in $CL ($(ls "$CL" | wc -l) files)"
