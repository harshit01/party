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
    /// A deck section that sags under weight and springs back.
    ///
    /// This is what stops the arena being furniture: standing on it TILTS it, so where you
    /// stand matters and two people crowding one end is a mistake you can watch developing.
    /// Kinematic, so it moves the world rather than being moved by it - a freely jointed
    /// see-saw under a ragdoll is a catapult.
    /// </summary>
    public class Tilter : MonoBehaviour
    {
        public float maxTilt = 9f;
        public float responsiveness = 3.5f;
        public float halfWidth = 1.1f;

        Quaternion _rest;
        float _tilt;

        void Awake() => _rest = transform.localRotation;

        void FixedUpdate()
        {
            float load = 0f;
            Collider[] on = Physics.OverlapBox(transform.position + Vector3.up * 0.6f,
                new Vector3(halfWidth, 0.6f, transform.localScale.z * 0.5f), transform.rotation);
            foreach (Collider c in on)
            {
                Rigidbody rb = c.attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                load += Mathf.Clamp((c.bounds.center.x - transform.position.x) / halfWidth, -1f, 1f)
                        * rb.mass;
            }
            _tilt = Mathf.Lerp(_tilt, Mathf.Clamp(load * 0.8f, -maxTilt, maxTilt),
                               Time.fixedDeltaTime * responsiveness);
            transform.localRotation = _rest * Quaternion.Euler(0f, 0f, -_tilt);
        }
    }
}
