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
  #
  # HARD DEADLINE. This used to be a bare foreground call trusting -partyseconds to end
  # it. It did not: MilestoneAutoRun checked its quit timer AFTER `if (_role == "none")
  # return;`, so a player that booted CORRECTLY never quit. Only a CORRUPT build ended
  # the smoke test - by crashing - which meant this script could report failure but
  # could never report success, and simply blocked on the first good build. The C# is
  # fixed, but the deadline stays: this tool exists because Unity is not trusted, and
  # trusting the player to exit on its own is the same mistake one level down.
  rm -f /tmp/bv_smoke.log
  "$APP" -batchmode -nographics -partyrole none -partyseconds 6 -logFile /tmp/bv_smoke.log >/dev/null 2>&1 &
  SMOKE_PID=$!
  for _ in $(seq 1 30); do kill -0 "$SMOKE_PID" 2>/dev/null || break; sleep 1; done
  if kill -0 "$SMOKE_PID" 2>/dev/null; then
    kill -9 "$SMOKE_PID" 2>/dev/null; wait "$SMOKE_PID" 2>/dev/null
    echo "  attempt $attempt: player never exited within 30s, rebuilding"
    continue
  fi
  wait "$SMOKE_PID" 2>/dev/null

  if grep -q 'corrupted' /tmp/bv_smoke.log; then
    echo "  attempt $attempt: corrupt level0, rebuilding"
    continue
  fi

  # POSITIVE evidence, not merely the absence of the word "corrupted". A build that
  # produced no log at all, or died before running a single script, passed the old
  # check silently. This line comes from MilestoneAutoRun.Start(), so it can only
  # appear if the scene loaded and our own code ran in it.
  if ! grep -q '\[AutoRun\]' /tmp/bv_smoke.log; then
    echo "  attempt $attempt: scene loaded no Party scripts, rebuilding"
    continue
  fi
  echo "  attempt $attempt: OK  ($(stat -f%z "$OUT_DIR"/*.app/Contents/Resources/Data/level0 2>/dev/null) bytes)"
  echo "$APP"
  exit 0
done

echo "FAIL: $MAX consecutive builds were corrupt"
exit 1
