using System.Collections.Generic;
using UnityEngine;

namespace Party.Ragdoll
{
    /// <summary>Every part of the body, so other scripts can reach one without a string lookup.</summary>
    public enum Bone { Pelvis, Chest, Head, UpperArmL, LowerArmL, UpperArmR, LowerArmR, LegL, LegR }

    /// <summary>
    /// Builds an ACTIVE RAGDOLL out of the same primitives the Filament is already made of.
    ///
    /// WHY THIS EXISTS
    /// `HANDOFF.md` §2 committed to "capsules and spheres; PHYSICS DOES THE ACTING" back in
    /// August, and nobody ever built the physics that acts. What shipped instead is a single
    /// Rigidbody with `FreezeRotation` - a body that cannot lean, stumble or be knocked over -
    /// wearing about fifteen decorative primitives that have no colliders and no rigidbodies
    /// at all. It slides like a hockey puck, which is exactly what the founder saw.
    ///
    /// Gang Beasts, the stated reference, has almost no keyframed animation. Its comedy is
    /// an active ragdoll: jointed rigidbodies pulled toward a target pose by joint drives
    /// behaving as muscles. Keyframing it would not look like it. So this needs no artist,
    /// no rig and no animation clips - it is physics on the capsules that already exist,
    /// which is the decision of record rather than a departure from it.
    ///
    /// THE ONE KNOB THAT MATTERS is drive strength. Strong drives stand up and walk; weak
    /// drives flop. Everything characterful - stumbling, being winded, going limp when
    /// grabbed, dying comically - is that number moving, not a state machine.
    /// </summary>
    public static class RagdollBuilder
    {
        public class Rig
        {
            public GameObject Root;
            /// <summary>
            /// A kinematic point in space the pelvis is sprung to. THIS is what stands the
            /// character up, and moving it is what walks them.
            /// </summary>
            public Rigidbody Anchor;
            public ConfigurableJoint AnchorJoint;
            public readonly Dictionary<Bone, Rigidbody> Bodies = new Dictionary<Bone, Rigidbody>();
            public readonly Dictionary<Bone, ConfigurableJoint> Joints = new Dictionary<Bone, ConfigurableJoint>();
            /// <summary>The pose the drives pull toward, captured at build time.</summary>
            public readonly Dictionary<Bone, Quaternion> RestLocal = new Dictionary<Bone, Quaternion>();

            public Rigidbody Get(Bone b) => Bodies.TryGetValue(b, out Rigidbody r) ? r : null;
        }

        // Masses are deliberately top-light and hip-heavy. A heavy pelvis is what keeps the
        // thing broadly upright without a rigid constraint; a head with real mass is what
        // makes it lurch when you turn, which is most of the charm.
        const float MassPelvis = 3.2f, MassChest = 2.6f, MassHead = 1.5f;
        const float MassUpperArm = 0.5f, MassLowerArm = 0.4f, MassLeg = 0.9f;

        public static Rig Build(Transform parent, Color livery, float scale = 1f)
        {
            var rig = new Rig();
            rig.Root = new GameObject("Ragdoll");
            rig.Root.transform.SetParent(parent, false);

            Material body = MakeMat(livery);
            Material skin = MakeMat(new Color(0.94f, 0.96f, 1f));

            // ---- torso ----
            Rigidbody pelvis = Part(rig, Bone.Pelvis, rig.Root.transform, PrimitiveType.Capsule,
                new Vector3(0f, 0.95f * scale, 0f), new Vector3(0.42f, 0.20f, 0.42f) * scale,
                MassPelvis, body);

            Rigidbody chest = Part(rig, Bone.Chest, rig.Root.transform, PrimitiveType.Capsule,
                new Vector3(0f, 1.35f * scale, 0f), new Vector3(0.48f, 0.26f, 0.48f) * scale,
                MassChest, body);

            Rigidbody head = Part(rig, Bone.Head, rig.Root.transform, PrimitiveType.Sphere,
                new Vector3(0f, 1.80f * scale, 0f), Vector3.one * 0.52f * scale,
                MassHead, skin);

            // ---- arms: TWO SEGMENTS, because one cannot reach round anything ----
            // Grabbing is the point, and a single stiff arm can only prod. An elbow is what
            // lets a carry look like a carry.
            for (int s = -1; s <= 1; s += 2)
            {
                bool left = s < 0;
                Bone up = left ? Bone.UpperArmL : Bone.UpperArmR;
                Bone lo = left ? Bone.LowerArmL : Bone.LowerArmR;

                Part(rig, up, rig.Root.transform, PrimitiveType.Capsule,
                    new Vector3(s * 0.42f * scale, 1.42f * scale, 0f),
                    new Vector3(0.14f, 0.18f, 0.14f) * scale, MassUpperArm, body);

                Part(rig, lo, rig.Root.transform, PrimitiveType.Capsule,
                    new Vector3(s * 0.42f * scale, 1.06f * scale, 0f),
                    new Vector3(0.13f, 0.18f, 0.13f) * scale, MassLowerArm, skin);
            }

            // ---- legs: one segment each, deliberately ----
            // Knees would buy a better walk and cost two more bodies per character, which is
            // eight more across a lobby before any netcode. Stumbling reads fine without them.
            for (int s = -1; s <= 1; s += 2)
            {
                Bone leg = s < 0 ? Bone.LegL : Bone.LegR;
                Part(rig, leg, rig.Root.transform, PrimitiveType.Capsule,
                    new Vector3(s * 0.17f * scale, 0.45f * scale, 0f),
                    new Vector3(0.17f, 0.34f, 0.17f) * scale, MassLeg, body);
            }

            // ---- the hip anchor ----
            //
            // WHY AN ANCHOR RATHER THAN BALANCING FORCES. Two force-based attempts failed
            // here: torquing the pelvis and chest independently made them fight through the
            // joint between them and corkscrew to the floor, and an unclamped angle-axis
            // correction threw three of four test characters off the map. Both were me
            // racing the physics solver every frame.
            //
            // A kinematic anchor lets the SOLVER do it. The pelvis is sprung to a point that
            // is always upright and at standing height; the body hangs off it, wobbling,
            // lagging, and colliding with everything on the way. Walking is moving the
            // anchor. Falling over is turning the spring down. This is how the Gang Beasts
            // family of games generally do it, and it is stable because the joint is solved
            // rather than fought.
            GameObject anchorGo = new GameObject("HipAnchor");
            anchorGo.transform.SetParent(rig.Root.transform, false);
            anchorGo.transform.localPosition = new Vector3(0f, 0.95f * scale, 0f);
            var anchor = anchorGo.AddComponent<Rigidbody>();
            anchor.isKinematic = true;
            anchor.useGravity = false;
            rig.Anchor = anchor;

            var aj = pelvis.gameObject.AddComponent<ConfigurableJoint>();
            aj.connectedBody = anchor;
            aj.autoConfigureConnectedAnchor = false;
            aj.anchor = Vector3.zero;
            aj.connectedAnchor = Vector3.zero;
            // Springy in all three axes rather than locked: the hips can be shoved off the
            // anchor and pulled back, which is the difference between a character being
            // knocked about and a character on a rail.
            aj.xMotion = aj.yMotion = aj.zMotion = ConfigurableJointMotion.Free;
            aj.angularXMotion = aj.angularYMotion = aj.angularZMotion = ConfigurableJointMotion.Free;
            aj.rotationDriveMode = RotationDriveMode.Slerp;
            rig.AnchorJoint = aj;

            // ---- joints ----
            Join(rig, Bone.Chest,     pelvis, 35f, 25f);
            Join(rig, Bone.Head,      chest,  40f, 18f);
            Join(rig, Bone.UpperArmL, chest,  75f, 45f);
            Join(rig, Bone.UpperArmR, chest,  75f, 45f);
            Join(rig, Bone.LowerArmL, rig.Get(Bone.UpperArmL), 90f, 10f);
            Join(rig, Bone.LowerArmR, rig.Get(Bone.UpperArmR), 90f, 10f);
            Join(rig, Bone.LegL,      pelvis, 55f, 30f);
            Join(rig, Bone.LegR,      pelvis, 55f, 30f);

            // Parts of the SAME body must not collide with each other, or the joints buzz and
            // the character vibrates itself across the floor. Done per-instance rather than by
            // a physics layer so two different characters still collide properly - which is
            // the entire point of a shoving game.
            var all = new List<Collider>();
            foreach (var kv in rig.Bodies) all.Add(kv.Value.GetComponent<Collider>());
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                    Physics.IgnoreCollision(all[i], all[j]);

            return rig;
        }

        static Rigidbody Part(Rig rig, Bone bone, Transform parent, PrimitiveType shape,
                              Vector3 pos, Vector3 sca, float mass, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = bone.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = sca;
            go.GetComponent<Renderer>().sharedMaterial = mat;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            // Continuous, because a thrown limb at speed will otherwise tunnel through the
            // floor and the character explodes off the map.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            rig.Bodies[bone] = rb;
            rig.RestLocal[bone] = go.transform.localRotation;
            return rb;
        }

        /// <summary>
        /// A joint that behaves like a muscle: it holds a pose with a spring rather than
        /// locking, so a hard enough shove wins and the recovery is visible.
        /// </summary>
        static void Join(Rig rig, Bone bone, Rigidbody connectTo, float swingLimit, float twistLimit)
        {
            Rigidbody rb = rig.Get(bone);
            if (rb == null || connectTo == null) return;

            var j = rb.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = connectTo;
            j.autoConfigureConnectedAnchor = false;
            j.anchor = Vector3.zero;
            j.connectedAnchor = connectTo.transform.InverseTransformPoint(rb.transform.position);

            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Locked;
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;

            j.lowAngularXLimit  = new SoftJointLimit { limit = -swingLimit };
            j.highAngularXLimit = new SoftJointLimit { limit =  swingLimit };
            j.angularYLimit     = new SoftJointLimit { limit =  twistLimit };
            j.angularZLimit     = new SoftJointLimit { limit =  twistLimit };

            // SLERP drive, not per-axis. Per-axis drives fight each other on a body being
            // pushed from an odd angle and produce a twitch that reads as a bug.
            j.rotationDriveMode = RotationDriveMode.Slerp;
            j.slerpDrive = new JointDrive
            {
                positionSpring = 0f,      // set by RagdollMuscles - this is THE knob
                positionDamper = 0f,
                maximumForce = float.MaxValue,
            };

            j.enablePreprocessing = false;   // preprocessing makes recovery from a big hit snap
            rig.Joints[bone] = j;
        }

        static Material MakeMat(Color c)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh);
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", 0.25f);
            return m;
        }
    }
}
