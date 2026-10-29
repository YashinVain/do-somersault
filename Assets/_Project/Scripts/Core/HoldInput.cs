using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JumpGame
{
    // Single "hold" control: mouse button, any touch, or space bar.
    public static class HoldInput
    {
        public static bool IsHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
                if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) return true;
                if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) return true;
                return false;
#else
                return Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space) || Input.touchCount > 0;
#endif
            }
        }
    }
}
