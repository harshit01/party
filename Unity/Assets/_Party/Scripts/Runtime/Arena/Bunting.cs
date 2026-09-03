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
    /// <summary>
    /// A run of pennants that ripples. A travelling sine down the row rather than a cloth
    /// sim - it costs nothing and nobody in a party game is inspecting the bunting.
    /// </summary>
    public class Bunting : MonoBehaviour
    {
        public float amplitude = 0.18f;
        public float period = 2.4f;
        public float wavelength = 2.6f;

        Transform[] _flags;
        Vector3[] _rest;

        void Start()
        {
            _flags = new Transform[transform.childCount];
            _rest = new Vector3[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                _flags[i] = transform.GetChild(i);
                _rest[i] = _flags[i].localPosition;
            }
        }

        void Update()
        {
            if (_flags == null) return;
            for (int i = 0; i < _flags.Length; i++)
            {
                if (_flags[i] == null) continue;
                float t = (float)(ArenaTime.Now / period * Mathf.PI * 2.0) + _rest[i].x / wavelength;
                _flags[i].localPosition = _rest[i] + Vector3.up * (Mathf.Sin(t) * amplitude);
                _flags[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Cos(t) * 22f);
            }
        }
    }
}
