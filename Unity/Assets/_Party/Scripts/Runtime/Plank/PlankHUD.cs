using Mirror;
using UnityEngine;
using Party.RedLight;

namespace Party.Plank
{
    /// <summary>Same shape as the other minigame HUDs, so the show feels like one show.</summary>
    public class PlankHUD : MonoBehaviour
    {
        PlankDirector _dir => PlankDirector.Instance;
        static readonly Color Ink = new Color(0.16f, 0.07f, 0.31f, 0.62f);
        static readonly Color Lilac = new Color(0.76f, 0.70f, 1f);
        Texture2D _tex;

        Texture2D Solid
        {
            get
            {
                if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }
                return _tex;
            }
        }

        void Box(Rect r, Color c) { Color p = GUI.color; GUI.color = c; GUI.DrawTexture(r, Solid); GUI.color = p; }

        GUIStyle T(int size, Color c, TextAnchor a = TextAnchor.MiddleLeft, FontStyle f = FontStyle.Normal)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = a, fontStyle = f, richText = true };
            s.normal.textColor = c;
            return s;
        }

        void OnGUI()
        {
            if (_dir == null || (!NetworkClient.active && !NetworkServer.active)) return;
            float h = Screen.height, w = Screen.width, u = h / 1080f;

            GUI.Label(new Rect(0, 40 * u, w, 150 * u), _dir.callText,
                      T(Mathf.RoundToInt(130 * u), new Color(1f, 0.82f, 0.34f), TextAnchor.MiddleCenter, FontStyle.Bold));
            if (!string.IsNullOrEmpty(_dir.verdictText))
                GUI.Label(new Rect(0, 190 * u, w, 46 * u), _dir.verdictText,
                          T(Mathf.RoundToInt(34 * u), Color.white, TextAnchor.MiddleCenter));

            int alive = 0, total = 0;
            foreach (PartyPlayer p in PlankDirector.Players()) { total++; if (!p.eliminated) alive++; }
            float bw = 258 * u, bh = 104 * u, x = w - bw - 52 * u, y = 52 * u;
            Box(new Rect(x, y, bw, bh), Ink);
            GUI.Label(new Rect(x, y + 10 * u, bw, 30 * u), "STILL ON", T(Mathf.RoundToInt(26 * u), Lilac, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(x, y + 44 * u, bw, 50 * u), $"{alive} / {total}",
                      T(Mathf.RoundToInt(42 * u), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold));

            if (!string.IsNullOrEmpty(HostVoice.Latest))
            {
                float lh = 116 * u, ly = h - lh;
                Box(new Rect(0, ly, Mathf.Min(w, 1420 * u), lh), new Color(0.16f, 0.07f, 0.31f, 0.86f));
                GUI.Label(new Rect(52 * u, ly + 14 * u, 400 * u, 26 * u), "BARNABY QUILL",
                          T(Mathf.RoundToInt(22 * u), new Color(1f, 0.82f, 0.34f), TextAnchor.MiddleLeft, FontStyle.Bold));
                var line = T(Mathf.RoundToInt(30 * u), Color.white); line.wordWrap = true;
                GUI.Label(new Rect(52 * u, ly + 44 * u, Mathf.Min(w, 1340 * u) - 52 * u, 60 * u),
                          "“" + HostVoice.Latest + "”", line);
            }

            if (NetworkServer.active && (_dir.phase == PlankPhase.Waiting || _dir.phase == PlankPhase.Finished))
            {
                float bwid = 260 * u, bhi = 56 * u;
                if (GUI.Button(new Rect((w - bwid) * 0.5f, h - bhi - 140 * u, bwid, bhi),
                               _dir.round == 0 ? "Start round" : "Next round"))
                    _dir.BeginRound();
            }
        }
    }
}
