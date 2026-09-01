#!/usr/bin/env python3
"""
"Say What He Says" (#10) - regression analysis over a headless session log.

Same standard as Tests/redlight_report.py, and for the same reason: this project has
twice shipped a minigame whose signature mechanic did not actually fire, because the
only test checked pacing. HANDOFF.md §6.1 - a test that passes everything measures
nothing, so this checks the claims individually and a failure names the mechanic.

The claims, from MINIGAMES.md #10 and the design rules at the top of that file:
  * an escalating sequence
  * a clear winner AND a clear loser
  * Barnaby spares favourites and frames grudges, on the SAME affinity as Red Light
  * a spare is a NEAR MISS, not a free pass
  * 30-60 seconds
"""
import re
import sys
from collections import defaultdict

# Non-greedy (.+?), not (\S+): an outcome embeds a player name and a human slot is
# "Player 0", with a space. See the same note in redlight_report.py - that analyser had
# been silently dropping every round a human won.
END   = re.compile(r"\[SayWhat\] ROUND (\d+) END outcome=(.+?) sequences=(\d+) "
                   r"eliminated=(\d+) spared=(\d+) framed=(\d+)(?: seconds=([\d.]+))?")
SEQ   = re.compile(r"\[SayWhat\] SEQUENCE (\d+) len=(\d+) steps=(\S+)")
STAND = re.compile(r"\[SayWhat\] STANDINGS round=(\d+)(.*)")
PAIR  = re.compile(r"\| ([^=|]+)=(-?\d+\.\d+)\(([a-z]+)\)")
SPARE = re.compile(r"\[SayWhat\] SPARED (.+?) \(matched (\d+)/(\d+), standing=(\w+)\)")
OUT   = re.compile(r"\[SayWhat\] (.+?) out \((\w+), matched=(\d+)/(\d+), standing=(\w+)\)")
BEGIN = re.compile(r"\[AutoRun\] beginning round (\d+)/(\d+)")
EXC   = re.compile(r"(?:^|\s)\w*Exception:|\bStackOverflow\b")

SPARE_OK = {"pet", "tolerated"}
FRAME_OK = {"grudge"}
VALID = ("winner:", "timeout:", "wipeout")


def parse(path):
    rounds, seqs, standings, spares, outs, begins, errors = [], [], {}, [], [], [], []
    cur = 0
    with open(path, errors="replace") as fh:
        for line in fh:
            m = BEGIN.search(line)
            if m:
                cur = int(m.group(1))
            m = END.search(line)
            if m:
                rounds.append(dict(round=int(m.group(1)), outcome=m.group(2),
                                   sequences=int(m.group(3)), eliminated=int(m.group(4)),
                                   spared=int(m.group(5)), framed=int(m.group(6)),
                                   seconds=float(m.group(7)) if m.group(7) else None))
                continue
            m = SEQ.search(line)
            if m:
                seqs.append((cur, int(m.group(1)), int(m.group(2))))
                continue
            m = STAND.search(line)
            if m:
                standings[int(m.group(1))] = {
                    n.strip(): (float(v), b) for n, v, b in PAIR.findall(m.group(2))}
                continue
            m = SPARE.search(line)
            if m:
                spares.append((m.group(1), int(m.group(2)), int(m.group(3)), m.group(4)))
                continue
            m = OUT.search(line)
            if m:
                outs.append((m.group(1), m.group(2), int(m.group(3)),
                             int(m.group(4)), m.group(5), cur))
                continue
            if EXC.search(line) or "is corrupted" in line:
                errors.append(line.strip())
    return rounds, seqs, standings, spares, outs, begins, errors


def main():
    path = sys.argv[1]
    min_seq = int(sys.argv[2]) if len(sys.argv) > 2 else 2
    rounds, seqs, standings, spares, outs, _b, errors = parse(path)

    begins = []
    with open(path, errors="replace") as fh:
        begins = BEGIN.findall(fh.read())

    checks = []
    def check(ok, label, detail=""):
        checks.append((bool(ok), label, detail))

    wanted = int(begins[0][1]) if begins else 0
    check(len(begins) == wanted and len(rounds) == wanted,
          "every round started AND ended",
          f"asked {wanted}, started {len(begins)}, ended {len(rounds)}")
    check(not errors, "no exceptions or corruption in the log",
          errors[0][:120] if errors else "clean")

    bad = [r for r in rounds if not r["outcome"].startswith(VALID)]
    check(rounds and not bad, "every round resolved to a real outcome",
          ", ".join(sorted({r["outcome"].split(":")[0] for r in rounds})) if rounds else "none")

    # A clear WINNER matters, but MINIGAMES.md says the loser matters more - so a round
    # that ends with nobody out has no story in it.
    check(rounds and all(r["eliminated"] > 0 for r in rounds),
          "every round produces a loser",
          f"eliminated per round: {[r['eliminated'] for r in rounds]}" if rounds else "")

    winners = sum(1 for r in rounds if r["outcome"].startswith("winner:"))
    check(winners > 0, "rounds produce a winner", f"{winners}/{len(rounds)} had a winner")

    # MINIGAMES.md opens with "30-60 seconds". Checked, not assumed - a round that ends
    # in 20s has not given the host enough to work with.
    timed = [r["seconds"] for r in rounds if r.get("seconds") is not None]
    if timed:
        short = [s for s in timed if s < 25]
        long_ = [s for s in timed if s > 75]
        check(not short and not long_, "rounds land near the 30-60s design window",
              f"seconds: {[round(s) for s in timed]}"
              + (f"  SHORT:{[round(s) for s in short]}" if short else "")
              + (f"  LONG:{[round(s) for s in long_]}" if long_ else ""))

    thin = [r["round"] for r in rounds if r["sequences"] < min_seq]
    check(rounds and not thin, f"at least {min_seq} sequences per round",
          f"thin: {thin}" if thin else
          "per round: " + ",".join(str(r["sequences"]) for r in rounds))

    # ESCALATION is the mechanic. Within a round, each sequence must be longer than the
    # last (until the cap), or it is just the same test repeated.
    bad_esc = []
    by_round = defaultdict(list)
    for rd, n, ln in seqs:
        by_round[rd].append((n, ln))
    for rd, items in by_round.items():
        items.sort()
        for (n1, l1), (n2, l2) in zip(items, items[1:]):
            if l2 < l1:
                bad_esc.append((rd, n1, l1, n2, l2))
    check(seqs and not bad_esc, "sequences escalate within a round",
          f"regressions: {bad_esc[:2]}" if bad_esc else
          "lengths: " + str([l for _, _, l in seqs][:10]))

    # ---- Barnaby ----
    tot_sp = sum(r["spared"] for r in rounds)
    tot_fr = sum(r["framed"] for r in rounds)
    check(tot_sp > 0, "Barnaby SPARES favourites", f"{tot_sp} spares")
    check(tot_fr > 0, "Barnaby FRAMES grudges", f"{tot_fr} frames")

    bad_sp = [(w, b) for w, _m, _t, b in spares if b not in SPARE_OK]
    check(not bad_sp, "spares only ever land on players he likes",
          f"violations: {bad_sp[:3]}" if bad_sp else
          f"buckets: {sorted({b for _w, _m, _t, b in spares})}")

    framed = [(w, st) for w, r, _m, _t, st, _rd in outs if r == "framed"]
    bad_fr = [(w, b) for w, b in framed if b not in FRAME_OK]
    check(not bad_fr, "frames only ever land on players he resents",
          f"violations: {bad_fr[:3]}" if bad_fr else
          f"buckets: {sorted({b for _w, b in framed})}")

    # A SPARE MUST BE A NEAR MISS. Measured on the first run, a favourite was spared at
    # 0/4 - waved through having done nothing at all, which reads as a broken referee
    # rather than as favouritism, and made pets unkillable.
    not_near = [(w, m, t) for w, m, t, _b in spares if m < (t + 1) // 2]
    check(not not_near, "spares are near misses, not free passes",
          f"waved through: {not_near[:3]}" if not_near else
          f"worst spare: {min((m/t for _w, m, t, _b in spares), default=1):.0%} of the sequence")

    # Framing must land on someone who was RIGHT, or it is just an elimination.
    fr_perfect = [(w, m, t) for w, r, m, t, _s, _rd in outs
                  if r == "framed" and m == t]
    check(not framed or fr_perfect,
          "framed players had actually got it right",
          f"{len(fr_perfect)}/{len(framed)} framed were perfect" if framed else "none framed")

    # ---- persistence, same engine as Red Light ----
    MAX_STEP = 0.30
    keys = sorted(standings)
    jumped, rose, fell, same = [], 0, 0, 0
    for a, b in zip(keys, keys[1:]):
        for name, (va, _) in standings[a].items():
            if name not in standings[b]:
                continue
            vb = standings[b][name][0]
            if vb > va + 1e-4: rose += 1
            elif vb < va - 1e-4: fell += 1
            else: same += 1
            if abs(vb - va) > MAX_STEP + 1e-6:
                jumped.append((name, a, round(va, 3), b, round(vb, 3)))
    check(len(keys) >= 2, "session ran enough rounds to test persistence",
          f"{len(keys)} rounds of standings")
    check(not jumped, f"standings carry between rounds (no jump > {MAX_STEP})",
          f"jumps: {jumped[:2]}" if jumped else f"{same} unchanged, {fell} soured, {rose} warmed")

    per_player = defaultdict(set)
    for w, r, _m, _t, _s, rd in outs:
        if r == "framed":
            per_player[w].add(rd)
    worst, worst_n = "", 0
    for w, rds in per_player.items():
        if len(rds) > worst_n:
            worst, worst_n = w, len(rds)
    check(not rounds or worst_n < len(rounds),
          "no single player is framed in EVERY round",
          f"worst: {worst} framed in {worst_n}/{len(rounds)}" if worst else "nobody framed")

    # ---- report ----
    print("=" * 70)
    print("SAY WHAT HE SAYS - regression report")
    print("=" * 70)
    if rounds:
        print(f"{'rd':>3} {'outcome':<24} {'seqs':>5} {'out':>4} {'spared':>6} {'framed':>6}")
        for r in rounds:
            print(f"{r['round']:>3} {r['outcome'][:24]:<24} {r['sequences']:>5} "
                  f"{r['eliminated']:>4} {r['spared']:>6} {r['framed']:>6}")
        print()
    if keys:
        names = sorted({n for d in standings.values() for n in d})
        print("standings by round (affinity, -1 grudge .. +1 pet):")
        print("     " + "".join(f"{n[:9]:>11}" for n in names))
        for k in keys:
            print(f"  r{k:<2}" + "".join(
                f"{standings[k][n][0]:>11.3f}" if n in standings[k] else f"{'-':>11}"
                for n in names))
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
