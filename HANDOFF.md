# HANDOFF — read this first

You are picking up a game project mid-flight, in a fresh session on a MacBook.
This file is the complete state.

**Read this, then `Docs/FIRST_STEPS_MAC.md` for the ordered setup, then
`Docs/CONCEPT.md` for the game itself.**


> ### Note on placeholders in this copy
> This copy is deliberately **anonymised** for development on a machine where the
> company name should not appear. Wherever you see **"the publisher"**, **"the private
> org"**, **"the admin account"**, **"the technical founder"** or **"the prior
> project"**, a real identifier has been replaced.
>
> Nothing of substance was lost - only names. The real values are held in the company
> records on the primary machine and get restored when the project is merged back.
> Do not invent replacements; leave the placeholders as they are.

---

## 1. What the game is

An **online party game show**, 2–8 players, **each on their own PC with their own
keyboard**, hosted by an AI presenter who genuinely watched you play and remembers it.

- **Structure:** a board/journey spine punctuated by short minigames (Pummel Party
  shaped), 8 rounds, ~30 minutes a session.
- **The differentiator:** the host. By round five he is running gags about your
  friends that he invented himself, and in the finale he pays them off.
- **Online:** real online multiplayer over **Steam P2P** — Lobbies for friend
  invites and join codes, SteamNetworkingSockets for transport. Valve supplies NAT
  punching and relay **free**, so there are no servers and no hosting bill.
  **Host-authoritative**; friend-invite first rather than public matchmaking, which
  sidesteps the empty-lobby problem.
- **Input: keyboard first** (WASD / Space / Shift), gamepad fully supported.
  Two action buttons maximum, hard rule.
- **Platform:** Steam first, Windows primary target.
- **Publisher:** the publisher. Public credit is a studio name
  only; **the technical founder is never publicly named** (see `the company context file (kept off this machine)`).

## 2. Decisions already made — do not silently reopen these

| Decision | Value | Why |
|---|---|---|
| AI's role | **Background, not the main character** | Two previous concepts died because the AI *was* the game. Fun must survive the AI being removed. Growth path to foreground is in `CONCEPT.md`. |
| Structure | **Hybrid: spine + minigame rounds** | Founder choice. A collection de-risks *fun itself* — build 12, cut 4. |
| Differentiator | **AI host with session memory** | Founder choice. Verified working (§4). |
| Multiplayer | **Online, Steam P2P, keyboard-first** | CORRECTED Aug 2026. Remote Play Together merges all keyboards into one stream - it supports ONE keyboard player. Most Steam players use keyboard and nobody sits together. |
| Uniqueness | **Hard requirement — checked, currently clear** | Re-verify before the Steam page. |
| Dev machine | **MacBook (editor), Windows laptop (test builds)** | The Windows laptop is 7.3 GB RAM / 0.5 GB VRAM — measurably below Unity's floor. It can still *run* a built lightweight game. |
| Engine | Unity 6 LTS, 3D URP | Was 6000.5.6f1 on Windows. |
| Art dimension | **3D URP, primitive art** | CONFIRMED Aug 2026. Capsules and spheres; physics does the acting. No bespoke art until the minigames have survived playtesting. |
| Net stack | **Mirror + FizzySteamworks** | CONFIRMED Aug 2026. Most trodden path for small-count Steam P2P; on a first netcode project, community answers matter more than first-party polish. Steamworks.NET for lobbies/invites/join codes. |

### Open, deliberately undecided
- **Game name.** Not chosen. Do the trademark/collision check BEFORE any art is
  commissioned (this cost us once already).
- **TTS provider** for the host's voice. A *read* host is a fraction as good as a
  *heard* one — this is the one place worth spending AI budget.
- **The board's exact form** (loop vs ladder vs bracket).
- **Employer-IP question:** if the MacBook and its account are employer-provided,
  commercial IP built there can carry ownership ambiguity. Flagged, founder's call.

## 3. What is built and working

```
party/
├── HANDOFF.md              <- you are here
├── Docs/
│   ├── CONCEPT.md          the game, the three layers, the AI growth path
│   ├── MINIGAMES.md        all 12, grouped into 5 shared tech families
│   ├── STEAM_PLAN.md       costs, 30-day rule, MANDATORY AI disclosure
│   ├── TOOLCHAIN.md        what to install, machine decision + specs
│   └── FINDING_01_steerability.md   why the previous concept was dropped
├── Server/
│   └── hostserver.py       THE HOST — built, running, proven
├── Prompts/characters/     3 sample characters (from the dropped concept)
└── Tests/
    ├── test_host.py        host quality check — PASSES
    └── test_steerability.py  from the dropped concept; kept for its method
```

**Unity project EXISTS as of Aug 2026** at `Unity/` — Unity 6000.5.9f1, 3D URP,
created from the editor's own `com.unity.template.urp-blank`. Netcode stack is
installed and compiles clean: Mirror 96.11.2 (vendored in `Assets/Mirror`),
FizzySteamworks 6.0.1 and Steamworks.NET 2025.164.1 (both UPM git deps, pinned in
`packages-lock.json`). Input System 1.20.0 ships with the template.

**No gameplay code has been written.** The next thing to build is the netcode
milestone in §5 step 2a.

Repo: private, `harshit01/party`, pushed over the `github-party` SSH alias.

## 4. The host — built and proven

`Server/hostserver.py`, `POST /host/say`, port 8790. Beats: `intro`, `reaction`,
`board`, `deal`, `finale`. Run it with `OPENAI_API_KEY` set.

Real output, unedited:

> **deal:** *"Priya, my frequent moat diver, I've got a deal sweeter than the cake
> you never reached. How about triple points — if you can avoid water-based
> humiliation in the next round? Do we have a splash-free agreement?"*

It referenced two earlier failures and a third unrelated one, made a concrete offer
with real risk, and asked for acceptance. **That is the product.**

**Pre-generation is built in and is not optional.** The game must call the host
*while the round is still playing* so the line is ready the instant it ends.
Responses are cached by state hash. Five seconds of dead air with four people
staring at a screen kills party pace.

## 5. Next steps

**Founder (no dependency on code):**
1. **Line up 2–3 friends to playtest with.** Now that the game is online and
   keyboard-first, controllers are NO LONGER a blocker — but you cannot playtest a
   party game alone, and remote testers are now the long pole instead.
2. **Steamworks Partner account** — weeks of company/tax verification (W-8BEN-E;
   the publisher has PAN/GST/Kotak already). Do NOT pay the $100 yet — it is per-title and
   starts a 30-day clock that only helps near launch.
3. **Private GitHub repo** under the the private org; push this folder (see §7).

**Build, in order** (full detail in `Docs/FIRST_STEPS_MAC.md`):
0. ~~Git remote + Git LFS~~ **DONE.**
1. ~~Unity project, 3D URP~~ **DONE.**
2. ~~Pick and install the net stack~~ **DONE** — Mirror + FizzySteamworks, verified
   compiling together by a headless build.
2a. **FIRST MILESTONE - PROVE THE NETCODE.** Two machines, a Steam lobby, two
   capsules moving on a flat plane in sync. Not a game. Netcode is now the biggest
   technical risk and every concept in this project has been corrected by a cheap
   test that should have run on day one.
3. **"Red Light, Barnaby"** next (`Docs/MINIGAMES.md` #9). One button, no art, and
   the host IS the mechanic — he calls GO/STOP, he is biased, and he lies. It proves
   input, the round loop, host integration and latency in one slice.
4. Round loop + scoring, then the rest of Family D, then Family A.

## 6. Lessons that cost real time — do not re-learn them

1. **Calibrate any LLM judge before trusting it.** Nine "failures" in the previous
   project were instrument bugs, not game bugs. Always keep a known-bad control; a
   test that passes everything measures nothing.
2. **Never swallow exceptions.** `except: return ""` hid broken API calls for three
   full test runs and produced confident, meaningless results.
3. **`response_format=json_object` requires the literal word "json" in the
   messages** or the API 400s. This silently broke an entire test suite.
4. **Salience beats position in prompts.** Rules must go in the block injected
   *last*, not in a long preamble. Measured: 0.08 vs 2.75 on the same metric.
5. **Do not let a metric become the goal.** A test scoring "keyword echo" taught the
   model to parrot; it scored 100% while the game got worse.
6. **Prove the risky thing before building the framework around it.** Both dropped
   concepts were disproven by a cheap test that should have run on day one.
7. **Small models fail stacked NEGATIVE constraints.** gpt-4o-mini obeyed an
   anti-parrot rule ~75% of the time; gpt-4o reliably.

## 7. Moving machines

```bash
cd ~/Documents/Dev/party
git init && git add -A && git commit -m "Party game: concept, host service, docs"
# create a PRIVATE repo under the the private org, then:
git remote add origin git@github.com:<org>/<repo>.git
git push -u origin main
```

On the Mac: clone it, install the toolchain in `Docs/TOOLCHAIN.md`, set
`OPENAI_API_KEY`, run `python Server/hostserver.py` and hit `/health` to confirm.

**Also worth preserving:** `the prior project (kept off this machine)` — the halted previous project.
Its `Tests/test_judge_calibration.py` and `Tests/charprompt.py` are genuinely
reusable, and `Docs/HALTED.md` explains why that concept was abandoned.
