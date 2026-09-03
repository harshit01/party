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
    /// <summary>Turns steadily. Sweeping beams, spinning discs, prize wheels.</summary>
    public class Spinner : MonoBehaviour
    {
        public float degreesPerSecond = 45f;
        public Vector3 axis = Vector3.up;
        public float phase;

        Quaternion _rest;
        void Awake() => _rest = transform.localRotation;

        void Update() =>
            transform.localRotation = _rest * Quaternion.AngleAxis(
                (float)(ArenaTime.Now * degreesPerSecond) + phase, axis);
    }
}
