# Red Light, Barnaby — measured behaviour

Evidence for how the minigame actually plays, so tuning arguments are settled with
numbers rather than impressions. Regenerate any of this with:

```bash
./Tests/bias_sweep.sh <label> 4 101 202 303 404 505   # 5 fixed seeds, 4 rounds each
./Tests/redlight_regression.sh                        # the full suite
```

**Always compare fixed seeds.** Two runs of *identical* code produced "12 spares, 0
wipeouts" and "0 spares, 6 wipeouts". A single before/after run measures the dice, not
the change (HANDOFF §6.8). `-partybiasseed N` fixes the draw; the seed is logged on
every run either way, so a session a playtester calls bad can be replayed exactly.

---

## Bias rework — 20 rounds per side, same five seeds

| metric | before | after |
|---|---|---|
| spares | 23 | **49** |
| spares per round | 1.15 | **2.45** |
| frames | 10 | 7 |
| frames per round | 0.50 | 0.35 |
| eliminations | 89 | 91 |
| reached the line | 1 | 2 |
| timeouts | 7 | 7 |
| **wipeout rate** | 12/20 (60%) | **11/20 (55%)** |

Sparing roughly doubled and now fires on every seed, so Barnaby visibly has favourites.
Framing became rarer and no longer fixates on one victim (worst case was 6/6 rounds
before the fix, 2/6 after).

**The wipeout rate did not move, and that is the real finding.** It was never a bias
problem.

## What actually eliminates people

Across the 20-round "after" sweep:

```
84 out (moved
 7 out (framed
```

Barnaby causes **7 of 91** eliminations. The other 84 are bots twitching during STOP,
so no amount of bias tuning will change how often a round wipes out.

## The twitch is not rare — same bug shape as the framing one

`RedLightBotInput` rolls, **every frame** of a stop:

```csharp
if (_twitchingThisStop && Random.value < 0.02f) return new Vector2(0f, 0.5f);
```

A stop lasts 1.5–2.6s. At 60fps that is 90–156 rolls, so a "twitching" stop produces
movement with probability `1 - 0.98^120 ≈ 91%` — not a flinch, a near-certainty.

With `_twitchOdds` of 0.05–0.30 per bot, the per-stop catch chance is ~0.045–0.27
(mean ~0.16). Rounds run to 15 stops, so `1 - 0.84^15 ≈ 93%` — which matches the
observed ~4.5 of 5 participants eliminated per round.

This is the **same defect as the per-frame `WouldFrame` roll** that made one player the
victim of all six rounds: a per-frame probability inside a multi-second window turns
"occasionally" into "always". The fix has the same shape — decide once per stop.

Observed stop counts per round are bimodal: 1–5 (everyone died early) or 14–16.

## Open

- Bot twitch: decide once per stop rather than per frame. Not yet done — it changes
  difficulty, which is a design call.
- Whether >50% of rounds ending with nobody reaching the line is acceptable. A party
  minigame arguably wants a winner most nights.
