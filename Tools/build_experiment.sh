#!/usr/bin/env bash
# Measure the Red Light build corruption rate under ONE named condition.
#
# WHY THIS EXISTS
# HANDOFF.md §6.8: eight rounds once went into this bug and produced four confident
# WRONG conclusions, because the isolation was cumulative - several things changed at
# once, and runs were compared against each other rather than against a re-run of the
# same configuration. The decisive clue (a level0 size that differed on every build) was
# visible from round two and read past twice.
#
# So: one condition per invocation, a fixed number of attempts, and every attempt's
# level0 size and boot result recorded. Compare CONDITIONS, never individual builds.
#
#   ./Tools/build_experiment.sh <label> <attempts> <regen-every-time: yes|no>
#
# Results append to Docs/build_experiments.tsv so they survive the session.
set -uo pipefail

LABEL="${1:?usage: build_experiment.sh <label> <attempts> <yes|no>}"
N="${2:-6}"
REGEN="${3:-yes}"

UNITY="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$(cd "$(dirname "$0")/.." && pwd)/Unity"
OUT_DIR="Unity/Build/MacRedLight"
RESULTS="Docs/build_experiments.tsv"

if pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null; then
  echo "FAIL: Unity editor is open - it locks the project"; exit 1
fi
[ -f "$RESULTS" ] || printf 'label\tattempt\tregen\tlevel0_bytes\tresult\n' > "$RESULTS"

find_player() {
  for app in "$OUT_DIR"/*.app; do
    [ -d "$app" ] || continue
    for exe in "$app/Contents/MacOS/"*; do [ -x "$exe" ] && { echo "$exe"; return; }; done
  done
}

regen_scene() {
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
           -executeMethod Party.EditorTools.RedLightSetup.Build \
           -logFile /tmp/exp_scene.log >/dev/null 2>&1
}

# Regenerate once up front when the condition is "no" - the point of that condition is
# to build the SAME saved scene repeatedly.
[ "$REGEN" = "no" ] && { echo "  [$LABEL] regenerating scene ONCE up front..."; regen_scene; }

ok=0
for i in $(seq 1 "$N"); do
  [ "$REGEN" = "yes" ] && regen_scene

  rm -rf "$OUT_DIR"
  "$UNITY" -batchmode -quit -nographics -projectPath "$PROJECT" \
           -executeMethod Party.EditorTools.BuildTools.BuildMacRedLight \
           -logFile /tmp/exp_build.log >/dev/null 2>&1

  APP="$(find_player)"
  size=$(stat -f%z "$OUT_DIR"/*.app/Contents/Resources/Data/level0 2>/dev/null || echo 0)

  if [ -z "$APP" ]; then
    res="no-player"
  else
    rm -f /tmp/exp_smoke.log
    "$APP" -batchmode -nographics -partyrole none -partyseconds 5 \
           -logFile /tmp/exp_smoke.log >/dev/null 2>&1 &
    pid=$!
    for _ in $(seq 1 30); do kill -0 $pid 2>/dev/null || break; sleep 1; done
    kill -9 $pid 2>/dev/null; wait $pid 2>/dev/null

    if grep -q 'corrupted' /tmp/exp_smoke.log 2>/dev/null; then res="corrupt"
    elif grep -q '\[AutoRun\]' /tmp/exp_smoke.log 2>/dev/null; then res="ok"; ok=$((ok+1))
    else res="no-scripts"; fi
  fi

  printf '%s\t%d\t%s\t%s\t%s\n' "$LABEL" "$i" "$REGEN" "$size" "$res" >> "$RESULTS"
  echo "  [$LABEL] attempt $i: $res (level0 $size bytes)"
done

echo "  [$LABEL] RESULT: $ok/$N good  (regen-every-time=$REGEN)"
