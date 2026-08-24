using UnityEngine;

namespace Party.RedLight
{
    /// <summary>
    /// Instant, local GO/STOP calls.
    ///
    /// These NEVER come from the model. The timing of a call is the game; an LLM round
    /// trip in this path would be dead air in the one place a party game cannot afford it
    /// (HANDOFF.md section 4: five seconds of silence with four people staring at a screen
    /// kills pace). The model writes commentary around the round, not the round itself.
    /// </summary>
    public static class HostPhrases
    {
        static readonly string[] Gos =
        {
            "GO!", "Off you go!", "RUN!", "Move!", "Now!", "Go on then!",
        };

        static readonly string[] Stops =
        {
            "STOP!", "FREEZE!", "Don't you dare!", "HOLD IT!", "STILL!", "AND... stop.",
        };

        public static string Go()   => Gos[Random.Range(0, Gos.Length)];
        public static string Stop() => Stops[Random.Range(0, Stops.Length)];
    }
}
