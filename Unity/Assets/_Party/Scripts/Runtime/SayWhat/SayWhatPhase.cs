namespace Party.SayWhat
{
    /// <summary>
    /// The six actions Barnaby is allowed to ask for.
    ///
    /// TWO BUTTONS MAXIMUM is a hard rule (MINIGAMES.md, HANDOFF.md §1) - "playable by
    /// all" means a parent can join without a tutorial. So the vocabulary is the four
    /// directions plus the two action buttons the game already uses everywhere else, and
    /// nothing needs teaching beyond "do what he says".
    ///
    /// Every one of these also has to be READABLE AT A GLANCE from across a room, which
    /// rules out anything that is not a whole-body movement: the Filament visibly steps,
    /// jumps or bows, so you can see someone get it wrong without reading their screen.
    /// </summary>
    public enum PartyAction : byte
    {
        None    = 0,
        Left    = 1,   // A / left stick
        Right   = 2,   // D
        Forward = 3,   // W
        Back    = 4,   // S
        Jump    = 5,   // Space   - action button 1
        Bow     = 6,   // Shift   - action button 2
    }

    /// <summary>
    /// "Say What He Says" - MINIGAMES.md #10, Family D.
    ///
    /// Barnaby calls an escalating sequence and you perform it back. The signature
    /// humiliation the design asks for is "confidently doing the sequence from two rounds
    /// ago", so the game is about MEMORY under pressure rather than reflex - which makes
    /// it genuinely different from Red Light (#9) despite sharing the family, the host and
    /// the bias engine.
    /// </summary>
    public enum SayWhatPhase : byte
    {
        Waiting   = 0,
        Countdown = 1,
        /// <summary>Barnaby calls the sequence. Inputs here do not count.</summary>
        Watch     = 2,
        /// <summary>Your turn. Every action is recorded in order.</summary>
        Perform   = 3,
        /// <summary>Server compares, and Barnaby decides who he believes.</summary>
        Judge     = 4,
        Finished  = 5,
    }
}
