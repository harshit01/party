using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>
    /// Drives one ragdoll through a fixed script and MEASURES what happened.
    ///
    /// WHY THIS EXISTS. Grab and throw were written and never once seen working - there is no
    /// way to press SHIFT in a headless build, so the code was shipped untested and "it looks
    /// better" was the only evidence for any of it. This project has been bitten repeatedly by
    /// exactly that (HANDOFF §6.1, §6.7): a mechanic that silently never fires still passes
    /// any test that does not look for it.
    ///
    /// So: stand, walk, grab, carry, throw, go limp - each phase timed, each outcome logged in
    /// a form Tests/ragdoll_report.py can parse. No screenshots involved; a still frame cannot
    /// tell standing from mid-collapse.
    /// </summary>
    public class RagdollProbe : MonoBehaviour
    {
        public Rigidbody target;          // the crate to go and pick up

        RagdollMuscles _m;
        RagdollGrab _g;

        // phase boundaries, seconds
        //
        // TURNING GETS ITS OWN PHASE. The original script walked in a straight line at a
        // fixed target and reported "0 falls while walking", which was true and useless -
        // it never tested the case the founder was actually seeing. Changing direction while
        // the legs are planted is the hard part, and it was going unmeasured.
        const float StandUntil = 4f, WalkUntil = 9f, TurnUntil = 15f,
                    GrabUntil = 19f, LimpAt = 22f, EndAt = 26f;

        int _standSamples, _downFrames, _falls, _recoveries;
        // Falls DURING THE WALK, counted separately. The probe deliberately makes the
        // character go limp later on, and lumping that in with stability measured the test
        // rather than the controller - it read as "on the floor 9% of the time" when the
        // walk itself had stopped falling entirely.
        int _walkFalls, _walkDownFrames;
        int _turnFalls, _turnDownFrames;
        float _tiltSum, _tiltMax;
        bool _wasDown, _wasDownWalk, _wasDownTurn;
        Vector3 _walkStart;
        bool _walkStarted;
        bool _grabTried, _grabWorked;
        Vector3 _crateAtGrab;
        float _carriedDist;
        float _throwSpeed = -1f;
        bool _limpTried;
        float _limpTilt = -1f;
        bool _done;

        void Start()
        {
            _m = GetComponent<RagdollMuscles>();
            _g = GetComponent<RagdollGrab>();
        }

        void FixedUpdate()
        {
            if (_m == null || _m.Rig == null || _done) return;
            if (_g == null) _g = GetComponent<RagdollGrab>();

            float t = Time.time;
            Rigidbody pelvis = _m.Rig.Get(Bone.Pelvis);
            Rigidbody chest = _m.Rig.Get(Bone.Chest);
            if (pelvis == null || chest == null) return;

            float tilt = Vector3.Angle(chest.transform.up, Vector3.up);

            // Falls and recoveries are counted across the WHOLE run, because a controller
            // that never falls and one that never gets up look identical in a summary.
            if (_m.IsDown && !_wasDown) _falls++;
            if (!_m.IsDown && _wasDown) _recoveries++;
            _wasDown = _m.IsDown;
            if (_m.IsDown) _downFrames++;

            if (t < StandUntil)
            {
                _m.MoveInput = Vector3.zero;
                _standSamples++;
                _tiltSum += tilt;
                _tiltMax = Mathf.Max(_tiltMax, tilt);
            }
            else if (t < WalkUntil)
            {
                if (!_walkStarted) { _walkStarted = true; _walkStart = pelvis.position; }
                if (_m.IsDown) _walkDownFrames++;
                if (_m.IsDown && !_wasDownWalk) _walkFalls++;
                _wasDownWalk = _m.IsDown;
                // Walk at the crate, so the grab phase has something in reach.
                Vector3 to = target != null
                    ? (target.position - pelvis.position)
                    : Vector3.forward;
                to.y = 0f;
                _m.MoveInput = to.normalized;
            }
            else if (t < TurnUntil)
            {
                // Hard direction changes every 1.2 s - the case a straight-line walk misses.
                int leg = Mathf.FloorToInt((t - WalkUntil) / 1.2f);
                float ang = leg * 90f * Mathf.Deg2Rad;
                _m.MoveInput = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));

                if (_m.IsDown) _turnDownFrames++;
                if (_m.IsDown && !_wasDownTurn) _turnFalls++;
                _wasDownTurn = _m.IsDown;
            }
            else if (t < GrabUntil)
            {
                _m.MoveInput = Vector3.zero;
                if (!_grabTried) { _grabTried = true; _g.SetGrab(true); if (target != null) _crateAtGrab = target.position; }

                if (_g.Holding && !_grabWorked)
                {
                    _grabWorked = true;
                    Debug.Log($"[Probe] GRAB attached=true target={(_g.Held != null ? _g.Held.name : "?")}");
                }
                if (_grabWorked && target != null)
                    _carriedDist = Vector3.Distance(target.position, _crateAtGrab);
            }
            else if (t < LimpAt)
            {
                if (_throwSpeed < 0f)
                {
                    _g.SetGrab(false);
                    // Sample a beat later: the speed AT release is the throw, and reading it
                    // on the same frame catches the joint still attached.
                    Invoke(nameof(SampleThrow), 0.25f);
                    _throwSpeed = 0f;
                }
            }
            else if (t < EndAt)
            {
                if (!_limpTried) { _limpTried = true; _m.Limp(3f); }
                if (t > LimpAt + 2.8f && _limpTilt < 0f) _limpTilt = tilt;
            }
            else
            {
                Report(pelvis);
                _done = true;
            }
        }

        void SampleThrow()
        {
            _throwSpeed = target != null ? target.linearVelocity.magnitude : 0f;
        }

        void Report(Rigidbody pelvis)
        {
            float avg = _standSamples > 0 ? _tiltSum / _standSamples : -1f;
            float walked = _walkStarted ? Vector3.Distance(
                new Vector3(pelvis.position.x, 0f, pelvis.position.z),
                new Vector3(_walkStart.x, 0f, _walkStart.z)) : 0f;

            Debug.Log($"[Probe] STAND tilt_avg={avg:F1} tilt_max={_tiltMax:F1}");
            Debug.Log($"[Probe] WALK dist={walked:F2}");
            Debug.Log($"[Probe] STABILITY falls={_falls} recoveries={_recoveries} down_frames={_downFrames}");
            Debug.Log($"[Probe] WALKSTABILITY falls={_walkFalls} down_frames={_walkDownFrames}");
            Debug.Log($"[Probe] TURNSTABILITY falls={_turnFalls} down_frames={_turnDownFrames}");
            Debug.Log($"[Probe] GRAB worked={_grabWorked} carried={_carriedDist:F2}");
            Debug.Log($"[Probe] THROW speed={_throwSpeed:F2}");
            Debug.Log($"[Probe] LIMP tilt={_limpTilt:F1}");
            Debug.Log("[Probe] DONE");
        }
    }
}
