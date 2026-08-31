#!/usr/bin/env bash
# Run the same fixed bias seeds and summarise what Barnaby actually did.
#
# WHY THIS EXISTS
# Red Light's feel swings wildly on Barnaby's opening random draw. Two runs of the
# SAME code produced "12 spares, 0 wipeouts" and "0 spares, 6 wipeouts". A single
# before/after run therefore measures the dice, not the change - HANDOFF.md section 6.8:
# change ONE thing and re-run the SAME configuration before believing any result.
#
# Usage:  ./Tests/bias_sweep.sh <label> [rounds] [seeds...]
set -uo pipefail
LABEL="${1:?usage: bias_sweep.sh <label> [rounds] [seeds...]}"
ROUNDS="${2:-4}"
shift 2 2>/dev/null || shift 1
SEEDS=("$@"); [ ${#SEEDS[@]} -eq 0 ] && SEEDS=(101 202 303 404 505)

find_player() {
  for app in "$1"/*.app; do
    [ -d "$app" ] || continue
    for exe in "$app/Contents/MacOS/"*; do [ -x "$exe" ] && { echo "$exe"; return; }; done
  done
}
APP="$(find_player Unity/Build/MacRedLight)"
[ -x "$APP" ] || { echo "FAIL: no player - run ./Tools/build_verified.sh"; exit 1; }

OUT="/tmp/party_sweep/$LABEL"; rm -rf "$OUT"; mkdir -p "$OUT"
pkill -f 'Contents/MacOS/Party' 2>/dev/null; sleep 1

for s in "${SEEDS[@]}"; do
  echo "  seed $s ..." >&2
  "$APP" -batchmode -nographics -partyrole host -partytarget 5 \
         -partyround -partyrounds "$ROUNDS" -partyseconds $((ROUNDS * 80)) \
         -partybiasseed "$s" -partyautopilot -logFile "$OUT/seed_$s.log" >/dev/null 2>&1
done

python3 Tests/sweep_summary.py "$LABEL" "$OUT"
