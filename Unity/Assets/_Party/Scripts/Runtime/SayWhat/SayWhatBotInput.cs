using System.Collections.Generic;
using UnityEngine;

namespace Party.SayWhat
{
    /// <summary>Something that performs discrete actions - a keyboard, or a bot.</summary>
    public interface IActionInput
    {
        /// <summary>The action to perform this frame, or None. Polled by PartyPlayer.</summary>
        PartyAction Poll();
    }

    /// <summary>
    /// Bot policy for "Say What He Says".
    ///
    /// THE SIGNATURE HUMILIATION IS THIS CLASS. MINIGAMES.md asks for "confidently doing
    /// the sequence from two rounds ago", and that is not something the round loop can
    /// produce on its own - it has to be a way of being wrong, not just a failure. So a
    /// confused bot picks an EARLIER sequence out of the history and performs it
    /// perfectly, at full speed, with no hesitation. It looks exactly like someone who is
    /// certain and wrong, which is the joke.
    ///
    /// Bots that recalled perfectly would be unbeatable and would give Barnaby nothing to
    /// react to - the same reasoning as RedLightBotInput. Each one gets its own recall
    /// accuracy and its own confusion streak, so they fail in different, characterful
    /// ways rather than all being equally mediocre.
    ///
    /// Bot behaviour is per-minigame (HANDOFF.md), and this is the second of twelve. That
    /// recurring cost is real and should weigh on which four get cut.
    /// </summary>
    public sealed class SayWhatBotInput : IActionInput
    {
        readonly float _accuracy;      // per-step chance of recalling correctly
        readonly float _confusionOdds; // chance of replaying an OLD sequence instead
        readonly float _pace;          // seconds between this bot's steps

        readonly List<PartyAction> _plan = new List<PartyAction>();
        int   _next;
        float _nextAt;
        int   _plannedFor = -1;   // which sequenceNumber the plan was built for

        public SayWhatBotInput()
        {
            _accuracy      = Random.Range(0.86f, 0.97f);
            _confusionOdds = Random.Range(0.08f, 0.26f);
            _pace          = Random.Range(0.28f, 0.55f);
        }

        public PartyAction Poll()
        {
            SayWhatDirector d = SayWhatDirector.Instance;
            if (d == null) return PartyAction.None;

            if (d.phase != SayWhatPhase.Perform)
            {
                // Rebuild on the next Perform. Planning during Watch would let a bot start
                // before the sequence has finished being called.
                if (_plannedFor != d.sequenceNumber) _plan.Clear();
                return PartyAction.None;
            }

            if (_plannedFor != d.sequenceNumber) BuildPlan(d);
            if (_next >= _plan.Count || Time.time < _nextAt) return PartyAction.None;

            PartyAction a = _plan[_next++];
            _nextAt = Time.time + _pace;
            return a;
        }

        void BuildPlan(SayWhatDirector d)
        {
            _plan.Clear();
            _next = 0;
            _plannedFor = d.sequenceNumber;
            _nextAt = Time.time + Random.Range(0.15f, 0.45f);   // a beat to gather themselves

            IReadOnlyList<PartyAction> truth = d.Sequence;
            if (truth == null || truth.Count == 0) return;

            // CONFIDENTLY WRONG. With an earlier sequence available, a confused bot
            // performs THAT one - correctly, and at its normal pace. No dithering.
            var history = d.History;
            if (history != null && history.Count > 1 && Random.value < _confusionOdds)
            {
                var old = history[Random.Range(0, history.Count - 1)];
                for (int i = 0; i < old.Count && i < truth.Count; i++) _plan.Add(old[i]);
                // If the old one was shorter, they simply stop - which reads as someone
                // who thinks they have finished.
                return;
            }

            for (int i = 0; i < truth.Count; i++)
            {
                if (Random.value <= _accuracy) { _plan.Add(truth[i]); continue; }

                // Misremember a step as a NEIGHBOURING one rather than at random. People
                // confuse left with right and jump with bow; they do not usually replace
                // "left" with something they were never told.
                _plan.Add(Slip(truth[i]));
            }
        }

        static PartyAction Slip(PartyAction a) => a switch
        {
            PartyAction.Left    => PartyAction.Right,
            PartyAction.Right   => PartyAction.Left,
            PartyAction.Forward => PartyAction.Back,
            PartyAction.Back    => PartyAction.Forward,
            PartyAction.Jump    => PartyAction.Bow,
            PartyAction.Bow     => PartyAction.Jump,
            _ => PartyAction.Jump,
        };
    }
}
