using Mirror;
using UnityEngine;

namespace Party.RedLight
{
    /// <summary>
    /// Round UI: the call, the verdict, and who is out. Throwaway OnGUI, like PartyHUD.
    ///
    /// The call is deliberately enormous and colour-coded. In a party game the screen is
    /// read from across a room, at a glance, while people are shouting.
    /// </summary>
    public class RedLightHUD : MonoBehaviour
    {
        // The director lives on a server-spawned object, not on this GameObject, so it
        // is looked up rather than fetched with GetComponent.
        RedLightDirector _dir => RedLightDirector.Instance;

        void OnGUI()
        {
            if (_dir == null || (!NetworkClient.active && !NetworkServer.active)) return;

            // Big call banner
            if (_dir.phase != RoundPhase.Waiting)
            {
                Color tint = _dir.MustFreeze ? new Color(0.9f, 0.2f, 0.2f)
                           : _dir.phase == RoundPhase.Go ? new Color(0.2f, 0.85f, 0.35f)
                           : new Color(0.85f, 0.8f, 0.3f);

                var big = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 54, alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold, richText = true
                };
                big.normal.textColor = tint;
                GUI.Label(new Rect(0, 40, Screen.width, 70), _dir.callText, big);

                var mid = new GUIStyle(GUI.skin.label)
                { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                mid.normal.textColor = Color.white;
                GUI.Label(new Rect(0, 112, Screen.width, 30), _dir.verdictText, mid);

                if (!string.IsNullOrEmpty(HostVoice.Latest))
                {
                    var quote = new GUIStyle(GUI.skin.label)
                    { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Italic };
                    quote.normal.textColor = new Color(0.85f, 0.8f, 0.6f);
                    GUI.Label(new Rect(Screen.width * 0.15f, 146, Screen.width * 0.7f, 60),
                              "“" + HostVoice.Latest + "”", quote);
                }
            }

            // Right-hand roster
            GUILayout.BeginArea(new Rect(Screen.width - 230, 10, 220, 260), GUI.skin.box);
            GUILayout.Label($"<b>Round {_dir.round} — {_dir.phase}</b>",
                            new GUIStyle(GUI.skin.label) { richText = true });
            foreach (PartyPlayer p in RedLightDirector.Players())
            {
                string mark = p.eliminated ? "OUT " : p.finished ? "WON " : "     ";
                GUILayout.Label($"{mark}{p.displayName}{(p.isBot ? " (bot)" : "")}");
            }
            GUILayout.Space(6);
            GUILayout.Label(HostVoice.ServiceHealthy
                ? "<color=#8f8>host service: ok</color>"
                : "<color=#f88>host: stand-in (service down)</color>",
                new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true });

            if (NetworkServer.active &&
                (_dir.phase == RoundPhase.Waiting || _dir.phase == RoundPhase.Finished))
                if (GUILayout.Button(_dir.round == 0 ? "Start round" : "Next round"))
                    _dir.BeginRound();

            GUILayout.EndArea();
        }
    }
}
