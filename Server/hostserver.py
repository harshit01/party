"""
The Host service — Barnaby Quill.

Takes structured game state, returns 2-3 sentences of live commentary. Runs
locally during development; the API key never reaches the game binary.

    POST /host/say
    {
      "session_id": "abc",
      "beat": "intro" | "reaction" | "board" | "deal" | "finale",
      "players": [{"name": "Priya", "score": 4, "position": 3}, ...],
      "history": ["Round 1 - Plank Panic: Ravi won, Priya fell first", ...],
      "just_happened": "Priya fell in the moat again",
      "next_game": "Bucket Roulette"          # for beat=intro
    }
 -> {"line": "...", "cached": false}

    GET  /health

WHY PRE-GENERATION MATTERS
A party game cannot afford five seconds of dead air waiting for an LLM while four
people stare at a screen. The outcome of a round is knowable a beat before it ends,
so the game calls /host/say EARLY and the line is ready the instant the round does.
Responses are cached by (session_id, beat, hash of state) so an early speculative
call and the real one return the same line without paying twice.

LESSON CARRIED OVER (see prior-project/Docs/HALTED.md):
Failures are LOUD. A silent fallback once hid a broken instrument for three test
runs and produced confident, meaningless results.
"""

import hashlib
import json
import os
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

from openai import OpenAI

NL = chr(10)
MODEL = os.environ.get("HOST_MODEL", "gpt-4o")
PORT = int(os.environ.get("HOST_PORT", "8790"))

HOST_SYS = """You are BARNABY QUILL, host of a chaotic televised game show.

You are a real presenter: theatrical, quick, delighted by disaster. You know these
contestants by name and you remember everything that has happened tonight.

HOW YOU TALK:
- MAXIMUM 2 sentences. Never more. Pace is everything on a live show and a host
  who rambles kills the room.
- Always name specific contestants and specific things that actually happened.
- Whenever you can, call back to something from an EARLIER round. Running gags are
  your whole job. If someone has failed the same way twice, that IS the joke.
- Punch at the situation, not at the person's worth. Warm, never genuinely cruel.
- Never mention being an AI, a model, or a program. You are a television host.
- No stage directions, no asterisks, no quote marks. Just what you say aloud.

{beat_note}"""

BEAT_NOTES = {
    "intro": "RIGHT NOW: introduce the next challenge. Tie it to where people stand.",
    "reaction": "RIGHT NOW: react to the round that just finished. This is your big moment.",
    "board": "RIGHT NOW: comment on someone's move or what they landed on.",
    "deal": ("RIGHT NOW: offer ONE contestant a shady deal, built from their actual "
             "night. Make the offer concrete (what they gain, what they risk) and "
             "end by asking if they accept."),
    "finale": ("RIGHT NOW: crown the winner and hand out one invented title per "
               "contestant based on how their night actually went."),
}

_client = OpenAI()
_cache = {}


def build_state(req):
    lines = []
    hist = req.get("history") or []
    if hist:
        lines.append("SHOW SO FAR:")
        for i, h in enumerate(hist, 1):
            lines.append(f"  Round {i} - {h}")
        lines.append("")
    players = req.get("players") or []
    if players:
        standing = sorted(players, key=lambda p: -int(p.get("score", 0)))
        lines.append("STANDINGS: " + ", ".join(
            f"{p.get('name','?')} {p.get('score',0)}pts" for p in standing))
        lines.append("")
    if req.get("next_game"):
        lines.append("NEXT CHALLENGE: " + str(req["next_game"]))
    if req.get("just_happened"):
        lines.append("WHAT JUST HAPPENED: " + str(req["just_happened"]))
    return NL.join(lines)


def cache_key(req):
    blob = json.dumps({k: req.get(k) for k in
                       ("session_id", "beat", "players", "history",
                        "just_happened", "next_game")}, sort_keys=True)
    return hashlib.sha256(blob.encode("utf-8")).hexdigest()


def say(req):
    key = cache_key(req)
    if key in _cache:
        return _cache[key], True

    beat = req.get("beat", "reaction")
    sys_prompt = HOST_SYS.format(beat_note=BEAT_NOTES.get(beat, BEAT_NOTES["reaction"]))
    r = _client.chat.completions.create(
        model=MODEL, temperature=1.0, max_tokens=90,
        messages=[{"role": "system", "content": sys_prompt},
                  {"role": "user", "content": build_state(req)}])
    line = (r.choices[0].message.content or "").strip().strip('"')
    _cache[key] = line
    if len(_cache) > 500:
        _cache.pop(next(iter(_cache)))
    return line, False


class Handler(BaseHTTPRequestHandler):
    def _send(self, code, payload):
        body = json.dumps(payload).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path == "/health":
            return self._send(200, {"ok": True, "model": MODEL, "cached": len(_cache)})
        self._send(404, {"error": "not found"})

    def do_POST(self):
        if self.path.rstrip("/") != "/host/say":
            return self._send(404, {"error": "not found"})
        try:
            n = int(self.headers.get("Content-Length", 0))
            req = json.loads(self.rfile.read(n) or b"{}")
        except Exception as e:
            return self._send(400, {"error": f"bad json: {e}"})
        try:
            line, cached = say(req)
        except Exception as e:
            # LOUD. A silent fallback once hid a broken instrument for three runs.
            print(f"  !! HOST FAILED: {type(e).__name__}: {str(e)[:120]}", file=sys.stderr)
            return self._send(502, {"error": f"{type(e).__name__}: {str(e)[:120]}"})
        self._send(200, {"line": line, "cached": cached})

    def log_message(self, fmt, *a):
        sys.stderr.write("  [host] " + fmt % a + NL)


if __name__ == "__main__":
    if not os.environ.get("OPENAI_API_KEY"):
        sys.exit("OPENAI_API_KEY not set.")
    print(f"{NL}  BARNABY QUILL - host service")
    print(f"    http://127.0.0.1:{PORT}/host/say")
    print(f"    model : {MODEL}")
    print(f"    (local only - the key never reaches the game binary){NL}")
    HTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
