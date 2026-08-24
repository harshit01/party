#!/usr/bin/env bash
# Netcode milestone, stage A: prove two processes share one authoritative world.
#
# Runs a headless host and a headless client and checks four things that a weaker
# test would miss:
#   1. both ends list the SAME participants by name - not merely the same count
#   2. bots exist on the client (spawned objects with no owning connection arrive)
#   3. the client's OWN capsule moves - proving input -> CmdMove -> host -> sync,
#      which is the actual risky path
#   4. positions change over time on the client
#
# Check 1 exists because an earlier version of this script compared counts only and
# reported PASS while the two logs disagreed. A test that passes everything measures
# nothing (HANDOFF.md section 6.1).
#
# Both processes run with -partyautopilot: headless builds have no keyboard, so
# without it a human-slot capsule never moves and a broken input path looks fine.
set -uo pipefail

APP="${1:-Unity/Build/Mac/Party.app/Contents/MacOS/Party}"
HOST_LOG=/tmp/party_host.log
CLI_LOG=/tmp/party_client.log
rm -f "$HOST_LOG" "$CLI_LOG"

[ -x "$APP" ] || { echo "FAIL: player not found at $APP"; exit 1; }

# Kill any player left over from a previous suite. Running the suites back to back
# produced "SocketException: Address already in use" and the new client silently
# connected to the OLD host, which invalidated every assertion that followed.
pkill -f 'Party.app/Contents/MacOS/Party' 2>/dev/null; sleep 1

echo "== host: 3 participants (1 human slot + 2 bots), autopilot on =="
"$APP" -batchmode -nographics -partyrole host -partytarget 3 -partyseconds 34 \
       -partyautopilot -logFile "$HOST_LOG" &
HOST_PID=$!
sleep 10

echo "== client: joining, autopilot on =="
"$APP" -batchmode -nographics -partyrole client -partyaddress localhost -partyseconds 18 \
       -partyautopilot -logFile "$CLI_LOG" &
CLI_PID=$!
wait $CLI_PID 2>/dev/null
kill $HOST_PID 2>/dev/null; wait $HOST_PID 2>/dev/null

# Roster = the sorted set of participant names in a census line.
roster() { sed -E 's/^.*count=[0-9]+ ?//; s/ -?[0-9]+\.[0-9]+,-?[0-9]+\.[0-9]+//g; s/ \| /\n/g' <<<"$1" | sed 's/^| //' | sort | tr '\n' ' '; }

fail=0
say() { echo "  $1"; }

echo
echo "== checks =="

# The client's last full census, and the host census taken while the client was on.
CLI_LAST=$(grep '\[CENSUS\]' "$CLI_LOG" | tail -1)
[ -n "$CLI_LAST" ] || { say "FAIL: client produced no census"; fail=1; }

# Host lines that mention the client's player id => host and client overlapped.
CLI_ID=$(grep -oE 'Player [0-9]{4,}' "$CLI_LOG" | head -1)
HOST_MATCH=$(grep '\[CENSUS\]' "$HOST_LOG" | grep -F "$CLI_ID" | tail -1)

if [ -n "$CLI_ID" ] && [ -n "$HOST_MATCH" ]; then
  rh=$(roster "$HOST_MATCH"); rc=$(roster "$(grep '\[CENSUS\]' "$CLI_LOG" | grep -F "$CLI_ID" | tail -1)")
  say "host   roster: $rh"
  say "client roster: $rc"
  [ "$rh" = "$rc" ] && say "OK  rosters identical" || { say "FAIL: rosters differ"; fail=1; }
else
  say "FAIL: could not find an overlapping host/client census"; fail=1
fi

grep -q '(bot)' "$CLI_LOG" && say "OK  client sees bots" || { say "FAIL: client sees no bots"; fail=1; }

# The client's own capsule must move - this is the input path under test.
if [ -n "$CLI_ID" ]; then
  posns=$(grep '\[CENSUS\]' "$CLI_LOG" | grep -oE "$CLI_ID\(human\) -?[0-9]+\.[0-9]+,-?[0-9]+\.[0-9]+" \
          | sed "s/.*) //" | sort -u | wc -l | tr -d ' ')
  say "client's own capsule distinct positions: $posns"
  [ "$posns" -ge 3 ] && say "OK  client input reaches the host and comes back" \
                     || { say "FAIL: client capsule never moved - input path broken"; fail=1; }
fi

# 5. Positional agreement at matched NETWORK times - the "in sync" in the milestone.
echo
echo "== positional agreement =="
python3 Tests/compare_census.py "$HOST_LOG" "$CLI_LOG" || fail=1

echo
[ $fail -eq 0 ] && echo "PASS: two processes, one authoritative world, client input round-tripping, positions agree" \
                || echo "RESULT: FAILED"
exit $fail
