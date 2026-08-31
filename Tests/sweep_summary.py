#!/usr/bin/env python3
"""Summarise a bias sweep: one line per seed, then totals."""
import glob, os, re, sys

END = re.compile(r"ROUND (\d+) END outcome=(\S+) stops=(\d+) eliminated=(\d+) "
                 r"spared=(\d+) framed=(\d+)")
SEED = re.compile(r"\[RedLight\] bias seed=(-?\d+)")

label, out = sys.argv[1], sys.argv[2]
rows, tot = [], dict(rounds=0, spared=0, framed=0, wipe=0, win=0, timeout=0, elim=0)

for path in sorted(glob.glob(os.path.join(out, "seed_*.log")),
                   key=lambda p: int(re.search(r"seed_(-?\d+)", p).group(1))):
    txt = open(path, errors="replace").read()
    seed = (SEED.search(txt).group(1) if SEED.search(txt) else "?")
    rs = END.findall(txt)
    sp = sum(int(r[4]) for r in rs); fr = sum(int(r[5]) for r in rs)
    el = sum(int(r[3]) for r in rs)
    wp = sum(1 for r in rs if r[1] == "wipeout")
    wn = sum(1 for r in rs if r[1].startswith("winner:"))
    to = sum(1 for r in rs if r[1].startswith("timeout:"))
    rows.append((seed, len(rs), sp, fr, el, wn, to, wp))
    tot["rounds"] += len(rs); tot["spared"] += sp; tot["framed"] += fr
    tot["elim"] += el; tot["wipe"] += wp; tot["win"] += wn; tot["timeout"] += to

print()
print(f"=== BIAS SWEEP: {label} ===")
print(f"{'seed':>8} {'rds':>4} {'spared':>7} {'framed':>7} {'out':>5} "
      f"{'win':>4} {'time':>5} {'wipe':>5}")
for r in rows:
    print(f"{r[0]:>8} {r[1]:>4} {r[2]:>7} {r[3]:>7} {r[4]:>5} {r[5]:>4} {r[6]:>5} {r[7]:>5}")
n = tot["rounds"] or 1
dead = sum(1 for r in rows if r[2] == 0)
print(f"{'TOTAL':>8} {tot['rounds']:>4} {tot['spared']:>7} {tot['framed']:>7} "
      f"{tot['elim']:>5} {tot['win']:>4} {tot['timeout']:>5} {tot['wipe']:>5}")
print()
print(f"  spares per round : {tot['spared']/n:.2f}")
print(f"  frames per round : {tot['framed']/n:.2f}")
print(f"  wipeout rate     : {tot['wipe']}/{tot['rounds']} rounds "
      f"({100*tot['wipe']/n:.0f}%)")
print(f"  seeds with NO spares at all : {dead}/{len(rows)}")
