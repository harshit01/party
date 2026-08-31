#!/usr/bin/env bash
# Red Light, Barnaby: does a round actually have a SHAPE?
#
# WHY THIS EXISTS
# The netcode test proved two processes agree about capsule positions. It said nothing
# about whether the game is playable, so a round that contained ONE Go phase, called
# STOP zero times and ended in under 8 seconds passed everything and shipped. The bug
# was found by a human playing it, which is the expensive way to find it.
#
# The course was 26 units - about 3.7s at the 7 m/s cap - while a single GO window ran
# up to 4.5s, so players reached the line before the first STOP existed. Course length
# is a TIMING decision, and this test pins it down.
set -uo pipefail

# Resolve the executable rather than hardcoding its name: it is derived from Unity's
# productName, which changed to "Party Game" and silently broke this test.
find_player() {
  for app in "$1"/*.app; do
    [ -d "$app" ] || continue
    for exe in "$app/Contents/MacOS/"*; do [ -x "$exe" ] && { echo "$exe"; return; }; done
  done
}
APP="${1:-$(find_player Unity/Build/MacRedLight)}"
LOG=/tmp/party_pacing.log
MIN_STOPS=4          # a round with fewer than this is a dash, not a game
MIN_SECONDS=20       # ditto
rm -f "$LOG"

[ -x "$APP" ] || { echo "FAIL: player not found at $APP"; exit 1; }

# Kill any player left over from a previous suite. Running the suites back to back
# produced "SocketException: Address already in use" and the new client silently
# connected to the OLD host, which invalidated every assertion that followed.
pkill -f 'Contents/MacOS/Party' 2>/dev/null; sleep 1

echo "== running a round (5 participants, autopilot) =="
"$APP" -batchmode -nographics -partyrole host -partytarget 5 -partyround \
       -partyseconds 90 -partyautopilot -logFile "$LOG"

# Unity reports build success even when it has written a corrupt player, so check.
if grep -q 'is corrupted' "$LOG"; then
  echo "FAIL: player data is corrupt - rebuild with the editor CLOSED"; exit 1
fi

fail=0
stops=$(grep -oE 'phase=Stop' "$LOG" | wc -l | tr -d ' ')
gos=$(grep -oE 'phase=Go' "$LOG" | wc -l | tr -d ' ')
# NOTE the leading space: 'nt=' also matches inside 'count=', which made an earlier
# version read the participant count as the round duration and fail a 37s round for
# lasting "5 seconds".
first=$(grep -oE ' nt=[0-9.]+' "$LOG" | head -1 | cut -d= -f2)
last=$(grep -oE ' nt=[0-9.]+' "$LOG" | tail -1 | cut -d= -f2)

# Samples are 0.5s apart, so sample counts stand in for phase duration.
echo "  Go samples   : $gos"
echo "  Stop samples : $stops"
echo "  round span   : ${first:-?}s -> ${last:-?}s"

grep -qE '\[RedLight\] ROUND' "$LOG" || { echo "  FAIL: round never ended"; fail=1; }

if [ "${stops:-0}" -lt "$MIN_STOPS" ]; then
  echo "  FAIL: only $stops Stop samples - Barnaby barely called stop (min $MIN_STOPS)"; fail=1
else
  echo "  OK  STOP is called repeatedly"
fi

if [ -n "${last:-}" ] && [ "${last%%.*}" -lt "$MIN_SECONDS" ]; then
  echo "  FAIL: round lasted ${last}s, under ${MIN_SECONDS}s - no shape to it"; fail=1
else
  echo "  OK  round lasts long enough to have a shape"
fi

echo
grep -E '\[RedLight\] ROUND' "$LOG" | tail -2
echo
[ $fail -eq 0 ] && echo "PASS: the round is a game, not a dash" || echo "RESULT: FAILED"
exit $fail
