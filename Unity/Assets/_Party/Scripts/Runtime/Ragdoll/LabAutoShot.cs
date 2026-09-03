using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>
    /// Screenshot the lab at fixed times and quit, so the feel can be checked from a build
    /// rather than by asking someone to watch it.
    ///
    /// Several shots, not one: a ragdoll that looks fine at t=2 may be face down at t=8, and
    /// a single frame cannot tell the difference between standing and mid-collapse.
    ///
    ///     Party -partyshot /tmp/rag -partyseconds 14
    /// </summary>
    public class LabAutoShot : MonoBehaviour
    {
        string _prefix;
        float _quitAt = -1f;
        int _shot;
        readonly float[] _at = { 2.5f, 5f, 8f, 11f };

        static string Arg(string name)
        {
            string[] a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        void Start()
        {
            _prefix = Arg("-partyshot");
            string s = Arg("-partyseconds");
            if (!string.IsNullOrEmpty(s) && float.TryParse(s, out float f)) _quitAt = Time.time + f;
        }

        float _nextTrace;

        void Update()
        {
            // Per-second trace. The end state alone cannot distinguish "never stood up" from
            // "stood up then got knocked over", and those need opposite fixes.
            if (Time.time >= _nextTrace)
            {
                _nextTrace = Time.time + 1f;
                foreach (RagdollMuscles m in Object.FindObjectsByType<RagdollMuscles>(FindObjectsSortMode.None))
                {
                    Rigidbody pv = m.Rig?.Get(Bone.Pelvis);
                    Rigidbody ch = m.Rig?.Get(Bone.Chest);
                    if (pv == null || ch == null) continue;
                    Debug.Log($"[Trace] t={Time.time:F0} {m.name} y={pv.position.y:F2} " +
                              $"tilt={Vector3.Angle(ch.transform.up, Vector3.up):F0} down={m.IsDown}");
                }
            }

            if (!string.IsNullOrEmpty(_prefix) && _shot < _at.Length && Time.time >= _at[_shot])
            {
                string path = $"{_prefix}_{_shot}.png";
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[Lab] shot {_shot} -> {path}");
                _shot++;
            }

            // Report what the muscles are actually doing, so a headless run says something
            // useful even without the images.
            if (_quitAt > 0f && Time.time >= _quitAt)
            {
                foreach (RagdollMuscles m in Object.FindObjectsByType<RagdollMuscles>(FindObjectsSortMode.None))
                {
                    Rigidbody pelvis = m.Rig?.Get(Bone.Pelvis);
                    Rigidbody chest = m.Rig?.Get(Bone.Chest);
                    if (pelvis == null || chest == null) continue;
                    float tilt = Vector3.Angle(chest.transform.up, Vector3.up);
                    Debug.Log($"[Lab] {m.name}: pelvisY={pelvis.position.y:F2} tilt={tilt:F0}deg " +
                              $"down={m.IsDown} tone={m.Tone:F2}");
                }
                Application.Quit();
            }
        }
    }
}
