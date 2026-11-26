#!/usr/bin/env bash
# Builds ClaudeStats.app (macOS, no Dock icon) and packs it as a zip.
#
# Usage:  scripts/package-macos.sh [osx-arm64|osx-x64] [version]
# Output: publish/<rid>/ClaudeStats.app and publish/<rid>/ClaudeStats-<version>-<rid>.zip
#
# Without arguments, the architecture of this machine is used.
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

# On a Mac: ad-hoc sign, otherwise Apple Silicon Macs refuse to launch the app.
if command -v codesign >/dev/null 2>&1; then
  codesign --force --deep -s - "$APP"
fi

rm -rf "$OUT/bin"
( cd "$OUT" && zip -qry "ClaudeStats-$VERSION-$RID.zip" ClaudeStats.app )

echo
echo "Done:  $APP"
echo "Zip:   $OUT/ClaudeStats-$VERSION-$RID.zip"
echo "Install: cp -R \"$APP\" /Applications/"
