#!/usr/bin/env bash
# Regenerate the Red Light scene until one BOOTS, then keep that scene.
#
# WHY THIS EXISTS
# Scene generation is nondeterministic in object ORDER (see WORKLOG), and some orders
# build a player that dies at startup. But a given scene is completely deterministic:
# measured, one saved scene built 8 times gave 8 byte-identical players. So the scene is
# the thing worth pinning down - regenerate until one is good, then keep it and stop
# rolling the dice on every build.
#
# BOOT THE PLAYER, DO NOT TRUST THE SIZE. An earlier version of this loop exited on
# `level0 == 199712`, the size every good build had shown so far. Ten regenerations later
# it had never matched, while producing several sizes LARGER than any known-bad build -
# builds that were quite possibly fine and were thrown away. Substituting a proxy for the
# measurement is the same mistake as trusting Unity's exit code (HANDOFF.md §6.7).
set -uo pipefail

N="${1:-12}"
UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)/Unity"
SCENE="Unity/Assets/_Party/Scenes/RedLight.unity"
OUT="Unity/Build/MacRedLight"
KEEP="${KEEP:-/tmp/party_repro}"
mkdir -p "$KEEP"

if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null; then
  echo "FAIL: Unity editor is open - it locks the project"; exit 1
fi

for i in $(seq 1 "$N"); do
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
           -executeMethod Party.EditorTools.RedLightSetup.Build \
           -logFile /tmp/cap_scene.log >/dev/null 2>&1
  rm -rf "$OUT"
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
           -executeMethod Party.EditorTools.BuildTools.BuildMacRedLight \
           -logFile /tmp/cap_build.log >/dev/null 2>&1

  size=$(stat -f%z "$OUT"/*.app/Contents/Resources/Data/level0 2>/dev/null || echo 0)
  APP=""
  for app in "$OUT"/*.app; do
    [ -d "$app" ] || continue
    for exe in "$app/Contents/MacOS/"*; do [ -x "$exe" ] && APP="$exe"; done
  done

  res="no-player"
  if [ -n "$APP" ]; then
    rm -f /tmp/cap_smoke.log
    "$APP" -batchmode -nographics -partyrole none -partyseconds 5 \
           -logFile /tmp/cap_smoke.log >/dev/null 2>&1 &
    pid=$!
    for _ in $(seq 1 30); do kill -0 $pid 2>/dev/null || break; sleep 1; done
    kill -9 $pid 2>/dev/null; wait $pid 2>/dev/null

    if grep -q 'corrupted' /tmp/cap_smoke.log 2>/dev/null; then res="corrupt"
    elif grep -q '\[AutoRun\]' /tmp/cap_smoke.log 2>/dev/null; then res="GOOD"
    else res="no-scripts"; fi
  fi

  echo "  try $i: level0 $size -> $res"

  if [ "$res" = "GOOD" ]; then
    cp "$SCENE" "$KEEP/RedLight.GOOD.$size.unity"
    echo "  kept $KEEP/RedLight.GOOD.$size.unity"
    exit 0
  fi
  # Keep one confirmed-bad scene for bisecting, if we do not have one yet.
  [ "$res" = "corrupt" ] && [ ! -f "$KEEP/RedLight.BAD.confirmed.unity" ] && \
    cp "$SCENE" "$KEEP/RedLight.BAD.confirmed.unity"
done

echo "FAIL: no good scene in $N attempts"
exit 1
