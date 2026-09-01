using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using Party.RedLight;

namespace Party.Prediction
{
    public enum PredictionPhase : byte
    {
        Waiting  = 0,
        /// <summary>Everyone secretly names who they think will come LAST.</summary>
        Betting  = 1,
        /// <summary>The wrapped minigame is running.</summary>
        Playing  = 2,
        /// <summary>Bets revealed, points awarded.</summary>
        Reveal   = 3,
        Finished = 4,
    }

    /// <summary>
    /// "The Prediction" - MINIGAMES.md #11, Family D.
    ///
    /// Everyone secretly bets on who will come LAST in the next minigame. Points for being
    /// right. The design doc wants it to create "table talk, alliances and betrayal
    /// between rounds", and its signature is "everyone unanimously betting against one
    /// person - who then wins".
    ///
    /// THIS ONE IS NOT A GAME, IT IS A WRAPPER. #9 and #10 are self-contained; this bets
    /// on another minigame's result, so it drives one through <see cref="IMinigameDirector"/>
    /// and scores what comes out. That makes it the first piece of the round loop
    /// (HANDOFF.md §5 step 5) rather than a twelfth of the collection.
    ///
    /// WHY THE SIGNATURE ACTUALLY HAPPENS, rather than being hoped for.
    /// Barnaby's favouritism is PUBLIC - it is the whole reason the Filament glows
    /// (HANDOFF.md §3), so the room can see who he has taken against. Betting on his
    /// target is therefore the obvious play, and the bots do exactly that. But framing is
    /// probabilistic, so the marked player quite often survives and occasionally wins -
    /// and everyone who piled on gets nothing. The unanimous-bet-that-backfires is an
    /// emergent consequence of the bias engine, not a scripted event.
    ///
    /// HIDDEN INFORMATION, DELIBERATELY. Bets live on the server until the reveal. Two of
    /// the founder's reference games - Codenames and The Chameleon - are built on who
    /// knows what, and this is the first mechanic here that is.
    /// </summary>
    public class PredictionDirector : NetworkBehaviour
    {
        [Header("Timing")]
        public float bettingSeconds = 12f;
        public float revealSeconds  = 6f;

        [Header("Scoring")]
        [Tooltip("Points for correctly naming who came last.")]
        public int pointsForCorrect = 3;
        [Tooltip("Points for winning the minigame itself.")]
        public int pointsForWinning = 2;
        [Tooltip("Bonus when you were the ONLY one who called it right.")]
        public int loneCallBonus = 2;

        [SyncVar] public PredictionPhase phase = PredictionPhase.Waiting;
        [SyncVar] public double phaseEndsAt;
        [SyncVar] public int    round;
        [SyncVar] public string callText = "";
        [SyncVar] public string verdictText = "";

        public static PredictionDirector Instance { get; private set; }

        HostVoice _voice;
        IMinigameDirector _game;

        /// <summary>Who each participant bet on. Server only until the reveal.</summary>
        readonly Dictionary<uint, uint> _bets = new Dictionary<uint, uint>();

        /// <summary>Who came last in the previous round - the one genuinely predictive signal.</summary>
        uint _previousLoser;

        void Awake()
        {
            Instance = this;
            _voice = GetComponent<HostVoice>();
        }

        [Server]
        IMinigameDirector FindGame()
        {
            // Whichever minigame is in the scene. Deliberately not configured by hand:
            // the wrapper should not need editing every time a game is added.
            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (mb is IMinigameDirector d) return d;
            return null;
        }

        [Server]
        public void BeginRound()
        {
            round++;
            _bets.Clear();
            _game = FindGame();

            if (_game == null)
            {
                Debug.LogError("[Prediction] no IMinigameDirector in the scene - nothing to bet on");
                return;
            }

            foreach (PartyPlayer p in _game.Participants) p.placement = 0;

            callText = $"Who comes LAST at {_game.GameName}?";
            verdictText = "";
            SetPhase(PredictionPhase.Betting, bettingSeconds);
            Debug.Log($"[Prediction] ROUND {round} betting opens on {_game.GameName}");

            if (_voice != null) _voice.PrefetchRoundIntro(round, _game.Participants);
        }

        [Server] void SetPhase(PredictionPhase p, float seconds)
        {
            phase = p;
            phaseEndsAt = NetworkTime.time + seconds;
        }

        /// <summary>Place or change a bet. Server-side; hidden from everyone until reveal.</summary>
        [Server]
        public void PlaceBet(PartyPlayer better, PartyPlayer target)
        {
            if (phase != PredictionPhase.Betting || better == null || target == null) return;
            if (better.netId == target.netId) return;   // you may not back yourself to lose
            _bets[better.netId] = target.netId;
        }

        void Update()
        {
            if (!isServer || _game == null) return;
            if (phase == PredictionPhase.Waiting || phase == PredictionPhase.Finished) return;

            if (phase == PredictionPhase.Playing)
            {
                if (_game.RoundIsOver) Reveal();
                return;
            }

            if (NetworkTime.time < phaseEndsAt) return;

            switch (phase)
            {
                case PredictionPhase.Betting: StartGame(); break;
                case PredictionPhase.Reveal:  SetPhase(PredictionPhase.Finished, 0f); break;
            }
        }

        [Server] void StartGame()
        {
            BotsBet();
            LogBets();
            callText = $"Bets are in. {_game.GameName}!";
            SetPhase(PredictionPhase.Playing, 0f);
            _game.BeginRound();
        }

        /// <summary>
        /// Bots read the room. Barnaby's standing is public - it is what the Filament
        /// glow is FOR - so backing whoever he has taken against is the obvious play, and
        /// a bot that ignored it would look stupid rather than beatable.
        ///
        /// The noise matters as much as the signal: if every bot always backed the same
        /// target there would be no table talk, just arithmetic.
        /// </summary>
        [Server] void BotsBet()
        {
            var all = new List<PartyPlayer>(_game.Participants);
            if (all.Count < 2) return;

            foreach (PartyPlayer bot in all)
            {
                // Bots, and autopilot humans in a headless test - otherwise the human
                // slot silently abstains and the betting path is exercised for nobody.
                bool drivesItself = bot.isBot || PartyPlayer.AutopilotEnabled;
                if (!drivesItself || _bets.ContainsKey(bot.netId)) continue;

                // THREE STRATEGIES, MIXED - and the mix was chosen from measurement.
                //
                // Backing Barnaby's target was originally 70% of bets, on the reasoning
                // that his favouritism is public and therefore the obvious read. Measured
                // over 8 rounds it was a TERRIBLE bet: the room piled onto his grudge
                // every round and was right 0-1 times out of 5, because standing does not
                // predict losing at a memory game. Predictions that are never right make
                // the betting pointless - nobody ever scores, so there is nothing to talk
                // about.
                //
                // Form is the signal that actually predicts: whoever came last tends to
                // keep coming last. Mixing the three keeps the pile-on dynamic (which
                // produces the signature) while letting somebody actually collect.
                PartyPlayer pick = null;

                // HERDING, first. People at a table copy each other, and this is what
                // actually produces the signature MINIGAMES.md asks for: "everyone
                // unanimously betting against one person - who then wins".
                //
                // Measured, this had to be added. Three independent strategies produced a
                // healthy 7 correct calls across 8 rounds and a real leaderboard, but
                // spread the bets so thinly that a pile-on NEVER happened once - the
                // signature moment was gone. Weighting the bias read back up would bring
                // pile-ons back but make them always wrong, which was the previous
                // failure. Copying a neighbour gives both: consensus forms around whatever
                // the early bets landed on, so it is sometimes shrewd and sometimes a
                // stampede off a cliff.
                if (_bets.Count > 0 && Random.value < 0.45f)
                {
                    var placed = new List<uint>(_bets.Values);
                    uint copy = placed[Random.Range(0, placed.Count)];
                    foreach (PartyPlayer p in all)
                        if (p.netId == copy && p.netId != bot.netId) pick = p;
                }

                float roll = Random.value;

                if (pick != null) { }
                else if (roll < 0.40f)
                {
                    // Back Barnaby's target - the obvious, public, often-wrong read.
                    float worst = float.MaxValue;
                    foreach (PartyPlayer p in all)
                    {
                        if (p.netId == bot.netId) continue;
                        if (p.standing < worst) { worst = p.standing; pick = p; }
                    }
                }
                else if (roll < 0.75f && _previousLoser != 0)
                {
                    // Back form.
                    foreach (PartyPlayer p in all)
                        if (p.netId == _previousLoser && p.netId != bot.netId) pick = p;
                }

                if (pick == null)
                {
                    // Or back a hunch.
                    var others = all.FindAll(p => p.netId != bot.netId);
                    if (others.Count > 0) pick = others[Random.Range(0, others.Count)];
                }

                if (pick != null) _bets[bot.netId] = pick.netId;
            }
        }

        [Server] void LogBets()
        {
            var byName = new Dictionary<uint, string>();
            foreach (PartyPlayer p in _game.Participants) byName[p.netId] = p.displayName;

            var sb = new StringBuilder($"[Prediction] BETS round={round}");
            foreach (var kv in _bets)
                sb.Append($" | {Name(byName, kv.Key)}->{Name(byName, kv.Value)}");
            Debug.Log(sb.ToString());
        }

        static string Name(Dictionary<uint, string> map, uint id) =>
            map.TryGetValue(id, out string n) ? n : id.ToString();

        [Server] void Reveal()
        {
            var all = new List<PartyPlayer>(_game.Participants);
            var byName = new Dictionary<uint, string>();
            foreach (PartyPlayer p in all) byName[p.netId] = p.displayName;

            // Who actually came last: the HIGHEST placement. Anyone still on 0 never
            // resolved, and must not be treated as the loser by accident.
            PartyPlayer last = null, won = null;
            int worst = 0;
            foreach (PartyPlayer p in all)
            {
                if (p.placement > worst) { worst = p.placement; last = p; }
                if (p.placement == 1) won = p;
            }

            if (last == null)
            {
                Debug.LogWarning("[Prediction] the minigame left no placements - nothing to score");
                verdictText = "No result. Nobody collects.";
                SetPhase(PredictionPhase.Reveal, revealSeconds);
                return;
            }

            var correct = new List<PartyPlayer>();
            foreach (PartyPlayer p in all)
                if (_bets.TryGetValue(p.netId, out uint t) && t == last.netId) correct.Add(p);

            foreach (PartyPlayer p in correct)
                p.score += pointsForCorrect + (correct.Count == 1 ? loneCallBonus : 0);
            if (won != null) won.score += pointsForWinning;

            // THE SIGNATURE, detected rather than hoped for: the room piled onto one
            // player and that player won it.
            //
            // MEASURED FIRST, THEN LOOSENED. This originally required STRICT unanimity,
            // which the design doc's wording asks for - and across three rounds it never
            // once fired, because bots dissent 30% of the time by design (without that
            // noise there is no table talk, just arithmetic). Meanwhile 4 of 5 backing
            // Barnaby's target happened twice. A supermajority is what the moment
            // actually looks like at a table, so both are tracked: `unanimous` for the
            // literal case and `piled` for the one that occurs.
            var tally = new Dictionary<uint, int>();
            foreach (var kv in _bets)
                tally[kv.Value] = tally.TryGetValue(kv.Value, out int c) ? c + 1 : 1;

            uint topPick = 0; int topCount = 0;
            foreach (var kv in tally) if (kv.Value > topCount) { topCount = kv.Value; topPick = kv.Key; }

            bool unanimous = _bets.Count >= 2 && topCount == _bets.Count;
            bool piled     = _bets.Count >= 3 && topCount * 4 >= _bets.Count * 3;   // >= 75%
            bool backfired = piled && won != null && topPick == won.netId;

            verdictText = correct.Count == 0
                ? $"{last.displayName} came last. Nobody called it."
                : $"{last.displayName} came last. {correct.Count} called it.";
            if (backfired)
                verdictText = $"The room backed {won.displayName} to lose. {won.displayName} WON.";

            var sb = new StringBuilder($"[Prediction] ROUND {round} RESULT last={last.displayName}" +
                                       $" winner={(won != null ? won.displayName : "none")}" +
                                       $" correct={correct.Count}/{_bets.Count}" +
                                       $" topPick={Name(byName, topPick)} topCount={topCount}" +
                                       $" unanimous={unanimous} piled={piled} backfired={backfired}");
            Debug.Log(sb.ToString());

            var sc = new StringBuilder($"[Prediction] SCORES round={round}");
            foreach (PartyPlayer p in all) sc.Append($" | {p.displayName}={p.score}");
            Debug.Log(sc.ToString());

            if (_voice != null)
                _voice.PrefetchCallout(last.displayName, framed: backfired, players: all);

            _previousLoser = last.netId;

            callText = "And the wooden spoon goes to...";
            SetPhase(PredictionPhase.Reveal, revealSeconds);
        }

        public static IEnumerable<PartyPlayer> Players() =>
            Object.FindObjectsByType<PartyPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        /// <summary>Bets stay secret until the reveal - clients cannot see them at all.</summary>
        public bool BetsVisible => phase == PredictionPhase.Reveal || phase == PredictionPhase.Finished;
    }
}
