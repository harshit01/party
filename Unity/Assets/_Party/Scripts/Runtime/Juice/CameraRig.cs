using UnityEngine;
using UnityEngine.InputSystem;
using Party.RedLight;

namespace Party.Juice
{
    /// <summary>
    /// Third-person follow camera, Fall Guys shaped.
    ///
    /// WHAT THIS REPLACED, AND WHY.
    /// This used to be a broadcast tripod: it framed whichever player was furthest up the
    /// lane, from 20 units back and 11 up. That put the cast at roughly 6% of frame
    /// height in a wide empty shot, and because it tracked the LEADER rather than YOU,
    /// your own character was frequently just one of five specks. Captured and compared
    /// against Docs/ArtTarget/redlight_target.svg: the founder's direction is "exactly
    /// like Fall Guys", which means the camera sits behind YOUR character and the other
    /// players stay visible around you rather than being the thing framed.
    ///
    /// The camera orbits to sit behind the way you are MOVING, with lag, and the look
    /// stick/mouse adds an offset on top. That lag is deliberate: snapping instantly to
    /// the movement direction reads as a bug when you strafe, and holding a fixed angle
    /// reads as a tripod again.
    ///
    /// Purely local. It never touches replicated state.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Header("Framing")]
        [Tooltip("Metres behind the subject. Tuned so a Filament is ~26% of frame height.")]
        public float distance = 7f;
        public float height = 3.1f;
        [Tooltip("Aim above the subject's feet, so it sits low in frame with the lane ahead.")]
        public float lookHeight = 1.55f;
        public float followSpeed = 9f;

        [Header("Orbit")]
        [Tooltip("How fast the camera swings round to behind your direction of travel.")]
        public float yawSpeed = 4.5f;
        [Tooltip("Below this speed the camera keeps the yaw it has, rather than snapping.")]
        public float yawMinSpeed = 1.2f;
        public float lookSensitivity = 150f;
        [Tooltip("Manual look drifts back to behind the player at this rate.")]
        public float lookReturn = 0.8f;

        [Header("Punch")]
        public float shakeDecay = 3.5f;

        float _yaw;
        float _lookOffset;
        float _shake;
        RoundPhase _lastPhase = RoundPhase.Waiting;
        Transform _subject;

        void Start()
        {
            _yaw = 0f;   // 0 looks up the lane, which is +Z
        }

        /// <summary>
        /// Whose shoulder we sit behind. YOUR player, always, if there is one - being
        /// eliminated should not rip the camera off you and onto a stranger. Only when
        /// there is no local player at all (a spectator, or a headless test process) does
        /// it fall back to whoever is furthest up the lane.
        /// </summary>
        Transform PickSubject()
        {
            Transform leader = null;
            float bestZ = float.MinValue;

            foreach (PartyPlayer p in RedLightDirector.Players())
            {
                if (p.isLocalPlayer) return p.transform;
                if (p.transform.position.z > bestZ) { bestZ = p.transform.position.z; leader = p.transform; }
            }
            return leader;
        }

        void LateUpdate()
        {
            RedLightDirector d = RedLightDirector.Instance;

            Transform s = PickSubject();
            if (s != null) _subject = s;
            if (_subject == null) return;

            // ---- yaw: swing behind the direction of travel, plus manual look ----
            Rigidbody rb = _subject.GetComponent<Rigidbody>();
            Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);

            float desiredYaw = _yaw;
            if (flat.magnitude > yawMinSpeed)
                desiredYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;

            _lookOffset += ReadLook() * lookSensitivity * Time.deltaTime;
            _lookOffset = Mathf.Lerp(_lookOffset, 0f, Time.deltaTime * lookReturn);
            _lookOffset = Mathf.Clamp(_lookOffset, -120f, 120f);

            _yaw = Mathf.LerpAngle(_yaw, desiredYaw, Time.deltaTime * yawSpeed);

            // ---- position ----
            Quaternion orbit = Quaternion.Euler(0f, _yaw + _lookOffset, 0f);
            Vector3 want = _subject.position + orbit * new Vector3(0f, height, -distance);

            // Push in on the payoff, the one moment the shot is allowed to be about
            // somebody else's business.
            if (d != null && d.phase == RoundPhase.Finished)
                want = _subject.position + orbit * new Vector3(0f, height * 0.75f, -distance * 0.62f);

            transform.position = Vector3.Lerp(transform.position, want, Time.deltaTime * followSpeed);

            // ---- aim ----
            Vector3 focus = _subject.position + Vector3.up * lookHeight;
            Quaternion look = Quaternion.LookRotation(focus - transform.position, Vector3.up);

            if (d != null && d.phase != _lastPhase)
            {
                if (d.MustFreeze && _lastPhase == RoundPhase.Go) _shake = 0.9f;
                _lastPhase = d.phase;
            }
            if (_shake > 0.001f)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * shakeDecay);
                look *= Quaternion.Euler(Random.Range(-1f, 1f) * _shake,
                                         Random.Range(-1f, 1f) * _shake, 0f);
            }
            transform.rotation = look;
        }

        /// <summary>
        /// Right stick, or mouse X while a button is held. Read straight from the devices
        /// like LocalMoveInput does, so there is no .inputactions wiring to get wrong -
        /// and it must never throw in a headless test process, where there are no devices
        /// at all.
        /// </summary>
        static float ReadLook()
        {
            Gamepad g = Gamepad.current;
            if (g != null)
            {
                float x = g.rightStick.ReadValue().x;
                if (Mathf.Abs(x) > 0.15f) return x;
            }

            Mouse m = Mouse.current;
            if (m != null && m.rightButton.isPressed)
                return Mathf.Clamp(m.delta.ReadValue().x * 0.05f, -1f, 1f);

            return 0f;
        }
    }
}
