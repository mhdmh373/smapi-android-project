#!/usr/bin/env bash
# بناء MeasureMod.dll عبر csc.dll مباشرة (نفس نهج بقية المودات).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/src/MeasureMod"
OUT="$ROOT/dist/MeasureMod"
CSC="/usr/lib/dotnet/sdk/9.0.121/Roslyn/bincore/csc.dll"
REFDIR="/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/9.0.20/ref/net9.0"
SMAPI="$ROOT/original/StardewModdingAPI.dll.orig"
TOOLKIT="$ROOT/original/SMAPI.Toolkit.CoreInterfaces.dll"

for p in "$CSC" "$REFDIR" "$SMAPI" "$TOOLKIT"; do
  [ -e "$p" ] || { echo "missing: $p" >&2; exit 1; }
done

REFS=()
for dll in "$REFDIR"/*.dll; do REFS+=("-r:$dll"); done
REFS+=("-r:$SMAPI" "-r:$TOOLKIT")

mkdir -p "$OUT"
DOTNET_GCHeapHardLimit=400000000 dotnet "$CSC" \
  -nologo \
  -nostdlib \
  -target:library \
  -langversion:latest \
  -optimize+ \
  -debug- \
  -nullable:disable \
  -out:"$OUT/MeasureMod.dll" \
  "${REFS[@]}" \
  "$SRC/ModEntry.cs"

echo "built $OUT/MeasureMod.dll"
