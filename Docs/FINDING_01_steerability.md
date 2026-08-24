# Finding 01 — the steerability bar (first VALID measurement)

## What broke first (and why 3 earlier runs were meaningless)
`response_format=json_object` requires the literal word "json" in the messages or
the API 400s. Neither the ban-list prompt nor the audience prompt had it, and a bare
`except: return ""` swallowed every failure. Result: ban lists never generated
(extractor was only forbidden the exact word) and **the audience never ran at all**.
Three runs reported "clean wins" from a test that was not executing.

Fixes: "json" in both prompts; failures now print loudly; instruments calibrated
before use (audience catches 3/5 obvious clues; ban lists now block the whole
giveaway cluster - penguin blocks antarctica/bird/flightless/ice/waddle).

## The valid result — 3 characters x 3 rounds, gpt-4o both sides
| outcome | count |
|---|---|
| CLEAN WIN (landed it, audience never guessed) | **0 / 9** |
| RUMBLED (audience read the intent) | **8 / 9** |
| FAILED (never landed it) | 1 / 9 |

Landing the word is easy: 8/9, median ~2 turns, even with real ban lists.
**Hiding the intent is essentially impossible: 0/9.** Audience often called it on turn 1.

## What this means
Steering the character toward a word requires clues strong enough to work - and any
clue strong enough to work is strong enough for an attentive watcher to read. That
is structural, not a tuning problem. **"Make it say the word without anyone guessing"
does not work as designed.**

## The reframe the data DOES support
The numbers are a race, not a stealth problem:
- extractor lands the word in ~1-3 turns
- audience locks a guess in ~1-2 turns

Those are close. So the game is **"can you land it before they call it"**, not
"can you hide it". Watchers guessing is not failure - it is them scoring, and it
puts both sides on the same clock.

## Caveat on the instrument
A gpt-4o audience reads a full transcript with perfect attention. Real players are
half-listening, talking over each other, and racing. The true difficulty sits
between the two broken measurements, and only humans can locate it.

## Status
Mechanic: WORKS. Original scoring premise: DISPROVEN. Race reframe: UNTESTED.
