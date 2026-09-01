using Mirror;
using UnityEngine;
using Party.RedLight;

namespace Party.SayWhat
{
    /// <summary>
    /// Round UI for "Say What He Says", built to the same shape as Red Light's so the two
    /// minigames feel like one show: the call enormous and colour-coded, a STILL IN count
    /// rather than a roster, your own standing as a meter, and Barnaby in a broadcast
    /// lower-third. Everything scales off screen height.
    ///
    /// THE ONE THING THIS SCREEN MUST DO is make it obvious whether you are WATCHING or
    /// PERFORMING, because pressing a key at the wrong time is the difference between
    /// fine and out. So the two phases do not merely change the text - they change the
    /// colour of the whole banner and put a row of pips under it, filling as you go.
    ///
    /// Still OnGUI, deliberately, for the same reason as RedLightHUD: replacing it with
    /// real UI before the minigames have survived playtesting is furniture for a house
    /// that may be demolished (MINIGAMES.md expects ~4 of 12 to be cut).
    /// </summary>
    public class SayWhatHUD : MonoBehaviour
    {
        SayWhatDirector _dir => SayWhatDirector.Instance;

        static readonly Color Ink     = new Color(0.16f, 0.07f, 0.31f, 0.62f);
        static readonly Color Lilac   = new Color(0.76f, 0.70f, 1f);
        static readonly Color Watch   = new Color(1f, 0.82f, 0.34f);   // his turn
        static readonly Color Perform = new Color(0.31f, 0.94f, 0.69f); // yours
        static readonly Color Gold    = new Color(1f, 0.82f, 0.34f);

        Texture2D _tex;
        Texture2D Solid
        {
            get
            {
                if (_tex == null)
                {
                    _tex = new Texture2D(1, 1);
                    _tex.SetPixel(0, 0, Color.white);
                    _tex.Apply();
                }
                return _tex;
            }
        }

        void Box(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Solid);
            GUI.color = prev;
        }

        GUIStyle Text(int size, Color c, TextAnchor anchor = TextAnchor.MiddleLeft,
                      FontStyle style = FontStyle.Normal)
        {
            var s = new GUIStyle(GUI.skin.label)
            { fontSize = size, alignment = anchor, fontStyle = style, richText = true };
            s.normal.textColor = c;
            return s;
        }

        void OnGUI()
        {
            if (_dir == null || (!NetworkClient.active && !NetworkServer.active)) return;

            float h = Screen.height, w = Screen.width, u = h / 1080f;

            DrawBanner(w, u);
            DrawPips(w, u);
            DrawStillIn(w, u);
            DrawStanding(u);
            DrawBarnaby(w, h, u);
            DrawHostControls(w, h, u);
        }

        void DrawBanner(float w, float u)
        {
            if (_dir.phase == SayWhatPhase.Waiting) return;

            bool yours = _dir.phase == SayWhatPhase.Perform;
            Color tint = yours ? Perform : Watch;

            GUI.Label(new Rect(0, 40 * u, w, 150 * u), _dir.callText,
                      Text(Mathf.RoundToInt(130 * u), tint, TextAnchor.MiddleCenter, FontStyle.Bold));

            if (!string.IsNullOrEmpty(_dir.verdictText))
                GUI.Label(new Rect(0, 190 * u, w, 46 * u), _dir.verdictText,
                          Text(Mathf.RoundToInt(34 * u), Color.white, TextAnchor.MiddleCenter));
        }

        /// <summary>
        /// The sequence so far, as pips. During Watch they light up as he calls them;
        /// during Perform they are blank, because the whole game is that it is gone.
        /// </summary>
        void DrawPips(float w, float u)
        {
            if (_dir.phase != SayWhatPhase.Watch && _dir.phase != SayWhatPhase.Perform) return;

            string shown = _dir.revealed ?? "";
            int n = Mathf.Max(shown.Length, 1);
            float pw = 150 * u, gap = 14 * u;
            float total = n * pw + (n - 1) * gap;
            float x = (w - total) * 0.5f, y = 250 * u;

            for (int i = 0; i < shown.Length; i++)
            {
                var act = (PartyAction)(shown[i] - '0');
                Box(new Rect(x + i * (pw + gap), y, pw, 64 * u), Ink);
                GUI.Label(new Rect(x + i * (pw + gap), y, pw, 64 * u), SayWhatDirector.Describe(act),
                          Text(Mathf.RoundToInt(28 * u), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold));
            }
        }

        void DrawStillIn(float w, float u)
        {
            int alive = 0, total = 0;
            foreach (PartyPlayer p in SayWhatDirector.Players())
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

        void DrawStanding(float u)
        {
            PartyPlayer me = null;
            foreach (PartyPlayer p in SayWhatDirector.Players())
                if (p.isLocalPlayer) { me = p; break; }
            if (me == null) return;

            float bw = 330 * u, bh = 104 * u, x = 52 * u, y = 52 * u;
            Box(new Rect(x, y, bw, bh), Ink);

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

        void DrawHostControls(float w, float h, float u)
        {
            if (!NetworkServer.active) return;
            if (_dir.phase != SayWhatPhase.Waiting && _dir.phase != SayWhatPhase.Finished) return;

            float bw = 260 * u, bh = 56 * u;
            if (GUI.Button(new Rect((w - bw) * 0.5f, h - bh - 140 * u, bw, bh),
                           _dir.round == 0 ? "Start round" : "Next round"))
                _dir.BeginRound();
        }
    }
}
