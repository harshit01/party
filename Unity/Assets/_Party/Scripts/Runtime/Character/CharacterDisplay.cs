using UnityEngine;

namespace Party.Character
{
    /// <summary>A Filament on a podium, turning slowly. Menu only, no netcode.</summary>
    public class CharacterDisplay : MonoBehaviour
    {
        public float spinSpeed = 16f;
        public float bobHeight = 0.05f;
        public float bobSpeed  = 1.5f;

        FilamentRig _rig;
        float _baseY;

        void Start() { _baseY = transform.localPosition.y; Rebuild(); }

        public void Rebuild()
        {
            CharacterLook.Build(transform, PlayerProfile.Look, out _, out _rig);
            if (_rig != null) { _rig.standing = 0.6f; _rig.mood = FilamentMood.Idle; }
        }

        void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            Vector3 p = transform.localPosition;
            p.y = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = p;
        }
    }
}
