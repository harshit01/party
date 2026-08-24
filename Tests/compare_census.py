"""
Compare two census logs at matched NETWORK times and report positional disagreement.

Aligning on Mirror's NetworkTime is the whole point: the host and client processes
start seconds apart, so local Time.time cannot align them. An earlier comparison used
local time, matched two unrelated instants, and drew a confident wrong conclusion.

Exit 0 if every matched sample agrees within the tolerance, 1 otherwise.
"""
import re, sys, collections

LINE = re.compile(r"\[CENSUS\] role=(\w+) nt=([\d.]+) .*?count=(\d+)(.*)")
ENT  = re.compile(r"([^|]+?)\((bot|human)\) (-?[\d.]+),(-?[\d.]+)")

def parse(path):
    out = collections.defaultdict(dict)   # nt -> {name: (x,z)}
    for ln in open(path, errors="ignore"):
        m = LINE.match(ln.strip())
        if not m: continue
        nt = round(float(m.group(2)), 1)
        for e in ENT.finditer(m.group(4)):
            out[nt][e.group(1).strip().lstrip("| ").strip()] = (float(e.group(3)), float(e.group(4)))
    return out

def main():
    host_log, cli_log = sys.argv[1], sys.argv[2]
    tol = float(sys.argv[3]) if len(sys.argv) > 3 else 1.5

    h, c = parse(host_log), parse(cli_log)
    shared = sorted(set(h) & set(c))
    if not shared:
        print("FAIL: no shared network timestamps - cannot compare"); return 1

    worst, worst_at, checked, bad = 0.0, None, 0, 0
    for nt in shared:
        for name, (cx, cz) in c[nt].items():
            if name not in h[nt]: continue
            hx, hz = h[nt][name]
            d = ((cx-hx)**2 + (cz-hz)**2) ** 0.5
            checked += 1
            if d > worst: worst, worst_at = d, (nt, name)
            if d > tol: bad += 1

    print(f"  matched network instants : {len(shared)}")
    print(f"  capsule samples compared : {checked}")
    print(f"  worst disagreement       : {worst:.2f} m"
          + (f"  ({worst_at[1]} at nt={worst_at[0]})" if worst_at else ""))
    print(f"  samples over {tol} m tolerance: {bad}")

    if checked == 0:
        print("FAIL: no capsules compared"); return 1
    if bad:
        print("FAIL: host and client disagree on positions"); return 1
    print("OK  host and client agree on every capsule position")
    return 0

sys.exit(main())
