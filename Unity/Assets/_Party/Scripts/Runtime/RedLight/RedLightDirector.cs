using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Party.RedLight
{
    /// <summary>
    /// "Red Light, Barnaby" - MINIGAMES.md #9, Family D.
    ///
    /// Move on GO, freeze on STOP, first to the line wins. The host calls it, and he is
    /// biased and he lies.
    ///
    /// WHY THIS IS THE RIGHT NEXT BUILD: one button, no art, and the host IS the mechanic.
    /// It exercises input, the round loop, host integration and latency in one slice.
    ///
    /// THE GRACE WINDOW - the important bit.
    /// A client renders behind the host (Mirror's snapshot interpolation buffers against
    /// jitter; measured at roughly 1.2-1.9 m on loopback at 7 m/s, which is ~200 ms). If
    /// the server judged the instant it decided to call STOP, players would be punished
    /// for a command they had not seen yet, and it would feel like cheating rather than
    /// looking like it - which is precisely the wrong kind of unfair for this game. The
    /// unfairness is supposed to come from Barnaby's opinions, not from the network.
    ///
    /// So STOP is announced, then a grace window passes, and only then is the freeze
    /// snapshot taken. The window is the interpolation buffer plus each player's own
    /// round-trip time, so a laggier player gets more slack, not less.
    /// </summary>
    public class RedLightDirector : NetworkBehaviour
    {
        [Header("Course")]
        // COURSE LENGTH IS A TIMING DECISION, NOT A LAYOUT ONE.
        // The original course was 26 units - about 3.7s at the 7 m/s cap - while a single
        // GO window lasts up to 4.5s. Players therefore reached the line during the FIRST
        // GO and the game ended in under 8 seconds having never once called STOP. The
        // course must be many GO windows long or there is no game.
        [Tooltip("Players win by crossing this Z.")]
        public float finishZ = 46f;
        [Tooltip("Players are returned here at the start of a round.")]
        public float startZ = -46f;

        [Header("Timing")]
        [Tooltip("Hard cap. Whoever is furthest up the lane wins when it expires.")]
        public float roundTimeLimit = 70f;
        public float countdownSeconds = 3f;
        // Short, unpredictable GO windows. Long ones let everyone sprint the whole
        // course; the tension is in being caught mid-stride.
        public Vector2 goDuration   = new Vector2(1.1f, 2.6f);
        public Vector2 stopDuration = new Vector2(1.5f, 2.6f);

        [Header("Judging")]
        [Tooltip("Movement beyond this during STOP counts as moving.")]
        public float moveThreshold = 0.75f;
        [Tooltip("Baseline grace after STOP is called, on top of each player's RTT.")]
        public float baseGrace = 0.65f;

        // ---- replicated round state ----
        [SyncVar(hook = nameof(OnPhaseChanged))] public RoundPhase phase = RoundPhase.Waiting;
        [SyncVar] public double phaseEndsAt;
        [SyncVar] public int    round;
        [SyncVar(hook = nameof(OnCallChanged))] public string callText = "";
        [SyncVar] public string verdictText = "";

        public static RedLightDirector Instance { get; private set; }

        BarnabyBias _bias;
        HostVoice   _voice;
        readonly Dictionary<uint, Vector3> _freezeSnapshot = new Dictionary<uint, Vector3>();
        // Who Barnaby has decided to fit up THIS stop. Decided once, when the freeze
        // snapshot is taken - see TakeFreezeSnapshot.
        readonly HashSet<uint> _framedThisStop = new HashSet<uint>();
        bool   _snapshotTaken;
        double _judgeAt;
        int    _stopsThisRound, _spares, _frames;
        double _roundEndsAt;
        double _nextStandingPush;

        void Awake()
        {
            Instance = this;
            _voice = GetComponent<HostVoice>();
        }

        public override void OnStartServer()
        {
            // The seed is CHOOSABLE and always LOGGED.
            //
            // Barnaby's opinions are drawn at random, and how the night goes depends
            // heavily on that draw - one session had nobody above the +0.25 spare gate
            // and produced 0 spares and six wipeouts, while another with the same code
            // produced 12 spares and none. Comparing two runs of different seeds and
            // calling the difference an improvement is exactly the mistake HANDOFF.md
            // section 6.8 was written about: change ONE thing and re-run the SAME
            // configuration before believing any result.
            //
            // Logging it also means any session a human reports as bad can be replayed
            // exactly, instead of being described from memory.
            int seed = Random.Range(int.MinValue, int.MaxValue);
            string[] argv = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (argv[i] == "-partybiasseed" && int.TryParse(argv[i + 1], out int s))
                { seed = s; break; }

            Debug.Log($"[RedLight] bias seed={seed}");
            _bias = new BarnabyBias(seed);
        }

        // ------------------------------------------------------------------
        // server round loop
        // ------------------------------------------------------------------

        [Server]
        public void BeginRound()
        {
            round++;
            _freezeSnapshot.Clear();
            _framedThisStop.Clear();
            _stopsThisRound = _spares = _frames = 0;

            // His memory softens between rounds. Without this, affinity only ever
            // accumulated and a player's first-round dice roll decided the whole night.
            if (round > 1) _bias?.Decay(0.10f);

            // A GAME SHOW HOST ALWAYS HAS A PET AND A VICTIM - it is not left to dice.
            // Affinity is seeded uniformly on [-1,1], so roughly one lobby in ten draws
            // nobody above the +0.25 spare gate and Barnaby silently has no favourites
            // all night: measured, a whole six-round session with 0 spares and every
            // round a wipeout, because sparing is the only thing that saves a player who
            // twitches. Floor the warmest player and cap the coldest so both halves of
            // the mechanic can always fire.
            _bias?.EnsureFavouriteAndTarget(Players(), 0.4f, -0.4f);
            foreach (PartyPlayer p in Players())
            {
                p.eliminated = false;
                p.finished   = false;
                p.ServerTeleport(new Vector3(Random.Range(-6f, 6f), 1.1f, startZ));
                p.GetComponent<Juice.PlayerJuice>()?.ResetJuice();
            }

            verdictText = "";
            _roundEndsAt = NetworkTime.time + roundTimeLimit;
            SetPhase(RoundPhase.Countdown, countdownSeconds);
            callText = "Places, please.";
            if (_voice != null) _voice.PrefetchRoundIntro(round, Players());
        }

        void Update()
        {
            if (!isServer) return;

            // Push standing onto every player so the FILAMENT shows it. This is the whole
            // point of the character design: his favouritism becomes visible in the room
            // instead of living in a log nobody reads.
            if (_bias != null && NetworkTime.time >= _nextStandingPush)
            {
                _nextStandingPush = NetworkTime.time + 0.5;
                foreach (PartyPlayer p in Players())
                    p.standing = _bias.AffinityOf(p.netId);
            }
            if (phase == RoundPhase.Waiting || phase == RoundPhase.Finished) return;

            // Judging happens once, after the grace window, not the moment STOP is called.
            if (phase == RoundPhase.Grace && !_snapshotTaken && NetworkTime.time >= _judgeAt)
            {
                TakeFreezeSnapshot();
                SetPhase(RoundPhase.Stop, Random.Range(stopDuration.x, stopDuration.y));
                return;
            }

            if (phase == RoundPhase.Stop) JudgeMovers();

            CheckFinishers();

            if (NetworkTime.time < phaseEndsAt) return;

            switch (phase)
            {
                case RoundPhase.Countdown:
                    CallGo();
                    break;
                case RoundPhase.Go:
                    CallStop();
                    break;
                case RoundPhase.Stop:
                    CallGo();
                    break;
            }
        }

        [Server] void SetPhase(RoundPhase p, float seconds)
        {
            phase = p;
            phaseEndsAt = NetworkTime.time + seconds;
        }

        [Server] void CallGo()
        {
            _snapshotTaken = false;
            _freezeSnapshot.Clear();
            // GO/STOP calls are LOCAL and instant. An LLM round trip here would be five
            // seconds of dead air in the one place the game cannot afford it
            // (HANDOFF.md section 4) - the model writes the commentary, never the timing.
            callText = HostPhrases.Go();
            SetPhase(RoundPhase.Go, Random.Range(goDuration.x, goDuration.y));
        }

        [Server] void CallStop()
        {
            callText = HostPhrases.Stop();
            _stopsThisRound++;
            // Announce now, judge later. Grace = interpolation buffer + slowest RTT.
            _snapshotTaken = false;
            // Compute the window ONCE - calling WorstRtt() twice could hand the snapshot
            // and the phase two different deadlines and judge before the grace elapsed.
            double grace = baseGrace + WorstRtt();
            _judgeAt = NetworkTime.time + grace;
            SetPhase(RoundPhase.Grace, (float)grace);
        }

        [Server] double WorstRtt()
        {
            double worst = 0;
            foreach (NetworkConnectionToClient c in NetworkServer.connections.Values)
                if (c != null && c.identity != null) worst = System.Math.Max(worst, NetworkTime.rtt);
            return System.Math.Min(worst, 0.5);   // never hand out more than half a second
        }

        [Server] void TakeFreezeSnapshot()
        {
            _freezeSnapshot.Clear();
            _framedThisStop.Clear();
            foreach (PartyPlayer p in Players())
            {
                _freezeSnapshot[p.netId] = p.transform.position;

                // ONE ROLL PER STOP. JudgeMovers runs every frame while the lane is
                // frozen, and it used to roll WouldFrame on every pass - so a 17%
                // per-frame chance became a certainty within milliseconds. Measured:
                // the same player framed in 6 of 6 rounds, and the remote client in
                // 3 of 3. BarnabyBias says framing "must sting, not exhaust"; rolled
                // per frame it could only ever exhaust. The odds live in WouldFrame;
                // this decides how often they are consulted.
                if (!p.eliminated && !p.finished && _bias.WouldFrame(p.netId))
                    _framedThisStop.Add(p.netId);
            }
            _snapshotTaken = true;
        }

        [Server] void JudgeMovers()
        {
            if (!_snapshotTaken) return;

            foreach (PartyPlayer p in Players())
            {
                if (p.eliminated || p.finished) continue;
                if (!_freezeSnapshot.TryGetValue(p.netId, out Vector3 frozen)) continue;

                bool moved = Vector3.Distance(p.transform.position, frozen) > moveThreshold;

                if (moved && _bias.WouldSpare(p.netId))
                {
                    // He saw it. He is choosing not to have seen it.
                    verdictText = $"{p.displayName} moved. Barnaby looks the other way.";
                    _spares++;
                    Debug.Log($"[RedLight] SPARED {p.displayName} (standing={_bias.Describe(p.netId)})");
                    _freezeSnapshot[p.netId] = p.transform.position;   // fresh baseline, so it is a real reprieve
                    continue;
                }

                if (moved) { Eliminate(p, "moved"); continue; }

                if (_framedThisStop.Contains(p.netId))
                    Eliminate(p, "framed");
            }
        }

        [Server] void Eliminate(PartyPlayer p, string reason)
        {
            p.eliminated = true;

            // READ HIS OPINION BEFORE CHANGING IT.
            // This log line is the only record of WHY someone went out, and the
            // regression suite reads it to check that frames land on grudges and
            // spares on favourites. Reading Describe() after the nudge below reported
            // the standing the player had AFTER being wronged: a grudge at -0.40 got
            // +0.15 and was logged as "neutral", so a correctly-targeted frame looked
            // like a mis-targeted one. The verdict must describe the moment of the
            // decision, not its consequence.
            string standing = _bias.Describe(p.netId);

            // THE DIRECTION HERE IS THE WHOLE MECHANIC.
            // Both cases used to be -0.1, so framing someone unfairly made him dislike
            // them MORE and framing them again more likely still. That is a death
            // spiral, not a grudge: one player was framed every round of a six-round
            // session and rode the affinity floor to -1.0 while nobody was ever spared.
            // A victim who stays the victim is a rule, and the game's signature is
            // supposed to be a host who is CAPRICIOUS.
            //
            // So he overreaches, the room sees it, and he softens on them - which sends
            // last round's victim up toward being this round's pet, and rotates who the
            // story is about. Getting genuinely caught still annoys him.
            // BEING CAUGHT IS NOT AN OPINION. It used to cost -0.10, but getting
            // caught moving is simply how you LOSE this game, so that penalty landed on
            // nearly everyone nearly every round. Affinity stopped tracking what Barnaby
            // thought of you and started tracking how many rounds had elapsed: with a
            // 10% fade the recurrence is a(n+1) = 0.9*a(n) - 0.10, whose fixed point is
            // -1.0, so the WHOLE LOBBY sank together. Measured over six rounds, one
            // player's standing went 0.230, 0.107, -0.003, -0.103, -0.193, -0.273 -
            // matching that recurrence to three decimals - and nobody was ever sparable
            // because the spare gate is +0.25. His opinions now come only from things
            // that are about HIM: he wronged you, or you are running away with it.
            if (reason == "framed") { _frames++; _bias.Nudge(p.netId, +0.15f); }

            verdictText = reason == "framed"
                ? $"{p.displayName} — OUT. (Barnaby: \"I saw that.\" They did not move.)"
                : $"{p.displayName} — OUT.";

            Debug.Log($"[RedLight] {p.displayName} out ({reason}, standing={standing})");
            if (_voice != null) _voice.PrefetchCallout(p.displayName, reason == "framed", Players());
        }

        [Server] void CheckFinishers()
        {
            foreach (PartyPlayer p in Players())
            {
                if (p.eliminated || p.finished) continue;
                if (p.transform.position.z < finishZ) continue;

                p.finished = true;
                // A showman wants a close race, not a runaway leader - so winning costs
                // you standing. This is also what stops Decay flattening everyone to
                // neutral: fading alone gives him no NEW opinions, just weaker old ones.
                _bias.Nudge(p.netId, -0.15f);
                verdictText = $"{p.displayName} reaches the line!";
                SetPhase(RoundPhase.Finished, 0f);
                Summarise("winner:" + p.displayName);
                if (_voice != null) _voice.PrefetchFinale(p.displayName, Players());
                return;
            }

            // A ROUND MUST END. Without this a survivor who never reaches the line keeps
            // the game running forever - observed in testing: 4 of 5 out, one straggler,
            // and 90 seconds with no result. A party minigame is meant to be short
            // (MINIGAMES.md: 8 rounds in a ~30 minute session), so time runs out and the
            // furthest player takes it.
            if (NetworkTime.time >= _roundEndsAt && phase != RoundPhase.Finished)
            {
                PartyPlayer best = null;
                float bestZ = float.MinValue;
                foreach (PartyPlayer p in Players())
                {
                    if (p.eliminated) continue;
                    if (p.transform.position.z > bestZ) { bestZ = p.transform.position.z; best = p; }
                }
                if (best != null)
                {
                    best.finished = true;
                    _bias.Nudge(best.netId, -0.15f);   // same: leading is not endearing
                    verdictText = $"Time! {best.displayName} was furthest.";
                    SetPhase(RoundPhase.Finished, 0f);
                    if (_voice != null) _voice.PrefetchFinale(best.displayName, Players());
                    Summarise("timeout:" + best.displayName);
                    return;
                }
            }

            int alive = 0;
            foreach (PartyPlayer p in Players()) if (!p.eliminated) alive++;
            if (alive == 0 && phase != RoundPhase.Finished)
            {
                verdictText = "Nobody survived. Barnaby is delighted.";
                SetPhase(RoundPhase.Finished, 0f);
                Summarise("wipeout");
            }
        }

        [Server] void Summarise(string outcome)
        {
            int stops = _stopsThisRound, outs = 0;
            foreach (PartyPlayer p in Players()) if (p.eliminated) outs++;
            Debug.Log($"[RedLight] ROUND {round} END outcome={outcome} stops={stops} " +
                      $"eliminated={outs} spared={_spares} framed={_frames}");

            // Standings, every round, so BIAS PERSISTENCE is observable rather than
            // asserted. Barnaby's whole claim is that he remembers who annoyed him three
            // rounds ago; until this line existed the only standing ever logged was the
            // one attached to an elimination, so nothing could check that affinity
            // actually carried between rounds - or that Nudge was souring the right people.
            if (_bias != null)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[RedLight] STANDINGS round={round}");
                foreach (PartyPlayer p in Players())
                    sb.Append($" | {p.displayName}={_bias.AffinityOf(p.netId):F3}({_bias.Describe(p.netId)})");
                Debug.Log(sb.ToString());
            }
        }

        public static IEnumerable<PartyPlayer> Players() =>
            Object.FindObjectsByType<PartyPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        /// <summary>May this player move right now? Consulted by PartyPlayer.</summary>
        public bool MovementAllowed(PartyPlayer p)
        {
            if (p.eliminated || p.finished) return false;
            return phase == RoundPhase.Go || phase == RoundPhase.Stop || phase == RoundPhase.Grace;
        }

        /// <summary>Is standing still currently required? Drives the UI colour.</summary>
        public bool MustFreeze => phase == RoundPhase.Stop || phase == RoundPhase.Grace;

        void OnPhaseChanged(RoundPhase _, RoundPhase now) { }
        void OnCallChanged(string _, string now) { }
    }
}
