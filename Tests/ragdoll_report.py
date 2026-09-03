#!/usr/bin/env python3
"""
Active ragdoll - verification over a RagdollProbe run.

WHY THIS EXISTS. Grab and throw were written and shipped without ever being executed:
there is no way to press a key in a headless build, so "it looks better" was the only
evidence for any of it. The first probe run found two real defects immediately - a throw
that left the crate at 0.74 m/s, which is a drop rather than a throw, and a limp that only
reached 30 degrees.

Every check here is a number the probe measured, not an impression of a screenshot. A
still frame cannot tell standing from mid-collapse (HANDOFF §6.7 - verify the artifact).
"""
import re
import sys

PATS = {
    "stand":  re.compile(r"\[Probe\] STAND tilt_avg=([\d.-]+) tilt_max=([\d.-]+)"),
    "walk":   re.compile(r"\[Probe\] WALK dist=([\d.-]+)"),
    "stab":   re.compile(r"\[Probe\] STABILITY falls=(\d+) recoveries=(\d+) down_frames=(\d+)"),
    "grab":   re.compile(r"\[Probe\] GRAB worked=(\w+) carried=([\d.-]+)"),
    "throw":  re.compile(r"\[Probe\] THROW speed=([\d.-]+)"),
    "limp":   re.compile(r"\[Probe\] LIMP tilt=([\d.-]+)"),
    "done":   re.compile(r"\[Probe\] DONE"),
}


def parse(path):
    out = {}
    txt = open(path, errors="replace").read()
    for key, pat in PATS.items():
        m = pat.search(txt)
        if m:
            out[key] = m.groups() if m.groups() else True
    out["errors"] = re.findall(r"(?:^|\s)\w*Exception:.*", txt)
    return out


def main():
    d = parse(sys.argv[1])
    checks = []
    def check(ok, label, detail=""):
        checks.append((bool(ok), label, detail))

    check("done" in d, "the probe ran to completion",
          "reached DONE" if "done" in d else "no DONE line - it crashed or hung")
    check(not d["errors"], "no exceptions",
          d["errors"][0][:100] if d["errors"] else "clean")

    if "stand" in d:
        avg, mx = float(d["stand"][0]), float(d["stand"][1])
        # Standing still should be genuinely still. This is the one thing the very first
        # force-based attempts could never do - they settled bent double at 79-126 degrees.
        check(avg < 8 and mx < 20, "stands still without drifting or folding",
              f"avg tilt {avg:.1f} deg, worst {mx:.1f} deg")

    if "walk" in d:
        dist = float(d["walk"][0])
        check(dist > 2.0, "actually walks somewhere", f"{dist:.2f} m in 5 s")

    if "stab" in d:
        falls, recov, down = int(d["stab"][0]), int(d["stab"][1]), int(d["stab"][2])
        # RECOVERY IS THE POINT. A controller that never falls is the frozen capsule again;
        # one that falls and stays down is a corpse. It has to do both.
        check(recov >= falls - 1 and (falls == 0 or recov > 0),
              "gets back up after falling over",
              f"{falls} falls, {recov} recoveries")
        # Falling is wanted; falling constantly is not. Roughly 20 s of run at 50 Hz.
        check(down < 450, "is not on the floor most of the time",
              f"{down} physics frames down (~{down/50:.1f} s)")

    if "grab" in d:
        worked, carried = d["grab"][0] == "True", float(d["grab"][1])
        check(worked, "GRAB attaches to something", "joint formed" if worked else "never attached")
        # Attaching is not carrying. The held thing has to actually travel with the hand.
        check(carried > 0.15, "a held object travels with the hand",
              f"moved {carried:.2f} m while held")

    if "throw" in d:
        speed = float(d["throw"][0])
        check(speed > 2.0, "THROW releases with real speed",
              f"{speed:.2f} m/s at release" + ("  (a drop, not a throw)" if speed <= 2.0 else ""))

    if "limp" in d:
        tilt = float(d["limp"][0])
        # Going limp must visibly collapse the body - it is the whole comedy, and it is
        # also how being grabbed, winded or knocked out is expressed.
        check(tilt > 45, "going limp actually collapses the body",
              f"tilt {tilt:.1f} deg while limp" + ("  (barely sagged)" if tilt <= 45 else ""))

    print("=" * 62)
    print("ACTIVE RAGDOLL - verification")
    print("=" * 62)
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
