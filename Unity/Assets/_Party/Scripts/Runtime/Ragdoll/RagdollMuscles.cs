using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>
    /// Holds the ragdoll up, walks it, and lets go when it should.
    ///
    /// THE WHOLE CHARACTER IS ONE NUMBER: <see cref="Tone"/>, how hard the joint drives pull
    /// toward the rest pose. At 1 it stands and walks; at 0 it is a sack of capsules. Nothing
    /// here is a state machine or an animation clip - being winded, being grabbed, being
    /// knocked out and getting back up are all that number moving, which is why Gang Beasts
    /// reads as alive rather than as a character playing "stumble".
    ///
    /// Balance is a PD controller on the CHEST, not a constraint. A constraint would make the
    /// thing unfallable, which is what the old FreezeRotation capsule was and why it looked
    /// dead. This one genuinely can be pushed over, and genuinely has to get up.
    /// </summary>
    public class RagdollMuscles : MonoBehaviour
    {
        [Header("Muscle tone")]
        [Range(0f, 1f)] public float Tone = 1f;
        [Tooltip("Drive spring at full tone. Higher = stiffer, more robotic, less funny.")]
        public float spring = 220f;
        public float damper = 22f;

        [Header("Balance")]
        [Tooltip("How hard it fights to stand up. Too high looks rigid; too low never recovers.")]
        public float uprightTorque = 520f;   // slerp spring on the hip anchor
        public float uprightDamping = 46f;
        [Tooltip("Beyond this tilt it has fallen and stops fighting - the give-up angle.")]
        public float giveUpAngle = 62f;
        [Tooltip("Extra righting effort while heaving itself back up off the floor.")]
        public float recoveryBoost = 1.8f;

        [Header("Hips")]
        [Tooltip("Height the pelvis is held at above whatever is underneath it. THIS is what stands it up.")]
        public float standHeight = 0.95f;
        public float hipSpring = 260f;
        public float hipDamper = 34f;

        [Header("Locomotion")]
        public float moveForce = 7f;    // most of the walking is the anchor lead below
        public float maxSpeed = 4.2f;
        [Tooltip("How far it leans into a run. Leaning is most of what reads as intent.")]
        public float leanAmount = 4f;
        public float turnSpeed = 9f;
        [Tooltip("How far ahead of the hips the anchor sits while moving. THIS is the walk speed.")]
        public float leadDistance = 0.035f;

        [Header("Stride")]
        [Tooltip("How far each leg swings. This is the whole walk cycle - there is no clip.")]
        public float strideAngle = 27f;
        [Tooltip("Steps per second at full pace.")]
        public float strideRate = 3.4f;

        [Header("Recovery")]
        [Tooltip("Seconds face-down before it tries to get back up.")]
        public float getUpAfter = 1.4f;

        public RagdollBuilder.Rig Rig { get; private set; }
        public bool IsDown { get; private set; }
        public Vector3 MoveInput { get; set; }

        Rigidbody _pelvis, _chest;
        float _downSince = -1f;
        float _facing;
        float _stridePhase;

        public void Bind(RagdollBuilder.Rig rig)
        {
            Rig = rig;
            _pelvis = rig.Get(Bone.Pelvis);
            _chest  = rig.Get(Bone.Chest);
            ApplyTone();
        }

        /// <summary>Go limp for a moment - hit, grabbed, or just humiliated.</summary>
        public void Limp(float seconds)
        {
            CancelInvoke(nameof(Restore));
            Tone = 0f;
            ApplyTone();
            Invoke(nameof(Restore), seconds);
        }

        void Restore() { Tone = 1f; ApplyTone(); }

        public void ApplyTone()
        {
            if (Rig == null) return;
            foreach (var kv in Rig.Joints)
            {
                // Arms stay softer than the spine even at full tone. Stiff arms look like a
                // shop mannequin; loose ones swing, and the swing is what sells a throw.
                float k = kv.Key is Bone.UpperArmL or Bone.UpperArmR
                                 or Bone.LowerArmL or Bone.LowerArmR ? 0.45f : 1f;
                var d = kv.Value.slerpDrive;
                d.positionSpring = spring * Tone * k;
                d.positionDamper = damper * Tone * k;
                kv.Value.slerpDrive = d;
            }
        }

        void FixedUpdate()
        {
            if (Rig == null || _pelvis == null || _chest == null) return;

            float tilt = Vector3.Angle(_chest.transform.up, Vector3.up);
            bool downNow = tilt > giveUpAngle;

            if (downNow && !IsDown) { IsDown = true; _downSince = Time.time; }
            else if (!downNow && IsDown) { IsDown = false; _downSince = -1f; }

            // GETTING UP IS NOT INSTANT. A beat on the floor is the payoff for being knocked
            // over - snapping upright throws the joke away. Tone is restored here; the
            // actual heave is the recovery boost inside Balance.
            if (IsDown && Time.time - _downSince > getUpAfter && Tone < 1f)
            {
                Tone = Mathf.MoveTowards(Tone, 1f, Time.fixedDeltaTime * 1.5f);
                ApplyTone();
            }

            Balance(tilt);
            Locomote();
        }

        /// <summary>
        /// Hold the character up by springing its hips to a kinematic anchor.
        ///
        /// TWO FORCE-BASED ATTEMPTS FAILED BEFORE THIS, and both failed the same way - by
        /// racing the solver instead of using it:
        ///   * torque on the pelvis AND chest made them fight through the joint between
        ///     them; bodies corkscrewed and settled at 79-126 degrees, held at the right
        ///     height but bent double
        ///   * an unclamped angle-axis correction is enormous when inverted (pi * 90 * 2.2,
        ///     about 620 rad/s^2) and threw three of four test characters off the map
        ///
        /// The anchor is a kinematic point that is always upright at standing height. The
        /// pelvis is sprung to it, so the SOLVER carries the body and everything above the
        /// hips hangs off the joint drives. Walking moves the anchor. Falling over is the
        /// spring weakening. Being knocked about still works, because the link is a spring
        /// and not a rail.
        /// </summary>
        void Balance(float tilt)
        {
            ConfigurableJoint aj = Rig.AnchorJoint;
            if (aj == null || Rig.Anchor == null) return;

            bool recovering = IsDown && _downSince > 0f && Time.time - _downSince > getUpAfter;

            // The beat on the floor: while down and not yet recovering the spring is off, so
            // the body genuinely lies there. Something that keeps straining face-down looks
            // broken; a moment of defeat before it heaves itself up is the joke.
            float grip = (IsDown && !recovering) ? 0f
                       : (recovering ? recoveryBoost : 1f) * Tone;

            var lin = new JointDrive
            {
                positionSpring = hipSpring * grip,
                positionDamper = hipDamper * grip,
                maximumForce = 420f,
            };
            aj.xDrive = aj.yDrive = aj.zDrive = lin;

            aj.slerpDrive = new JointDrive
            {
                positionSpring = uprightTorque * grip,
                positionDamper = uprightDamping * grip,
                maximumForce = 700f,
            };

            // Keep the anchor over the body and at standing height, upright and facing the
            // way we are going. It never rotates with the ragdoll - that is the whole point,
            // it is the idea of standing that the body is pulled toward.
            Vector3 pos = _pelvis.position;

            // THE GROUND RAY MUST IGNORE THIS CHARACTER'S OWN BODY.
            //
            // A plain downward ray from the pelvis hits its own LEGS first, so groundY became
            // the leg position, the anchor jumped to legY + standHeight, that hauled the body
            // upward, which lifted the legs, which raised the ray hit again - a feedback loop
            // that launched bots to y = 6.3 and threw one to y = -27. The stationary test
            // character stood fine at 0.88 the whole time, which is what made it look like a
            // movement problem rather than a raycast problem.
            float groundY = pos.y - standHeight;
            float best = float.NegativeInfinity;
            foreach (RaycastHit h in Physics.RaycastAll(pos + Vector3.up * 0.2f, Vector3.down, 6f))
            {
                if (h.rigidbody != null && IsOwnBody(h.rigidbody)) continue;
                if (h.point.y > best) best = h.point.y;
            }
            if (best > float.NegativeInfinity) groundY = best;

            // Move toward the target rather than snapping to it. A hard set means stepping
            // onto a crate teleports the hips, and the body is yanked after them.
            Vector3 want = new Vector3(pos.x, groundY + standHeight, pos.z);
            Rig.Anchor.MovePosition(Vector3.MoveTowards(Rig.Anchor.position, want,
                                                        Time.fixedDeltaTime * 6f));
            Rig.Anchor.MoveRotation(Quaternion.Euler(0f, _facing, 0f));
        }

        bool IsOwnBody(Rigidbody rb)
        {
            foreach (var kv in Rig.Bodies) if (kv.Value == rb) return true;
            return false;
        }

        void Locomote()
        {
            Vector3 want = MoveInput;
            if (want.sqrMagnitude > 1f) want.Normalize();
            if (Tone <= 0.05f) return;

            Vector3 flat = new Vector3(_pelvis.linearVelocity.x, 0f, _pelvis.linearVelocity.z);

            if (want.sqrMagnitude > 0.001f)
            {
                // WALKING IS THE ANCHOR LEADING, not a shove at the hips.
                //
                // Doing both was pushing ~44 m/s^2 through the pelvis: 34 from the direct
                // force plus another 10 from the lead spring. Applied at hip height on a
                // body balanced on two dangling capsules, that levers it straight over -
                // traced, it fell at t=2, got up at t=4, fell at t=5, up at t=7, and so on
                // for the whole run. Falling and heaving back up is exactly the behaviour
                // that should exist; falling every two seconds is not a walk.
                //
                // Leading with the anchor makes the body chase a point, so it leans after it
                // and the lean is a consequence rather than a torque I apply by hand.
                if (flat.magnitude < maxSpeed)
                    _pelvis.AddForce(want * (moveForce * Tone), ForceMode.Acceleration);
                Rig.Anchor.MovePosition(Rig.Anchor.position + want * (leadDistance * Tone));

                // Face the way we are going, and LEAN into it. The lean is doing most of the
                // work: it is the difference between a capsule translating and a body running.
                _facing = Mathf.LerpAngle(_facing, Mathf.Atan2(want.x, want.z) * Mathf.Rad2Deg,
                                          Time.fixedDeltaTime * turnSpeed);
                Vector3 lean = Quaternion.Euler(0f, _facing, 0f) * Vector3.forward;
                _chest.AddTorque(Vector3.Cross(Vector3.up, lean) * (-leanAmount * Tone),
                                 ForceMode.Acceleration);
            }
            else
            {
                // Gentle braking, not a hard stop - sliding to a halt is part of the look.
                _pelvis.AddForce(-flat * (6f * Tone), ForceMode.Acceleration);
            }

            SetSpineTarget(Quaternion.Euler(0f, _facing, 0f));
            Stride(want, flat.magnitude);
        }

        /// <summary>
        /// The walk cycle. There is no animation clip anywhere in this project - the legs
        /// swing because a sine wave moves their joint targets, and the body reacts.
        ///
        /// IT IS ALSO A STABILITY FIX, which is why it exists now rather than as polish.
        /// Dangling legs drag along the floor while the body translates and act as levers,
        /// tripping it - the character was on the floor about 17% of a run. Legs that lift
        /// and place themselves stop catching, and the alternation keeps one under the body
        /// at all times.
        ///
        /// Phase advances with actual speed, so it never moonwalks: stop moving and the
        /// stride settles back to standing rather than pedalling on the spot.
        /// </summary>
        void Stride(Vector3 want, float speed)
        {
            bool moving = want.sqrMagnitude > 0.001f && !IsDown && Tone > 0.4f;

            if (moving)
                _stridePhase += strideRate * Mathf.Clamp(speed / Mathf.Max(maxSpeed, 0.01f), 0.35f, 1f)
                                * Mathf.PI * 2f * Time.fixedDeltaTime;
            else
                _stridePhase = Mathf.LerpAngle(_stridePhase * Mathf.Rad2Deg, 0f,
                                               Time.fixedDeltaTime * 6f) * Mathf.Deg2Rad;

            float a = Mathf.Sin(_stridePhase) * strideAngle * (moving ? 1f : 0.15f);
            SetLegTarget(Bone.LegL,  a);
            SetLegTarget(Bone.LegR, -a);
        }

        void SetLegTarget(Bone leg, float degrees)
        {
            if (!Rig.Joints.TryGetValue(leg, out ConfigurableJoint j)) return;
            if (!Rig.RestLocal.TryGetValue(leg, out Quaternion rest)) return;
            // Same inversion as the spine: ConfigurableJoint.targetRotation is relative to
            // the joint's original orientation, and inverted.
            j.targetRotation = Quaternion.Inverse(Quaternion.Euler(degrees, 0f, 0f)) * rest;
        }

        /// <summary>Point the spine where we are facing, through the drive rather than by force.</summary>
        void SetSpineTarget(Quaternion yaw)
        {
            if (!Rig.Joints.TryGetValue(Bone.Chest, out ConfigurableJoint j)) return;
            // ConfigurableJoint.targetRotation is expressed relative to the joint's original
            // orientation, and INVERTED. Getting this backwards is the classic way to make a
            // ragdoll spin itself inside out.
            j.targetRotation = Quaternion.Inverse(yaw) * Rig.RestLocal[Bone.Chest];
        }
    }
}
