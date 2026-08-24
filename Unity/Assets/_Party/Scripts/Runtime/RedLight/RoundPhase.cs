namespace Party.RedLight
{
    /// <summary>Phases of a Red Light, Barnaby round. Server owns transitions.</summary>
    public enum RoundPhase
    {
        Waiting,    // lobby, nobody committed yet
        Countdown,  // "here we go" - nobody may move
        Go,         // move freely
        Stop,       // freeze. Movement during Stop is what gets you called out.
        Grace,      // Stop has been called but judging has not begun yet - see below
        Finished    // someone reached the line, or everyone is out
    }
}
