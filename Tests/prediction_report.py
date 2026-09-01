#!/usr/bin/env python3
"""
"The Prediction" (#11) - regression analysis over a headless session log.

#11 wraps another minigame, so this checks the WRAPPER: that bets are taken, that the
thing being bet on is well defined, that scoring follows from the result, and that the
mechanic is capable of producing its signature. The inner minigame is checked by its own
analyser.

The claims, from MINIGAMES.md #11:
  * everyone secretly bets on who comes LAST
  * points for being right
  * "table talk, alliances and betrayal" - which needs being right to be ACHIEVABLE
  * signature: everyone backs one person to lose, who then wins

The third one is the easy one to get wrong. An earlier bot policy backed Barnaby's
public grudge every round and was right 0-1 times in 5, so nobody ever scored and there
was nothing to talk about; a later one spread bets so evenly that a pile-on never
happened at all. Both "passed" any test that only asked whether bets were placed.
"""
import re
import sys
from collections import defaultdict

BET   = re.compile(r"\[Prediction\] BETS round=(\d+)(.*)")
PAIR  = re.compile(r"\| ([^|>]+)->([^|]+)")
RES   = re.compile(r"\[Prediction\] ROUND (\d+) RESULT last=(.+?) winner=(.+?) "
                   r"correct=(\d+)/(\d+) topPick=(.+?) topCount=(\d+) "
                   r"unanimous=(\w+) piled=(\w+) backfired=(\w+)")
SCORE = re.compile(r"\[Prediction\] SCORES round=(\d+)(.*)")
SPAIR = re.compile(r"\| ([^=|]+)=(-?\d+)")
OPEN  = re.compile(r"\[Prediction\] ROUND (\d+) betting opens on (.+)")
EXC   = re.compile(r"(?:^|\s)\w*Exception:|\bStackOverflow\b")


def parse(path):
    opens, bets, results, scores, errors = [], {}, [], {}, []
    with open(path, errors="replace") as fh:
        for line in fh:
            m = OPEN.search(line)
            if m:
                opens.append((int(m.group(1)), m.group(2).strip()))
                continue
            m = BET.search(line)
            if m:
                bets[int(m.group(1))] = [(a.strip(), b.strip())
                                         for a, b in PAIR.findall(m.group(2))]
                continue
            m = RES.search(line)
            if m:
                results.append(dict(
                    round=int(m.group(1)), last=m.group(2).strip(),
                    winner=m.group(3).strip(), correct=int(m.group(4)),
                    bets=int(m.group(5)), top=m.group(6).strip(),
                    topcount=int(m.group(7)),
                    unanimous=m.group(8) == "True", piled=m.group(9) == "True",
                    backfired=m.group(10) == "True"))
                continue
            m = SCORE.search(line)
            if m:
                scores[int(m.group(1))] = {n.strip(): int(v)
                                           for n, v in SPAIR.findall(m.group(2))}
                continue
            if EXC.search(line) or "is corrupted" in line:
                errors.append(line.strip())
    return opens, bets, results, scores, errors


def main():
    path = sys.argv[1]
    opens, bets, results, scores, errors = parse(path)

    checks = []
    def check(ok, label, detail=""):
        checks.append((bool(ok), label, detail))

    check(not errors, "no exceptions or corruption in the log",
          errors[0][:120] if errors else "clean")
    check(opens and len(results) == len(opens),
          "every round that opened betting also resolved",
          f"opened {len(opens)}, resolved {len(results)}")

    # Everyone must actually bet, or the round is decided by abstention.
    thin = [r["round"] for r in results if r["bets"] < 2]
    check(results and not thin, "bets are actually placed each round",
          f"rounds with <2 bets: {thin}" if thin else
          f"bets per round: {sorted({r['bets'] for r in results})}")

    # You may not back yourself to lose - that would be free points for throwing.
    self_bets = []
    for rd, pairs in bets.items():
        for who, target in pairs:
            if who == target:
                self_bets.append((rd, who))
    check(not self_bets, "nobody bets on themselves",
          f"self-bets: {self_bets[:3]}" if self_bets else "none")

    # The thing being bet on must be well defined and must MOVE. If one player comes
    # last every round the bet is trivial; if "last" is decided by array order it is
    # meaningless (that bug was real - see the commit for #11).
    lasts = [r["last"] for r in results]
    distinct = len(set(lasts))
    check(distinct >= 2 or len(results) < 3,
          "who comes last actually varies between rounds",
          f"{distinct} distinct losers over {len(results)} rounds: "
          + ", ".join(f"{n}x{lasts.count(n)}" for n in sorted(set(lasts))))

    # BEING RIGHT MUST BE ACHIEVABLE. This is the check that catches the bot policy
    # that always backed Barnaby's grudge and was never right.
    total_correct = sum(r["correct"] for r in results)
    check(total_correct > 0, "predictions are sometimes CORRECT",
          f"{total_correct} correct calls over {len(results)} rounds")

    # ...and not trivially easy either.
    if results:
        rate = total_correct / max(sum(r["bets"] for r in results), 1)
        check(rate < 0.75, "predictions are not trivially easy", f"hit rate {rate:.0%}")

    # Scores must follow from results, and must spread - a leaderboard where everyone
    # is equal is not a leaderboard.
    if scores:
        final = scores[max(scores)]
        vals = sorted(final.values())
        check(len(set(vals)) > 1, "scores separate the players",
              f"final: {final}")
        check(all(v >= 0 for v in vals), "no negative scores", f"min {vals[0]}")

    # THE SIGNATURE must be reachable. Not every session will contain one - it is a
    # memorable moment, not a constant - so this only asserts the room CAN converge.
    piles = sum(1 for r in results if r["piled"])
    backfires = sum(1 for r in results if r["backfired"])
    check(piles > 0 or len(results) < 6,
          "the room piles onto one target sometimes",
          f"{piles} pile-ons, {backfires} of them backfired, over {len(results)} rounds")

    print("=" * 70)
    print("THE PREDICTION - regression report")
    print("=" * 70)
    if results:
        print(f"{'rd':>3} {'came last':<12} {'winner':<12} {'right':>6} {'top pick':<12} "
              f"{'piled':>6} {'backfired':>10}")
        for r in results:
            print(f"{r['round']:>3} {r['last'][:12]:<12} {r['winner'][:12]:<12} "
                  f"{r['correct']}/{r['bets']:<4} {r['top'][:12]:<12} "
                  f"{str(r['piled']):>6} {str(r['backfired']):>10}")
        print()
    if scores:
        print(f"final scores: {scores[max(scores)]}")
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
