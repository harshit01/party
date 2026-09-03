using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>
    /// Grab, carry and throw - the Gang Beasts verbs, on two buttons.
    ///
    /// THROWING IS NOT A BUTTON. You grab, you swing, you let go, and the momentum you built
    /// does the rest. That is how it feels in the reference and it is also what keeps this
    /// inside `HANDOFF.md` §1's hard rule of two action buttons: A grabs and holds, B jumps.
    /// A dedicated throw button would be a third, and would make throws feel like a vending
    /// machine rather than a wind-up.
    ///
    /// The grab itself is a JOINT, not a parent-swap. Parenting a grabbed body to the hand
    /// makes it rigid and weightless, and you lose the entire comedy: the sag of something
    /// heavy, the swing of something light, the tug-of-war when two players grab the same
    /// crate. A joint keeps all of that for free because the physics is still running.
    /// </summary>
    public class RagdollGrab : MonoBehaviour
    {
        [Header("Reach")]
        public float grabRadius = 0.42f;
        [Tooltip("How strongly a held thing is pulled to the hand. Low = things sag and drag.")]
        public float holdSpring = 900f;
        public float holdDamper = 40f;
        [Tooltip("Above this force the grip tears - so a heavy thing CAN be wrestled away.")]
        public float gripStrength = 1400f;

        [Header("Reaching")]
        [Tooltip("How far the arms swing forward while the grab button is held.")]
        public float reachAngle = 62f;

        public bool Holding => _left != null || _right != null;

        RagdollMuscles _m;
        Joint _left, _right;
        Rigidbody _heldL, _heldR;
        bool _wantGrab;

        public void Bind(RagdollMuscles m) => _m = m;

        /// <summary>Held down = reaching and gripping. Released = let go, and whatever speed you had is the throw.</summary>
        public void SetGrab(bool held)
        {
            if (held == _wantGrab) return;
            _wantGrab = held;
            if (!held) Release();
        }

        void FixedUpdate()
        {
            if (_m == null || _m.Rig == null) return;

            Reach(_wantGrab);
            if (!_wantGrab) return;

            if (_left == null)  TryGrab(Bone.LowerArmL, ref _left,  ref _heldL);
            if (_right == null) TryGrab(Bone.LowerArmR, ref _right, ref _heldR);
        }

        /// <summary>
        /// Swing the arms forward while reaching. Purely through the joint drive - the arms
        /// are still physical, so they collide with the world on the way and can be knocked
        /// aside mid-reach, which is exactly the sort of failure that is funny.
        /// </summary>
        void Reach(bool reaching)
        {
            foreach (Bone b in new[] { Bone.UpperArmL, Bone.UpperArmR })
            {
                if (!_m.Rig.Joints.TryGetValue(b, out ConfigurableJoint j)) continue;
                Quaternion target = reaching
                    ? Quaternion.Euler(-reachAngle, 0f, 0f)
                    : Quaternion.identity;
                j.targetRotation = Quaternion.Slerp(j.targetRotation,
                    Quaternion.Inverse(target) * _m.Rig.RestLocal[b], Time.fixedDeltaTime * 10f);
            }
        }

        void TryGrab(Bone hand, ref Joint slot, ref Rigidbody held)
        {
            Rigidbody handRb = _m.Rig.Get(hand);
            if (handRb == null) return;

            foreach (Collider c in Physics.OverlapSphere(handRb.position, grabRadius))
            {
                Rigidbody target = c.attachedRigidbody;
                if (target == null || target.isKinematic) continue;
                if (IsOwnBody(target)) continue;

                var j = handRb.gameObject.AddComponent<ConfigurableJoint>();
                j.connectedBody = target;
                j.autoConfigureConnectedAnchor = false;
                j.anchor = Vector3.zero;
                j.connectedAnchor = target.transform.InverseTransformPoint(handRb.position);

                // Springy rather than locked, so a held thing has weight and lag. A locked
                // joint makes a carried player look welded to your fist.
                j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Limited;
                j.linearLimit = new SoftJointLimit { limit = 0.12f };
                var drive = new JointDrive
                {
                    positionSpring = holdSpring,
                    positionDamper = holdDamper,
                    maximumForce = gripStrength,
                };
                j.xDrive = j.yDrive = j.zDrive = drive;

                // The grip can BREAK. Two players hauling on the same body should end with
                // somebody losing their hold, not with the physics solver screaming.
                j.breakForce = gripStrength;
                j.breakTorque = gripStrength;

                slot = j;
                held = target;

                // Whatever you grabbed goes briefly limp. Grabbing a player who is still
                // rigidly walking looks like grabbing a lamppost.
                RagdollMuscles other = target.GetComponentInParent<RagdollMuscles>();
                if (other != null && other != _m) other.Limp(0.55f);
                return;
            }
        }

        bool IsOwnBody(Rigidbody rb)
        {
            foreach (var kv in _m.Rig.Bodies) if (kv.Value == rb) return true;
            return false;
        }

        void Release()
        {
            Throw(ref _left,  ref _heldL);
            Throw(ref _right, ref _heldR);
        }

        /// <summary>
        /// Let go. No impulse is added: the thing already has the velocity your swing gave it,
        /// and adding more is what makes a throw feel like a catapult instead of a heave.
        /// The small nudge below only stops a released body immediately re-colliding with the
        /// hand that dropped it.
        /// </summary>
        void Throw(ref Joint j, ref Rigidbody held)
        {
            if (j == null) { held = null; return; }
            Destroy(j);
            j = null;

            if (held != null)
            {
                Vector3 away = (held.position - transform.position).normalized;
                held.AddForce(away * 0.6f, ForceMode.VelocityChange);
                held = null;
            }
        }
    }
}
