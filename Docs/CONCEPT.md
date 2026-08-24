# Party game — concept v0.1

**Shape:** local party game, 2–8 players, one screen. Online for free via Steam
Remote Play Together (no netcode, no servers, no matchmaking, no empty lobbies).

**Structure (founder decision):** hybrid — one core spine + minigame rounds.
**Differentiator (founder decision):** an AI host who genuinely watched you play.

---

## The pitch

You are contestants on a chaotic televised game show. A host runs the whole
night — introduces the challenges, mocks the losers, invents nicknames, and
**remembers everything that happened**. By round five he is running gags about
your friends that he invented himself.

The game is fun without him. He is what makes it *your* night instead of a
generic product.

## Why this shape (the lessons that produced it)

Two previous concepts died because **the AI WAS the game**, so fun depended
entirely on the AI performing — unreliable, and every fix cost a measured test
cycle. See `prior-project/Docs/HALTED.md` and `Docs/FINDING_01_steerability.md`.

This inverts it:
- Fun comes from **mechanics and players** — tunable, testable, reliable.
- If the AI vanished, there is still a game.
- No "AI game" positioning → dodges the Steam AI-disclosure penalty
  (17% of Next Fest demos disclosed AI; only 6% of the top 50).
- **AI can be promoted to the foreground later** — the growth path.

And the hybrid structure de-risks *fun itself*: build 12 minigames, playtest,
delete the 5 that flop. Fun is discovered **incrementally** instead of gambled on
one novel mechanic. That is the single most important property for a solo dev who
cannot afford another all-or-nothing bet.

## The three layers

**1. The spine — the show's course**
A board/ladder the contestants move along. Landing on spaces triggers show events:
traps, swaps, the host offering a shady deal. This is what creates sabotage,
comebacks and grudges — the reason to keep playing between minigames.

**2. The minigames — the moment-to-moment fun**
Short (30–60s), simple controls, 2–8 players, physical comedy. Each is small,
independently testable, and independently disposable. Target ~12 built, ~8 shipped.

**3. The host — the identity**
Barnaby Quill. Receives structured game state each beat (standings, what just
happened, full history) and speaks 2–3 sentences. Memory across the whole session
is the whole point: running gags, callbacks, nicknames that stick.

VERIFIED (`Tests/test_host.py`): given three rounds of state, the host invented a
running gag about a player falling in the same moat twice, then paid it off in the
finale while also calling back to round one. Specific, warm, genuinely funny.
Needs tightening — runs 3–4 sentences, wants to be 2.

## The AI growth path (founder requirement)

| Stage | Host role | Risk |
|---|---|---|
| **v1.0** | Narrates and remembers. Pure flavour. | None — cut it and the game still ships |
| **v1.x** | Runs show events; can favour or punish players | Low |
| **v2** | **Can be bargained with** — argue, flatter, bribe him for advantage | Medium |
| **v3** | A genuine opponent/character in his own right | The original "AI as main character" ambition, on a shipped foundation |

## Open / not yet decided
- Uniqueness check — **not yet run.** Hard requirement. Must verify no shipped
  party game has a memory-driven AI host before committing.
- The spine's exact form (board vs ladder vs bracket).
- Minigame list.
- Whether the host is voiced (TTS) at v1 — almost certainly yes; a *read* host is
  a fraction as good as a *heard* one.
- Machine: Windows here, or the MacBook. Repo is not in git yet either way.
