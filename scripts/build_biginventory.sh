#!/usr/bin/env bash
# بناء BigInventory.dll بدون الحاجة إلى msbuild كامل.
# السبب: حزمة dotnet SDK في هذه البيئة ناقصة الأسماء (Alpine)ysc.
# نستخدم csc.dll مباشرة مع مراجع net9.0 المرجعية + StardewModdingAPI.dll.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/src/BigInventory"
OUT="$ROOT/dist/BigInventory"
CSC="/usr/lib/dotnet/sdk/9.0.121/Roslyn/bincore/csc.dll"
REFDIR="/usr/lib/dotnet/packs/Microsoft.NETCore.App.Ref/9.0.20/ref/net9.0"
SMAPI="$ROOT/original/StardewModdingAPI.dll.orig"

for p in "$CSC" "$REFDIR" "$SMAPI"; do
  [ -e "$p" ] || { echo "missing: $p" >&2; exit 1; }
done

REFS=()
for dll in "$REFDIR"/*.dll; do REFS+=("-r:$dll"); done
REFS+=("-r:$SMAPI")

mkdir -p "$OUT"
DOTNET_GCHeapHardLimit=400000000 dotnet "$CSC" \
  -nologo \
  -nostdlib \
  -target:library \
  -langversion:latest \
  -optimize+ \
  -debug- \
  -nullable:disable \
  -out:"$OUT/BigInventory.dll" \
  "${REFS[@]}" \
  "$SRC/GameAccess.cs" \
  "$SRC/ModConfig.cs" \
  "$SRC/ModEntry.cs"

echo "built $OUT/BigInventory.dll"
