
using System;
using Godot;
using Fractural.GodotCodeGenerator.Attributes;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class StateNode : FlowChartNode
    {
        [Signal] public delegate void NameEditEntered(string newName); // Emits when focused exit || Enter pressed

        [OnReadyGet("MarginContainer/NameEdit")]
        public LineEdit NameEdit { get; set; }

        private State state;
        public State State
        {
            get => state;
            set
            {
                if (state != value)
                {
                    state = value;
                    OnStateChanged(value);
                }
            }
        }

        private UndoRedo undoRedo;

        public void Construct(UndoRedo undoRedo)
        {
            State = new State("State");
            this.undoRedo = undoRedo;
        }

        [OnReady]
        public void RealReady()
        {
            NameEdit.Text = "State";
            NameEdit.Connect("focus_exited", this, nameof(OnNameEditFocusExited));
            NameEdit.Connect("text_entered", this, nameof(OnNameEditTextEntered));
            SetProcessInput(false);// _Input only required when NameEdit enabled to check mouse click outside
        }

        public override void _Draw()
        {
            if (state is StateMachine)
            {
                if (Selected)
                    DrawStyleBox(GetStylebox("nested_focus", "StateNode"), new Rect2(Vector2.Zero, RectSize));
                else
                    DrawStyleBox(GetStylebox("nested_normal", "StateNode"), new Rect2(Vector2.Zero, RectSize));
            }
            else
                base._Draw();
        }

        public override void _Input(InputEvent inputEvent)
        {
            NameEdit.TryReleaseFocusWithMouseClick(inputEvent);
        }

        public void EnableNameEdit(bool enabled)
        {
            if (enabled)
            {
                SetProcessInput(true);
                NameEdit.Editable = true;
                NameEdit.SelectingEnabled = true;
                NameEdit.MouseFilter = MouseFilterEnum.Pass;
                MouseDefaultCursorShape = CursorShape.Ibeam;
                NameEdit.GrabFocus();
            }
            else
            {
                SetProcessInput(false);
                NameEdit.Editable = false;
                NameEdit.SelectingEnabled = false;
                NameEdit.MouseFilter = MouseFilterEnum.Ignore;
                MouseDefaultCursorShape = CursorShape.Arrow;
                NameEdit.ReleaseFocus();

            }
        }

        private void OnStateNameChanged(string newName)
        {
            NameEdit.Text = newName;
            RectSize = new Vector2(0, RectSize.y); // Force reset horizontal size
        }

        private void OnStateChanged(State newState)
        {
            if (state != null)
            {
                state.Connect(nameof(State.NameChanged), this, nameof(OnStateNameChanged));
                if (NameEdit != null)
                    NameEdit.Text = state.Name;
            }
        }

        private void OnNameEditFocusExited()
        {
            EnableNameEdit(false);
            NameEdit.Deselect();
            EmitSignal(nameof(NameEditEntered), NameEdit.Text);

        }

        private void OnNameEditTextEntered(string newText)
        {
            EnableNameEdit(false);
            EmitSignal(nameof(NameEditEntered), newText);
        }
    }
}