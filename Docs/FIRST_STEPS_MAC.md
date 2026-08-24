# First steps on the MacBook — do these in order

Written for a fresh session picking this up on the Mac. Read `HANDOFF.md` first.

---

## STEP 0 — the repo, BEFORE the Unity project

Do this first so nothing is ever lost and both machines stay in sync.

```bash
# on Windows (already committed locally, just needs a remote):
cd ~/Documents/Dev/party
git remote add origin git@github.com:<org>/<repo>.git
git push -u origin main

# on the Mac:
git clone git@github.com:<org>/<repo>.git
cd <repo>
```

**Enable Git LFS before the first Unity asset lands.** Unity projects accumulate
binaries (models, textures, audio) and git handles them badly. Retrofitting LFS after
they are committed is a rewrite of history.

```bash
brew install git-lfs && git lfs install
```

**CORRECTED Aug 2026 — do NOT put `*.unity` and `*.asset` in LFS.** The original
line here did. Unity 6 writes both as *text YAML*: the URP template alone produces
31 `.asset` files, including every file in `ProjectSettings/`. In LFS they become
opaque blobs — no diffs, and a scene or project-settings conflict between the Mac
and the Windows laptop can only be resolved by discarding one side wholesale.

The committed `.gitattributes` therefore puts only genuinely binary types in LFS
and routes Unity's YAML through Unity's own merge tool.

**Register the merge driver once per machine** (the path is machine-local, so it
lives in `.git/config`, not in the repo):

```bash
# macOS — note it is under Helpers/, not Tools/ as Unity's docs claim
UYM="/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/Helpers/UnityYAMLMerge"
git config --local merge.unityyamlmerge.name "Unity SmartMerge"
git config --local merge.unityyamlmerge.driver "'$UYM' merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""
git config --local merge.unityyamlmerge.recursive binary
```

On the Windows laptop the binary is at
`C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\Tools\UnityYAMLMerge.exe`.
**Until that is registered on both machines, scene merges will conflict as plain text.**

---

## STEP 1 — two decisions to make BEFORE creating the project

### 1a. 2D or 3D?  → **CONFIRMED Aug 2026: 3D URP with deliberately primitive art**

This was the open question. Confirmed by the founder. 3D, but not expensive 3D:

- Characters are **capsules and spheres**. No rigged models, no animation, no artist.
  Physics does the acting — exactly the "bath toy" look in the design document.
- Unity's **3D physics is more mature than 2D** for shoving, ragdolls and falling,
  which is what most of the minigames are built on.
- Art can be upgraded later from the Asset Store without redesigning anything.
- Cost is close to 2D as long as the discipline holds: *no bespoke art until the
  minigames have survived playtesting.*

If that discipline feels unlikely to hold, 2D is the safer choice and nothing in the
design breaks. But do not start in 2D and change your mind — that is a rewrite.

### 1b. Net stack → **CONFIRMED Aug 2026: Mirror + FizzySteamworks**

| Option | Verdict |
|---|---|
| **Mirror** (+ FizzySteamworks transport) | **Recommended.** Free, mature, enormous community. For small-player-count P2P over Steam it is the most trodden path, which matters a great deal on a first netcode project — when you get stuck, someone has already asked your question. |
| Unity Netcode for GameObjects (+ Facepunch transport) | Official and well documented, but fewer answers in the wild for this exact shape. |

**Pick before writing gameplay.** Retrofitting netcode is the worst retrofit in game
development — worse than input, worse than art.

---

## STEP 2 — install Unity

Unity Hub → Install Editor → newest **Unity 6 LTS** (version starts `6000.`, tagged LTS).

**Already installed on the Mac: `6000.5.9f1`** with `mac-il2cpp`, `windows-mono`,
`webgl`, `documentation`. Nothing more needs installing to start.

**Modules to tick:**
- ✅ **Windows Build Support (Mono)** — already installed.
  CORRECTION: Unity does not offer a Windows *IL2CPP* module on a macOS host — IL2CPP
  for Windows needs MSVC and must be compiled on Windows. Mono is the only Windows
  target buildable from this Mac, which is fine for dev/test builds. The shipping
  IL2CPP build gets made on the Windows laptop.
- ✅ Mac Build Support (usually default)
- ❌ Android / iOS / WebGL / Linux — not needed, several GB each

Then: **New project → 3D (URP)**, created *inside the cloned repo folder*.

---

## STEP 3 — packages, immediately after first open

| Package | Where | Why |
|---|---|---|
| **Input System** | Package Manager | Keyboard + gamepad, rebindable. Add now. |
| **Mirror** | Asset Store (free) | Netcode |
| **FizzySteamworks** | GitHub | Mirror transport over Steam P2P |
| **Steamworks.NET** | GitHub | Steam Lobbies, invites, join codes |

---

## STEP 4 — 🔴 the first milestone: PROVE THE NETCODE

**Before any minigame, before the host, before art:**

> Two machines. A Steam lobby. Two capsules moving around a flat plane, synced,
> with names above them.

That is the whole first milestone. It is deliberately not a game.

**Why this order:** every concept in this project so far was killed or corrected by a
cheap test that should have run on day one. Netcode is now the single biggest
technical risk, and it is the one thing that makes everything downstream either easy
or miserable. If lobby + join + movement sync is painful, that must be discovered in
week one, not month four.

Only once two capsules move around reliably: **Red Light, Barnaby** (`MINIGAMES.md`
#9). One button, no art, and the host IS the mechanic.

---

## STEP 5 — verify the host service runs on macOS

```bash
brew install python
pip3 install openai
export OPENAI_API_KEY="sk-..."          # add to ~/.zshrc to persist
python3 Server/hostserver.py
curl http://127.0.0.1:8790/health
```

Then confirm a real line comes back:

```bash
curl -X POST http://127.0.0.1:8790/host/say \
  -H "Content-Type: application/json" \
  -d '{"beat":"intro","next_game":"Plank Panic","players":[{"name":"Ravi","score":0}]}'
```

---

## What is no longer needed

- **Controllers are not a blocker.** The game is online and keyboard-first; every
  player is on their own PC. Buy one or two only to test gamepad support.
- **Remote Play Together is not the online strategy.** It merges all keyboards into a
  single input stream and supports only one keyboard player. See `STEAM_PLAN.md`.

## Still open

- The game's **name** — and the trademark/collision check happens *before* any art is
  commissioned.
- **TTS provider** for the host's voice. The one place worth spending AI budget.
- Which four of the twelve minigames get cut. Answered by playtesting, not argument.
- **Employer-IP question** if the MacBook and account are employer-provided.
