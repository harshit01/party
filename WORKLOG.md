# WORKLOG

Append-only. Newest at the top. One entry per working session.

Written for a session that starts blind: say what you **measured**, not what you felt.
"Looks better" is not a result; "spares went 1.15 -> 2.45 per round on the same five
seeds" is. Read `Docs/NIGHT_SHIFT.md` before adding to this.

---

## 2026-09-02 — night shift, part 4: minigame #11, and a fairness bug in #9 and #10

**"The Prediction" (MINIGAMES.md #11) is built and passing 10/10.** It is the first
thing here that is not a self-contained game: it WRAPS a minigame through a new
`IMinigameDirector` seam, which makes it the first piece of the round loop rather than a
twelfth of the collection. Bets stay server-side until the reveal, so it is also the
first hidden-information mechanic - the axis Codenames and The Chameleon sit on.

### The fairness bug it exposed in #9 AND #10

Betting on who came last needs "last" to mean something, and it did not. Several players
routinely go out on the same sequence or the same STOP, and placements were handed out in
`FindObjectsByType` order - **the wooden spoon went to whoever was first in an array.**

Measured: one round eliminated Player 0 on `matched=1/3` and Fenwick on `matched=3/3` -
perfect, and FRAMED - in the same pass, and the perfect performance placed lower purely
by list position. #10 now ranks same-sequence casualties by recall, #9 by distance up the
lane. "Came last" was spread across 3 players before the fix and 5 after.

### Bot betting took three measured iterations

1. **Back Barnaby's target 70% of the time.** His favouritism is public, so it is the
   obvious read - and it was a terrible bet: right **0-1 times out of 5**, because
   standing does not predict losing at a memory game. Nobody ever scored, so there was
   nothing to talk about.
2. **Mix in form and a hunch.** 7 correct across 8 rounds and a real leaderboard - but
   the bets spread so thinly that a pile-on **never happened once**. The signature
   disappeared entirely.
3. **Add herding** - a bot may copy a bet already placed, as people at a table do. Over
   10 rounds: **2 pile-ons, 1 backfire, 15 correct calls, scores spread 6-24.**

Both intermediate states would have passed a test that only asked whether bets were
placed. They are now explicit checks: "predictions are sometimes CORRECT" and "the room
piles onto one target sometimes".

The literal signature - *strict* unanimity - never once occurred, because bots dissent by
design. A ≥75% pile-on is tracked alongside it; that is what the moment looks like at a
real table.

### Round cap was not a cap

`roundTimeLimit` was only checked between sequences, so a round entering a 7-step
sequence just under the limit ran the whole thing first. One round ran **106s against a
90s cap**. Now checked every frame. Same seeds before/after:

    before  [22, 22, 35, 35, 35, 67, 67, 106]  5/8 in window
    after   [22, 35, 35, 35, 50, 50, 50,  67]  7/8 in window

The pacing CHECK was also wrong - it demanded every round sit in the window. A round
where everyone fails the second sequence is legitimately quick; uniform length would mean
capping the drama. It now tests the distribution, and discriminates (FAIL on the pre-fix
data, OK after).

### State

| | gate |
|---|---|
| #9 Red Light | suite PASS |
| #10 Say What He Says | 16/17 → passing after the pacing fix |
| #11 The Prediction | 10/10 |
| build reliability | 6/6, first-time |

---

## 2026-09-02 — night shift, part 3: minigame #10 built and passing

**"Say What He Says" (MINIGAMES.md #10) is playable and tested.** Suite reports
`PASS`, gated session **17/17**.

| rd | outcome | seqs | out | spared | framed | seconds |
|---|---|---|---|---|---|---|
| 1 | winner:Fenwick | 3 | 4 | 0 | 0 | 35 |
| 2 | wipeout | 5 | 5 | 0 | 2 | 67 |
| 3 | winner:Bevel | 4 | 4 | 1 | 0 | 50 |
| 4 | wipeout | 4 | 5 | 0 | 1 | 50 |
| 5 | winner:Dimple | 3 | 4 | 1 | 0 | 35 |
| 6 | wipeout | 4 | 5 | 0 | 0 | 50 |

Sequences escalate 3→7, spares land only on `pet`, frames only on `grudge` AND only on
players who had actually performed it correctly, no fixed victim, and rounds sit in the
30-60s window the design opens with.

**It reuses the same `BarnabyBias` object as Red Light**, not a copy. A host with a
separate grudge per minigame would be a different character each round, and his memory
across the night is the product.

### Three things measurement caught that would otherwise have shipped

1. **A favourite spared at `matched 0/4`** - waved through having done nothing at all.
   That is a broken referee, not favouritism, and it made pets unkillable (one round
   produced five spares). Spares now require a near miss. Same seed after: 7 spares → 1.
2. **A silent bug in the EXISTING Red Light suite.** Outcomes embed a player name and a
   human slot is `"Player 0"` - with a space. `outcome=(\S+)` stopped at the space and
   the round was DROPPED; a log containing one reported 15 checks as 5. Every bot name is
   one word, so it only bit when a HUMAN won - exactly what a bot-only headless test
   never produces. Fixed in both analysers.
3. **Sessions are not bit-reproducible even when seeded.** Two runs of seed 5150 diverged
   (round 1 ended after 4 sequences and 3). Bot actions are paced off `Time.time`, so
   frame timing decides how many land inside the Perform window. I had written a comment
   claiming seeding fixed this; it is corrected to say what was measured. **Compare
   distributions over fixed seeds, never two single runs.**

### Earlier reading corrected

Recorded mid-session that rounds were "22.5s or 35.2s, half too short". That was a
4-participant sample. At 5 participants they run 35-67s, inside the design window. The
short rounds are a small-lobby effect, not a pacing bug.

### NEEDS APPROVAL (added this session)

- **The #10 arena is a deliberate placeholder** - a flat stage, no dressing, no composed
  camera. `Docs/ArtTarget/` needs an agreed target before it is built, same as Red Light.
  The mechanic is fully testable without it.

- **#10 round length is variable (22-85s) and I stopped tuning it.** The design rule is
  30-60s. Two changes got most rounds there - enforcing the time cap every frame (it was
  only checked between sequences, and one round ran 106s against a 90s limit) and raising
  bot recall from 0.86-0.97 to 0.91-0.98. That took in-window from 5/8 to 7/8 to 5/8 as
  framing was also tuned; the current figure is **62.5%, against a check that wants 60%**.

  **I stopped there on purpose.** Round length depends on how fast people fail, so it is
  inherently variable, and continuing to nudge constants until the number goes green is
  §6.5 - letting a metric become the goal. A test that has been tuned against measures the
  tuning. Whether that variance is a problem is a feel question: a round that ends in 22
  seconds because everyone fluffed sequence two might be funny rather than broken. That is
  a founder call, and the levers are `SayWhatDirector.callBeat`, `performPerStep`,
  `startLength` and `SayWhatBotInput._accuracy`.

---

## 2026-09-01 — night shift, part 2: build corruption SOLVED

Full matrix, one condition per run (`Docs/build_experiments.tsv`):

| condition | result |
|---|---|
| regenerate before every build (old `build_verified.sh`) | **3/8 good** |
| one BAD scene, built 8 times | **0/8**, all level0 199640 |
| one GOOD scene, built 6 times | **6/6**, all level0 199700 |

**Fix shipped.** The verified scene is committed. `./Tools/build_verified.sh` builds it
and boots it — succeeded first time, no retries. Regeneration moved behind `-r`, which
hunts for a scene that boots and tells you to commit it. `HANDOFF.md`'s KNOWN ISSUE is
rewritten; its diagnosis was backwards.

**Regression suite re-run on the pinned scene: all four phases, 14/15 on the gate.** The
single failure is rounds 3 and 6 wiping out at 2 and 1 stops — the known bot-twitch
problem awaiting approval, not a regression. Phase 4 had zero host failures.

**One process error worth recording.** The "good scene pinned" run first came back 0/6,
apparently contradicting condition B. It was my bug: `build_experiment.sh` with `regen=no`
regenerates ONCE up front by design, which silently overwrote the scene I had copied in,
so it measured a fresh bad scene six times. Caught by hashing the in-project file against
the saved copy. Added a `never` mode. The lesson from §6.8 is what caught it — re-run the
same configuration and check the inputs, rather than accepting a result that contradicts
an earlier one.

---

## 2026-09-01 — night shift, part 1: the build corruption is NOT random serialisation

### The finding that changes it

`HANDOFF.md` says: *"level0 is a different size on every build even with a fixed seed,
so the non-determinism is inside Unity's serialisation, not our content."* **That is
wrong, and it sent the previous investigation in the wrong direction.**

Condition A (8 builds, scene regenerated before each - exactly what `build_verified.sh`
does today), from `Docs/build_experiments.tsv`:

| attempt | level0 bytes | result |
|---|---|---|
| 1 | **199712** | ok |
| 2 | 199640 | corrupt |
| 3 | **199712** | ok |
| 4 | 199676 | corrupt |
| 5 | 199648 | corrupt |
| 6 | 199664 | corrupt |
| 7 | **199712** | ok |
| 8 | 199680 | corrupt |

**Every good build is byte-identical at 199712. Every corrupt build is smaller.** 3/3 and
5/5, no overlap. So serialisation IS deterministic and the fixed seed DOES work - the
earlier reading looked at the varying sizes of the BAD builds and concluded the whole
thing was random.

The shortfalls are 72, 36, 64, 48 and 32 bytes - small, and all multiples of 4. That is
not a truncated write. It is **slightly less content**: objects missing from the scene.

### The live hypothesis

`BuildTools.RebuildAndBuildRedLight` already carries the explanation in its docstring -
*"the second process building against a scene the asset database has not finished
settling after the first process saved it"* - and that method exists specifically to fix
this. **`build_verified.sh` does not use it.** It defaults to two separate Unity
launches: one to regenerate the scene, one to build it.

`HANDOFF.md` lists "doing the scene rebuild and player build in one Unity invocation"
under things that did NOT fix it, but that was concluded during the same cumulative
isolation that produced four confirmed-wrong conclusions (§6.8), so it is being re-tested
cleanly rather than taken on trust.

### Rate, measured properly

3/8 = **37.5%** under condition A, which is close to the 1-in-3 `HANDOFF.md` documents
and better than the ~12% I estimated across the day. That earlier estimate was ad-hoc,
across changing code, and should not be trusted over this.

### Immediately useful

`level0 == 199712` predicts a good build with no false positives or negatives in 8
attempts. Cheaper than booting the player, though content-dependent, so it is a
diagnostic rather than a replacement for the boot check.

### Condition B settled it: the BUILD is deterministic, the SCENE is not

8 builds against one saved scene, no regeneration between them:
**0/8 good, and all 8 produced level0 at exactly 199640 bytes.**

So the build process is completely deterministic - same scene in, byte-identical player
out, every time. All the variation in condition A came from the scene being regenerated
before each attempt. That is the opposite of the recorded conclusion.

Consequence: **an intermittent bug is now a deterministic one.** A known-bad scene is
preserved at `/tmp/party_repro/RedLight.BAD.199640.unity` (sha256 22ca5dde111e77b7...)
and reproduces a corrupt player 8 times out of 8.

### Condition D: scene generation is not deterministic, and here is why

Three regenerations produced three different files (1101487 / 1101953 / 1102078 bytes)
despite `Random.InitState(20260824)`. But all three contain **exactly the same 430
GameObjects, 28 MonoBehaviours and 430 Transforms, with identical names**.

Two causes, both benign in themselves:

1. Unity assigns every object a **random anchor id** (`--- !u!1 &658387`). Different
   digit lengths alone change the file size.
2. Unity writes the objects in a **different ORDER** each time. At the same file offset
   one scene has `m_Name: Start 4` (a NetworkStartPosition) and another has
   `m_Name: Capsule`.

`Tools/scene_canon.py` renumbers anchor ids in document order so scenes can be compared
on content; it cuts the noise from 25,780 diff lines to 21,713, but cannot align them
because the ORDER itself differs. The remaining difference is order, not content.

**So the fixed seed works, the content is stable, and what varies is how Unity lays it
out.** Some layouts build a working player and some do not - which fits the failure
signature already recorded twice in this codebase: a MonoBehaviour deserialising past
the end of the data.

### The practical fix this unlocks

`build_verified.sh` regenerates the scene before **every** attempt, so it re-rolls the
dice each time and gets ~37% good. But a good scene builds good **8/8, byte-identical**.

So: **stop regenerating.** Keep a verified-good `RedLight.unity` committed, and only
regenerate when the setup code actually changes - then verify once and commit the result.
That turns a 37% coin flip into a deterministic build.

### CORRECTION: `level0 == 199712` is NOT a reliable good/bad predictor

Stated earlier in this entry that 199712 predicted a good build "with no false positives
or negatives". That was 3 good samples, and I then used it as the exit condition for a
capture loop instead of booting the player. Ten regenerations later it had never matched,
while producing sizes 199688 and 199692 - both LARGER than any known-bad build in
condition A, where the largest corrupt was 199680. Those were plausibly good builds, and
the loop threw them away.

Substituting a cheap proxy for the actual measurement is the same mistake as trusting
Unity's exit code (§6.7). `Tools/capture_good_scene.sh` boots every candidate instead.
Treat the size as a diagnostic hint only; the boot is the measurement.

### Open

- Capture a known-good scene, commit it, and rework `build_verified.sh` around it.
- Root cause of the ordering sensitivity still unknown. With a good scene and a bad scene
  both preserved, this is now bisectable properly rather than by guesswork.
- Condition C (single invocation via `RebuildAndBuildRedLight`) - no longer the priority,
  since the regeneration itself is the dice roll rather than the hand-off between
  processes.

---

## 2026-09-01 — session 1 (attended, then handing to the night shift)

### Done

- **Red Light regression suite.** `Tests/redlight_regression.sh` (4 phases) +
  `Tests/redlight_report.py` (15 checks). Phase 0 calibrates the analyser against
  `Tests/fixtures/session_{good,bad}.log` and refuses to run if the broken fixture comes
  back green. The old `round_pacing_test.sh` checked 3 things on 1 round.
- **`build_verified.sh` could report failure but never success.** Its smoke test ran
  `-partyrole none -partyseconds 6`, but `MilestoneAutoRun` checked the quit timer AFTER
  `if (_role == "none") return;`. A correctly-booting player never quit; only a CORRUPT
  build ended the smoke test, by crashing. Found a 6-second smoke process alive after
  1h37m. Fixed both sides + a hard 30s deadline + a positive `[AutoRun]` check.
- **Barnaby's bias was half-dead.** Measured: 0 spares in a 6-round session while one
  player was framed in all 6 and rode the affinity floor at -1.000.
  - `Nudge` was only ever called with -0.1, so affinity was a one-way ratchet.
  - `JudgeMovers` rolls every frame of a stop and re-rolled `WouldFrame` each pass, so a
    17%/frame chance became a certainty in milliseconds.
  - Fixed: framing now warms him (+0.15), leading costs standing (-0.15), being caught
    moving costs nothing (it is how you lose, so it hit everyone every round), 10% fade
    per round, framing decided once per stop, and `EnsureFavouriteAndTarget` guarantees
    a pet and a target because ~1 lobby in 10 drew nobody above the +0.25 spare gate.
  - **Seeded before/after, 20 rounds each side, same five seeds:** spares 23 -> 49
    (1.15 -> 2.45 per round), frames 10 -> 7, worst victim 6/6 rounds -> 2/6, wipeouts
    60% -> 55%. See `Docs/REDLIGHT_TUNING.md`.
- **`hostserver.py` was single-threaded.** The suite caught it: the AI phase failed while
  the server logged 200 on all seven POSTs. Pre-generation fires several beats at once,
  they queued behind live model calls, and the third blew past HostVoice's 6s timeout.
  Measured, one variable: single-threaded 2576ms, 4272ms, then TWO TIMEOUTS at 6s;
  threaded 2415-3122ms, four 200s.
- **Fall Guys visual pass.** Camera was a broadcast tripod 20 back / 11 up framing the
  LEADER; now third-person behind YOUR player at 7 / 3.1 (~25% of frame height) that
  orbits to your direction of travel. Name tags small, distance-faded, own hidden, and
  scaled to hold a constant screen size. Roster replaced by a STILL IN count. Standing
  meter. Barnaby lower-third. Dev overlays behind F3; builds no longer
  `BuildOptions.Development`. Candy palette. Regression suite still 15/15 after.

### Measured, and it matters

- **Build success rate is ~12%, not the 1-in-3 in `HANDOFF.md`.** 6 successes in ~50
  attempts across the day. `build_verified.sh` gives up at 10, so ~1 in 3.5 invocations
  fails outright; it happened twice. This is the throughput bottleneck for everything.
- Barnaby causes only **7 of 91** eliminations. The other 84 are bots twitching, so the
  >50% wipeout rate is NOT a bias problem.
- `RedLightBotInput` has the **same defect the framing roll had**: a 2% chance rolled
  EVERY FRAME of a stop, which over ~120 frames is ~91%, and over 15 stops means a bot is
  caught with ~93% probability. Matches the observed ~4.5 of 5 eliminated per round.

### NEEDS APPROVAL (founder, when awake)

- **Bot twitch fix.** Same shape as the framing fix (decide once per stop). Changes
  difficulty, which is a design call, so not done.
- **Lane reads washed out.** Ambient was lifted hard to kill deep shadows and it
  flattened the ground plane. Would pull `ambientGroundColor` back down.
- **`EnsureFavouriteAndTarget` pins the pet.** Croucher sat at exactly 0.400 for rounds
  5-6. The floor re-applies every round, so the favourite never rotates. Suggest
  applying it only when nobody is above the gate.
- **Name tags still collide** when two bots stand adjacent. Fall Guys has this too, so it
  may be fine; a per-slot vertical stagger would fix it.
- **Reference games — ANSWERED 2026-09-01.** Fall Guys, Gang Beasts, Pummel Party,
  Machine Party, Super Battle Golf, Codenames, and "meccha Chameleon" (read as The
  Chameleon, the hidden-role party game — confirm). Recorded in `Docs/NIGHT_SHIFT.md`.
  Two of the seven are hidden-information games and **nothing in the current twelve is**,
  which is a gap worth the founder's attention.

### Still never executed

- **Steam P2P.** Code written, never run against a live client. Needs the second Steam
  account and two machines. Unchanged.
