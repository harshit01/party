# NIGHT SHIFT — brief for an unattended session

You are continuing this project with **nobody watching**. The founder is asleep. Read
`HANDOFF.md` first, then this, then `WORKLOG.md` to see what the previous session did.

This file is not documentation. It is the mechanism. A fresh session starts blind, so
anything not written down here or in `WORKLOG.md` gets re-litigated or silently
reversed at 3am.

---

## HARD BOUNDARIES — do not cross these

- **Never touch bazaarvoice or salesforce.** Explicitly out of bounds, stated by the
  founder. That means no work systems at all: no Jira, no Confluence, no Google Drive,
  no work email. This repo and this machine's local toolchain only.
- **Git identity is `Harshit Pandey <hprishu@gmail.com>`**, repo-local. Never the work
  email. Check `git config --local user.email` before the first commit of a session.
- **Never force-push, never rewrite history, never `git reset --hard` on pushed work.**
  Push to `origin/redlight-regression`. Do not push to `main`.
- **Never delete a test to make it pass.** If a test fails, either the code is wrong or
  the test is wrong — decide which, in writing, in `WORKLOG.md`.
- **No bespoke/paid art, no asset purchases, no accounts, no spending.** The standing
  rule is CC0 or generated only (`HANDOFF.md` §2).
- The regression suite makes **live paid API calls**. Do not run it in a loop.

## Reference games

The founder named **Fall Guys** as the visual and camera target — see
`Docs/ArtTarget/redlight_target.svg` and the Fall Guys notes in `CameraRig.cs`. The
concept doc describes the overall structure as **Pummel Party shaped** (a board/journey
spine punctuated by short minigames). Those two are the references actually on record.
**Do not invent others.** The founder mentioned "other games" but had not named them at
the time of writing; if a later message names them, add them here.

What "like Fall Guys" has meant in practice, decided and built:
- Third-person camera **behind your own character**, not a broadcast shot of the field
- Other players stay **visible** — the crowd stumbling around you is the appeal
- Name tags stay, but **small, near, faded with distance, never your own**
- A **count** of who is left, never a roster of names
- Bright saturated candy palette, chunky rounded geometry, no deep black
- Dev overlays never on screen in normal play (F3 toggles; `PARTY_DEV_BUILD=1` for a
  development build)

## Priority order

1. **The build corruption.** Measured 2026-09-01: ~6 successes in ~50 attempts (**12%**,
   not the 1-in-3 `HANDOFF.md` claims). `build_verified.sh` gives up after 10, so about
   **1 in 3.5 invocations fails outright** — twice in one day. This throttles everything
   else. Fix or materially improve it before building new content.
2. **Minigame #10 "Say What He Says"** (`MINIGAMES.md` Family D). Host-driven, minimal
   art, so few unattended visual decisions.
3. **Minigame #11 "The Prediction"** (Family D).
4. **Round loop / scoring spine** across minigames.

Do NOT build all twelve. `MINIGAMES.md` expects ~4 of 12 to be cut after playtesting,
and nobody outside this machine has played it yet. Depth over breadth.

## Standard of work — these are not optional

From `HANDOFF.md` §6, each learned the expensive way:

- **Verify the artifact, never the report.** Unity reports success on builds that
  produce a corrupt player. `build_verified.sh` boots the player before believing it.
- **Calibrate the instrument before trusting it.** Every analyser gets a known-good and
  a known-bad fixture. `Tests/redlight_regression.sh` phase 0 does this and refuses to
  run if the broken fixture comes back green. Any new suite must do the same.
- **Change ONE thing, re-run the SAME configuration.** Eight rounds once went into a
  build bug and produced four confident wrong conclusions from cumulative isolation.
- **Never swallow exceptions.** Fail loudly.
- **For anything visual, produce a mockup FIRST** and leave it for approval rather than
  committing a guess. `Docs/ArtTarget/` is where they go.

## Anything visual

The founder's rule is reference-image-before-building, and it has already paid for
itself twice in one session. Unattended, that means: **build the mockup, commit the
mockup, do not build the feature.** Queue it in `WORKLOG.md` under "NEEDS APPROVAL".
Camera, lighting, HUD layout and palette all count as visual.

## Every session must

1. `git config --local user.email` → confirm it is the personal address.
2. Confirm Unity is closed (`pgrep -f "Unity.app/Contents/MacOS/Unity"`) — it locks the
   project and every build dies.
3. Append to `WORKLOG.md`: what you did, what you measured, what you decided and why,
   what is still open. Measurements, not impressions.
4. Commit in small reviewable pieces with real commit messages, and push before the
   session ends.
5. If you hit something genuinely needing the founder's judgement, **write it under
   "NEEDS APPROVAL" in `WORKLOG.md` and carry on with something else** — the instruction
   is to keep going, not to stop and wait.
