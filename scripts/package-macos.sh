#!/usr/bin/env bash
# Baut ClaudeStats.app (macOS, ohne Dock-Icon) und packt sie als Zip.
#
# Aufruf:   scripts/package-macos.sh [osx-arm64|osx-x64] [version]
# Ergebnis: publish/<rid>/ClaudeStats.app und publish/<rid>/ClaudeStats-<version>-<rid>.zip
#
# Ohne Argument wird die Architektur dieses Rechners gewählt.
set -euo pipefail

if [ "${1:-}" ]; then
  RID="$1"
elif [ "$(uname -m)" = "arm64" ]; then
  RID="osx-arm64"
else
  RID="osx-x64"
fi
VERSION="${2:-0.1.0}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/publish/$RID"
APP="$OUT/ClaudeStats.app"

rm -rf "$OUT"

dotnet publish "$ROOT/src/ClaudeStats/ClaudeStats.csproj" \
  -c Release -r "$RID" --self-contained true \
  -p:Version="$VERSION" \
  -o "$OUT/bin"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$OUT/bin/." "$APP/Contents/MacOS/"
sed "s/__VERSION__/$VERSION/g" "$ROOT/packaging/macos/Info.plist" > "$APP/Contents/Info.plist"
chmod +x "$APP/Contents/MacOS/ClaudeStats"

# Auf dem Mac selbst: ad-hoc signieren, sonst verweigern Apple-Silicon-Macs den Start.
if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep -s - "$APP"
fi

rm -rf "$OUT/bin"
( cd "$OUT" && zip -qry "ClaudeStats-$VERSION-$RID.zip" ClaudeStats.app )

echo
echo "Fertig: $APP"
echo "Zip:    $OUT/ClaudeStats-$VERSION-$RID.zip"
echo "Installieren: cp -R \"$APP\" /Applications/"
