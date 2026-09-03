using UnityEngine;

namespace Party.Plank
{
    /// <summary>
    /// Bot policy for Plank Panic: go at the nearest opponent and grab.
    ///
    /// Bot behaviour is per-minigame (HANDOFF.md) and this is the third of twelve - the
    /// recurring cost the doc warns about, and it should weigh on which four get cut.
    ///
    /// The policy is deliberately CRUDE, and that is the design rather than laziness. A bot
    /// that pathed carefully along a narrow beam would be better at the game and much worse
    /// to play against: the signature humiliation here is "falling off in the first two
    /// seconds having touched nobody", and a bot that charges at people on a plank produces
    /// that for itself several times a round.
    /// </summary>
    public sealed class PlankBotInput : IMoveInput
    {
        readonly Transform _self;
        readonly float _aggression;   // how eagerly it closes
        readonly float _caution;      // how much it corrects back toward the centre line

        float _nextGrabRoll;
        bool  _grabbing;

        public PlankBotInput(Transform self)
        {
            _self = self;
            _aggression = Random.Range(0.6f, 1f);
            _caution    = Random.Range(0.2f, 0.8f);
        }

        /// <summary>Read by PartyPlayer to decide whether this bot is holding the grab.</summary>
        public bool WantsGrab => _grabbing;

        public Vector2 Move
        {
            get
            {
                if (_self == null) return Vector2.zero;

                PartyPlayer nearest = null;
                float best = float.MaxValue;
                foreach (PartyPlayer p in PlankDirector.Players())
                {
                    if (p.eliminated || p.finished) continue;
                    if (p.transform == _self || p.transform.IsChildOf(_self)) continue;
                    float d = Vector3.Distance(p.transform.position, _self.position);
                    if (d < best) { best = d; nearest = p; }
                }

                // Grab when close enough to have a chance, and hold it for a beat rather
                // than flickering - a grab that turns on and off every frame never forms a
                // joint at all.
                if (Time.time >= _nextGrabRoll)
                {
                    _nextGrabRoll = Time.time + Random.Range(0.4f, 1.1f);
                    _grabbing = nearest != null && best < 2.2f && Random.value < 0.8f;
                }

                Vector3 dir = Vector3.zero;
                if (nearest != null)
                {
                    dir = nearest.transform.position - _self.position;
                    dir.y = 0f;
                    dir = dir.normalized * _aggression;
                }

                // Correct back toward the centre line of the plank. Without this they walk
                // off the side immediately and the round is over in four seconds - funny
                // once, then simply broken.
                dir.x -= Mathf.Clamp(_self.position.x, -1f, 1f) * _caution;

                return Vector2.ClampMagnitude(new Vector2(dir.x, dir.z), 1f);
            }
        }
    }
}
