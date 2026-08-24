"""
Test — is the AI HOST actually worth having?

The host is the differentiator: a game-show presenter who genuinely watched you
play and remembers it. Cheap to test, and unlike the last two concepts this is NOT
existential — if the host is flat we still have a party game. But if it is good, it
is the identity.

Bar (judged by reading the output, not a score):
  1. SPECIFIC   — names real players and real events, never generic filler
  2. CALLBACKS  — references something from an EARLIER round
  3. SHORT      — 2-3 sentences; a host who rambles kills pace
  4. FUNNY      — punches at the situation, stays warm, never actually cruel

Usage:  python Tests/test_host.py
"""
import os
import sys

from openai import OpenAI

NL = chr(10)

HOST_SYS = """You are BARNABY QUILL, host of a chaotic televised game show.

You are a real presenter: theatrical, quick, delighted by disaster. You know these
contestants by name and you remember everything that has happened tonight.

HOW YOU TALK:
- 2-3 short sentences. Never more. Pace is everything on a live show.
- Always name specific contestants and specific things that just happened.
- Whenever you can, call back to something from an EARLIER round. Running gags are
  your whole job. If someone has failed the same way twice, that IS the joke.
- Punch at the situation, not at the person's worth. Warm, never genuinely cruel.
- Never mention being an AI, a model, or a program. You are a television host.
- No stage directions, no asterisks. Just what you say into the microphone.

Speak your next piece to camera."""


def state_block(rounds, standings, note):
    lines = ["SHOW SO FAR:"]
    for i, r in enumerate(rounds, 1):
        lines.append(f"  Round {i} — {r}")
    lines.append("")
    lines.append("STANDINGS: " + ", ".join(f"{n} {p}pts" for n, p in standings))
    lines.append("")
    lines.append("WHAT JUST HAPPENED: " + note)
    return NL.join(lines)


SCENARIOS = [
    dict(
        label="round 1 — first blood",
        rounds=["'Plank Panic' — everyone fell off except Ravi."],
        standings=[("Ravi", 3), ("Priya", 0), ("Sam", 0), ("Kofi", 0)],
        note="Ravi won Plank Panic without moving at all. He just stood still.",
    ),
    dict(
        label="round 3 — a pattern forms",
        rounds=["'Plank Panic' — everyone fell off except Ravi.",
                "'Cake Sprint' — Priya fell in the moat on the first corner.",
                "'Bucket Roulette' — Priya fell in the moat again."],
        standings=[("Ravi", 7), ("Kofi", 5), ("Sam", 4), ("Priya", 1)],
        note="Priya has now fallen in the same moat two rounds running.",
    ),
    dict(
        label="final round — a comeback",
        rounds=["'Plank Panic' — everyone fell off except Ravi.",
                "'Cake Sprint' — Priya fell in the moat on the first corner.",
                "'Bucket Roulette' — Priya fell in the moat again.",
                "'Sudden Ladder' — Sam knocked Ravi off at the last second.",
                "'Final Gauntlet' — Priya won by a mile."],
        standings=[("Priya", 11), ("Ravi", 10), ("Kofi", 8), ("Sam", 7)],
        note="Priya, who fell in the moat twice, has just won the whole show.",
    ),
]


def main():
    if not os.environ.get("OPENAI_API_KEY"):
        sys.exit("OPENAI_API_KEY not set.")
    client = OpenAI()
    model = sys.argv[1] if len(sys.argv) > 1 else "gpt-4o"

    print(f"{NL}  HOST TEST — Barnaby Quill ({model}){NL}")
    for s in SCENARIOS:
        block = state_block(s["rounds"], s["standings"], s["note"])
        r = client.chat.completions.create(
            model=model, temperature=1.0, max_tokens=110,
            messages=[{"role": "system", "content": HOST_SYS},
                      {"role": "user", "content": block}])
        print(f"  [{s['label']}]")
        print(f"    {r.choices[0].message.content.strip()}")
        print()


if __name__ == "__main__":
    main()
