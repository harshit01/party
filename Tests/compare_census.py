"""
Compare two census logs at matched NETWORK times.

WHAT THIS ASSERTS, AND WHY

A client is SUPPOSED to render behind the host. Mirror's snapshot interpolation holds
a buffer (bufferTimeMultiplier, default 2) so that network jitter does not turn into
visible stutter. So a non-zero host/client position difference is correct behaviour,
not a bug, and picking a tight tolerance would only measure the buffer.

Worse, the obvious way to make that number look good - shrinking the buffer - is
actively harmful. Loopback has no jitter, so a buffer tuned here would be far too
small over Steam relay. DO NOT TUNE INTERPOLATION AGAINST LOOPBACK NUMBERS.

So the checks that carry weight are:
  * BOUNDED, not growing - lag stays flat; a real desync diverges over time
  * UNIFORM across capsules - one capsule drifting alone means a per-object bug
  * under a loose sanity ceiling - catches gross breakage, not tuning

The absolute figures are reported for the record. They only become meaningful when
measured between two real machines over Steam relay.
"""
import re, sys, collections, statistics

LINE = re.compile(r"\[CENSUS\] role=(\w+) nt=([\d.]+) .*?count=(\d+)(.*)")
ENT  = re.compile(r"([^|]+?)\((bot|human)\) (-?[\d.]+),(-?[\d.]+)")
WINDOW  = 0.30   # seconds; both ends sample on their own phase
CEILING = 4.0    # metres; gross-breakage sanity bound, not a quality target

def parse(path):
    out = collections.defaultdict(dict)
    for ln in open(path, errors="ignore"):
        m = LINE.match(ln.strip())
        if not m: continue
        for e in ENT.finditer(m.group(4)):
            out[round(float(m.group(2)), 1)][e.group(1).strip().lstrip("| ").strip()] = \
                (float(e.group(3)), float(e.group(4)))
    return out

def main():
    h, c = parse(sys.argv[1]), parse(sys.argv[2])
    if not h or not c:
        print("FAIL: one of the logs has no census lines"); return 1

    host_times = sorted(h)
    per = collections.defaultdict(list)
    series = []
    matched = 0

    for nt in sorted(c):
        best = min(host_times, key=lambda x: abs(x - nt))
        if abs(best - nt) > WINDOW: continue
        matched += 1
        for name, (cx, cz) in c[nt].items():
            if name not in h[best]: continue
            hx, hz = h[best][name]
            d = ((cx-hx)**2 + (cz-hz)**2) ** 0.5
            per[name].append(d); series.append(d)

    if matched == 0:
        print(f"FAIL: no host sample within {WINDOW}s of any client sample"); return 1
    if not series:
        print("FAIL: no capsules could be compared"); return 1

    print(f"  matched instants: {matched}   samples: {len(series)}")
    for name, ds in sorted(per.items()):
        print(f"    {name:<24} median={statistics.median(ds):.2f}m  max={max(ds):.2f}m")

    fail = 0

    # 1. bounded, not growing
    half = len(series) // 2
    a, b = statistics.median(series[:half]), statistics.median(series[half:])
    print(f"  drift: first half {a:.2f}m -> second half {b:.2f}m")
    if b > a * 1.6 + 0.5:
        print("  FAIL: disagreement is GROWING - real desync, not interpolation lag"); fail = 1
    else:
        print("  OK  bounded - consistent with interpolation lag, not desync")

    # 2. uniform across capsules
    meds = [statistics.median(ds) for ds in per.values()]
    if len(meds) > 1 and max(meds) > min(meds) * 2.5 + 0.5:
        print(f"  FAIL: one capsule drifts more than the others ({min(meds):.2f} vs {max(meds):.2f})"); fail = 1
    else:
        print("  OK  uniform across capsules")

    # 3. loose sanity ceiling
    worst = max(series)
    if worst > CEILING:
        print(f"  FAIL: worst disagreement {worst:.2f}m exceeds sanity ceiling {CEILING}m"); fail = 1
    else:
        print(f"  OK  worst {worst:.2f}m under {CEILING}m sanity ceiling")

    print("  NOTE: absolute figures are loopback. Real numbers require two machines over Steam relay.")
    return fail

sys.exit(main())
