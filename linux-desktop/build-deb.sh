#!/bin/bash
set -euo pipefail

# ==========================================================
# Portable Debian packaging script (WSL + native Linux)
# - Publish + staging happens on Linux FS (/tmp) to avoid
#   permission issues on /mnt/c, /mnt/d, etc.
# - Outputs the .deb back into the project folder.
#
# You can override key values via env vars, e.g.:
#   PKG_NAME=myapp APP_VERSION=1.2.3 MAINTAINER="Me <me@x.com>" ./build-deb.sh
# ==========================================================

PKG_NAME="${PKG_NAME:-twofactorauth-desktop}"                 
APP_VERSION="${APP_VERSION:-1.0.0}"
APP_DISPLAY_NAME="${APP_DISPLAY_NAME:-TwoFactorAuth Desktop}"
ARCH="${ARCH:-amd64}"
RUNTIME="${RUNTIME:-linux-x64}"
ENTRY_EXE_NAME="${ENTRY_EXE_NAME:-TwoFactorAuthDesktop}"
MAINTAINER="${MAINTAINER:-Your Name <you@example.com>}"
DEPS="${DEPS:-libx11-6, libice6, libsm6, libfontconfig1, libfreetype6, libxkbcommon0, libglib2.0-0}"
ICON_PNG_PATH="${ICON_PNG_PATH:-app.png}"
ICON_ICO_PATH="${ICON_ICO_PATH:-app.ico}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

find_project_root() {
  local d="$SCRIPT_DIR"
  while true; do
    if ls "$d"/*.sln >/dev/null 2>&1 || ls "$d"/*.csproj >/dev/null 2>&1; then
      echo "$d"
      return 0
    fi
    [ "$d" = "/" ] && break
    d="$(dirname "$d")"
  done
  return 1
}

PROJECT_ROOT="$(find_project_root)" || {
  echo "ERROR: Could not locate project root (no *.sln or *.csproj found walking up from: $SCRIPT_DIR)"
  echo "Tip: run this script from within your repo, or place it inside the repo."
  exit 1
}

cd "$PROJECT_ROOT"

TMP_BASE="/tmp/${PKG_NAME}-build"
PUBLISH_DIR="${TMP_BASE}/publish/${RUNTIME}"
DEB_ROOT="${TMP_BASE}/deb-staging/${PKG_NAME}_${APP_VERSION}_${ARCH}"
DEB_FILE="${TMP_BASE}/deb-staging/${PKG_NAME}_${APP_VERSION}_${ARCH}.deb"
PROJECT_DEB_OUT="bin/deb-staging"

echo "ProjectRoot : $PROJECT_ROOT"
echo "PublishDir  : $PUBLISH_DIR"
echo "DebRoot     : $DEB_ROOT"
echo "OutputDeb   : $DEB_FILE"
echo "CopyTo      : ${PROJECT_ROOT}/${PROJECT_DEB_OUT}/"

# Validate package name
if [[ ! "$PKG_NAME" =~ ^[a-z0-9][a-z0-9+.-]*$ ]]; then
  echo "ERROR: PKG_NAME '$PKG_NAME' is not a valid Debian package name."
  echo "       Use lowercase and only [a-z0-9+.-], starting with [a-z0-9]."
  exit 1
fi

echo "=== Step 0: Check build dependencies ==="
MISSING=""
for pkg in clang zlib1g-dev dpkg-dev; do
  if ! dpkg -s "$pkg" &>/dev/null; then
    MISSING="$MISSING $pkg"
  fi
done
if [ -n "$MISSING" ]; then
  echo "Installing missing dependencies:$MISSING"
  sudo apt-get update
  sudo apt-get install -y $MISSING
fi

if ! command -v dotnet &>/dev/null; then
  echo "ERROR: dotnet SDK not found. Please install .NET 8 SDK first."
  exit 1
fi

echo "=== Step 0.5: Clean tmp build dirs ==="
rm -rf "$TMP_BASE"
mkdir -p "$(dirname "$PUBLISH_DIR")" "$(dirname "$DEB_FILE")"

echo "=== Step 1: Publish (NativeAOT) ==="
dotnet publish -c Release -r "$RUNTIME" --self-contained true \
  -p:PublishAot=true \
  -o "$PUBLISH_DIR"

echo "=== Step 1.1: Verify publish output ==="
if [ ! -f "$PUBLISH_DIR/$ENTRY_EXE_NAME" ]; then
  echo "ERROR: Published executable not found:"
  echo "  $PUBLISH_DIR/$ENTRY_EXE_NAME"
  echo "If your binary name differs, run with:"
  echo "  ENTRY_EXE_NAME=YourBinaryName ./build-deb.sh"
  exit 1
fi

echo "=== Step 2: Build .deb structure ==="
rm -rf "$DEB_ROOT"

mkdir -p "$DEB_ROOT/opt/$PKG_NAME"
cp -f "$PUBLISH_DIR/$ENTRY_EXE_NAME" "$DEB_ROOT/opt/$PKG_NAME/"
chmod 0755 "$DEB_ROOT/opt/$PKG_NAME/$ENTRY_EXE_NAME"

find "$PUBLISH_DIR" -maxdepth 1 -name "*.so"   -exec cp -f {} "$DEB_ROOT/opt/$PKG_NAME/" \; 2>/dev/null || true
find "$PUBLISH_DIR" -maxdepth 1 -name "*.so.*" -exec cp -f {} "$DEB_ROOT/opt/$PKG_NAME/" \; 2>/dev/null || true

mkdir -p "$DEB_ROOT/usr/bin"
ln -sf "/opt/$PKG_NAME/$ENTRY_EXE_NAME" "$DEB_ROOT/usr/bin/$PKG_NAME"

mkdir -p "$DEB_ROOT/usr/share/applications"
cat > "$DEB_ROOT/usr/share/applications/${PKG_NAME}.desktop" << EOF
[Desktop Entry]
Name=$APP_DISPLAY_NAME
Comment=Two-Factor Authentication Manager
Exec=/opt/$PKG_NAME/$ENTRY_EXE_NAME
Icon=$PKG_NAME
Terminal=false
Type=Application
Categories=Utility;Security;
StartupWMClass=$ENTRY_EXE_NAME
EOF
chmod 0644 "$DEB_ROOT/usr/share/applications/${PKG_NAME}.desktop"

mkdir -p "$DEB_ROOT/usr/share/metainfo"
cat > "$DEB_ROOT/usr/share/metainfo/${PKG_NAME}.metainfo.xml" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>${PKG_NAME}.desktop</id>
  <name>$APP_DISPLAY_NAME</name>
  <summary>Two-Factor Authentication Manager</summary>
  <description>
    <p>A desktop two-factor authentication (2FA/TOTP/HOTP) manager built with Avalonia UI. Supports Linux and Windows.</p>
  </description>
  <launchable type="desktop-id">${PKG_NAME}.desktop</launchable>
  <provides><binary>$PKG_NAME</binary></provides>
  <metadata_license>MIT</metadata_license>
  <project_license>MIT</project_license>
</component>
EOF
chmod 0644 "$DEB_ROOT/usr/share/metainfo/${PKG_NAME}.metainfo.xml"

mkdir -p "$DEB_ROOT/usr/share/icons/hicolor/256x256/apps"
if [ -f "$ICON_PNG_PATH" ]; then
  cp -f "$ICON_PNG_PATH" "$DEB_ROOT/usr/share/icons/hicolor/256x256/apps/${PKG_NAME}.png"
  chmod 0644 "$DEB_ROOT/usr/share/icons/hicolor/256x256/apps/${PKG_NAME}.png"
elif [ -f "$ICON_ICO_PATH" ]; then
  cp -f "$ICON_ICO_PATH" "$DEB_ROOT/usr/share/icons/hicolor/256x256/apps/${PKG_NAME}.ico"
  chmod 0644 "$DEB_ROOT/usr/share/icons/hicolor/256x256/apps/${PKG_NAME}.ico"
else
  echo "WARNING: No icon found ($ICON_PNG_PATH or $ICON_ICO_PATH). Icon will be missing."
fi

mkdir -p "$DEB_ROOT/DEBIAN"
cat > "$DEB_ROOT/DEBIAN/control" << EOF
Package: $PKG_NAME
Version: $APP_VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Depends: $DEPS
Maintainer: $MAINTAINER
Description: $APP_DISPLAY_NAME
 A desktop two-factor authentication (2FA/TOTP/HOTP) manager
 built with Avalonia UI. Supports Linux and Windows.
EOF

cat > "$DEB_ROOT/DEBIAN/postinst" << EOF
#!/bin/bash
set -e
chmod 755 /opt/$PKG_NAME/$ENTRY_EXE_NAME 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true
gtk-update-icon-cache -f /usr/share/icons/hicolor 2>/dev/null || true
EOF

cat > "$DEB_ROOT/DEBIAN/prerm" << EOF
#!/bin/bash
set -e
update-desktop-database /usr/share/applications 2>/dev/null || true
gtk-update-icon-cache -f /usr/share/icons/hicolor 2>/dev/null || true
EOF

chmod 0755 "$DEB_ROOT/DEBIAN"
chmod 0644 "$DEB_ROOT/DEBIAN/control"
chmod 0755 "$DEB_ROOT/DEBIAN/postinst" "$DEB_ROOT/DEBIAN/prerm"

echo "=== Step 3: Build .deb ==="
dpkg-deb --build "$DEB_ROOT" "$DEB_FILE"

echo "=== Step 4: Copy .deb back to project dir ==="
mkdir -p "$PROJECT_DEB_OUT"
cp -f "$DEB_FILE" "$PROJECT_DEB_OUT/"
echo "Copied to: $PROJECT_ROOT/$PROJECT_DEB_OUT/$(basename "$DEB_FILE")"

echo ""
echo "=== Done ==="
echo "Output (proj): $PROJECT_ROOT/$PROJECT_DEB_OUT/$(basename "$DEB_FILE")"
echo "Install: sudo apt install ./$PROJECT_DEB_OUT/$(basename "$DEB_FILE")"
echo "Run: $PKG_NAME"
