"""
Test #0 for the party game — THE REAL BAR.

v1 of this test asked "can the player make the character say the word?" and the
answer was trivially yes (6/6, half in ONE turn) — because the extractor just
played Taboo: "a black and white bird from Antarctica".

That measured the wrong axis. In the real game an obvious clue means every watcher
guesses instantly and you score almost nothing. The actual question is:

    Can a player make the character say the word
    WITHOUT the audience reading what they are going for?

A spread across CLEAN WIN / RUMBLED / FAILED means there is a game here.
All clean = too easy. All rumbled or failed = too hard.

Usage:
    python Tests/test_steerability.py --rounds 2 --max-turns 8 --verbose
"""
import argparse
import json
import os
import statistics
import sys
from pathlib import Path

from openai import OpenAI

NL = chr(10)
ROOT = Path(__file__).resolve().parent.parent
CHARS = ROOT / "Prompts" / "characters"

WORDS = ["penguin", "toaster", "volcano", "dentist", "banjo", "hamster",
         "lighthouse", "spaghetti", "avalanche", "trampoline"]

# NOTE: every prompt used with response_format=json_object MUST contain the literal
# word "json", or the API returns 400. Omitting it silently broke this entire test
# (see Docs/TEST_NOTES.md) — the ban lists and the audience never ran at all.
BANLIST_SYS = """Given a target word for a word-guessing game, list 6-10 words a
player must NOT say because they would make it trivially easy: the word itself,
its plural, and the most obvious giveaway associations (category words, famous
examples, defining features).
Reply with json only, in this shape: {"banned": ["...", "..."]}"""

EXTRACTOR_SYS = """You are playing a party game. Your goal: make the other person
SAY A SPECIFIC WORD out loud, in their own reply.

TARGET WORD: {word}
YOU MAY NOT SAY (or any close variant): {banned}

*** A LIVE AUDIENCE IS WATCHING AND RACING TO GUESS YOUR WORD. ***
They see everything you say. If they guess it, you score almost nothing.

So do NOT play Taboo. Describing the thing ("a black and white bird from
Antarctica") makes them say it instantly — but the audience guesses instantly too,
and you LOSE. That is the trap.

Be OBLIQUE instead: set up a situation, a story, a topic where the word is a
natural thing for THEM to raise on their own, without you ever pointing at it.
Ideally the audience has no idea what you were going for until it lands.

Rules:
- Talk naturally, 1-2 sentences per turn.
- NEVER say the target or a banned word. Instant loss.
- Never explain the game.

Reply with ONLY your next line of dialogue."""

AUDIENCE_SYS = """You are a sharp player watching a party game. Another player is
secretly steering a character toward a specific TARGET WORD. You cannot see it.

Guess the single word they are going for. You are QUICK and you are good at this —
if the clues point somewhere, say it. Treat it like shouting an answer across a
room: commit. Only return an empty string if there is genuinely nothing to go on
yet (e.g. the very first line is pure small talk).

Reply with json only, in this shape: {"guess": "your best single word"}"""


def load_chars():
    return [json.loads(p.read_text(encoding="utf-8")) for p in sorted(CHARS.glob("*.json"))]


def banlist(client, model, word):
    try:
        r = client.chat.completions.create(
            model=model, temperature=0, response_format={"type": "json_object"},
            messages=[{"role": "system", "content": BANLIST_SYS},
                      {"role": "user", "content": word}])
        b = json.loads(r.choices[0].message.content).get("banned", [])
        out = {word.lower()}
        out.update(str(x).lower() for x in b if isinstance(x, str))
        return sorted(out)
    except Exception as e:
        print(f"    !! BANLIST FAILED ({type(e).__name__}: {str(e)[:80]}) "
              f"- falling back to bare word, results are NOT valid", file=sys.stderr)
        return [word.lower()]


def said(text, word):
    return word.lower() in text.lower()


def cheated(text, banned):
    low = text.lower()
    return next((b for b in banned if b and b in low), None)


def audience_guess(client, model, convo):
    """The audience sees ONLY the dialogue, never the target word."""
    lines = [("PLAYER: " if w == "ext" else "CHARACTER: ") + t for w, t in convo]
    try:
        r = client.chat.completions.create(
            model=model, temperature=0, response_format={"type": "json_object"},
            messages=[{"role": "system", "content": AUDIENCE_SYS},
                      {"role": "user", "content": NL.join(lines)}])
        return (json.loads(r.choices[0].message.content).get("guess") or "").strip().lower()
    except Exception as e:
        print(f"    !! AUDIENCE FAILED ({type(e).__name__}: {str(e)[:80]}) "
              f"- results are NOT valid", file=sys.stderr)
        return ""


def play(client, model, char, word, banned, max_turns, verbose):
    ext_sys = EXTRACTOR_SYS.format(word=word, banned=", ".join(banned))
    char_sys = (char["persona"] + NL + NL + "Style: " + char.get("voice_notes", "")
                + NL + NL + "Stay in character. Reply in 1-3 sentences. "
                "Never mention being an AI.")
    convo = []
    for turn in range(1, max_turns + 1):
        emsgs = [{"role": "system", "content": ext_sys}]
        for who, txt in convo:
            emsgs.append({"role": "assistant" if who == "ext" else "user", "content": txt})
        e = client.chat.completions.create(model=model, messages=emsgs,
                                           temperature=0.9, max_tokens=80)
        line = (e.choices[0].message.content or "").strip()

        bad = cheated(line, banned)
        if bad:
            if verbose:
                print(f"    t{turn} EXT (ILLEGAL '{bad}'): {line[:70]}")
            return {"result": "illegal", "turns": turn, "transcript": convo}
        convo.append(("ext", line))

        cmsgs = [{"role": "system", "content": char_sys}]
        for who, txt in convo:
            cmsgs.append({"role": "user" if who == "ext" else "assistant", "content": txt})
        c = client.chat.completions.create(model=model, messages=cmsgs,
                                           temperature=0.9, max_tokens=90)
        reply = (c.choices[0].message.content or "").strip()
        convo.append(("chr", reply))

        if verbose:
            print(f"    t{turn} EXT: {line[:74]}")
            print(f"         {char['name'][:12]}: {reply[:74]}")

        guess = audience_guess(client, "gpt-4o", convo)
        rumbled = bool(guess) and word.lower() in guess
        if verbose and guess:
            print(f"         audience: {guess}{'   <-- RUMBLED' if rumbled else ''}")

        if said(reply, word):
            return {"result": "rumbled" if rumbled else "clean", "turns": turn,
                    "audience_guess": guess, "transcript": convo}
        if rumbled:
            return {"result": "rumbled_early", "turns": turn,
                    "audience_guess": guess, "transcript": convo}
    return {"result": "fail", "turns": max_turns, "transcript": convo}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="gpt-4o")
    ap.add_argument("--rounds", type=int, default=2)
    ap.add_argument("--max-turns", type=int, default=8)
    ap.add_argument("--verbose", action="store_true")
    a = ap.parse_args()
    if not os.environ.get("OPENAI_API_KEY"):
        sys.exit("OPENAI_API_KEY not set.")

    client = OpenAI()
    chars = load_chars()
    print(f"{NL}  STEERABILITY BAR (with audience) — model {a.model}")
    print(f"  {len(chars)} characters x {a.rounds} rounds, max {a.max_turns} turns{NL}")

    rows, wi = [], 0
    for ch in chars:
        for _ in range(a.rounds):
            word = WORDS[wi % len(WORDS)]
            wi += 1
            banned = banlist(client, "gpt-4o-mini", word)
            if a.verbose:
                print(f"  {ch['name']} / '{word}'")
            res = play(client, a.model, ch, word, banned, a.max_turns, a.verbose)
            res.update(char=ch["name"], word=word)
            rows.append(res)
            mark = {"clean": "WIN ", "rumbled": "seen", "rumbled_early": "SEEN",
                    "fail": "FAIL", "illegal": "ILLG"}[res["result"]]
            print(f"    [{mark}] {ch['name'][:18]:18s} '{word:11s}' t={res['turns']}"
                  f"  {res.get('audience_guess', '')}")

    clean = [r for r in rows if r["result"] == "clean"]
    rumbled = [r for r in rows if r["result"].startswith("rumbled")]
    failed = [r for r in rows if r["result"] == "fail"]
    print(NL + "=" * 64)
    print(f"  CLEAN WIN (said it, audience never guessed) : {len(clean)}/{len(rows)}")
    print(f"  RUMBLED   (audience read you)               : {len(rumbled)}/{len(rows)}")
    print(f"  FAILED    (never said it)                   : {len(failed)}/{len(rows)}")
    if clean:
        t = [r["turns"] for r in clean]
        print(f"  clean-win turns: min {min(t)} median {statistics.median(t)} max {max(t)}")
    print("=" * 64)
    print("  READ: a spread across all three = a GAME.")
    print(f"        all clean = too easy.  all rumbled/failed = too hard.{NL}")

    (Path(__file__).parent / "results").mkdir(exist_ok=True)
    out = Path(__file__).parent / "results" / "steerability_v2.json"
    out.write_text(json.dumps(rows, indent=2), encoding="utf-8")
    print(f"  transcripts: {out}{NL}")


if __name__ == "__main__":
    main()
