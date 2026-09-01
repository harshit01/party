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
| Bots fill empty slots | **Yes — a session runs with 1 real player** | CONFIRMED Aug 2026. 2–8 participants; any slot not taken by a human is a bot. Solo play must work. Consequence: a *participant* is never assumed to be a network connection. |

### Bots — implications to keep in view
- **Architectural (settled by building it right):** a participant is an abstraction
  over *either* a `NetworkConnection` *or* a bot controller. Host-authoritative
  already, so bots simulate on the host. Deciding this before gameplay exists is
  cheap; retrofitting it would be a second netcode-grade retrofit.
- **Cost (open):** bot behaviour is per-minigame. Twelve minigames means twelve bot
  policies. This is real, recurring work and should weigh on which four get cut.
- **Product (open, founder's call):** the differentiator is a host who runs gags
  about *your friends*. A lobby of five bots gives him less to work with. Worth
  deciding whether the host treats bots as characters in their own right or mostly
  ignores them.

### SOLVED (Sep 2026) — Red Light builds were intermittently corrupt

**The scene was the problem, not the build.** Measured one condition at a time
(`Docs/build_experiments.tsv`, `WORKLOG.md`):

| condition | result |
|---|---|
| regenerate the scene before every build | **3/8 good** — a coin flip |
| one BAD scene, built 8 times | **0/8**, all `level0` 199640 bytes |
| one GOOD scene, built 6 times | **6/6**, all `level0` 199700 bytes |

The build is **completely deterministic**: same scene in, byte-identical player out.
What is nondeterministic is scene *generation*. Three regenerations produce three
different files containing exactly the same 430 GameObjects with identical names —
Unity assigns random anchor ids and writes the objects in a different **order** each
time. Some orders produce a player that dies at startup, which matches the signature
already recorded twice in this codebase: a MonoBehaviour deserialising past the end of
the data.

**The old entry here said the opposite** — that `level0` differed on every build even
with a fixed seed, so the non-determinism was inside Unity's serialisation. That reading
came from diffing two scenes whose anchor ids had all moved (25,780 lines of churn for
identical content) and from the varying sizes of the *failed* builds. The fixed seed
works fine; good builds are byte-identical. `Tools/scene_canon.py` normalises anchor ids
so scenes can be compared on content.

**What this means day to day:** the committed `RedLight.unity` has been built and booted.
Leave it alone and `./Tools/build_verified.sh` succeeds first time. Only when
`RedLightSetup` or the scene content changes do you regenerate — `./Tools/build_verified.sh -r`
hunts for a scene that boots and tells you to commit it. **Commit the regenerated scene.**

Still open: *why* certain object orders break serialisation. A known-good and a
known-bad scene are both preserved, so this is now bisectable rather than intermittent —
it just is not worth doing while the workaround is a pinned file.

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
│   ├── CONCEPT.md  MINIGAMES.md  STEAM_PLAN.md  TOOLCHAIN.md
│   ├── REDLIGHT_TUNING.md         measured behaviour; settle tuning with numbers
│   ├── FIRST_STEPS_MAC.md         setup, corrected for this Mac
│   ├── FINDING_01_steerability.md why the previous concept was dropped
│   └── ArtTarget/menu_target.svg  APPROVED design target for the home screen
├── Server/hostserver.py    THE HOST - built, running, proven live in-game
├── Tools/build_verified.sh build + smoke-test + retry (see KNOWN ISSUE)
├── Tests/
│   ├── netcode_sync_test.sh    two processes, one world, positions compared
│   ├── round_pacing_test.sh    a round has a shape (stops, duration, ends)
│   ├── redlight_regression.sh  FULL Red Light suite - artifact, session, networked, AI
│   ├── redlight_report.py      15 checks over a session log
│   ├── bias_sweep.sh           same fixed seeds either side of a change
│   ├── sweep_summary.py        spares/frames/wipeouts per seed
│   ├── fixtures/               known-good + known-bad logs; the analyser is
│   │                           calibrated against these before it is believed
│   ├── compare_census.py       positional agreement at matched NetworkTime
│   └── test_host.py            host quality check - PASSES
└── Unity/                  Unity 6000.5.9f1, 3D URP
    └── Assets/_Party/
        ├── Scenes/  Menu.unity  RedLight.unity  NetTest.unity
        ├── Prefabs/ PartyPlayer.prefab  RedLightDirector.prefab
        ├── Art/     generated textures, LuckiestGuy.ttf, Kenney/ (CC0)
        └── Scripts/ Runtime/{Character,Juice,RedLight}  Editor/
```

### Proven with evidence
- **Netcode.** Host-authoritative, Mirror + kcp locally. Two processes agree on rosters
  and positions at matched NetworkTime; lag is bounded and uniform, not divergent.
  `Tests/netcode_sync_test.sh`.
- **Red Light, Barnaby (MINIGAMES.md #9).** A real round: ~10 STOP calls, hazards,
  eliminations, a winner or a timeout. Barnaby SPARES favourites and FRAMES grudges,
  and both fire - now measured at 2.45 spares and 0.35 frames per round over 20 rounds
  on fixed seeds. `Tests/redlight_regression.sh` (15 checks, calibrated against a
  known-bad fixture) and `Tests/bias_sweep.sh`.

  CORRECTED Aug 2026. This entry used to cite `round_pacing_test.sh` as evidence that
  both levers fire. That script only ever checked STOP count and round duration, so the
  claim was untested - and when it WAS tested, a six-round session produced 0 spares
  while one player was framed in all six rounds. Both were fixed; see
  `Docs/REDLIGHT_TUNING.md` for the numbers.

  Still open: >50% of rounds end in a wipeout with nobody reaching the line. That is
  NOT a bias problem - Barnaby causes 7 of 91 eliminations; the rest are bots twitching
  during STOP. See the tuning doc.
- **The host, live in game.** Real callbacks to earlier rounds. Cold call 1.7-2.9s,
  cached 0.083s - pre-generation is load-bearing exactly as section 4 says.
- **Bots.** Per-minigame policies, unstick themselves, indistinguishable from humans
  on the wire. Solo play works.

### Built, working in the editor, not independently verified
- **Menu.unity** - five panels (Home, Character, Multiplayer, Settings, Controls),
  character customiser with live preview, settings that persist, procedural audio.
- **The Filament** - this game's contestant. Wide dome head, white eyes, cone nose,
  and inside the dome a glowing wire whose brightness and steadiness encode standing
  with Barnaby. Seven customisation categories, ~77k combinations, all primitives.
- **Art** - Kenney CC0 packs (171 models) dress both scenes. Nothing commissioned,
  nothing paid for, so the no-bespoke-art rule and the trademark check both hold.

### NEVER EXECUTED
- **Steam.** No SteamAPI.Init, no lobby, no join code, not once. The code is written
  and fails loudly rather than silently. It needs a SECOND STEAM ACCOUNT: Windows has
  `hprishu`, the Mac needs a different one. This is the last of the four risks in
  section 2 still completely unproven.

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
1. **Second Steam account** (new email, free). Windows already has `hprishu`; the Mac
   needs a different one. New accounts are "limited" until $5 is spent and limited
   accounts CANNOT ADD FRIENDS - so use the JOIN CODE path, which is already built and
   works on a limited account.
2. **Line up 2-3 friends to playtest.** Still the long pole. Nobody outside this machine
   has played it.
3. **Steamworks Partner account** - weeks of verification. Do NOT pay the $100 yet.
4. **Rotate the OpenAI API key** if it was ever pasted into a chat transcript.

**Build, in order:**
0. ~~Git remote + LFS~~ **DONE**
1. ~~Unity project, 3D URP~~ **DONE**
2. ~~Net stack installed and compiling~~ **DONE**
2a. ~~Prove the netcode locally~~ **DONE** - two processes, measured, tested
2b. **PROVE STEAM P2P.** Two machines, a lobby, two Filaments in sync. Blocked only on
    the second account. `Tools/build_verified.sh` produces the Windows-testable player.
3. ~~Red Light, Barnaby~~ **DONE and playable**
4. **Minigames #10 and #11** (`MINIGAMES.md` Family D). The docs put these next for a
   reason: three cheap games answer "is this host actually fun?" far faster than one
   polished one, and that question is the whole project. Currently ONE minigame exists
   while the front end has had many passes.
5. Round loop + scoring across minigames - the spine that turns games into a session.

**How to run everything:**
```bash
# the host (leave running in its own terminal; needs OPENAI_API_KEY in ~/.zshrc)
.venv/bin/python Server/hostserver.py
curl -s http://127.0.0.1:8790/health          # expect {"ok": true, ...}

# tests - Unity must be CLOSED, it locks the project
./Tests/netcode_sync_test.sh
./Tests/round_pacing_test.sh

# builds - Unity must be CLOSED. Never call Unity's build method directly.
./Tools/build_verified.sh
```
In the editor: open `Assets/_Party/Scenes/Menu.unity` and press Play.

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
7. **Unity's success signals mean nothing.** In one session it silently no-opped
   `-importPackage`, exited 0 through compile errors, and reported `Succeeded` on builds
   that produced a corrupt player. Verify the ARTIFACT, never the report. This is why
   `BuildTools` calls `EditorApplication.Exit(1)` by hand and why `build_verified.sh`
   boots the player before believing it.
8. **Calibrate your own experiments, not just the game's tests.** Eight rounds went into
   a build bug and produced four confident WRONG conclusions - cumulative isolation that
   changed four things at once, comparisons between runs that used different scene files,
   and a "it's flaky" call that was retracted then turned out right. The decisive clue
   (a file size that differed on every build) was visible from round two and read past
   twice. Change ONE thing, restore it, and re-run the same configuration before
   believing any result.
9. **For anything visual, get a reference image FIRST.** Three visual passes were spent
   guessing at a look the founder could show in one screenshot. `Docs/ArtTarget/` exists
   for this: agree the target, then build to it and compare.
10. **A per-frame probability inside a multi-second window is a certainty with extra
   steps.** Three instances of this exact shape were found in one night (Sep 2026):
   `WouldFrame` rolled every frame of a STOP made "rarer than sparing" into the same
   victim being framed in 6 of 6 rounds; a bot's "small twitch chance" of 2% per frame
   became ~91% across a 1.5-2.6s freeze; and the same odds in a game that only rolls
   once per sequence produced ZERO frames in 6 rounds. Decide ONCE per event and leave
   the odds alone. Related: check the JOINT probability, not the conditional - the twitch
   was already gated at 5-30% per stop, so fixing the inner roll barely moved the outcome
   and a confident prediction that it would was wrong.

11. **Small models fail stacked NEGATIVE constraints.** gpt-4o-mini obeyed an
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
