#!/usr/bin/env bash
# Red Light, Barnaby - FULL regression.
#
# WHY THIS EXISTS
# round_pacing_test.sh checked that a round had a shape: STOP got called a few times and
# it lasted more than 20 seconds. That is a pacing test, and it was written as one. It
# says nothing about whether the game still WORKS - and every distinctive thing about
# this minigame lives in the parts it does not touch:
#
#   * eliminations                      - the only consequence in the game
#   * Barnaby SPARING his favourites    - MINIGAMES.md #9, the signature
#   * Barnaby FRAMING his grudges       - ditto
#   * affinity PERSISTING across rounds - "he remembers who annoyed him three rounds ago"
#   * every round resolving             - a straggler once hung a round for 90s
#   * hazards freezing during STOP      - once wiped the whole lobby every round
#   * the game surviving the AI's absence - the standing rule that fun must not depend on it
#
# HANDOFF.md section 6.1: a test that passes everything measures nothing.
#
# Usage:  ./Tests/redlight_regression.sh [path-to-player]
set -uo pipefail

ROUNDS=${ROUNDS:-6}
PARTICIPANTS=${PARTICIPANTS:-5}
MIN_STOPS=${MIN_STOPS:-4}
LOGDIR=/tmp/party_regression
mkdir -p "$LOGDIR"

find_player() {
  for app in "$1"/*.app; do
    [ -d "$app" ] || continue
    for exe in "$app/Contents/MacOS/"*; do [ -x "$exe" ] && { echo "$exe"; return; }; done
  done
}
APP="${1:-$(find_player Unity/Build/MacRedLight)}"
[ -x "$APP" ] || { echo "FAIL: no player at Unity/Build/MacRedLight - run ./Tools/build_verified.sh"; exit 1; }

# Leftover players from an earlier suite bind the port and the next client silently
# connects to the OLD host, invalidating everything after it.
cleanup() { pkill -f 'Contents/MacOS/Party' 2>/dev/null; }
cleanup; sleep 1
trap cleanup EXIT

suite_fail=0
hdr() { echo; echo "================================================================"; echo "$1"; echo "================================================================"; }

# ---------------------------------------------------------------- 0. calibration
# CHECK THE INSTRUMENT BEFORE TRUSTING THE MEASUREMENT (HANDOFF.md section 6.1 and 6.8).
# Nine "failures" in the previous project were bugs in the judge, not in the game. So
# the analyser is run first against two fixtures: one clean session it must pass, and
# one containing known defects it must catch. If the known-bad log comes back green,
# the analyser is measuring nothing and every result below it is worthless.
hdr "0/4  CALIBRATION - does the analyser still detect a broken session?"
if python3 Tests/redlight_report.py Tests/fixtures/session_good.log 4 >/dev/null 2>&1; then
  echo "  OK   clean fixture passes"
else
  echo "  FAIL: the analyser rejects a KNOWN-GOOD session - it is over-strict"; exit 1
fi
if python3 Tests/redlight_report.py Tests/fixtures/session_bad.log 4 >/dev/null 2>&1; then
  echo "  FAIL: the analyser PASSES a known-broken session - it measures nothing"; exit 1
else
  echo "  OK   broken fixture is caught"
fi

# ---------------------------------------------------------------- 1. artifact
hdr "1/4  ARTIFACT - does the player actually boot?"
SMOKE="$LOGDIR/smoke.log"; rm -f "$SMOKE"
"$APP" -batchmode -nographics -partyrole none -partyseconds 6 -logFile "$SMOKE" >/dev/null 2>&1
if grep -q 'is corrupted' "$SMOKE"; then
  echo "  FAIL: level0 corrupt - rebuild with ./Tools/build_verified.sh (Unity CLOSED)"; exit 1
fi
echo "  OK   player boots, scene loads"

# ---------------------------------------------------------------- 2. session, AI down
# Deliberately with the host service DOWN. HostVoice must complain loudly and the game
# must carry on regardless - "fun must survive the AI being removed" is a decision in
# HANDOFF.md section 2, not an aspiration, so it is tested as the DEFAULT case.
hdr "2/4  SESSION - $ROUNDS rounds, $PARTICIPANTS participants, AI host OFFLINE"
if curl -s -m 2 http://127.0.0.1:8790/health >/dev/null 2>&1; then
  echo "  NOTE: host service is up; this phase wanted it down. Results still valid," \
       "but the degradation check is skipped."
  AI_WAS_UP=1
else
  AI_WAS_UP=0
fi
SESSION="$LOGDIR/session.log"; rm -f "$SESSION"
echo "  running (up to ~$((ROUNDS * 75))s)..."
"$APP" -batchmode -nographics -partyrole host -partytarget "$PARTICIPANTS" \
       -partyround -partyrounds "$ROUNDS" -partyseconds $((ROUNDS * 80)) \
       -partyautopilot -logFile "$SESSION" >/dev/null 2>&1
echo
python3 Tests/redlight_report.py "$SESSION" "$MIN_STOPS" || suite_fail=1

echo
if [ "$AI_WAS_UP" = "0" ]; then
  if grep -q 'host service unavailable' "$SESSION"; then
    echo "  OK   AI outage is reported LOUDLY (not swallowed)"
  else
    echo "  FAIL: host service was down and HostVoice never said so"; suite_fail=1
  fi
  if grep -q '\[RedLight\] ROUND' "$SESSION"; then
    echo "  OK   the game is fully playable with the AI switched off"
  else
    echo "  FAIL: no rounds completed without the AI"; suite_fail=1
  fi
fi

# ---------------------------------------------------------------- 3. networked
hdr "3/4  NETWORKED - a second machine plays the round, not just watches"
NHOST="$LOGDIR/net_host.log"; NCLI="$LOGDIR/net_client.log"; rm -f "$NHOST" "$NCLI"
cleanup; sleep 1
"$APP" -batchmode -nographics -partyrole host -partytarget 4 \
       -partyround -partyrounds 3 -partyseconds 240 \
       -partyautopilot -logFile "$NHOST" >/dev/null 2>&1 & HPID=$!
sleep 8
echo "  client joining..."
"$APP" -batchmode -nographics -partyrole client -partyaddress localhost \
       -partyseconds 150 -partyautopilot -logFile "$NCLI" >/dev/null 2>&1 & CPID=$!
wait $CPID 2>/dev/null
kill $HPID 2>/dev/null; wait $HPID 2>/dev/null

CLI_ID=$(grep -oE 'Player [0-9]+' "$NCLI" | head -1)
if [ -z "$CLI_ID" ]; then
  echo "  FAIL: client never got a player object"; suite_fail=1
else
  echo "  client is '$CLI_ID'"
  # The client must see the ROUND, not just the world: phases have to replicate.
  phases=$(grep -oE 'phase=[A-Za-z]+' "$NCLI" | sort -u | tr '\n' ' ')
  echo "  phases seen by client: $phases"
  for want in Go Stop Countdown; do
    if grep -q "phase=$want" "$NCLI"; then echo "  OK   client sees phase=$want"
    else echo "  FAIL: client never saw phase=$want"; suite_fail=1; fi
  done

  # The client's OWN capsule must move - input -> CmdMove -> host -> back.
  posns=$(grep '\[CENSUS\]' "$NCLI" | grep -oE "$CLI_ID\(human\) -?[0-9]+\.[0-9]+,-?[0-9]+\.[0-9]+" \
          | sed 's/.*) //' | sort -u | wc -l | tr -d ' ')
  echo "  client capsule distinct positions: $posns"
  if [ "${posns:-0}" -ge 3 ]; then echo "  OK   remote input round-trips during a real round"
  else echo "  FAIL: client capsule never moved"; suite_fail=1; fi

  # GRACE WINDOW. The remote player renders behind the host; if the grace window were
  # broken they would be eliminated on essentially every STOP, for lag rather than for
  # anything they did. Barnaby is allowed to be unfair. The network is not.
  cli_outs=$(grep -c "\[RedLight\] $CLI_ID out" "$NHOST" 2>/dev/null | tr -d ' ')
  stops_total=$(grep -c 'phase=Stop' "$NHOST" 2>/dev/null | tr -d ' ')
  echo "  client eliminated $cli_outs time(s) across 3 rounds"
  if [ "${cli_outs:-0}" -le 3 ]; then echo "  OK   remote player is not being punished for latency"
  else echo "  FAIL: client out $cli_outs times - grace window looks broken"; suite_fail=1; fi

  rounds_net=$(grep -c '\[RedLight\] ROUND' "$NHOST" | tr -d ' ')
  echo "  rounds completed with a client attached: $rounds_net"
  [ "${rounds_net:-0}" -ge 2 ] || { echo "  FAIL: networked session did not complete rounds"; suite_fail=1; }
  echo
  echo "  --- supplementary: same analysis over the NETWORKED session ---"
  echo "  (not a gate: 3 rounds is too small a sample for the probabilistic checks)"
  python3 Tests/redlight_report.py "$NHOST" "$MIN_STOPS" 2>&1 | sed 's/^/  /'
fi

# ---------------------------------------------------------------- 4. AI online
hdr "4/4  AI HOST ONLINE - real commentary path"
OWN_SERVER=0
if ! curl -s -m 2 http://127.0.0.1:8790/health >/dev/null 2>&1; then
  # Start it ourselves so this phase is not silently skipped. Phase 2 deliberately
  # needs the service DOWN and this one needs it UP, so the suite owns the lifecycle
  # rather than depending on whatever happened to be running.
  echo "  starting hostserver.py..."
  zsh -lc 'source ~/.zshrc >/dev/null 2>&1; exec .venv/bin/python Server/hostserver.py' \
      > "$LOGDIR/hostserver.log" 2>&1 &
  OWN_SERVER=$!
  for i in $(seq 1 15); do
    curl -s -m 2 http://127.0.0.1:8790/health >/dev/null 2>&1 && break
    sleep 1
  done
fi

if curl -s -m 2 http://127.0.0.1:8790/health >/dev/null 2>&1; then
  AILOG="$LOGDIR/ai.log"; rm -f "$AILOG"
  cleanup; sleep 1
  echo "  running 2 rounds against the live host service..."
  "$APP" -batchmode -nographics -partyrole host -partytarget 4 \
         -partyround -partyrounds 2 -partyseconds 180 \
         -partyautopilot -logFile "$AILOG" >/dev/null 2>&1
  if grep -q 'host service unavailable' "$AILOG"; then
    echo "  FAIL: service was up but HostVoice could not reach it:"
    grep -m2 'host service unavailable' "$AILOG" | sed 's/^/        /'
    suite_fail=1
  else
    echo "  OK   every host call succeeded (no fallback to the scripted stand-in)"
  fi
  ai_rounds=$(grep -c '\[RedLight\] ROUND' "$AILOG" | tr -d ' ')
  echo "  rounds completed with AI attached: $ai_rounds"
  [ "${ai_rounds:-0}" -ge 2 ] || { echo "  FAIL: rounds did not complete with the AI attached"; suite_fail=1; }
  echo
  echo "  --- supplementary: same analysis over the AI session ---"
  python3 Tests/redlight_report.py "$AILOG" "$MIN_STOPS" 2>&1 | sed 's/^/  /'
else
  echo "  SKIPPED - could not reach hostserver.py on 127.0.0.1:8790"
  echo "  it needs OPENAI_API_KEY; see $LOGDIR/hostserver.log"
  tail -3 "$LOGDIR/hostserver.log" 2>/dev/null | sed 's/^/        /'
fi
[ "${OWN_SERVER:-0}" != "0" ] && kill "$OWN_SERVER" 2>/dev/null

hdr "$([ $suite_fail -eq 0 ] && echo 'PASS - Red Light, Barnaby is intact' || echo 'RESULT: FAILED')"
echo "logs: $LOGDIR"
exit $suite_fail
