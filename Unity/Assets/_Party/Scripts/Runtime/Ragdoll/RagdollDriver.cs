using UnityEngine;
using UnityEngine.InputSystem;

namespace Party.Ragdoll
{
    /// <summary>
    /// Assembles a ragdoll and drives it - keyboard, or a simple bot for the test scene.
    ///
    /// TWO ACTION BUTTONS, which is a hard rule (HANDOFF.md §1) and the one place this cannot
    /// match Gang Beasts, which uses four or more. Here: SPACE jumps, SHIFT grabs and holds.
    /// Throwing falls out of releasing SHIFT while moving, so the verb costs no button.
    /// </summary>
    [RequireComponent(typeof(RagdollMuscles))]
    public class RagdollDriver : MonoBehaviour
    {
        public Color livery = new Color(0.92f, 0.28f, 0.45f);
        public float scale = 1f;
        public float jumpImpulse = 5.2f;
        [Tooltip("No keyboard: wander and grab at random. Used for the test scene's extras.")]
        public bool bot;

        RagdollMuscles _m;
        RagdollGrab _grab;
        float _nextBotThink;
        Vector3 _botDir;
        bool _botGrab;

        void Start()
        {
            var rig = RagdollBuilder.Build(transform, livery, scale);
            _m = GetComponent<RagdollMuscles>();
            _m.Bind(rig);

            _grab = gameObject.AddComponent<RagdollGrab>();
            _grab.Bind(_m);
        }

        void Update()
        {
            if (_m == null || _m.Rig == null) return;

            if (bot) { BotThink(); return; }

            Keyboard k = Keyboard.current;
            if (k == null) return;

            Vector3 dir = Vector3.zero;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed)  dir.x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) dir.x += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed)  dir.z -= 1f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed)    dir.z += 1f;
            _m.MoveInput = dir;

            _grab.SetGrab(k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);

            if (k.spaceKey.wasPressedThisFrame) Jump();
            // Deliberate ragdoll, for feel-testing and for laughing at.
            if (k.rKey.wasPressedThisFrame) _m.Limp(1.6f);
        }

        void BotThink()
        {
            if (Time.time >= _nextBotThink)
            {
                _nextBotThink = Time.time + Random.Range(2.2f, 4.5f);
                _botDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                _botGrab = Random.value < 0.35f;
                if (Random.value < 0.15f) Jump();
            }
            _m.MoveInput = _botDir;
            _grab.SetGrab(_botGrab);
        }

        void Jump()
        {
            if (_m.IsDown) return;
            Rigidbody pelvis = _m.Rig.Get(Bone.Pelvis);
            if (pelvis == null) return;
            // Only off the ground, or you can swim upward by mashing it.
            if (!Physics.Raycast(pelvis.position, Vector3.down, 1.1f)) return;
            pelvis.AddForce(Vector3.up * jumpImpulse, ForceMode.VelocityChange);
        }
    }
}
