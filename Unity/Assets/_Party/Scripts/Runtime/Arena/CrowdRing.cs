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
    /// Spectators bobbing out of phase, who jump when something happens.
    ///
    /// A crowd that only bobs is wallpaper. One that reacts is the difference between a set
    /// and an audience, and the audience is the entire premise of a game show.
    /// </summary>
    public class CrowdRing : MonoBehaviour
    {
        public float bob = 0.16f;
        public float period = 1.8f;

        Transform[] _people;
        Vector3[] _rest;
        float[] _offset;
        float _cheer;

        void Start()
        {
            int n = transform.childCount;
            _people = new Transform[n]; _rest = new Vector3[n]; _offset = new float[n];
            for (int i = 0; i < n; i++)
            {
                _people[i] = transform.GetChild(i);
                _rest[i] = _people[i].localPosition;
                // Deterministic per-index offset rather than Random, so every machine draws
                // the same crowd without syncing a hundred transforms.
                _offset[i] = (i * 2.399963f) % (Mathf.PI * 2f);
            }
        }

        /// <summary>Somebody just went off the plank. React.</summary>
        public void Cheer() => _cheer = 1f;

        void Update()
        {
            if (_people == null) return;
            _cheer = Mathf.MoveTowards(_cheer, 0f, Time.deltaTime * 0.8f);
            for (int i = 0; i < _people.Length; i++)
            {
                if (_people[i] == null) continue;
                float t = (float)(ArenaTime.Now / period * Mathf.PI * 2.0) + _offset[i];
                _people[i].localPosition = _rest[i]
                    + Vector3.up * Mathf.Abs(Mathf.Sin(t)) * (bob * (1f + _cheer * 4f));
            }
        }
    }
}
