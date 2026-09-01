#!/usr/bin/env python3
"""
Canonicalise a Unity scene so two of them can be compared on CONTENT.

WHY THIS EXISTS
Unity assigns every object in a scene a random anchor id (`--- !u!1 &658387`). Two
scenes generated from identical code with a fixed Random seed therefore differ in
thousands of lines and by several hundred bytes while containing exactly the same
objects - a plain `diff` between them is 25,780 lines of noise and tells you nothing.

That noise is very likely what produced the conclusion recorded in HANDOFF.md, that
"level0 is a different size on every build even with a fixed seed, so the
non-determinism is inside Unity's serialisation". Measured, the good builds are
byte-identical; it is the anchor ids that move, not the content.

This renumbers every anchor id in document order and rewrites every reference to match,
so what is left in a diff is real: a missing component, a different mesh, a changed
float.

    ./Tools/scene_canon.py <scene.unity> [out.unity]
"""
import re
import sys

ANCHOR = re.compile(r"^--- !u!(\d+) &(\d+)(.*)$")
FILEID = re.compile(r"fileID: (\d+)")


def canon(path):
    lines = open(path, errors="replace").read().split("\n")

    # Pass 1: map every anchor id to its position in the document.
    order, n = {}, 0
    for ln in lines:
        m = ANCHOR.match(ln)
        if m and m.group(2) not in order:
            n += 1
            order[m.group(2)] = n

    # Pass 2: rewrite anchors and every reference to them. Ids not declared in this
    # file (0, and references into other assets) are left exactly as they are - they
    # are real content, and renumbering them would hide genuine differences.
    out = []
    for ln in lines:
        m = ANCHOR.match(ln)
        if m:
            out.append(f"--- !u!{m.group(1)} &{order[m.group(2)]}{m.group(3)}")
            continue
        out.append(FILEID.sub(
            lambda r: f"fileID: {order.get(r.group(1), r.group(1))}", ln))
    return "\n".join(out)


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    text = canon(sys.argv[1])
    if len(sys.argv) > 2:
        open(sys.argv[2], "w").write(text)
        print(f"canonicalised -> {sys.argv[2]}")
    else:
        sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
