using System.Collections.Generic;

namespace Party
{
    /// <summary>
    /// What every minigame director must expose so the SPINE can drive it.
    ///
    /// WHY THIS EXISTS NOW. #11 "The Prediction" is a bet on who comes last in the NEXT
    /// minigame, so unlike #9 and #10 it is not a self-contained game - it wraps one.
    /// That makes it the first thing in the project that needs a minigame to be an
    /// interchangeable part rather than a scene with a bespoke director, and it is the
    /// same seam the round loop (HANDOFF.md §5 step 5) will need.
    ///
    /// Deliberately tiny. A minigame has to be startable, has to say when it is done, and
    /// has to leave a finishing order behind; everything else - phases, hazards,
    /// sequences, bias - stays private to the game that owns it. Twelve minigames means
    /// twelve directors, and a fat interface would be twelve times the cost.
    /// </summary>
    public interface IMinigameDirector
    {
        /// <summary>Start a round. Server only.</summary>
        void BeginRound();

        /// <summary>True once the round has resolved and placements are final.</summary>
        bool RoundIsOver { get; }

        /// <summary>Human-readable name, for the host and the HUD.</summary>
        string GameName { get; }

        /// <summary>Everyone taking part, human or bot.</summary>
        IEnumerable<PartyPlayer> Participants { get; }
    }
}
