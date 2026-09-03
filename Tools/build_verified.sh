#!/usr/bin/env bash
# Build a player and PROVE it runs.
#
# WHAT WAS ACTUALLY WRONG (measured 2026-09-01, see WORKLOG.md and
# Docs/build_experiments.tsv)
#
# HANDOFF.md recorded that "level0 is a different size on every build even with a fixed
# seed, so the non-determinism is inside Unity's serialisation". That is not what is
# happening, and this script was built around the wrong model - it regenerated the scene
# before EVERY attempt, which re-rolled the dice each time:
#
#     regenerate before every build ...... 3/8 good     (a coin flip)
#     one BAD scene, built 8 times ....... 0/8 good, all level0 199640 bytes
#     one GOOD scene, built 6 times ...... 6/6 good, all level0 199700 bytes
#
# The BUILD is completely deterministic: same scene in, byte-identical player out. What
# is nondeterministic is scene GENERATION. Three regenerations produce three different
# files containing exactly the same 430 GameObjects with identical names - Unity assigns
# random anchor ids and writes the objects in a different ORDER each time. Some orders
# produce a player that dies at startup with "level0 is corrupted", which matches the
# failure signature already recorded twice in this codebase: a MonoBehaviour
# deserialising past the end of the data.
#
# So the scene is the artifact worth pinning. The committed RedLight.unity is a scene
# that has been built and booted; leave it alone and builds are reliable. Regenerate only
# when the setup code actually changes, with -r, which hunts for a good scene and keeps
# it - then COMMIT the result.
#
# Usage:
#   ./Tools/build_verified.sh [game]        build the committed scene and verify it
#   ./Tools/build_verified.sh [game] -r     regenerate first, retry until it boots
#
# game is redlight (default), saywhat, plank or ragdoll.
set -uo pipefail

UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)/Unity"

# ONE TOOL FOR EVERY MINIGAME. This was hardcoded to Red Light, so when Plank Panic was
# built it rolled the corrupt-scene dice on every build with nothing catching it - the
# player came out with a corrupt level0 and simply never started, silently.
GAME="${GAME:-redlight}"
for a in "$@"; do
  case "$a" in
    redlight|saywhat|plank|ragdoll) GAME="$a" ;;
  esac
done

case "$GAME" in
  redlight) SCENE="Unity/Assets/_Party/Scenes/RedLight.unity"
            OUT_DIR="Unity/Build/MacRedLight"
            BUILD_METHOD="Party.EditorTools.BuildTools.BuildMacRedLight"
            SCENE_METHOD="Party.EditorTools.RedLightSetup.Build" ;;
  saywhat)  SCENE="Unity/Assets/_Party/Scenes/SayWhat.unity"
            OUT_DIR="Unity/Build/MacSayWhat"
            BUILD_METHOD="Party.EditorTools.BuildTools.BuildMacSayWhat"
            SCENE_METHOD="Party.EditorTools.SayWhatSetup.Build" ;;
  plank)    SCENE="Unity/Assets/_Party/Scenes/Plank.unity"
            OUT_DIR="Unity/Build/MacPlank"
            BUILD_METHOD="Party.EditorTools.BuildTools.BuildMacPlank"
            SCENE_METHOD="Party.EditorTools.PlankSetup.Build" ;;
  ragdoll)  SCENE="Unity/Assets/_Party/Scenes/RagdollLab.unity"
            OUT_DIR="Unity/Build/MacRagdoll"
            BUILD_METHOD="Party.EditorTools.BuildTools.BuildMacRagdoll"
            SCENE_METHOD="Party.EditorTools.RagdollSetup.Build" ;;
  *) echo "FAIL: unknown game '$GAME' (redlight|saywhat|plank|ragdoll)"; exit 1 ;;
esac

REGEN=0
for a in "$@"; do [ "$a" = "-r" ] && REGEN=1; done

if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null; then
  echo "FAIL: Unity editor is open - it locks the project"; exit 1
fi

find_player() {
  for app in "$OUT_DIR"/*.app; do
    [ -d "$app" ] || continue
    for exe in "$app/Contents/MacOS/"*; do [ -x "$exe" ] && { echo "$exe"; return; }; done
  done
}

# Build once and boot the result. Echoes the player path on success.
attempt() {
  rm -rf "$OUT_DIR"
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
           -executeMethod "$BUILD_METHOD" -logFile /tmp/bv_build.log >/dev/null 2>&1

  local app size
  app="$(find_player)"
  [ -n "$app" ] || { echo "  no player produced" >&2; return 1; }
  size=$(stat -f%z "$OUT_DIR"/*.app/Contents/Resources/Data/level0 2>/dev/null || echo 0)

  # HARD DEADLINE. Trusting the player to exit on its own is the same mistake as
  # trusting Unity's exit code: -partyseconds was once unreachable for -partyrole none,
  # so a CORRECT build hung this script forever and only a corrupt one ended it.
  rm -f /tmp/bv_smoke.log
  "$app" -batchmode -nographics -partyrole none -partyseconds 6 \
         -logFile /tmp/bv_smoke.log >/dev/null 2>&1 &
  local pid=$!
  local _
  for _ in $(seq 1 30); do kill -0 "$pid" 2>/dev/null || break; sleep 1; done
  if kill -0 "$pid" 2>/dev/null; then
    kill -9 "$pid" 2>/dev/null; wait "$pid" 2>/dev/null
    echo "  player never exited within 30s (level0 $size)" >&2; return 1
  fi
  wait "$pid" 2>/dev/null

  if grep -q 'corrupted' /tmp/bv_smoke.log 2>/dev/null; then
    echo "  corrupt level0 ($size bytes)" >&2; return 1
  fi
  # POSITIVE evidence, not merely the absence of the word "corrupted": a build that
  # produced no log at all, or died before running a single script, passed the old check
  # silently. This line comes from MilestoneAutoRun.Start().
  if ! grep -q '\[AutoRun\]' /tmp/bv_smoke.log 2>/dev/null; then
    echo "  scene loaded no Party scripts ($size bytes)" >&2; return 1
  fi

  echo "  OK  level0 $size bytes" >&2
  echo "$app"
}

if [ "$REGEN" -eq 0 ]; then
  # The committed scene is known good, so this should succeed first time. It is still
  # VERIFIED rather than trusted - that is the whole point of this script.
  if APP="$(attempt)"; then
    echo "$APP"; exit 0
  fi
  echo "FAIL: the committed scene did not build a working player." >&2
  echo "      If RedLightSetup or the scene content changed, re-run with -r to find and" >&2
  echo "      keep a good scene, then COMMIT the regenerated $SCENE." >&2
  exit 1
fi

# -r: scene generation is a lottery (~37%), so regenerate until one boots, then keep it.
MAX=12
for i in $(seq 1 $MAX); do
  echo "  regenerating scene (attempt $i/$MAX)" >&2
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
           -executeMethod "$SCENE_METHOD" -logFile /tmp/bv_scene.log >/dev/null 2>&1
  if APP="$(attempt)"; then
    echo >&2
    echo "  GOOD SCENE FOUND. Commit it:" >&2
    echo "      git add $SCENE && git commit -m 'Regenerate RedLight scene (verified)'" >&2
    echo "$APP"; exit 0
  fi
done

echo "FAIL: no good scene in $MAX regenerations" >&2
exit 1
