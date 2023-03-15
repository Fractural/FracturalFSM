
using System;
using Godot;

namespace Fractural.Flowchart
{
    [CSharpScript]
    [Tool]
    public class FlowchartNode : Container, ISelectable
    {
        // FlowchartNode has a custom style normal, focus

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

        public FlowchartNode()
        {
            FocusMode = FocusModeEnum.None; // Let Flowchart has the focus to handle guiInput
            MouseFilter = MouseFilterEnum.Pass;
        }

        public override void _Draw()
        {
            if (selected)
                DrawStyleBox(GetStylebox("focus", "FlowchartNode"), new Rect2(Vector2.Zero, RectSize));
            else
                DrawStyleBox(GetStylebox("normal", "FlowchartNode"), new Rect2(Vector2.Zero, RectSize));
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