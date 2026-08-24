# Toolchain

## DECIDED (2 Aug 2026): we build on WINDOWS, not the MacBook.

Reasons, in order of weight:
1. **Windows is the Steam target platform.** You can build a Windows exe from a Mac
   but you cannot properly TEST it there (Apple Silicon cannot run x86 Windows
   natively). The Mac would ADD a second environment, not replace this one.
2. **The toolchain already works here** - Unity 6 + URP compiling, Python, API key,
   test harnesses all verified running.
3. **No employer-IP ambiguity.** The MacBook is an office machine on an office
   account; commercial IP built there can carry ownership questions.

## Already installed and verified on this machine
- Unity Hub + Unity 6000.5.6f1, Universal 3D (URP)
- Python 3.12 + openai package
- OPENAI_API_KEY set
- git (via Git Bash)

## Still needed here
| Item | Why | Status |
|---|---|---|
| ~~2-4 controllers~~ | **NO LONGER REQUIRED.** The game is online and keyboard-first; each player is on their own PC. Buy one or two for testing gamepad support only. | optional |
| **2-3 friends who will playtest** | You cannot playtest a party game alone, and remote testers are now the long pole | **line up now** |
| Unity **Input System** package | Keyboard + gamepad rebinding | in-Unity, 1 min |
| **Net stack**: Mirror or Unity NGO + a Steam transport | Online is now core. Decide BEFORE writing gameplay - retrofitting netcode is the worst retrofit there is | decide early |
| **Steamworks.NET or Facepunch.Steamworks** | Steam Lobbies + P2P sockets | free |
| New Unity project (3D URP) for the party game | The the prior project project is the halted game | 5 min in Hub |
| Steamworks Partner account | Weeks of company/tax verification, no dependency on the game | start now |
| TTS provider for the host's voice | A read host is a fraction as good as a heard one | decision open |

## Reference: the Mac path (NOT taken)
Kept only in case the decision is ever revisited: Unity Hub + Unity 6 LTS,
Xcode Command Line Tools (`xcode-select --install`), Homebrew, Python 3, VS Code or
Rider (Visual Studio for Mac is discontinued). Tick **Windows Build Support
(IL2CPP)** so Windows builds are possible from macOS.

---

## 4. Art and audio (can wait)

| Tool | Why |
|---|---|
| **Unity Asset Store** | Characters, props, arenas. Money solves the art bottleneck — that was the reason for choosing Unity |
| **Blender** (free) | Tweaking bought assets |
| **Freesound / asset-store SFX** | Impacts, whooshes, crowd reactions |

Placeholder capsules are fine for the first playable. Do not buy art until the
minigames survive playtesting.

## 5. Publishing (much later)

| Thing | Cost | Notes |
|---|---|---|
| **Steamworks account** | **$100** one-off per title | Via the publisher + Kotak |
| **Steam client** | Free | Needed to test **Remote Play Together** — the whole online strategy |
| A Windows machine or VM | — | To smoke-test the Windows build. The existing Windows box covers this |

**Remote Play Together needs no netcode and no SDK integration** — it works on any
game with local multiplayer. That is the entire reason for the local-first design.

---

## Quick check script

Run this in Terminal on the Mac to see what is already there:

```bash
echo "--- git ---";     git --version 2>/dev/null || echo "MISSING"
echo "--- python ---";  python3 --version 2>/dev/null || echo "MISSING"
echo "--- brew ---";    brew --version 2>/dev/null || echo "MISSING"
echo "--- xcode clt ---"; xcode-select -p 2>/dev/null || echo "MISSING"
echo "--- unity hub ---"; ls -d "/Applications/Unity Hub.app" 2>/dev/null || echo "MISSING"
echo "--- unity editors ---"; ls "/Applications/Unity/Hub/Editor" 2>/dev/null || echo "none installed"
echo "--- vscode ---"; ls -d "/Applications/Visual Studio Code.app" 2>/dev/null || echo "MISSING"
```

## Before anything moves machines

**Nothing is in version control yet.** First practical step either way: create a
**private GitHub repo under the the private org** and push `party`. Without
that, moving between the Windows box and the Mac means copying folders by hand.

## The IP note, once

If the MacBook or the account is employer-provided, work created on it can carry
ownership ambiguity for commercial IP. For a game intended to sell under the publisher
Technologies, a personal machine or personal account removes that question entirely.
Flagging once; your call.
