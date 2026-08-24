using UnityEngine;

namespace Party
{
    /// <summary>
    /// Where a participant's movement comes from.
    ///
    /// This interface is the whole point of the bot decision. A participant is NEVER
    /// assumed to be a network connection: a slot is driven either by a human at a
    /// keyboard or by a bot, and nothing downstream of here can tell the difference.
    /// Retrofitting that distinction later would be a second netcode-grade retrofit.
    /// </summary>
    public interface IMoveInput
    {
        /// <summary>Desired move on the XZ plane, magnitude clamped to 1.</summary>
        Vector2 Move { get; }
    }
}
