using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>Controls, on screen, so the lab is usable without reading the source.</summary>
    public class RagdollLabHUD : MonoBehaviour
    {
        void OnGUI()
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            s.normal.textColor = Color.white;
            GUI.Label(new Rect(24, 20, 900, 30),
                "<b>RAGDOLL LAB</b>   WASD move   SHIFT hold to grab (release to throw)   SPACE jump   R go limp", s);
        }
    }
}
