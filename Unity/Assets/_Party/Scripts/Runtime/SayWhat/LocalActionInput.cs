using UnityEngine;
using UnityEngine.InputSystem;

namespace Party.SayWhat
{
    /// <summary>
    /// Keyboard and gamepad, read straight from the devices like LocalMoveInput does, so
    /// there is no .inputactions asset wiring to get wrong.
    ///
    /// EDGE-TRIGGERED, NOT HELD. A held key must produce exactly one step: this is a
    /// sequence of discrete instructions, and reading `isPressed` would submit thirty
    /// LEFTs in half a second and end your night instantly.
    ///
    /// One action per frame, in a fixed priority order. Pressing two keys on the same
    /// frame is a player being imprecise, and taking the first is kinder - and more
    /// predictable - than taking both and failing them.
    /// </summary>
    public sealed class LocalActionInput : IActionInput
    {
        public PartyAction Poll()
        {
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.spaceKey.wasPressedThisFrame)                                    return PartyAction.Jump;
                if (k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame)
                                                                                       return PartyAction.Bow;
                if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame)  return PartyAction.Left;
                if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) return PartyAction.Right;
                if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame)    return PartyAction.Forward;
                if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame)  return PartyAction.Back;
            }

            Gamepad g = Gamepad.current;
            if (g != null)
            {
                if (g.buttonSouth.wasPressedThisFrame) return PartyAction.Jump;
                if (g.buttonEast.wasPressedThisFrame)  return PartyAction.Bow;
                if (g.dpad.left.wasPressedThisFrame)   return PartyAction.Left;
                if (g.dpad.right.wasPressedThisFrame)  return PartyAction.Right;
                if (g.dpad.up.wasPressedThisFrame)     return PartyAction.Forward;
                if (g.dpad.down.wasPressedThisFrame)   return PartyAction.Back;
            }

            return PartyAction.None;
        }
    }
}
