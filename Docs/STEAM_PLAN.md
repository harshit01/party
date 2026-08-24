# Steam release plan

Steam-first was always the plan, and the design is already built around it: the
game is **local multiplayer specifically so Steam Remote Play Together gives us
online for free** — no netcode, no servers, no matchmaking, no empty lobbies.

## What Steam actually requires

| Item | Detail |
|---|---|
| **Steam Direct fee** | **$100 USD per title**, paid when the app is created. Refunded once the game earns $1,000 gross |
| **30-day rule** | After paying, a **mandatory 30-day wait** before you may release. This is the main bottleneck |
| **Partner account** | Company identity + **tax verification** + bank details. Slow, admin-heavy, and independent of the game |
| **Reviews** | Valve reviews the **store page** before it goes live, and the **build** before launch |
| **Realistic timeline** | ~4–6 weeks absolute minimum from account to launch; **3–4 months is normal** to allow wishlist building |

the publisher already has PAN, GST and the Kotak account, so the company
side exists. As a non-US company we file a **W-8BEN-E**; the India–US treaty
reduces withholding if it is filed correctly. Worth doing properly the first time.

## 🔴 AI disclosure — mandatory, and it applies to us

Valve updated the rules on **16 Jan 2026** into a two-tier system:

- **Pre-generated AI content** — assets shipping in the game files (art, voice lines, text)
- **Live-generated AI content** — *"any content created with the help of AI tools
  while the game is running"*
- **Developer tools are exempt** — code assistants used to build the game do not count

**Our AI host is live-generated content. We must disclose it.** This is not
optional and enforcement is real: 7,300+ games have disclosed, and Valve has begun
removing pages for non-compliance.

### How we handle it
Disclose honestly, but **control the framing**. The AI disclosure field is separate
from the store description, so:
- The **store page sells the party game** — friends, chaos, a host who roasts you.
- The **disclosure field states the truth plainly** — the host's lines are generated
  live by a language model.

This matters because of the measured penalty (see `CONCEPT.md`): 17% of Next Fest
demos disclosed AI, but only 6% of the top-50 most-played. Leading with "AI game"
volunteers for that. Leading with the party game does not — and the disclosure is
still fully honest.

If any TTS voice lines ship pre-rendered, that is **pre-generated** and gets
disclosed too.

## Sequencing — what to do when

### Now (admin, slow, independent of the game)
1. **Create the Steamworks Partner account** for the publisher — company
   verification, W-8BEN-E, bank details. Weeks of lead time, zero dependency on code.
2. **Private GitHub repo** under the the private org. Nothing is in version control yet.

### Do NOT do yet
- **Don't pay the $100.** It is per-title, it starts a 30-day clock that helps us
  only near launch, and we have no playable game. Money spent before we know the
  minigames are fun repeats the mistake we just halted twice.

### When the first minigames are playtested and fun
3. Decide the **name** (and run the trademark/collision check properly — the lesson
   from the prior project: verify *before* commissioning any art).
4. Capsule art, screenshots, a short trailer.
5. Pay the $100, create the app, **put the store page live to start collecting
   wishlists.** Wishlists are the single biggest predictor of launch visibility.

### Before launch
6. Build review, **multiplayer/netcode testing across real connections**, AI
   disclosure completed, pricing set.

## The one thing to remember

**Wishlists are the product before the product.** The store page should go up as
early as it can be made to look good — but not before the game is proven fun, or
we will be marketing something we may still cut.
