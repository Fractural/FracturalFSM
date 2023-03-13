
using System;
using Godot;

namespace Fractural.FlowChart
{
    [Tool]
    public class FlowChartNode : Container, ISelectable
    {
        // FlowChartNode has a custom style normal, focus

        private bool selected = false;
        public bool Selected
        {
            get => selected;
            set
            {
                if (selected != value)
                {
                    selected = value;
                    Update();
                }
            }
        }

        public FlowChartNode()
        {
            FocusMode = FocusModeEnum.None; // Let FlowChart has the focus to handle guiInput
            MouseFilter = MouseFilterEnum.Pass;
        }

        public override void _Draw()
        {
            if (selected)
                DrawStyleBox(GetStylebox("focus", "FlowChartNode"), new Rect2(Vector2.Zero, RectSize));
            else
                DrawStyleBox(GetStylebox("normal", "FlowChartNode"), new Rect2(Vector2.Zero, RectSize));
        }

        public override void _Notification(int what)
        {
            if (what == NotificationSortChildren)
            {
                foreach (Node child in GetChildren())
                    if (child is Control control)
                        FitChildInRect(control, new Rect2(Vector2.Zero, RectSize));
            }
        }

        public override Vector2 _GetMinimumSize() => new Vector2(50, 50);
    }
}