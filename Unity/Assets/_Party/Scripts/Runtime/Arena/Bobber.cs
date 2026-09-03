// ONE MonoBehaviour PER FILE, NAMED AFTER THE CLASS.
//
// These six lived together in ArenaKit.cs, and Unity resolves serialised script references
// by file name - so a component in a file named after something else cannot be read back
// out of a scene. That is the "level0 is corrupted" failure this codebase has now hit three
// times, always as a MonoBehaviour deserialising past the end of the data. It did not bite
// the ragdoll because those components are added at runtime and never written into a scene;
// the arena bakes its own in, so every one of 12 builds came out dead.
using UnityEngine;

namespace Party.Arena
{
    /// <summary>Floats up and down and rolls slightly. Balloons, floating platforms.</summary>
    public class Bobber : MonoBehaviour
    {
        public float height = 0.35f;
        public float period = 4f;
        public float phase;
        public float tilt = 6f;

        Vector3 _rest;
        Quaternion _restRot;

        void Awake() { _rest = transform.localPosition; _restRot = transform.localRotation; }

        void Update()
        {
            double t = ArenaTime.Now / period * Mathf.PI * 2.0 + phase;
            transform.localPosition = _rest + Vector3.up * (Mathf.Sin((float)t) * height);
            transform.localRotation = _restRot * Quaternion.Euler(
                Mathf.Sin((float)t * 0.7f) * tilt, 0f, Mathf.Cos((float)t * 0.5f) * tilt);
        }
    }
}
