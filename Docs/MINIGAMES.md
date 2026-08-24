# The 12 minigames — v0.1

Design rules every one of these obeys:
- **2–8 players, one screen**, 30–60 seconds.
- **Two buttons maximum.** "Playable by all" means a parent can join without a tutorial.
- **A clear winner AND a clear loser.** The loser matters more — that is what the
  host mocks, and the mockery is the product.
- **A signature humiliation.** Each game must fail in its own recognisable way so
  running gags can form ("Priya and the moat, *again*").
- **Readable at a glance** from across a room.

## The structural trick: 12 games, 5 tech families

Each family shares a character controller, arena and camera, so game #4 in a family
costs a fraction of game #1. This is what makes 12 achievable solo — and it means
cutting a flop costs nothing.

---

## Family A — ARENA  (shared: physics character + platform + shove)

**1. Plank Panic**
A narrow plank over a long drop. Shove everyone off. Last one standing wins.
*Signature humiliation:* falling off in the first two seconds, having touched nobody.

**2. Sumo Circle**
A platform that shrinks every 10 seconds. Barge people off the edge.
*Signature:* knocking yourself off with your own charge.

**3. Vanishing Floor**
Tiles drop away in waves. Be standing on something when the wave passes.
*Signature:* sprinting confidently onto the exact tile that is about to go.

**4. The Rising Room**
The floor rises toward a spiked ceiling. Climb over each other to survive.
*Signature:* being used as a step-ladder by a friend.

## Family B — DODGE  (shared: overhead spawner + falling objects)

**5. Bucket Roulette**
Buckets rain down. Most are harmless. Some flatten you.
*Signature:* standing perfectly still in the one fatal spot.

**6. Sticky Fingers**
Grab falling prizes for points. Some are bombs. Greed is punished.
*Signature:* catching three bombs in a row while everyone else scores.

## Family C — RACE  (shared: track + obstacles + bump)

**7. Cake Sprint**
Carry a wobbling cake to the finish. Any bump and it goes everywhere.
*Signature:* dropping it on the finish line.

**8. Greased Ladder**
Climb a slippery ladder. You slide. Others can grab your ankles.
*Signature:* being dragged from first to last in the final second.

## Family D — HOST-DRIVEN  (minimal art, maximum AI — cheapest AND most distinctive)

These are nearly free to build (little animation, no complex physics) and they are
where the host stops being flavour and becomes a **mechanic**. Strong candidates to
build first because they prove the identity at the lowest cost.

**9. Red Light, Barnaby**
The host calls GO and STOP. Move on GO, freeze on STOP. **He is biased and he lies.**
He will let a favourite creep forward, and he will call someone out unfairly —
because he remembers who annoyed him three rounds ago.
*Signature:* being eliminated by a host who is openly cheating against you.

**10. Say What He Says**
The host issues an escalating sequence of silly instructions. Perform them in order.
He generates them live, so they reference the night ("jump twice, then apologise to
Priya for the moat").
*Signature:* confidently doing the sequence from two rounds ago.

**11. The Prediction**
Everyone secretly bets on who will come last in the *next* minigame. Points for
being right. Creates table talk, alliances and betrayal between rounds.
*Signature:* everyone unanimously betting against one person — who then wins.

## Family E — CHAOS  (shared: passable object + hidden timer)

**12. Live Grenade**
Pass the parcel with a timer nobody can see. Hold it when it goes off, you lose.
*Signature:* accepting it with two seconds left because someone lied to you.

---

## Build order (proposed)

1. **Family D first** (#9, #10, #11) — cheapest, proves the AI host as a *mechanic*,
   playable with placeholder art. If the host is not fun here, we learn it for almost nothing.
2. **Family A** (#1–4) — one controller and arena unlocks four games.
3. **Family B, C, E** — fill out variety.

Expect to **cut ~4 of the 12** after playtesting. That is the plan, not a failure —
it is the entire reason for building a collection instead of one mechanic.
