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
        [Tooltip("Players win by crossing this Z.")]
        public float finishZ = 14f;
        [Tooltip("Players are returned here at the start of a round.")]
        public float startZ = -12f;

        [Header("Timing")]
        public float countdownSeconds = 3f;
        public Vector2 goDuration   = new Vector2(1.6f, 4.5f);
        public Vector2 stopDuration = new Vector2(1.6f, 2.8f);

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
        bool   _snapshotTaken;
        double _judgeAt;
        int    _stopsThisRound, _spares, _frames;

        void Awake()
        {
            Instance = this;
            _voice = GetComponent<HostVoice>();
        }

        public override void OnStartServer()
        {
            _bias = new BarnabyBias(Random.Range(int.MinValue, int.MaxValue));
        }

        // ------------------------------------------------------------------
        // server round loop
        // ------------------------------------------------------------------

        [Server]
        public void BeginRound()
        {
            round++;
            _freezeSnapshot.Clear();
            _stopsThisRound = _spares = _frames = 0;
            foreach (PartyPlayer p in Players())
            {
                p.eliminated = false;
                p.finished   = false;
                p.ServerTeleport(new Vector3(Random.Range(-6f, 6f), 1.1f, startZ));
            }

            verdictText = "";
            SetPhase(RoundPhase.Countdown, countdownSeconds);
            callText = "Places, please.";
            if (_voice != null) _voice.PrefetchRoundIntro(round, Players());
        }

        void Update()
        {
            if (!isServer) return;
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
            foreach (PartyPlayer p in Players())
                _freezeSnapshot[p.netId] = p.transform.position;
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

                if (_bias.WouldFrame(p.netId))
                    Eliminate(p, "framed");
            }
        }

        [Server] void Eliminate(PartyPlayer p, string reason)
        {
            p.eliminated = true;
            if (reason == "framed") _frames++;
            _bias.Nudge(p.netId, -0.1f);   // being called out sours things further

            verdictText = reason == "framed"
                ? $"{p.displayName} — OUT. (Barnaby: \"I saw that.\" They did not move.)"
                : $"{p.displayName} — OUT.";

            Debug.Log($"[RedLight] {p.displayName} out ({reason}, standing={_bias.Describe(p.netId)})");
            if (_voice != null) _voice.PrefetchCallout(p.displayName, reason == "framed", Players());
        }

        [Server] void CheckFinishers()
        {
            foreach (PartyPlayer p in Players())
            {
                if (p.eliminated || p.finished) continue;
                if (p.transform.position.z < finishZ) continue;

                p.finished = true;
                verdictText = $"{p.displayName} reaches the line!";
                SetPhase(RoundPhase.Finished, 0f);
                Summarise("winner:" + p.displayName);
                if (_voice != null) _voice.PrefetchFinale(p.displayName, Players());
                return;
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
