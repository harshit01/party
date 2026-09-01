# WORKLOG

Append-only. Newest at the top. One entry per working session.

Written for a session that starts blind: say what you **measured**, not what you felt.
"Looks better" is not a result; "spares went 1.15 -> 2.45 per round on the same five
seeds" is. Read `Docs/NIGHT_SHIFT.md` before adding to this.

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
