#!/usr/bin/env bash
# Active ragdoll verification: build the probe scene, run it, check what it measured.
#
# Grab and throw cannot be exercised by hand in a headless build, so without this they ship
# untested - which they were, and the first run immediately found a throw releasing at
# 0.74 m/s (a drop, not a throw).
#
# Phase 0 calibrates the analyser against a known-good and a known-broken fixture and
# refuses to run if the broken one comes back green (HANDOFF §6.1).
#
#   ./Tests/ragdoll_verify.sh
set -uo pipefail

UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)/Unity"
OUT="Unity/Build/MacRagdoll"
LOG=/tmp/ragdoll_verify.log

if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null; then
  echo "FAIL: Unity editor is open - it locks the project"; exit 1
fi

echo "== 0/2  CALIBRATION =="
python3 Tests/ragdoll_report.py Tests/fixtures/ragdoll_good.log >/dev/null 2>&1 \
  && echo "  OK   clean fixture passes" \
  || { echo "  FAIL: analyser rejects a KNOWN-GOOD run - it is over-strict"; exit 1; }
python3 Tests/ragdoll_report.py Tests/fixtures/ragdoll_bad.log >/dev/null 2>&1 \
  && { echo "  FAIL: analyser PASSES a known-broken run - it measures nothing"; exit 1; } \
  || echo "  OK   broken fixture is caught"

echo
echo "== 1/2  BUILD the probe scene =="
RAGDOLL_PROBE=1 "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
  -executeMethod Party.EditorTools.RagdollSetup.Build -logFile /tmp/rv_scene.log >/dev/null 2>&1
"$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
  -executeMethod Party.EditorTools.BuildTools.BuildMacRagdoll -logFile /tmp/rv_build.log >/dev/null 2>&1

APP=""
for app in "$OUT"/*.app; do
  [ -d "$app" ] || continue
  for exe in "$app/Contents/MacOS/"*; do [ -x "$exe" ] && APP="$exe"; done
done
[ -n "$APP" ] || { echo "  FAIL: no player produced"; exit 1; }
echo "  OK   built"

echo
echo "== 2/2  RUN the probe =="
pkill -f 'Contents/MacOS/Party' 2>/dev/null; sleep 1
rm -f "$LOG"
# Hard deadline. Trusting the player to exit on its own is the mistake that once left a
# 6-second smoke test running for 1h37m.
# 30 s, not 22: the probe's script runs to t=26 since the turning phase was added, and
# a run cut short reports "no DONE line - it crashed or hung" when nothing crashed at all.
"$APP" -batchmode -nographics -partyseconds 30 -logFile "$LOG" >/dev/null 2>&1 &
pid=$!
for _ in $(seq 1 75); do kill -0 $pid 2>/dev/null || break; sleep 1; done
kill -9 $pid 2>/dev/null; wait $pid 2>/dev/null

echo
python3 Tests/ragdoll_report.py "$LOG"
