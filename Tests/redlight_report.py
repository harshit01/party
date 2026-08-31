#!/usr/bin/env python3
"""
Red Light, Barnaby - regression analysis over a headless SESSION log.

WHY THIS EXISTS
round_pacing_test.sh asserted three things (stops >= 4, duration >= 20s, the round
ended) on ONE round. Everything the game actually claims to do went unchecked:
eliminations, Barnaby sparing his favourites, framing his grudges, every round
resolving to an outcome, and affinity persisting between rounds. HANDOFF.md section
6.1 - a test that passes everything measures nothing.

This reads the log of a multi-round session and checks the claims individually, so a
failure names the mechanic that broke instead of just saying the round felt wrong.
"""
import re
import sys
from collections import defaultdict

END_RE   = re.compile(
    r"\[RedLight\] ROUND (\d+) END outcome=(\S+) stops=(\d+) "
    r"eliminated=(\d+) spared=(\d+) framed=(\d+)")
STAND_RE = re.compile(r"\[RedLight\] STANDINGS round=(\d+)(.*)")
PAIR_RE  = re.compile(r"\| ([^=|]+)=(-?\d+\.\d+)\(([a-z]+)\)")
SPARE_RE = re.compile(r"\[RedLight\] SPARED (.+?) \(standing=(\w+)\)")
OUT_RE   = re.compile(r"\[RedLight\] (.+?) out \((\w+), standing=(\w+)\)")
BEGIN_RE = re.compile(r"\[AutoRun\] beginning round (\d+)/(\d+)")
EXC_RE   = re.compile(r"(?:^|\s)\w*Exception:|\bStackOverflow\b")

# Barnaby may only spare someone he likes and only frame someone he does not.
# BarnabyBias gates spare on affinity > 0.25 and frame on affinity < -0.35, which
# Describe() renders as these buckets. A spare landing on a "grudge" would mean the
# bias is decorative and the unfairness is really just noise - the exact failure the
# mechanic is designed not to be.
SPARE_OK = {"pet", "tolerated"}
FRAME_OK = {"grudge"}
VALID_OUTCOME = ("winner:", "timeout:", "wipeout")


def parse(path):
    rounds, standings, spares, outs, begins = [], {}, [], [], []
    errors = []
    cur = 0
    with open(path, errors="replace") as fh:
        for line in fh:
            mb = BEGIN_RE.search(line)
            if mb:
                cur = int(mb.group(1))
            m = END_RE.search(line)
            if m:
                rounds.append(dict(round=int(m.group(1)), outcome=m.group(2),
                                   stops=int(m.group(3)), eliminated=int(m.group(4)),
                                   spared=int(m.group(5)), framed=int(m.group(6))))
                continue
            m = STAND_RE.search(line)
            if m:
                standings[int(m.group(1))] = {
                    n.strip(): (float(v), b) for n, v, b in PAIR_RE.findall(m.group(2))}
                continue
            m = SPARE_RE.search(line);  spares.append(m.groups()) if m else None
            m = OUT_RE.search(line)
            if m:
                outs.append(m.groups() + (cur,))
            m = BEGIN_RE.search(line);  begins.append(m.groups()) if m else None
            # Match Unity's actual exception format ("NullReferenceException: ...")
            # rather than the bare substring: every ordinary Debug.Log prints a stack
            # trace mentioning ExtractStackTrace, and a loose match flags all of them.
            if EXC_RE.search(line) or "is corrupted" in line:
                errors.append(line.strip())
    return rounds, standings, spares, outs, begins, errors


def main():
    path = sys.argv[1]
    min_stops = int(sys.argv[2]) if len(sys.argv) > 2 else 4
    rounds, standings, spares, outs, begins, errors = parse(path)

    checks = []   # (ok, label, detail)
    def check(ok, label, detail=""):
        checks.append((bool(ok), label, detail))

    # ---------------- structural ----------------
    wanted = int(begins[0][1]) if begins else 0
    started, ended = len(begins), len(rounds)
    check(started == wanted and ended == wanted,
          "every round started AND ended",
          f"asked {wanted}, started {started}, ended {ended}")
    check(not errors, "no exceptions or corruption in the log",
          errors[0][:120] if errors else "clean")

    # ---------------- per round ----------------
    bad_outcome = [r for r in rounds if not r["outcome"].startswith(VALID_OUTCOME)]
    check(rounds and not bad_outcome, "every round resolved to a real outcome",
          "bad: " + str([r["outcome"] for r in bad_outcome]) if bad_outcome else
          ", ".join(sorted({r["outcome"].split(":")[0] for r in rounds})))

    thin = [r["round"] for r in rounds if r["stops"] < min_stops]
    check(rounds and not thin, f"STOP called >= {min_stops}x in every round",
          f"thin rounds: {thin}" if thin else
          "per round: " + ",".join(str(r["stops"]) for r in rounds))

    # A round where nobody is ever out is a parade, not a game.
    tot_elim = sum(r["eliminated"] for r in rounds)
    check(tot_elim > 0, "eliminations actually happen", f"{tot_elim} across the session")

    # The sweeper-freeze regression: hazards that kept moving during STOP wiped the
    # whole lobby every round. If EVERY round is a wipeout, that bug is back.
    wipes = [r["round"] for r in rounds if r["outcome"] == "wipeout"]
    check(len(wipes) < len(rounds) if rounds else False,
          "not every round is a total wipeout (hazards still freeze on STOP)",
          f"{len(wipes)}/{len(rounds)} wipeouts")

    # ---------------- Barnaby's bias ----------------
    tot_spare, tot_frame = sum(r["spared"] for r in rounds), sum(r["framed"] for r in rounds)
    check(tot_spare > 0, "Barnaby SPARES favourites", f"{tot_spare} spares")
    check(tot_frame > 0, "Barnaby FRAMES grudges", f"{tot_frame} frames")

    bad_spare = [(w, b) for w, b in spares if b not in SPARE_OK]
    check(not bad_spare, "spares only ever land on players he likes",
          f"violations: {bad_spare[:3]}" if bad_spare else
          f"buckets seen: {sorted({b for _, b in spares})}")

    framed = [(w, b) for w, r, b, _rd in outs if r == "framed"]
    bad_frame = [(w, b) for w, b in framed if b not in FRAME_OK]
    check(not bad_frame, "frames only ever land on players he resents",
          f"violations: {bad_frame[:3]}" if bad_frame else
          f"buckets seen: {sorted({b for _, b in framed})}")

    # ---------------- persistence across rounds ----------------
    # Affinity is only ever nudged DOWN (-0.1 on being called out), so across a session
    # a player's standing must never rise. If it does, the bias is being re-seeded and
    # "he remembers who annoyed him three rounds ago" is false.
    # Affinity now moves BOTH ways (framed +0.15, caught -0.10, leading -0.15, plus a
    # 10% fade toward neutral), so "never rises" is no longer the invariant - it was the
    # bug. What must still hold is that standings CARRY: one round can only shift a
    # player by a fade plus at most one nudge, whereas a re-seed draws fresh on [-1,1].
    MAX_STEP = 0.30
    keys = sorted(standings)
    jumped, rose, fell, carried = [], 0, 0, 0
    for a, b in zip(keys, keys[1:]):
        for name, (va, _) in standings[a].items():
            if name not in standings[b]:
                continue
            vb = standings[b][name][0]
            if vb > va + 1e-4:
                rose += 1
            elif vb < va - 1e-4:
                fell += 1
            else:
                carried += 1
            if abs(vb - va) > MAX_STEP + 1e-6:
                jumped.append((name, a, round(va, 3), b, round(vb, 3)))
    check(len(keys) >= 2, "session ran enough rounds to test persistence",
          f"{len(keys)} rounds of standings")
    check(not jumped, f"standings carry between rounds (no jump > {MAX_STEP})",
          f"jumps: {jumped[:2]}" if jumped else
          f"{carried} unchanged, {fell} soured, {rose} warmed")
    check(fell > 0, "being called out actually sours Barnaby (Nudge persists)",
          f"{fell} player-rounds declined")
    # The fix under test: affinity used to be a one-way ratchet downward, which is why
    # sparing died out. Standing must be able to RECOVER.
    check(rose > 0, "affinity can RISE again (the one-way ratchet is gone)",
          f"{rose} player-rounds warmed toward him")

    # The other half of the fix: nobody may be framed every single round. That is what
    # a per-frame WouldFrame roll produced - a fixed victim, which reads as a rule
    # rather than as a host being capricious.
    per_player = defaultdict(set)
    for w, r, _b, rd in outs:
        if r == "framed":
            per_player[w].add(rd)
    worst, worst_n = "", 0
    for w, rds in per_player.items():
        if len(rds) > worst_n:
            worst, worst_n = w, len(rds)
    n_rounds = len(rounds)
    check(n_rounds == 0 or worst_n < n_rounds,
          "no single player is framed in EVERY round",
          f"worst: {worst} framed in {worst_n}/{n_rounds} rounds" if worst
          else "nobody was framed")

    # ---------------- report ----------------
    print("=" * 68)
    print("RED LIGHT, BARNABY - regression report")
    print("=" * 68)
    if rounds:
        print(f"{'rd':>3} {'outcome':<22} {'stops':>5} {'out':>4} {'spared':>6} {'framed':>6}")
        for r in rounds:
            print(f"{r['round']:>3} {r['outcome'][:22]:<22} {r['stops']:>5} "
                  f"{r['eliminated']:>4} {r['spared']:>6} {r['framed']:>6}")
        print()
    if keys:
        print("standings by round (affinity, -1 grudge .. +1 pet):")
        names = sorted({n for d in standings.values() for n in d})
        print("     " + "".join(f"{n[:9]:>11}" for n in names))
        for k in keys:
            row = "".join(
                f"{standings[k][n][0]:>11.3f}" if n in standings[k] else f"{'-':>11}"
                for n in names)
            print(f"  r{k:<2}{row}")
        print()

    if rounds:
        n_win = sum(1 for r in rounds if r["outcome"].startswith("winner:"))
        n_to  = sum(1 for r in rounds if r["outcome"].startswith("timeout:"))
        n_wo  = sum(1 for r in rounds if r["outcome"] == "wipeout")
        print(f"outcomes: {n_win} reached the line, {n_to} won on time, "
              f"{n_wo} wipeouts  ({len(rounds)} rounds)")
        print()

    print("checks:")
    for ok, label, detail in checks:
        print(f"  {'OK  ' if ok else 'FAIL'}  {label}")
        if detail:
            print(f"          {detail}")

    failed = [c for c in checks if not c[0]]
    print()
    print(f"RESULT: {len(checks) - len(failed)}/{len(checks)} checks passed")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
