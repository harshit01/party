#!/usr/bin/env bash
# Build a player and PROVE it runs, retrying if Unity emits a corrupt one.
#
# WHY THIS EXISTS
# Unity 6000.5.9f1 on this project intermittently writes a corrupt level0: identical
# inputs, three consecutive scene-rebuild-then-build cycles gave corrupt / clean /
# corrupt, with the good build a different byte size from the bad ones. Nothing in the
# project content causes it - six rounds of isolation found no culprit because there
# is none to find.
#
# Roughly one build in three is good. Ruled out and NOT the cause: the Kenney props,
# the player prefab (NetTest builds clean every time), DepthOfField, the whole
# post-processing volume, and MatchBootstrap. Also did NOT fix it: retrying, doing the
# scene rebuild and player build in one Unity invocation, and seeding Random so the
# scene content is identical. level0 comes out a different size on every build even
# with a fixed seed, so something in Unity's own serialisation is non-deterministic.
#
# So the build is treated as unreliable and VERIFIED instead of trusted, the same way
# BuildTools calls EditorApplication.Exit(1) rather than believing Unity's exit code.
# With ~1-in-3 success and 10 attempts, the odds of total failure are about 2%.
set -uo pipefail

UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)/Unity"
SCENE_METHOD="${1:-Party.EditorTools.RedLightSetup.Build}"  # combined method preferred
BUILD_METHOD="${2:-Party.EditorTools.BuildTools.BuildMacRedLight}"
OUT_DIR="${3:-Unity/Build/MacRedLight}"
MAX=10

if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null; then
  echo "FAIL: Unity editor is open - it locks the project"; exit 1
fi

find_player() {
  for app in "$OUT_DIR"/*.app; do
    [ -d "$app" ] || continue
    for exe in "$app/Contents/MacOS/"*; do [ -x "$exe" ] && { echo "$exe"; return; }; done
  done
}

for attempt in $(seq 1 $MAX); do
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
           -executeMethod "$SCENE_METHOD" -logFile /tmp/bv_scene.log >/dev/null 2>&1
  rm -rf "$OUT_DIR"
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
           -executeMethod "$BUILD_METHOD" -logFile /tmp/bv_build.log >/dev/null 2>&1

  APP="$(find_player)"
  if [ -z "$APP" ]; then echo "  attempt $attempt: no player produced"; continue; fi

  # Smoke test: boot it briefly and see whether the scene actually loads.
  rm -f /tmp/bv_smoke.log
  "$APP" -batchmode -nographics -partyrole none -partyseconds 6 -logFile /tmp/bv_smoke.log >/dev/null 2>&1
  if grep -q 'corrupted' /tmp/bv_smoke.log; then
    echo "  attempt $attempt: corrupt level0, rebuilding"
    continue
  fi
  echo "  attempt $attempt: OK  ($(stat -f%z "$OUT_DIR"/*.app/Contents/Resources/Data/level0 2>/dev/null) bytes)"
  echo "$APP"
  exit 0
done

echo "FAIL: $MAX consecutive builds were corrupt"
exit 1
