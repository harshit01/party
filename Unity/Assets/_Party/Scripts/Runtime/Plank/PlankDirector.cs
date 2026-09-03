using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;
using Party.RedLight;

namespace Party.Plank
{
    public enum PlankPhase : byte { Waiting = 0, Countdown = 1, Live = 2, Finished = 3 }

    /// <summary>
    /// "Plank Panic" - MINIGAMES.md #1, Family A.
    ///
    /// A narrow plank over a long drop. Shove everyone off. Last one standing wins, and the
    /// signature humiliation is "falling off in the first two seconds, having touched
    /// nobody" - which is not something to script. It is what a floppy body on a narrow
    /// beam does on its own, and it is the first thing in this project that the active
    /// ragdoll makes possible at all.
    ///
    /// WHY THIS IS THE RIGHT GAME TO PUT THE RAGDOLL IN. Family A's whole premise is
    /// "physics character + platform + shove" - the ragdoll IS the mechanic here, rather
    /// than a nicer costume on top of one. #9 and #10 would play identically with the old
    /// sliding capsule; this one cannot exist without grabbing, shoving and falling over.
    ///
    /// BARNABY'S BIAS IS PHYSICAL HERE, and that is the interesting part. In #9 and #10 his
    /// favouritism is a VERDICT - he says you moved, he says you got it wrong. On a plank
    /// there is nothing to adjudicate: you are on it or you are not. So instead he leans on
    /// the world. A pet who is toppling gets a helpful nudge back toward the middle; a
    /// grudge gets a shove they did not earn. Same affinity, same character, expressed as a
    /// force rather than a ruling - and far more legible, because everyone sees the push.
    /// </summary>
    public class PlankDirector : NetworkBehaviour, IMinigameDirector
    {
        // ---- IMinigameDirector: the seam #11 and the round loop drive minigames through ----
        public bool   RoundIsOver => phase == PlankPhase.Finished;
        public string GameName    => "Plank Panic";
        public IEnumerable<PartyPlayer> Participants => Players();

        [Header("Arena")]
        [Tooltip("Below this Y a player has fallen and is out.")]
        public float killY = -6f;
        public float plankHalfLength = 14f;

        [Header("Timing")]
        public float countdownSeconds = 3f;
        [Tooltip("Whole-round cap. A party minigame is 30-60s (MINIGAMES.md).")]
        public float roundTimeLimit = 70f;

        [Header("Barnaby's thumb on the scale")]
        [Tooltip("Seconds between his interventions.")]
        public float meddleEvery = 2.5f;
        [Tooltip("How hard he helps a favourite back toward the middle.")]
        public float helpForce = 7f;
        [Tooltip("How hard he shoves someone he has taken against.")]
        public float shoveForce = 9f;

        [SyncVar] public PlankPhase phase = PlankPhase.Waiting;
        [SyncVar] public double phaseEndsAt;
        [SyncVar] public int    round;
        [SyncVar] public string callText = "";
        [SyncVar] public string verdictText = "";

        public static PlankDirector Instance { get; private set; }

        BarnabyBias _bias;
        HostVoice   _voice;
        int    _nextPlace, _outs, _helps, _shoves;
        double _roundEndsAt, _roundStartedAt, _nextMeddle, _nextStandingPush;

        void Awake()
        {
            Instance = this;
            _voice = GetComponent<HostVoice>();
        }

        public override void OnStartServer()
        {
            int seed = Random.Range(int.MinValue, int.MaxValue);
            string[] argv = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < argv.Length - 1; i++)
                if (argv[i] == "-partybiasseed" && int.TryParse(argv[i + 1], out int sv))
                { seed = sv; break; }

            Debug.Log($"[Plank] bias seed={seed}");
            _bias = new BarnabyBias(seed);
            Random.InitState(seed);
        }

        [Server]
        public void BeginRound()
        {
            round++;
            _outs = _helps = _shoves = 0;

            if (round > 1) _bias?.Decay(0.10f);
            _bias?.EnsureFavouriteAndTarget(Players(), 0.4f, -0.4f);

            _nextPlace = 0;
            foreach (PartyPlayer p in Players()) _nextPlace++;

            int i = 0;
            foreach (PartyPlayer p in Players())
            {
                p.eliminated = false;
                p.finished   = false;
                p.placement  = 0;
                // Spread along the plank so nobody starts inside anybody.
                float t = _nextPlace > 1 ? (float)i / (_nextPlace - 1) : 0.5f;
                p.ServerTeleport(new Vector3(0f, 1.4f, Mathf.Lerp(-plankHalfLength + 2f,
                                                                  plankHalfLength - 2f, t)));
                i++;
            }

            verdictText = "";
            callText = "Mind the drop.";
            _roundStartedAt = NetworkTime.time;
            _roundEndsAt = NetworkTime.time + roundTimeLimit;
            SetPhase(PlankPhase.Countdown, countdownSeconds);
            if (_voice != null) _voice.PrefetchRoundIntro(round, Players());
        }

        [Server] void SetPhase(PlankPhase p, float seconds)
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

            if (phase == PlankPhase.Waiting || phase == PlankPhase.Finished) return;

            if (phase == PlankPhase.Countdown)
            {
                if (NetworkTime.time >= phaseEndsAt)
                {
                    // Where is everybody, actually? Rounds were wiping out on the first live
                    // frame, which means they were already below the kill plane while
                    // movement was frozen - so the spawn or the teleport is wrong, not the
                    // gameplay.
                    foreach (PartyPlayer p in Players())
                        Debug.Log($"[Plank] at go: {p.displayName} y={p.transform.position.y:F2} " +
                                  $"x={p.transform.position.x:F2} z={p.transform.position.z:F2}");
                    callText = "SHOVE.";
                    SetPhase(PlankPhase.Live, 0f);
                }
                return;
            }

            CheckFallers();
            Meddle();
            CheckEnd();
        }

        /// <summary>Off the plank is off the plank. No verdict, no appeal - you can see it.</summary>
        [Server] void CheckFallers()
        {
            foreach (PartyPlayer p in Players())
            {
                if (p.eliminated || p.finished) continue;
                if (p.transform.position.y > killY) continue;

                p.eliminated = true;
                p.placement = _nextPlace > 0 ? _nextPlace-- : 0;
                _outs++;

                double t = NetworkTime.time - _roundStartedAt;
                verdictText = $"{p.displayName} is off.";
                Debug.Log($"[Plank] {p.displayName} fell at {t:F1}s " +
                          $"(standing={_bias.Describe(p.netId)})");
                if (_voice != null) _voice.PrefetchCallout(p.displayName, framed: false, players: Players());
            }
        }

        /// <summary>
        /// Barnaby leans on the world.
        ///
        /// He only intervenes on someone already in trouble - a nudge to a player standing
        /// safely in the middle is invisible, and the whole point is that the room SEES him
        /// do it. So he waits for a wobble and then makes it better or worse.
        /// </summary>
        [Server] void Meddle()
        {
            if (NetworkTime.time < _nextMeddle) return;
            _nextMeddle = NetworkTime.time + meddleEvery;

            foreach (PartyPlayer p in Players())
            {
                if (p.eliminated || p.finished) continue;

                Ragdoll.RagdollMuscles m = p.GetComponentInChildren<Ragdoll.RagdollMuscles>();
                Rigidbody pelvis = m != null ? m.Rig?.Get(Ragdoll.Bone.Pelvis) : null;
                if (pelvis == null) continue;

                // "In trouble" = near an edge. Off-centre across the plank, not along it.
                float offCentre = Mathf.Abs(pelvis.position.x);
                if (offCentre < 0.35f) continue;

                if (_bias.WouldSpare(p.netId))
                {
                    pelvis.AddForce(new Vector3(-Mathf.Sign(pelvis.position.x) * helpForce, 2f, 0f),
                                    ForceMode.VelocityChange);
                    _helps++;
                    verdictText = $"{p.displayName} wobbles. Something helps them back.";
                    Debug.Log($"[Plank] HELPED {p.displayName} (standing={_bias.Describe(p.netId)})");
                }
                else if (_bias.WouldFrame(p.netId))
                {
                    pelvis.AddForce(new Vector3(Mathf.Sign(pelvis.position.x) * shoveForce, 1f, 0f),
                                    ForceMode.VelocityChange);
                    _shoves++;
                    verdictText = $"{p.displayName} is shoved by nobody at all.";
                    Debug.Log($"[Plank] SHOVED {p.displayName} (standing={_bias.Describe(p.netId)})");
                    _bias.Nudge(p.netId, +0.15f);   // he overreached; the room saw it
                }
            }
        }

        [Server] void CheckEnd()
        {
            int alive = 0;
            PartyPlayer last = null;
            foreach (PartyPlayer p in Players())
                if (!p.eliminated) { alive++; last = p; }

            if (alive == 1)
            {
                last.finished = true;
                last.placement = 1;
                _bias.Nudge(last.netId, -0.15f);
                verdictText = $"{last.displayName} is the last one standing.";
                SetPhase(PlankPhase.Finished, 0f);
                if (_voice != null) _voice.PrefetchFinale(last.displayName, Players());
                Summarise("winner:" + last.displayName);
                return;
            }

            if (alive == 0)
            {
                verdictText = "Everyone is in the drink. Barnaby is delighted.";
                SetPhase(PlankPhase.Finished, 0f);
                Summarise("wipeout");
                return;
            }

            // A ROUND MUST END - the trap Red Light fell into with one straggler.
            if (NetworkTime.time >= _roundEndsAt)
            {
                PartyPlayer best = null;
                foreach (PartyPlayer p in Players()) if (!p.eliminated) { best = p; break; }
                if (best != null) { best.finished = true; best.placement = 1; }
                verdictText = best != null ? $"Time! {best.displayName} held on." : "Time!";
                SetPhase(PlankPhase.Finished, 0f);
                Summarise("timeout:" + (best != null ? best.displayName : "nobody"));
            }
        }

        [Server] void Summarise(string outcome)
        {
            var order = new List<PartyPlayer>(Players());
            order.Sort((a, b) => a.placement.CompareTo(b.placement));
            var ob = new StringBuilder("[Plank] PLACEMENTS");
            foreach (PartyPlayer p in order) ob.Append($" | {p.placement}:{p.displayName}");
            Debug.Log(ob.ToString());

            double secs = NetworkTime.time - _roundStartedAt;
            Debug.Log($"[Plank] ROUND {round} END outcome={outcome} eliminated={_outs} " +
                      $"helped={_helps} shoved={_shoves} seconds={secs:F1}");

            if (_bias == null) return;
            var sb = new StringBuilder($"[Plank] STANDINGS round={round}");
            foreach (PartyPlayer p in Players())
                sb.Append($" | {p.displayName}={_bias.AffinityOf(p.netId):F3}({_bias.Describe(p.netId)})");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// May players move right now? Consulted by PartyPlayer.
        ///
        /// Nobody moves during the countdown. Without this the bots walked off the plank
        /// WHILE BARNABY WAS STILL COUNTING - two of five were already below the kill plane
        /// on the first live frame, recorded as falling "at 3.0s" having never been given a
        /// chance to play.
        /// </summary>
        public bool MovementAllowed => phase == PlankPhase.Live;

        public static IEnumerable<PartyPlayer> Players() =>
            Object.FindObjectsByType<PartyPlayer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }
}
