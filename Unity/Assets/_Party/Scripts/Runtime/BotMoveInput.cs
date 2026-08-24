using UnityEngine;

namespace Party
{
    /// <summary>
    /// Milestone-grade bot: wanders to a point, picks another. Deliberately dumb.
    ///
    /// Bot behaviour is per-minigame (see HANDOFF.md) and twelve minigames means twelve
    /// policies. This one exists only to prove a bot-driven capsule is indistinguishable
    /// from a human-driven one on the wire.
    ///
    /// Runs on the host only - bots never exist on a client.
    /// </summary>
    public sealed class BotMoveInput : IMoveInput
    {
        readonly Transform _self;
        readonly float _range;
        Vector3 _target;
        float _repathAt;

        public BotMoveInput(Transform self, float range = 8f)
        {
            _self = self;
            _range = range;
            PickTarget();
        }

        void PickTarget()
        {
            Vector2 p = Random.insideUnitCircle * _range;
            _target = new Vector3(p.x, 0f, p.y);
            _repathAt = Time.time + Random.Range(1.5f, 4f);
        }

        public Vector2 Move
        {
            get
            {
                if (_self == null) return Vector2.zero;

                Vector3 d = _target - _self.position;
                d.y = 0f;

                if (d.sqrMagnitude < 0.75f || Time.time >= _repathAt) PickTarget();

                return Vector2.ClampMagnitude(new Vector2(d.x, d.z), 1f);
            }
        }
    }
}
