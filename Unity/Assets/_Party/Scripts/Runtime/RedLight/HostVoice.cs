using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Party.RedLight
{
    /// <summary>
    /// Talks to Server/hostserver.py for Barnaby's commentary.
    ///
    /// PRE-GENERATION IS NOT OPTIONAL (HANDOFF.md section 4). Every call here fires while
    /// the round is still running so the line is ready the instant it is needed. Nothing
    /// in the game ever waits on a response.
    ///
    /// FAILURE IS LOUD BUT NOT FATAL. If the service is down or OPENAI_API_KEY is unset,
    /// the error is logged plainly and a scripted stand-in is used, so the game remains
    /// playable and fun without the AI - which is the standing rule that the AI is
    /// background, not the main character, and fun must survive its removal.
    /// </summary>
    public class HostVoice : MonoBehaviour
    {
        [Tooltip("Where hostserver.py is listening.")]
        public string endpoint = "http://127.0.0.1:8790/host/say";
        public float timeoutSeconds = 6f;

        /// <summary>
        /// Which minigame is being narrated. Set per scene.
        ///
        /// This used to be the literal string "Red Light, Barnaby" inside PrefetchRoundIntro
        /// and the round number was read off RedLightDirector.Instance - which is null in
        /// every other minigame, so #10 would have told the host it was introducing Red
        /// Light and filed its history under "Round :". The host's memory across the night
        /// is the product (HANDOFF.md §4); it cannot be wired to one scene.
        /// </summary>
        public string gameName = "Red Light, Barnaby";

        /// <summary>Set by whichever director owns the round, so history is labelled right.</summary>
        public static int CurrentRound;

        /// <summary>Most recent line Barnaby has ready. Empty until one arrives.</summary>
        public static string Latest { get; private set; } = "";
        public static bool ServiceHealthy { get; private set; } = true;
        public static string LastError { get; private set; } = "";

        readonly List<string> _history = new List<string>();

        public void PrefetchRoundIntro(int round, IEnumerable<PartyPlayer> players)
        {
            CurrentRound = round;
            Send("intro", players, justHappened: null, nextGame: gameName);
        }

        public void PrefetchCallout(string who, bool framed, IEnumerable<PartyPlayer> players)
        {
            string what = framed
                ? $"{who} did NOT move, but Barnaby called them out anyway"
                : $"{who} moved during a stop and is out";
            _history.Add($"Round {CurrentRound}: {what}");
            Send("reaction", players, justHappened: what, nextGame: null);
        }

        public void PrefetchFinale(string who, IEnumerable<PartyPlayer> players)
            => Send("finale", players, justHappened: $"{who} reached the line and won", nextGame: null);

        void Send(string beat, IEnumerable<PartyPlayer> players, string justHappened, string nextGame)
            => StartCoroutine(SendRoutine(beat, players, justHappened, nextGame));

        IEnumerator SendRoutine(string beat, IEnumerable<PartyPlayer> players,
                                string justHappened, string nextGame)
        {
            var sb = new StringBuilder();
            sb.Append("{\"beat\":\"").Append(beat).Append("\",\"session_id\":\"redlight\"");

            sb.Append(",\"players\":[");
            bool first = true;
            foreach (PartyPlayer p in players)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"name\":\"").Append(Escape(p.displayName))
                  .Append("\",\"score\":").Append(p.finished ? 1 : 0).Append('}');
            }
            sb.Append(']');

            if (!string.IsNullOrEmpty(justHappened))
                sb.Append(",\"just_happened\":\"").Append(Escape(justHappened)).Append('"');
            if (!string.IsNullOrEmpty(nextGame))
                sb.Append(",\"next_game\":\"").Append(Escape(nextGame)).Append('"');

            sb.Append(",\"history\":[");
            for (int i = 0; i < _history.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('"').Append(Escape(_history[i])).Append('"');
            }
            sb.Append("]}");

            using UnityWebRequest req = new UnityWebRequest(endpoint, "POST");
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(sb.ToString()));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.CeilToInt(timeoutSeconds);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                ServiceHealthy = false;
                LastError = $"{req.result}: {req.error}";
                // Loud, but the game carries on with a stand-in.
                Debug.LogWarning($"[HostVoice] host service unavailable ({LastError}). " +
                                 "Using scripted stand-in. Start it with: " +
                                 ".venv/bin/python Server/hostserver.py (needs OPENAI_API_KEY)");
                Latest = Standin(beat, justHappened);
                yield break;
            }

            ServiceHealthy = true; LastError = "";
            string body = req.downloadHandler.text;
            int i0 = body.IndexOf("\"line\"");
            if (i0 < 0) { Latest = Standin(beat, justHappened); yield break; }
            int q1 = body.IndexOf('"', i0 + 6);
            int q2 = body.IndexOf('"', q1 + 1);
            while (q2 > 0 && body[q2 - 1] == '\\') q2 = body.IndexOf('"', q2 + 1);
            Latest = q1 >= 0 && q2 > q1
                ? body.Substring(q1 + 1, q2 - q1 - 1).Replace("\\\"", "\"").Replace("\\n", " ")
                : Standin(beat, justHappened);
        }

        /// <summary>
        /// Deliberately plain. It must be obvious when Barnaby is not really talking -
        /// a stand-in good enough to mistake for the real thing would hide an outage.
        /// </summary>
        static string Standin(string beat, string justHappened) => beat switch
        {
            "intro"  => "Right then. On your marks.",
            "finale" => "And that's the round. Someone had to win.",
            _        => string.IsNullOrEmpty(justHappened) ? "Well, well." : justHappened + ".",
        };

        static string Escape(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
