#!/usr/bin/env bash
# "Say What He Says" (#10) - FULL regression.
#
# Same four-phase shape as Tests/redlight_regression.sh, for the same reason: this
# project has shipped a minigame whose signature mechanic silently did not fire, because
# the only test measured pacing. A minigame is not "done" until the thing that makes it
# distinctive is checked by a machine.
#
# Phase 0 calibrates the analyser against a known-good and a known-broken fixture and
# REFUSES TO RUN if the broken one comes back green (HANDOFF.md §6.1 - nine "failures"
# in the previous project were bugs in the judge, not the game).
#
# Usage:  ./Tests/saywhat_regression.sh [path-to-player]
set -uo pipefail

ROUNDS=${ROUNDS:-6}
PARTICIPANTS=${PARTICIPANTS:-5}
MIN_SEQ=${MIN_SEQ:-2}
# FIXED SEED BY DEFAULT, so a run is comparable to the last one.
#
# The suite used a fresh random seed every time, which meant a pass could flip to a fail
# with no code change at all: one run of #9 came back 6/6 wipeouts and failed two checks,
# and a same-seed sweep then showed the code was actually slightly BETTER than before.
# Chasing that is exactly how this project produced four confident wrong conclusions
# (HANDOFF.md §6.8). Override with SEED= to explore other draws.
SEED=${SEED:-20260902}

LOGDIR=/tmp/saywhat_regression
mkdir -p "$LOGDIR"

find_player() {
  for app in "$1"/*.app; do
    [ -d "$app" ] || continue
    for exe in "$app/Contents/MacOS/"*; do [ -x "$exe" ] && { echo "$exe"; return; }; done
  done
}
APP="${1:-$(find_player Unity/Build/MacSayWhat)}"
[ -x "$APP" ] || { echo "FAIL: no player at Unity/Build/MacSayWhat - build it with:"; \
  echo "  Unity ... -executeMethod Party.EditorTools.BuildTools.BuildMacSayWhat"; exit 1; }

cleanup() { pkill -f 'Contents/MacOS/Party' 2>/dev/null; }
cleanup; sleep 1
trap cleanup EXIT

fail=0
hdr() { echo; echo "================================================================"; echo "$1"; echo "================================================================"; }

# ---------------------------------------------------------------- 0. calibration
hdr "0/4  CALIBRATION - does the analyser detect a broken session?"
if python3 Tests/saywhat_report.py Tests/fixtures/saywhat_good.log 2 >/dev/null 2>&1; then
  echo "  OK   clean fixture passes"
else
  echo "  FAIL: the analyser rejects a KNOWN-GOOD session - it is over-strict"; exit 1
fi
if python3 Tests/saywhat_report.py Tests/fixtures/saywhat_bad.log 2 >/dev/null 2>&1; then
  echo "  FAIL: the analyser PASSES a known-broken session - it measures nothing"; exit 1
else
  echo "  OK   broken fixture is caught"
fi
# #11 wraps #10 in this scene, so its analyser is calibrated here too.
if python3 Tests/prediction_report.py Tests/fixtures/prediction_good.log >/dev/null 2>&1; then
  echo "  OK   clean Prediction fixture passes"
else
  echo "  FAIL: the Prediction analyser rejects a KNOWN-GOOD session"; exit 1
fi
if python3 Tests/prediction_report.py Tests/fixtures/prediction_bad.log >/dev/null 2>&1; then
  echo "  FAIL: the Prediction analyser PASSES a known-broken session"; exit 1
else
  echo "  OK   broken Prediction fixture is caught"
fi

# ---------------------------------------------------------------- 1. artifact
hdr "1/4  ARTIFACT - does the player actually boot?"
SMOKE="$LOGDIR/smoke.log"; rm -f "$SMOKE"
"$APP" -batchmode -nographics -partyrole none -partyseconds 6 -logFile "$SMOKE" >/dev/null 2>&1
grep -q 'is corrupted' "$SMOKE" && { echo "  FAIL: level0 corrupt"; exit 1; }
grep -q '\[AutoRun\]' "$SMOKE" || { echo "  FAIL: scene loaded no Party scripts"; exit 1; }
echo "  OK   player boots, scene loads"

# ---------------------------------------------------------------- 2. session
# AI host OFFLINE deliberately: "fun must survive the AI being removed" is a decision in
# HANDOFF.md §2, not an aspiration, so it is the DEFAULT case under test.
hdr "2/4  SESSION - $ROUNDS rounds, $PARTICIPANTS participants, AI host OFFLINE"
if curl -s -m 2 http://127.0.0.1:8790/health >/dev/null 2>&1; then AI_UP=1; else AI_UP=0; fi
SESSION="$LOGDIR/session.log"; rm -f "$SESSION"
echo "  running..."
"$APP" -batchmode -nographics -partyrole host -partytarget "$PARTICIPANTS" \
       -partyround -partyrounds "$ROUNDS" -partybiasseed "$SEED" -partyseconds $((ROUNDS * 90)) \
       -partyautopilot -logFile "$SESSION" >/dev/null 2>&1
echo
python3 Tests/saywhat_report.py "$SESSION" "$MIN_SEQ" || fail=1

# #11 rides on the same session: it wraps the minigame, so one run tests both.
echo
echo "  --- #11 THE PREDICTION, wrapping the same rounds ---"
python3 Tests/prediction_report.py "$SESSION" | sed 's/^/  /' || fail=1

if [ "$AI_UP" = "0" ]; then
  echo
  grep -q 'host service unavailable' "$SESSION" \
    && echo "  OK   AI outage is reported LOUDLY (not swallowed)" \
    || { echo "  FAIL: service was down and HostVoice never said so"; fail=1; }
  grep -q '\[SayWhat\] ROUND' "$SESSION" \
    && echo "  OK   the game is fully playable with the AI switched off" \
    || { echo "  FAIL: no rounds completed without the AI"; fail=1; }
fi

# ---------------------------------------------------------------- 3. networked
hdr "3/4  NETWORKED - a second machine performs, not just watches"
NHOST="$LOGDIR/net_host.log"; NCLI="$LOGDIR/net_client.log"; rm -f "$NHOST" "$NCLI"
cleanup; sleep 1
"$APP" -batchmode -nographics -partyrole host -partytarget 4 \
       -partyround -partyrounds 3 -partybiasseed "$SEED" -partyseconds 260 \
       -partyautopilot -logFile "$NHOST" >/dev/null 2>&1 & HPID=$!
sleep 8
echo "  client joining..."
"$APP" -batchmode -nographics -partyrole client -partyaddress localhost \
       -partyseconds 170 -partyautopilot -logFile "$NCLI" >/dev/null 2>&1 & CPID=$!
wait $CPID 2>/dev/null
kill $HPID 2>/dev/null; wait $HPID 2>/dev/null

CLI_ID=$(grep -oE 'Player [0-9]+' "$NCLI" | head -1)
if [ -z "$CLI_ID" ]; then
  echo "  FAIL: client never got a player object"; fail=1
else
  echo "  client is '$CLI_ID'"
  for want in Watch Perform; do
    grep -q "phase=$want" "$NCLI" \
      && echo "  OK   client sees phase=$want" \
      || { echo "  FAIL: client never saw phase=$want"; fail=1; }
  done
  # The remote player's actions must REACH the host, or they are judged on silence.
  subs=$(grep -cE "$CLI_ID (out|.*SPARED)" "$NHOST" 2>/dev/null | tr -d ' ')
  rounds_net=$(grep -c '\[SayWhat\] ROUND' "$NHOST" | tr -d ' ')
  echo "  rounds completed with a client attached: $rounds_net"
  [ "${rounds_net:-0}" -ge 2 ] || { echo "  FAIL: networked session did not complete rounds"; fail=1; }
  echo
  echo "  --- supplementary: same analysis over the NETWORKED session ---"
  echo "  (not a gate: 3 rounds is a small sample for the probabilistic checks)"
  python3 Tests/saywhat_report.py "$NHOST" "$MIN_SEQ" 2>&1 | sed 's/^/  /'
fi

# ---------------------------------------------------------------- 4. AI online
hdr "4/4  AI HOST ONLINE - real commentary path"
OWN=0
if ! curl -s -m 2 http://127.0.0.1:8790/health >/dev/null 2>&1; then
  echo "  starting hostserver.py..."
  zsh -lc 'source ~/.zshrc >/dev/null 2>&1; exec .venv/bin/python Server/hostserver.py' \
      > "$LOGDIR/hostserver.log" 2>&1 &
  OWN=$!
  for i in $(seq 1 15); do curl -s -m 2 http://127.0.0.1:8790/health >/dev/null 2>&1 && break; sleep 1; done
fi

if curl -s -m 2 http://127.0.0.1:8790/health >/dev/null 2>&1; then
  AILOG="$LOGDIR/ai.log"; rm -f "$AILOG"
  cleanup; sleep 1
  "$APP" -batchmode -nographics -partyrole host -partytarget 4 \
         -partyround -partyrounds 2 -partybiasseed "$SEED" -partyseconds 200 \
         -partyautopilot -logFile "$AILOG" >/dev/null 2>&1
  if grep -q 'host service unavailable' "$AILOG"; then
    echo "  FAIL: service was up but HostVoice could not reach it:"
    grep -m2 'host service unavailable' "$AILOG" | sed 's/^/        /'; fail=1
  else
    echo "  OK   every host call succeeded (no fallback to the scripted stand-in)"
  fi
  ai_rounds=$(grep -c '\[SayWhat\] ROUND' "$AILOG" | tr -d ' ')
  echo "  rounds completed with AI attached: $ai_rounds"
  [ "${ai_rounds:-0}" -ge 2 ] || { echo "  FAIL: rounds did not complete with the AI"; fail=1; }
else
  echo "  SKIPPED - could not reach hostserver.py; see $LOGDIR/hostserver.log"
fi
[ "$OWN" != "0" ] && kill "$OWN" 2>/dev/null

hdr "$([ $fail -eq 0 ] && echo 'PASS - Say What He Says is intact' || echo 'RESULT: FAILED')"
echo "logs: $LOGDIR"
exit $fail
