using UnityEngine;
using Party.RedLight;

namespace Party.Juice
{
    /// <summary>
    /// A camera that behaves like a broadcast camera rather than a fixed tripod.
    ///
    /// It tracks the leading pack up the lane, punches on STOP, and pushes in on the
    /// winner. None of this is cosmetic fiddling: a static camera is the single biggest
    /// reason a working prototype reads as "not a game".
    ///
    /// Purely local. It never touches replicated state.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        [Header("Framing")]
        public Vector3 offset = new Vector3(0f, 11f, -20f);
        public float   followSpeed = 2.2f;
        public float   pitch = 24f;

        [Header("Punch")]
        public float shakeDecay = 3.5f;

        float _shake;
        RoundPhase _lastPhase = RoundPhase.Waiting;
        Vector3 _target;

        void Start() => _target = transform.position;

        void LateUpdate()
        {
            RedLightDirector d = RedLightDirector.Instance;

            // Frame the furthest player still in it - that is where the tension is.
            //
            // The fallback used to be a hardcoded -12, the OLD start line. When the course
            // was lengthened to start at -46 and everyone got eliminated, nothing updated
            // 'lead', so the camera parked 34 units up an empty lane and the players were
            // off screen entirely. Never hardcode a position that another file owns.
            float startZ = d != null ? d.startZ : -46f;
            float lead = float.MinValue, anyone = float.MinValue;
            Transform winner = null;
            foreach (PartyPlayer p in RedLightDirector.Players())
            {
                anyone = Mathf.Max(anyone, p.transform.position.z);
                if (p.finished) winner = p.transform;
                if (p.eliminated) continue;
                lead = Mathf.Max(lead, p.transform.position.z);
            }

            // Everyone out? Show the wreckage rather than an empty stretch of lane.
            if (lead == float.MinValue) lead = anyone != float.MinValue ? anyone : startZ;

            float zoom = 1f;
            if (d != null && d.phase == RoundPhase.Finished && winner != null)
            {
                lead = winner.position.z;
                zoom = 0.55f;   // push in for the payoff
            }

            _target = new Vector3(0f, 0f, lead) + offset * zoom;
            transform.position = Vector3.Lerp(transform.position, _target, Time.deltaTime * followSpeed);

            // Punch the frame when the call flips to STOP.
            if (d != null && d.phase != _lastPhase)
            {
                if (d.MustFreeze && _lastPhase == RoundPhase.Go) _shake = 0.9f;
                _lastPhase = d.phase;
            }

            Quaternion look = Quaternion.Euler(pitch, 0f, 0f);
            if (_shake > 0.001f)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, Time.deltaTime * shakeDecay);
                look *= Quaternion.Euler(Random.Range(-1f, 1f) * _shake,
                                         Random.Range(-1f, 1f) * _shake, 0f);
            }
            transform.rotation = look;
        }
    }
}
