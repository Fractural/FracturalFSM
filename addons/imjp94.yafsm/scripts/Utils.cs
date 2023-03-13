
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

namespace GodotRollbackNetcode.StateMachine
{
    public static class Utils
    {
        /// <summary>
        /// Converts the float into a string with a specified number of decimal places.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="decimalPlaces"></param>
        /// <returns></returns>
        public static string ToString(this float value, int decimalPlaces = 2)
        {
            return Mathf.Stepify(value, 0.01f).ToString().PadDecimals(decimalPlaces);
        }

        /// <summary>
        /// If the inputEvent is a mouse button click outside of this control, we release focus from this control.
        /// </summary>
        /// <param name="control"></param>
        /// <param name="inputEvent"></param>
        /// <returns></returns>
        public static bool TryReleaseFocusWithMouseClick(this Control control, InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseButton mouseButtonEvent && control.GetFocusOwner() == control && mouseButtonEvent.Pressed)
            {
                var localEvent = control.MakeInputLocal(inputEvent) as InputEventMouseButton;
                if (!control.GetRect().HasPoint(localEvent.Position))
                {
                    control.ReleaseFocus();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Position Popup near to its target while within window, 
        /// solution from ColorPickerButton source code (https://github.com/godotengine/godot/blob/6d8c14f849376905e1577f9fc3f9512bcffb1e3c/scene/gui/color_picker.cpp#L878)
        /// </summary>
        /// <param name="popup"></param>
        /// <param name="target"></param>
        public static void PopupOnTarget(Popup popup, Control target)
        {
            popup.SetAsMinsize();
            Rect2 usableRect = new Rect2(Vector2.Zero, OS.GetRealWindowSize());
            Rect2 cpRect = new Rect2(Vector2.Zero, popup.RectSize);
            for (int i = 0; i < 4; i++)
            {
                var cpRectPos = cpRect.Position;
                if (i > 1)
                    cpRectPos.y = target.RectGlobalPosition.y - cpRect.Size.y;
                else
                    cpRectPos.y = target.RectGlobalPosition.y + target.RectSize.y;

                if ((i & 1) > 0)
                    cpRectPos.x = target.RectGlobalPosition.x;
                else
                    cpRectPos.x = target.RectGlobalPosition.x - Mathf.Max(0, cpRect.Size.x - target.RectSize.x);

                cpRect.Position = cpRectPos;
                if (usableRect.Encloses(cpRect))
                    break;
            }
            popup.SetPosition(cpRect.Position);
            popup.Popup_();
        }

        public static Color GetComplementaryColor(Color color)
        {
            var r = Mathf.Max(color.r, Mathf.Max(color.b, color.g)) + Mathf.Min(color.r, Mathf.Min(color.b, color.g)) - color.r;
            var g = Mathf.Max(color.r, Mathf.Max(color.b, color.g)) + Mathf.Min(color.r, Mathf.Min(color.b, color.g)) - color.g;
            var b = Mathf.Max(color.r, Mathf.Max(color.b, color.g)) + Mathf.Min(color.r, Mathf.Min(color.b, color.g)) - color.b;
            return new Color(r, g, b);
        }
    }
}