using System.Text;
using Mirror;
using UnityEngine;

namespace Party
{
    /// <summary>
    /// Verification harness for the netcode milestone. Not shipped game code.
    ///
    /// Lets a headless build start as host or client from the command line and print a
    /// periodic census of every participant it can see:
    ///
    ///     MyGame -partyrole host   -partyseconds 25
    ///     MyGame -partyrole client -partyaddress localhost
    ///
    /// The point is to prove sync mechanically - two processes, compared logs - rather
    /// than by watching two windows and deciding it looks about right. Every concept in
    /// this project so far was corrected by a cheap test that should have run on day one
    /// (HANDOFF.md section 6.6).
    /// </summary>
    public class MilestoneAutoRun : MonoBehaviour
    {
        NetworkManager _nm;
        string  _role = "none";
        float   _nextReport;
        float   _quitAt = -1f;

        // Multi-round support. BeginRound used to fire exactly once, which meant no test
        // could ever reach BarnabyBias's central claim - that affinity PERSISTS, so "he
        // remembers who annoyed him three rounds ago" is literally true. One round per
        // process also re-seeds the bias every time, so spare/frame rates could only ever
        // be sampled one round at a time.
        bool  _roundMode;
        int   _roundsWanted = 1;
        int   _roundsDone;
        float _nextRoundAt = -1f;

        static string Arg(string name, string fallback = null)
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == name) return a[i + 1];
            return fallback;
        }

        string _shotPath;
        float  _shotAt = -1f;

        void Start()
        {
            // Runtime capture. An edit-mode cam.Render() in batchmode lacks proper
            // lighting/ambient data and produced a uniformly gold image that had nothing
            // to do with the actual materials - so presentation must be verified from a
            // real running player, not from the editor's offline renderer.
            _shotPath = Arg("-partyshot");
            if (!string.IsNullOrEmpty(_shotPath)) _shotAt = 7f;

            _nm = GetComponent<NetworkManager>();
            _role = Arg("-partyrole", "none");

            string secs = Arg("-partyseconds");
            if (!string.IsNullOrEmpty(secs) && float.TryParse(secs, out float s))
                _quitAt = Time.time + s;

            string addr = Arg("-partyaddress");
            if (!string.IsNullOrEmpty(addr)) _nm.networkAddress = addr;

            string target = Arg("-partytarget");
            if (!string.IsNullOrEmpty(target) && int.TryParse(target, out int t)
                && _nm is PartyNetworkManager pnm)
                pnm.targetParticipants = Mathf.Clamp(t, 2, 8);

            switch (_role)
            {
                case "host":   Debug.Log("[AutoRun] starting HOST");   _nm.StartHost();
                               if (Arg("-partyround") != null)
                               {
                                   _roundMode = true;
                                   string n = Arg("-partyrounds");
                                   if (!string.IsNullOrEmpty(n) && int.TryParse(n, out int rn))
                                       _roundsWanted = Mathf.Clamp(rn, 1, 50);
                                   Invoke(nameof(BeginRound), 2f);
                               }
                               break;
                case "server": Debug.Log("[AutoRun] starting SERVER"); _nm.StartServer(); break;
                case "client": Debug.Log("[AutoRun] starting CLIENT to " + _nm.networkAddress);
                               _nm.StartClient(); break;
                default:       Debug.Log("[AutoRun] no -partyrole given; idle"); break;
            }
        }

        void Update()
        {
            // THE QUIT TIMER IS CHECKED FIRST, BEFORE THE ROLE GUARD.
            // It used to sit below `if (_role == "none") return;`, which made
            // -partyseconds silently unreachable for the one role that needs it most:
            // build_verified.sh smoke-tests every build with `-partyrole none
            // -partyseconds 6`, so a player that booted CORRECTLY never quit and the
            // build script blocked forever. Only a CORRUPT build ended the smoke test,
            // by crashing - so the tool could report failure but never success. One
            // smoke process was found still alive after 1h37m of a 6-second run.
            if (_quitAt > 0f && Time.time >= _quitAt)
            {
                Debug.Log("[AutoRun] duration elapsed, quitting");
                if (_nm != null) { _nm.StopHost(); _nm.StopClient(); }
                Application.Quit();
                return;
            }

            if (_role == "none") return;

            if (Time.time >= _nextReport)
            {
                _nextReport = Time.time + 0.5f;
                Report();
            }

            if (_shotAt > 0f && Time.time >= _shotAt)
            {
                _shotAt = -1f;
                ScreenCapture.CaptureScreenshot(_shotPath);
                Debug.Log($"[AutoRun] screenshot -> {_shotPath}");
            }

            if (_roundMode) PumpRounds();
        }

        /// <summary>
        /// Kick off a round from the command line, for headless testing.
        ///
        /// WHICHEVER MINIGAME IS IN THE SCENE. This used to reach straight for
        /// RedLightDirector, which meant a second minigame could not be tested headlessly
        /// at all - and headless testing is the only way anything in this project has ever
        /// been believed.
        /// </summary>
        void BeginRound()
        {
            _roundsDone++;
            _nextRoundAt = -1f;
            Debug.Log($"[AutoRun] beginning round {_roundsDone}/{_roundsWanted}");

            // PREDICTION FIRST. #11 wraps a minigame rather than being one, so when it
            // is present it owns the round and starts the inner game itself. Starting the
            // inner game directly would skip the betting entirely.
            if (Prediction.PredictionDirector.Instance != null)
            { Prediction.PredictionDirector.Instance.BeginRound(); return; }
            if (Plank.PlankDirector.Instance != null)
            { Plank.PlankDirector.Instance.BeginRound(); return; }
            if (RedLight.RedLightDirector.Instance != null)
            { RedLight.RedLightDirector.Instance.BeginRound(); return; }
            if (SayWhat.SayWhatDirector.Instance != null)
            { SayWhat.SayWhatDirector.Instance.BeginRound(); return; }

            Debug.LogError("[AutoRun] -partyround but no director in the scene");
        }

        /// <summary>True when the scene's director has finished its round.</summary>
        bool RoundFinished()
        {
            if (Prediction.PredictionDirector.Instance != null)
                return Prediction.PredictionDirector.Instance.phase == Prediction.PredictionPhase.Finished;
            if (Plank.PlankDirector.Instance != null)
                return Plank.PlankDirector.Instance.phase == Plank.PlankPhase.Finished;
            if (RedLight.RedLightDirector.Instance != null)
                return RedLight.RedLightDirector.Instance.phase == RedLight.RoundPhase.Finished;
            if (SayWhat.SayWhatDirector.Instance != null)
                return SayWhat.SayWhatDirector.Instance.phase == SayWhat.SayWhatPhase.Finished;
            return false;
        }

        bool HasDirector =>
            Prediction.PredictionDirector.Instance != null ||
            Plank.PlankDirector.Instance != null ||
            RedLight.RedLightDirector.Instance != null ||
            SayWhat.SayWhatDirector.Instance != null;

        /// <summary>
        /// Chain rounds back to back so one process plays a SESSION, not a single round.
        ///
        /// The gap matters: the director fires PrefetchFinale the instant a round ends and
        /// the next BeginRound wipes the verdict, so restarting immediately would cut the
        /// finale off mid-flight and hide whether it ever completed.
        /// </summary>
        void PumpRounds()
        {
            if (!HasDirector) return;
            if (!RoundFinished()) return;

            if (_roundsDone >= _roundsWanted)
            {
                // Session over. Leave a beat for the last finale, then stop.
                if (_nextRoundAt < 0f) _nextRoundAt = Time.time + 3f;
                if (Time.time >= _nextRoundAt)
                {
                    Debug.Log($"[AutoRun] session complete: {_roundsDone} round(s)");
                    _quitAt = 0.0001f;   // fall through to the quit path below next frame
                }
                return;
            }

            if (_nextRoundAt < 0f) _nextRoundAt = Time.time + 3f;
            if (Time.time >= _nextRoundAt) BeginRound();
        }

        void Report()
        {
            PartyPlayer[] all = Object.FindObjectsByType<PartyPlayer>(FindObjectsSortMode.None);
            StringBuilder sb = new StringBuilder();
            // NetworkTime.time is synchronised between host and clients, so two logs
            // can be aligned to the same instant. Local Time.time cannot do this - the
            // processes start seconds apart, which made an earlier comparison of two
            // logs meaningless.
            sb.Append("[CENSUS] role=").Append(_role)
              .Append(" nt=").Append(NetworkTime.time.ToString("F1"))
              .Append(" t=").Append(Time.time.ToString("F1"))
              .Append(" count=").Append(all.Length);

            var dir = RedLight.RedLightDirector.Instance;
            if (dir != null)
                sb.Append(" phase=").Append(dir.phase)
                  .Append(" call=\"").Append(dir.callText).Append('"');
            var sw = SayWhat.SayWhatDirector.Instance;
            if (sw != null)
                sb.Append(" phase=").Append(sw.phase)
                  .Append(" seq=").Append(sw.sequenceNumber)
                  .Append(" call=\"").Append(sw.callText).Append('"');

            System.Array.Sort(all, (x, y) => string.CompareOrdinal(x.displayName, y.displayName));
            foreach (PartyPlayer p in all)
            {
                Vector3 v = p.transform.position;
                sb.Append(" | ").Append(p.displayName)
                  .Append(p.isBot ? "(bot)" : "(human)")
                  .Append(' ')
                  .Append($"{v.x:F2},{v.z:F2}");
            }
            Debug.Log(sb.ToString());
        }
    }
}
