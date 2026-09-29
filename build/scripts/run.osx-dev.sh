#!/usr/bin/env bash
# Builds PerfectGit (Debug) and runs it as a macOS app bundle, so that the Dock and the
# menu bar show its name and icon (a bare `dotnet run` shows the executable name instead).
# Usage: build/scripts/run.osx-dev.sh [repository path]

set -euo pipefail

cd "$(dirname "$0")/../.."

DOTNET=dotnet
if ! command -v "$DOTNET" >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
  DOTNET="$HOME/.dotnet/dotnet"
  export DOTNET_ROOT="$HOME/.dotnet"
fi

"$DOTNET" build src/SourceGit.csproj -c Debug

APP="build/dev/PerfectGit.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
rsync -a --delete --exclude setsid src/bin/Debug/net10.0/ "$APP/Contents/MacOS/"
cp build/resources/app/App.icns "$APP/Contents/Resources/App.icns"
sed "s/SOURCE_GIT_VERSION/$(cat VERSION)/g" build/resources/app/App.plist > "$APP/Contents/Info.plist"

if [ ! -x "$APP/Contents/MacOS/setsid" ] && command -v clang >/dev/null 2>&1; then
  clang tools/setsid-macos/setsid.c -o "$APP/Contents/MacOS/setsid" -mmacosx-version-min=13.0
fi

if [ $# -gt 0 ]; then
  open "$APP" --args "$@"
else
  open "$APP"
fi
