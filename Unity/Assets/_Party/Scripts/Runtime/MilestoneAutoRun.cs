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
                               if (Arg("-partyround") != null) Invoke(nameof(BeginRound), 2f);
                               break;
                case "server": Debug.Log("[AutoRun] starting SERVER"); _nm.StartServer(); break;
                case "client": Debug.Log("[AutoRun] starting CLIENT to " + _nm.networkAddress);
                               _nm.StartClient(); break;
                default:       Debug.Log("[AutoRun] no -partyrole given; idle"); break;
            }
        }

        void Update()
        {
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

            if (_quitAt > 0f && Time.time >= _quitAt)
            {
                Debug.Log("[AutoRun] duration elapsed, quitting");
                _nm.StopHost(); _nm.StopClient();
                Application.Quit();
            }
        }

        /// <summary>Kick off a Red Light round from the command line, for headless testing.</summary>
        void BeginRound()
        {
            var d = RedLight.RedLightDirector.Instance;
            if (d == null) { Debug.LogError("[AutoRun] -partyround but no RedLightDirector in scene"); return; }
            Debug.Log("[AutoRun] beginning round");
            d.BeginRound();
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
