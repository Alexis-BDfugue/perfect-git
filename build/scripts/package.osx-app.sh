#!/usr/bin/env bash

set -e
set -o
set -u
set pipefail

cd build

APP="PerfectGit.app"

mkdir -p "$APP/Contents/Resources"
mv SourceGit "$APP/Contents/MacOS"
cp resources/app/App.icns "$APP/Contents/Resources/App.icns"
sed "s/SOURCE_GIT_VERSION/$VERSION/g" resources/app/App.plist > "$APP/Contents/Info.plist"
rm -rf "$APP/Contents/MacOS/SourceGit.dsym"
rm -f "$APP"/Contents/MacOS/*.pdb

case "$RUNTIME" in
  osx-arm64) ARCH=arm64 ;;
  osx-x64)   ARCH=x86_64 ;;
  *) echo "Unsupported RUNTIME: $RUNTIME" >&2; exit 1 ;;
esac

clang -arch "$ARCH" ../tools/setsid-macos/setsid.c -o "$APP/Contents/MacOS/setsid" -mmacosx-version-min=13.0

zip "sourcegit_$VERSION.$RUNTIME.zip" -r "$APP"
