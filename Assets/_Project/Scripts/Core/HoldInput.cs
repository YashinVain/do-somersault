using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JumpGame
{
    // Single "hold" control: mouse button, any touch, or space bar.
    // A press that starts on a UI button belongs to the button and is ignored until released.
    public static class HoldInput
    {
        static int frame = -1;
        static bool held, rawBefore, blocked;

        public static bool IsHeld
        {
            get
            {
                if (frame != Time.frameCount)
                {
                    frame = Time.frameCount;
                    Refresh();
                }
                return held;
            }
        }

        static void Refresh()
        {
            bool key = KeyHeld();
            bool pointer = PointerHeld();

            if (pointer && !rawBefore) blocked = PointerOverUi();
            if (!pointer) blocked = false;
            rawBefore = pointer;

            held = key || (pointer && !blocked);
        }

        static bool PointerOverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
#if !ENABLE_INPUT_SYSTEM
            if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
#endif
            return es.IsPointerOverGameObject();
        }

        static bool KeyHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }

        static bool PointerHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
            return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;
#else
            return Input.GetMouseButton(0) || Input.touchCount > 0;
#endif
        }
    }
}
