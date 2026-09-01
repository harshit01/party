using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using Party.RedLight;

namespace Party.SayWhat
{
    /// <summary>
    /// "Say What He Says" - MINIGAMES.md #10, Family D.
    ///
    /// Barnaby calls a sequence, you perform it back, and it gets one step longer every
    /// time. Last one still standing wins; the first one out is the loser, which the
    /// design doc says matters more, because the loser is what he mocks.
    ///
    /// WHY THIS IS THE RIGHT SECOND MINIGAME
    /// It shares Family D's whole toolkit with Red Light - the host, the bias, the
    /// participant model, the bots - so it costs a fraction of #9 (MINIGAMES.md: "game #4
    /// in a family costs a fraction of game #1"). But it is not the same game: Red Light
    /// is reflex under a lying referee, this is MEMORY under a lying referee. Two cheap
    /// games that fail differently answer "is this host actually fun?" far faster than one
    /// polished one.
    ///
    /// BARNABY'S BIAS IS THE SAME OBJECT, DELIBERATELY.
    /// This uses <see cref="BarnabyBias"/> rather than a private copy, because the whole
    /// product claim is a host who remembers you across the night (HANDOFF.md §4). If he
    /// held a separate grudge per minigame he would be a different character each round.
    /// Here his favouritism reads as: a pet gets away with fluffing the sequence, and
    /// someone he has taken against is told they got it wrong when they did not.
    ///
    /// HOST-AUTHORITATIVE. The sequence exists only on the server until it is called, so
    /// a client cannot read ahead - which matters because two of the founder's reference
    /// games (Codenames, The Chameleon) are exactly about who knows what.
    /// </summary>
    public class SayWhatDirector : NetworkBehaviour
    {
        [Header("Sequence")]
        [Tooltip("Steps in the first sequence of a round.")]
        public int startLength = 3;
        [Tooltip("Hard cap, so a round cannot run forever if everyone is very good.")]
        public int maxLength = 9;

        [Header("Timing")]
        [Tooltip("Seconds Barnaby spends calling each step.")]
        public float callBeat = 0.85f;
        [Tooltip("Seconds a player gets per step to perform it back.")]
        public float performPerStep = 1.15f;
        public float countdownSeconds = 3f;
        public float judgeSeconds = 2.2f;
        [Tooltip("Whole-round cap. A party minigame is 30-60s (MINIGAMES.md).")]
        public float roundTimeLimit = 90f;

        // ---- replicated round state ----
        [SyncVar] public SayWhatPhase phase = SayWhatPhase.Waiting;
        [SyncVar] public double phaseEndsAt;
        [SyncVar] public int    round;
        [SyncVar] public int    sequenceNumber;
        [SyncVar] public string callText = "";
        [SyncVar] public string verdictText = "";
        /// <summary>The sequence, packed one action per byte, revealed step by step.</summary>
        [SyncVar] public string revealed = "";

        public static SayWhatDirector Instance { get; private set; }

        BarnabyBias _bias;
        HostVoice   _voice;
        System.Random _rng;
        int _seed;

        readonly List<PartyAction> _sequence = new List<PartyAction>();
        /// <summary>Every sequence this round, so a bot can confidently replay an old one.</summary>
        readonly List<List<PartyAction>> _history = new List<List<PartyAction>>();
        readonly Dictionary<uint, List<PartyAction>> _submitted = new Dictionary<uint, List<PartyAction>>();

        int    _revealIndex;
        double _nextReveal;
        double _roundEndsAt;
        double _roundStartedAt;
        int    _spares, _frames, _outs;
        double _nextStandingPush;

        public IReadOnlyList<PartyAction> Sequence => _sequence;
        public IReadOnlyList<List<PartyAction>> History => _history;

        void Awake()
        {
            Instance = this;
            _voice = GetComponent<HostVoice>();
        }

        public override void OnStartServer()
        {
            // Seed is choosable and always logged, for the same reason Red Light's is: a
            // session a playtester calls bad has to be replayable exactly, and comparing
            // two runs on different seeds is how you conclude the wrong thing (§6.8).
            int seed = Random.Range(int.MinValue, int.MaxValue);
            string[] argv = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (argv[i] == "-partybiasseed" && int.TryParse(argv[i + 1], out int s))
                { seed = s; break; }

            Debug.Log($"[SayWhat] bias seed={seed}");
            _seed = seed;
            _bias = new BarnabyBias(seed);
            _rng  = new System.Random(seed);

            // SEED THE BOTS TOO - but know what this does and does not buy.
            //
            // Every bot's recall accuracy, confusion odds and pace come from
            // UnityEngine.Random, which was never seeded, so two runs of the same seed
            // drew different bots. Seeding here (before PartyNetworkManager fills the
            // lobby) fixes that, and makes the SEQUENCES identical run to run.
            //
            // It does NOT make a session bit-reproducible, and measurement says so:
            // two runs of seed 5150 still ended round 1 after 4 sequences and 3. Bot
            // actions are paced off Time.time, so frame timing decides how many land
            // inside the Perform window - and that is inherent to a real-time game
            // rather than something worth engineering away.
            //
            // So do NOT compare two single runs and call the difference a result. Compare
            // DISTRIBUTIONS over fixed seeds, the way Tools/bias_sweep.sh does for #9.
            // That is the §6.8 lesson in its practical form.
            Random.InitState(seed);
        }

        // ------------------------------------------------------------------
        // round loop (server)
        // ------------------------------------------------------------------

        [Server]
        public void BeginRound()
        {
            round++;
            _sequence.Clear();
            _history.Clear();
            _submitted.Clear();
            _spares = _frames = _outs = 0;
            sequenceNumber = 0;

            // Re-seed per round so each round is deterministic AND different from the
            // last, rather than every round replaying identically.
            Random.InitState(unchecked(_seed * 397) ^ round);

            if (round > 1) _bias?.Decay(0.10f);
            _bias?.EnsureFavouriteAndTarget(Players(), 0.4f, -0.4f);

            foreach (PartyPlayer p in Players())
            {
                p.eliminated = false;
                p.finished   = false;
            }

            verdictText = "";
            revealed    = "";
            _roundStartedAt = NetworkTime.time;
            _roundEndsAt = NetworkTime.time + roundTimeLimit;
            callText = "Watch me very carefully.";
            SetPhase(SayWhatPhase.Countdown, countdownSeconds);
            if (_voice != null) _voice.PrefetchRoundIntro(round, Players());
        }

        [Server] void SetPhase(SayWhatPhase p, float seconds)
        {
            phase = p;
            phaseEndsAt = NetworkTime.time + seconds;
        }

        void Update()
        {
            if (!isServer) return;

            if (_bias != null && NetworkTime.time >= _nextStandingPush)
            {
                _nextStandingPush = NetworkTime.time + 0.5;
                foreach (PartyPlayer p in Players()) p.standing = _bias.AffinityOf(p.netId);
            }

            if (phase == SayWhatPhase.Waiting || phase == SayWhatPhase.Finished) return;

            // Reveal the sequence one beat at a time so it is watchable rather than a wall
            // of text - and so a player can be caught out by the LAST step, which is where
            // the tension is.
            if (phase == SayWhatPhase.Watch && NetworkTime.time >= _nextReveal
                && _revealIndex < _sequence.Count)
            {
                revealed = Pack(_sequence, _revealIndex + 1);
                callText = Describe(_sequence[_revealIndex]);
                _revealIndex++;
                _nextReveal = NetworkTime.time + callBeat;
            }

            if (NetworkTime.time < phaseEndsAt) return;

            switch (phase)
            {
                case SayWhatPhase.Countdown: StartSequence(); break;
                case SayWhatPhase.Watch:     StartPerform();  break;
                case SayWhatPhase.Perform:   JudgeAll();      break;
                case SayWhatPhase.Judge:     NextOrFinish();  break;
            }
        }

        [Server] void StartSequence()
        {
            sequenceNumber++;
            int len = Mathf.Min(startLength + sequenceNumber - 1, maxLength);

            // A fresh sequence, not an extension of the last one. Extending would let a
            // player who learned steps 1-4 coast; a new one every time is pure memory.
            _sequence.Clear();
            for (int i = 0; i < len; i++)
                _sequence.Add((PartyAction)_rng.Next(1, 7));
            _history.Add(new List<PartyAction>(_sequence));

            _submitted.Clear();
            _revealIndex = 0;
            revealed = "";
            _nextReveal = NetworkTime.time;
            callText = $"Sequence {sequenceNumber}. {len} steps. Watch.";
            SetPhase(SayWhatPhase.Watch, len * callBeat + 0.5f);
            Debug.Log($"[SayWhat] SEQUENCE {sequenceNumber} len={len} steps={Readable(_sequence)}");
        }

        [Server] void StartPerform()
        {
            revealed = "";        // it is gone. That is the game.
            callText = "NOW YOU.";
            SetPhase(SayWhatPhase.Perform, _sequence.Count * performPerStep);
        }

        /// <summary>Called by PartyPlayer when a participant performs an action.</summary>
        [Server]
        public void SubmitAction(PartyPlayer p, PartyAction a)
        {
            if (phase != SayWhatPhase.Perform || p.eliminated || p.finished) return;
            if (a == PartyAction.None) return;

            if (!_submitted.TryGetValue(p.netId, out List<PartyAction> list))
                _submitted[p.netId] = list = new List<PartyAction>();

            // Ignore anything past the length of the sequence rather than failing them for
            // it - a panicked extra keypress after you have already finished correctly
            // should not be the thing that ends your night.
            if (list.Count < _sequence.Count) list.Add(a);
        }

        [Server] void JudgeAll()
        {
            foreach (PartyPlayer p in Players())
            {
                if (p.eliminated || p.finished) continue;

                _submitted.TryGetValue(p.netId, out List<PartyAction> got);
                int matched = Matched(got);
                bool perfect = got != null && got.Count == _sequence.Count
                                           && matched == _sequence.Count;

                // A SPARE HAS TO BE A NEAR MISS.
                //
                // Measured on the first headless run: Barnaby spared a favourite who had
                // matched 0 of 4 - they did nothing right at all and were waved through.
                // That is not favouritism, it is the referee being broken, and it made
                // pets effectively unkillable (one round produced five spares).
                //
                // Requiring at least half the sequence keeps the joke legible: he is
                // choosing not to have seen a fumble, which everyone in the room can
                // recognise as unfair. Letting off someone who stood there doing nothing
                // is not readable as anything.
                bool nearMiss = matched >= Mathf.CeilToInt(_sequence.Count * 0.5f);

                if (!perfect && nearMiss && _bias.WouldSpare(p.netId))
                {
                    _spares++;
                    verdictText = $"{p.displayName} fluffed it. Barnaby decides they did not.";
                    Debug.Log($"[SayWhat] SPARED {p.displayName} " +
                              $"(matched {matched}/{_sequence.Count}, standing={_bias.Describe(p.netId)})");
                    continue;
                }

                if (!perfect) { Eliminate(p, "wrong", matched); continue; }

                if (_bias.WouldFrame(p.netId)) Eliminate(p, "framed", matched);
            }

            SetPhase(SayWhatPhase.Judge, judgeSeconds);
        }

        [Server] int Matched(List<PartyAction> got)
        {
            if (got == null) return 0;
            int n = 0;
            for (int i = 0; i < _sequence.Count && i < got.Count; i++)
            {
                if (got[i] != _sequence[i]) break;
                n++;
            }
            return n;
        }

        [Server] void Eliminate(PartyPlayer p, string reason, int matched)
        {
            // Read his opinion BEFORE changing it - the verdict must describe the moment
            // of the decision, not its consequence. Reading it after the nudge reported a
            // correctly-targeted frame on a grudge as landing on a "neutral".
            string standing = _bias.Describe(p.netId);

            p.eliminated = true;
            _outs++;
            if (reason == "framed") { _frames++; _bias.Nudge(p.netId, +0.15f); }

            verdictText = reason == "framed"
                ? $"{p.displayName} — OUT. (Barnaby: \"That was not what I said.\" It was.)"
                : $"{p.displayName} — OUT after {matched}.";

            Debug.Log($"[SayWhat] {p.displayName} out ({reason}, matched={matched}/{_sequence.Count}, " +
                      $"standing={standing})");
            if (_voice != null) _voice.PrefetchCallout(p.displayName, reason == "framed", Players());
        }

        [Server] void NextOrFinish()
        {
            int alive = 0;
            PartyPlayer last = null;
            foreach (PartyPlayer p in Players())
                if (!p.eliminated) { alive++; last = p; }

            if (alive == 1)
            {
                last.finished = true;
                _bias.Nudge(last.netId, -0.15f);   // leading is not endearing
                verdictText = $"{last.displayName} remembered. Everyone else did not.";
                SetPhase(SayWhatPhase.Finished, 0f);
                if (_voice != null) _voice.PrefetchFinale(last.displayName, Players());
                Summarise("winner:" + last.displayName);
                return;
            }

            if (alive == 0)
            {
                verdictText = "Nobody was listening. Barnaby is thrilled.";
                SetPhase(SayWhatPhase.Finished, 0f);
                Summarise("wipeout");
                return;
            }

            // A ROUND MUST END. Red Light once ran 90 seconds with one straggler and no
            // result; the same trap exists here if everyone is competent.
            if (NetworkTime.time >= _roundEndsAt || sequenceNumber >= maxLength + 2)
            {
                PartyPlayer best = null;
                foreach (PartyPlayer p in Players()) if (!p.eliminated) { best = p; break; }
                if (best != null)
                {
                    best.finished = true;
                    verdictText = $"Time! {best.displayName} was still standing.";
                    if (_voice != null) _voice.PrefetchFinale(best.displayName, Players());
                }
                SetPhase(SayWhatPhase.Finished, 0f);
                Summarise("timeout:" + (best != null ? best.displayName : "nobody"));
                return;
            }

            StartSequence();
        }

        [Server] void Summarise(string outcome)
        {
            // DURATION IS A DESIGN CONSTRAINT, so it gets measured rather than assumed.
            // MINIGAMES.md opens with "2-8 players, one screen, 30-60 seconds" - a round
            // that ends in 20s has not given the host enough to work with, and one that
            // runs to 90s is not a party minigame any more.
            double secs = NetworkTime.time - _roundStartedAt;
            Debug.Log($"[SayWhat] ROUND {round} END outcome={outcome} sequences={sequenceNumber} " +
                      $"eliminated={_outs} spared={_spares} framed={_frames} seconds={secs:F1}");

            if (_bias == null) return;
            var sb = new StringBuilder();
            sb.Append($"[SayWhat] STANDINGS round={round}");
            foreach (PartyPlayer p in Players())
                sb.Append($" | {p.displayName}={_bias.AffinityOf(p.netId):F3}({_bias.Describe(p.netId)})");
            Debug.Log(sb.ToString());
        }

        // ------------------------------------------------------------------
        // helpers
        // ------------------------------------------------------------------

        public static IEnumerable<PartyPlayer> Players() =>
            Object.FindObjectsByType<PartyPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        static string Pack(List<PartyAction> seq, int upTo)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < upTo && i < seq.Count; i++) sb.Append((char)('0' + (int)seq[i]));
            return sb.ToString();
        }

        public static string Readable(IReadOnlyList<PartyAction> seq)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < seq.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(seq[i]);
            }
            return sb.ToString();
        }

        public static string Describe(PartyAction a) => a switch
        {
            PartyAction.Left    => "LEFT",
            PartyAction.Right   => "RIGHT",
            PartyAction.Forward => "FORWARD",
            PartyAction.Back    => "BACK",
            PartyAction.Jump    => "JUMP",
            PartyAction.Bow     => "BOW",
            _ => "",
        };

        /// <summary>Are inputs currently being recorded? Drives the UI colour.</summary>
        public bool Recording => phase == SayWhatPhase.Perform;
    }
}
