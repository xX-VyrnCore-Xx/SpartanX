#!/usr/bin/env bash
# Builds a universal (Apple Silicon + Intel) SpartanX.app and packs it into
#   dist/SpartanX-<version>-macOS.dmg  (+ .sha256)
# Usage: macos/scripts/build-dmg.sh 0.2.0
# The app is ad-hoc signed (no Apple Developer ID yet), so first launch needs
# right-click → Open, or System Settings → Privacy & Security → Open Anyway.
set -euo pipefail

VERSION="${1:-0.0.0-dev}"
PKG="$(cd "$(dirname "$0")/.." && pwd)"
ROOT="$(cd "$PKG/.." && pwd)"
OUT="$PKG/.build/package"
DIST="$ROOT/dist"
NUMERIC="${VERSION%%-*}"

rm -rf "$OUT"
mkdir -p "$OUT" "$DIST"

echo "› Building SpartanX $VERSION (universal)"
cd "$PKG"
swift build -c release --arch arm64 --arch x86_64
BIN_DIR="$(swift build -c release --arch arm64 --arch x86_64 --show-bin-path)"

APP="$OUT/SpartanX.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/SpartanX" "$APP/Contents/MacOS/SpartanX"
sed -e "s/__VERSION__/$VERSION/g" -e "s/__BUILD__/$NUMERIC/g" "$PKG/Resources/Info.plist" > "$APP/Contents/Info.plist"
plutil -lint "$APP/Contents/Info.plist"

echo "› Icon"
ICONSET="$OUT/AppIcon.iconset"
mkdir -p "$ICONSET"
for s in 16 32 128 256 512; do
  sips -z "$s" "$s" "$ROOT/docs/assets/logo.png" --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
  sips -z "$((s * 2))" "$((s * 2))" "$ROOT/docs/assets/logo.png" --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"

echo "› Ad-hoc signing"
codesign --force --deep --sign - "$APP"
codesign --verify --verbose=2 "$APP"
lipo -info "$APP/Contents/MacOS/SpartanX"

echo "› Disk image"
STAGE="$OUT/dmg"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
DMG="$DIST/SpartanX-$VERSION-macOS.dmg"
rm -f "$DMG"
hdiutil create -volname "SpartanX" -srcfolder "$STAGE" -ov -format UDZO "$DMG" >/dev/null
(cd "$DIST" && shasum -a 256 "$(basename "$DMG")" > "$(basename "$DMG").sha256")
echo "✓ $DMG"
