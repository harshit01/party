using Mirror;
using UnityEngine;

namespace Party.RedLight
{
    /// <summary>
    /// Round UI, built to Docs/ArtTarget/redlight_target.svg.
    ///
    /// WHAT THIS REPLACED. The old HUD listed every participant by name down the right
    /// hand side, under a "Round N - Phase" header, with the host's line in 16px italic.
    /// The founder's direction is Fall Guys: you are behind your own character and you do
    /// not want a roster of everyone else. So the list is gone and what is left is the
    /// four things you actually read mid-round:
    ///
    ///   the CALL          - enormous, colour-coded, legible across a room
    ///   how many are LEFT - a count, not a list of names
    ///   YOUR standing     - your own state only, mirroring the Filament glow
    ///   BARNABY           - a broadcast lower-third, because he is the differentiator
    ///
    /// Still OnGUI. It is throwaway and it is meant to be: replacing it with UI Toolkit
    /// before the minigames have survived playtesting would be building furniture for a
    /// house that might get demolished. This is layout and hierarchy, not a UI rewrite.
    /// </summary>
    public class RedLightHUD : MonoBehaviour
    {
        RedLightDirector _dir => RedLightDirector.Instance;

        static readonly Color Ink      = new Color(0.16f, 0.07f, 0.31f, 0.62f);
        static readonly Color Lilac    = new Color(0.76f, 0.70f, 1f);
        static readonly Color GoGreen  = new Color(0.31f, 0.94f, 0.69f);
        static readonly Color StopRed  = new Color(1f, 0.23f, 0.36f);
        static readonly Color WaitAmber= new Color(1f, 0.82f, 0.34f);
        static readonly Color Gold     = new Color(1f, 0.82f, 0.34f);

        Texture2D _panelTex;

        Texture2D Panel
        {
            get
            {
                if (_panelTex == null)
                {
                    _panelTex = new Texture2D(1, 1);
                    _panelTex.SetPixel(0, 0, Color.white);
                    _panelTex.Apply();
                }
                return _panelTex;
            }
        }

        void Box(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Panel);
            GUI.color = prev;
        }

        GUIStyle Text(int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft,
                      FontStyle style = FontStyle.Normal)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size, alignment = anchor, fontStyle = style,
                richText = true, wordWrap = false
            };
            s.normal.textColor = c;
            return s;
        }

        /// <summary>Everything scales off screen height, so it reads the same at any size.</summary>
        void OnGUI()
        {
            if (_dir == null || (!NetworkClient.active && !NetworkServer.active)) return;

            float h = Screen.height, w = Screen.width;
            float u = h / 1080f;      // design units: the art target is 1920x1080

            if (_dir.phase != RoundPhase.Waiting) DrawCall(w, u);
            DrawStillIn(w, u);
            DrawYourStanding(u);
            DrawBarnaby(w, h, u);
            DrawHostControls(w, h, u);
        }

        // ---------------------------------------------------------------- the call
        void DrawCall(float w, float u)
        {
            Color tint = _dir.MustFreeze ? StopRed
                       : _dir.phase == RoundPhase.Go ? GoGreen
                       : WaitAmber;

            // Deliberately enormous. A party game screen is read from across a room while
            // four people shout, and this is the only thing that must never be missed.
            GUI.Label(new Rect(0, 40 * u, w, 160 * u), _dir.callText,
                      Text(Mathf.RoundToInt(150 * u), tint, TextAnchor.MiddleCenter, FontStyle.Bold));

            if (!string.IsNullOrEmpty(_dir.verdictText))
                GUI.Label(new Rect(0, 196 * u, w, 50 * u), _dir.verdictText,
                          Text(Mathf.RoundToInt(38 * u), Color.white, TextAnchor.MiddleCenter));
        }

        // ------------------------------------------------------- how many are left
        void DrawStillIn(float w, float u)
        {
            int alive = 0, total = 0;
            foreach (PartyPlayer p in RedLightDirector.Players())
            {
                total++;
                if (!p.eliminated) alive++;
            }

            float bw = 258 * u, bh = 104 * u, x = w - bw - 52 * u, y = 52 * u;
            Box(new Rect(x, y, bw, bh), Ink);
            GUI.Label(new Rect(x, y + 10 * u, bw, 30 * u), "STILL IN",
                      Text(Mathf.RoundToInt(26 * u), Lilac, TextAnchor.MiddleCenter));
            GUI.Label(new Rect(x, y + 44 * u, bw, 50 * u), $"{alive} / {total}",
                      Text(Mathf.RoundToInt(42 * u), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold));
        }

        // ------------------------------------------------------------ your standing
        void DrawYourStanding(float u)
        {
            PartyPlayer me = null;
            foreach (PartyPlayer p in RedLightDirector.Players())
                if (p.isLocalPlayer) { me = p; break; }
            if (me == null) return;

            float bw = 330 * u, bh = 104 * u, x = 52 * u, y = 52 * u;
            Box(new Rect(x, y, bw, bh), Ink);

            // Same words the Filament's glow is saying, for anyone who has not yet learned
            // to read the glow.
            string mood = me.standing > 0.5f  ? "BARNABY ADORES YOU"
                        : me.standing > 0.15f ? "BARNABY LIKES YOU"
                        : me.standing > -0.35f ? "BARNABY TOLERATES YOU"
                        : "BARNABY HAS IT IN FOR YOU";
            GUI.Label(new Rect(x + 28 * u, y + 12 * u, bw, 30 * u), mood,
                      Text(Mathf.RoundToInt(23 * u), Lilac));

            float mw = bw - 56 * u, mx = x + 28 * u, my = y + 58 * u, mh = 16 * u;
            Box(new Rect(mx, my, mw, mh), new Color(0.10f, 0.05f, 0.21f, 0.9f));
            float t = Mathf.InverseLerp(-1f, 1f, me.standing);
            Box(new Rect(mx, my, mw * t, mh),
                Color.Lerp(new Color(1f, 0.35f, 0.48f), new Color(0.31f, 0.94f, 0.69f), t));
        }

        // ----------------------------------------------------------------- Barnaby
        void DrawBarnaby(float w, float h, float u)
        {
            if (string.IsNullOrEmpty(HostVoice.Latest)) return;

            float bh = 116 * u, y = h - bh;
            Box(new Rect(0, y, Mathf.Min(w, 1420 * u), bh), new Color(0.16f, 0.07f, 0.31f, 0.86f));

            GUI.Label(new Rect(52 * u, y + 14 * u, 400 * u, 26 * u), "BARNABY QUILL",
                      Text(Mathf.RoundToInt(22 * u), Gold, TextAnchor.MiddleLeft, FontStyle.Bold));

            var line = Text(Mathf.RoundToInt(30 * u), Color.white);
            line.wordWrap = true;
            GUI.Label(new Rect(52 * u, y + 44 * u, Mathf.Min(w, 1340 * u) - 52 * u, 60 * u),
                      "“" + HostVoice.Latest + "”", line);

            if (!HostVoice.ServiceHealthy)
                GUI.Label(new Rect(w - 320 * u, y + 14 * u, 300 * u, 26 * u),
                          "stand-in (host service down)",
                          Text(Mathf.RoundToInt(18 * u), new Color(1f, 0.55f, 0.55f), TextAnchor.MiddleRight));
        }

        // ------------------------------------------------------------ host controls
        void DrawHostControls(float w, float h, float u)
        {
            if (!NetworkServer.active) return;
            if (_dir.phase != RoundPhase.Waiting && _dir.phase != RoundPhase.Finished) return;

            float bw = 260 * u, bh = 56 * u;
            if (GUI.Button(new Rect((w - bw) * 0.5f, h - bh - 140 * u, bw, bh),
                           _dir.round == 0 ? "Start round" : "Next round"))
                _dir.BeginRound();
        }
    }
}
