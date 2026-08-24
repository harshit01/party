using UnityEngine;

namespace Party.Juice
{
    /// <summary>
    /// Makes a capsule read as a character without a single art asset.
    ///
    /// This is the "bath toy" look from the design document, done in code: the body
    /// squashes when it lands, stretches when it runs, leans into its own movement, and
    /// topples over when eliminated. Physics does the acting - exactly the documented art
    /// direction, and it costs nothing.
    ///
    /// IMPORTANT: this only ever touches a VISUAL CHILD, never the networked root. The
    /// root's transform is what NetworkTransform replicates; scaling or rotating it here
    /// would fight the netcode and desync the very thing the milestone proved.
    /// </summary>
    public class PlayerJuice : MonoBehaviour
    {
        [Tooltip("Visual-only child. Never the networked root.")]
        public Transform visual;

        [Header("Feel")]
        public float leanDegrees   = 14f;
        public float stretchAmount = 0.16f;
        public float responsiveness = 9f;

        Rigidbody _rb;
        PartyPlayer _player;
        Vector3 _baseScale;
        bool _toppled;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _player = GetComponent<PartyPlayer>();
            if (visual != null) _baseScale = visual.localScale == Vector3.zero ? Vector3.one : visual.localScale;
        }

        void LateUpdate()
        {
            if (visual == null) return;

            if (_player != null && _player.eliminated)
            {
                Topple();
                return;
            }

            Vector3 v = _rb != null ? _rb.linearVelocity : Vector3.zero;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float speed = flat.magnitude;
            float t = Mathf.Clamp01(speed / 7f);

            // Stretch along travel, squash across it. Reads as effort.
            Vector3 target = _baseScale;
            target.y *= 1f + stretchAmount * t;
            target.x *= 1f - stretchAmount * 0.5f * t;
            target.z *= 1f - stretchAmount * 0.5f * t;
            visual.localScale = Vector3.Lerp(visual.localScale, target, Time.deltaTime * responsiveness);

            // Lean into the direction of travel.
            Quaternion lean = Quaternion.identity;
            if (speed > 0.15f)
            {
                Vector3 dir = flat.normalized;
                lean = Quaternion.AngleAxis(leanDegrees * t, new Vector3(dir.z, 0f, -dir.x));
            }
            visual.localRotation = Quaternion.Slerp(visual.localRotation, lean, Time.deltaTime * responsiveness);

        }

        void Topple()
        {
            // Falling over is funnier than fading out, and it is legible from across a
            // room - which is the only way anyone reads a party game screen.
            if (!_toppled)
            {
                _toppled = true;
                if (_rb != null)
                {
                    _rb.constraints = RigidbodyConstraints.None;
                    _rb.AddTorque(new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f)),
                                  ForceMode.VelocityChange);
                }
            }
            visual.localScale = Vector3.Lerp(visual.localScale, _baseScale * 0.88f, Time.deltaTime * 4f);
        }

        /// <summary>Called when a fresh round resets the player.</summary>
        public void ResetJuice()
        {
            _toppled = false;
            if (_rb != null) _rb.constraints = RigidbodyConstraints.FreezeRotation;
            if (visual != null)
            {
                visual.localScale = _baseScale;
                visual.localRotation = Quaternion.identity;
            }
            transform.rotation = Quaternion.identity;
        }
    }
}
