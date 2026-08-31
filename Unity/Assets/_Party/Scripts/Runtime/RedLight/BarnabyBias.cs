using System.Collections.Generic;
using UnityEngine;

namespace Party.RedLight
{
    /// <summary>
    /// Barnaby is biased and he lies (MINIGAMES.md #9).
    ///
    /// This is the mechanic, not a bug and not flavour text. The signature of the game is
    /// "being eliminated by a host who is openly cheating against you", so the unfairness
    /// has to be real, legible, and consistent - a host who is randomly wrong is just
    /// broken, whereas a host with a grudge is a story.
    ///
    /// Two levers:
    ///   SPARE - a favourite who genuinely moved is let off
    ///   FRAME - a player he has taken against is called out despite standing still
    ///
    /// Affinity persists across rounds so "he remembers who annoyed him three rounds ago"
    /// is literally true.
    /// </summary>
    public class BarnabyBias
    {
        readonly Dictionary<uint, float> _affinity = new Dictionary<uint, float>();
        readonly System.Random _rng;

        public BarnabyBias(int seed) { _rng = new System.Random(seed); }

        public float AffinityOf(uint netId)
        {
            if (!_affinity.TryGetValue(netId, out float a))
            {
                // Everyone starts somewhere other than neutral, so he has opinions from
                // round one rather than warming up into them.
                a = (float)(_rng.NextDouble() * 2.0 - 1.0);
                _affinity[netId] = a;
            }
            return a;
        }

        /// <summary>He liked that. Or he didn't.</summary>
        public void Nudge(uint netId, float delta)
        {
            _affinity[netId] = Mathf.Clamp(AffinityOf(netId) + delta, -1f, 1f);
        }

        /// <summary>A favourite who moved may be let off entirely.</summary>
        public bool WouldSpare(uint netId)
        {
            float a = AffinityOf(netId);
            if (a <= 0.25f) return false;
            return _rng.NextDouble() < a * 0.8f;   // up to 80% for a real pet
        }

        /// <summary>Someone he has taken against may be called out for nothing.</summary>
        public bool WouldFrame(uint netId)
        {
            float a = AffinityOf(netId);
            if (a >= -0.35f) return false;
            return _rng.NextDouble() < (-a) * 0.35f;  // rarer than sparing - it must sting, not exhaust
        }

        /// <summary>
        /// Opinions fade a little each round.
        ///
        /// A safety valve, not a mechanic. Affinity used to move in one direction only
        /// (-0.1, on every call-out) so a session was a ratchet: the spare pool could
        /// only shrink and the grudge pool could only grow. Measured over six rounds,
        /// one player was framed every single round and slid to the -1.0 floor while
        /// nobody was ever spared once. Keep this GENTLE - the nudges are supposed to
        /// be what moves his mind, and too much fade here flattens everyone to neutral
        /// and he stops having opinions at all.
        /// </summary>
        public void Decay(float fraction)
        {
            var keys = new List<uint>(_affinity.Keys);
            foreach (uint k in keys)
                _affinity[k] = Mathf.Lerp(_affinity[k], 0f, fraction);
        }

        public string Describe(uint netId)
        {
            float a = AffinityOf(netId);
            if (a > 0.5f)  return "pet";
            if (a > 0.15f) return "tolerated";
            if (a > -0.35f) return "neutral";
            return "grudge";
        }
    }
}
