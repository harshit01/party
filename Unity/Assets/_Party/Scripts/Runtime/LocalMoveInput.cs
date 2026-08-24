using UnityEngine;
using UnityEngine.InputSystem;

namespace Party
{
    /// <summary>
    /// Keyboard first (WASD / arrows), gamepad fully supported - HANDOFF.md section 1.
    /// Read directly from the devices rather than through an .inputactions asset so the
    /// milestone has no asset wiring to get wrong. Rebindable actions come with the
    /// round loop, not here.
    /// </summary>
    public sealed class LocalMoveInput : IMoveInput
    {
        public Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;

                Keyboard k = Keyboard.current;
                if (k != null)
                {
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed)  v.x -= 1f;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed)  v.y -= 1f;
                    if (k.wKey.isPressed || k.upArrowKey.isPressed)    v.y += 1f;
                }

                Gamepad g = Gamepad.current;
                if (g != null && v.sqrMagnitude < 0.01f)
                    v = g.leftStick.ReadValue();

                return Vector2.ClampMagnitude(v, 1f);
            }
        }
    }
}
