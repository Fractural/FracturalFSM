
using System;
using Godot;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Flowchart;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class StateNode : FlowchartNode
    {
        [Signal] public delegate void NewNameEntered(string newName); // Emits when focused exit || Enter pressed

        [OnReadyGet("MarginContainer/NameEdit")]
        private LineEdit nameEdit;

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
            State = CSharpScript<State>.New("State");
            this.undoRedo = undoRedo;
        }

        [OnReady]
        public void RealReady()
        {
            nameEdit.Text = "State";
            nameEdit.Connect("focus_exited", this, nameof(OnNameEditFocusExited));
            nameEdit.Connect("text_entered", this, nameof(OnNameEditTextEntered));
            SetProcessInput(false);// _Input only required when nameEdit enabled to check mouse click outside
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
            nameEdit.TryReleaseFocusWithMouseClick(inputEvent);
        }

        private void EnableNameEdit(bool enabled)
        {
            if (enabled)
            {
                SetProcessInput(true);
                nameEdit.Editable = true;
                nameEdit.SelectingEnabled = true;
                nameEdit.MouseFilter = MouseFilterEnum.Pass;
                MouseDefaultCursorShape = CursorShape.Ibeam;
                nameEdit.GrabFocus();
            }
            else
            {
                SetProcessInput(false);
                nameEdit.Editable = false;
                nameEdit.SelectingEnabled = false;
                nameEdit.MouseFilter = MouseFilterEnum.Ignore;
                MouseDefaultCursorShape = CursorShape.Arrow;
                nameEdit.ReleaseFocus();
            }
        }

        /// <summary>
        /// Reverts the editor to display the old condition name
        /// </summary>
        public void RevertStateName()
        {
            nameEdit.Text = State.Name;
        }

        private void OnStateNameChanged(string newName)
        {
            nameEdit.Text = newName;
            RectSize = new Vector2(0, RectSize.y); // Force reset horizontal size
        }

        private void OnStateChanged(State newState)
        {
            if (state != null)
            {
                state.Connect(nameof(State.NameChanged), this, nameof(OnStateNameChanged));
                if (nameEdit != null)
                    nameEdit.Text = state.Name;
            }
        }

        private void OnNameEditFocusExited()
        {
            nameEdit.Deselect();
            OnNameEditTextEntered(nameEdit.Text);
        }

        private void OnNameEditTextEntered(string newText)
        {
            EnableNameEdit(false);
            EmitSignal(nameof(NewNameEntered), newText);
        }

        internal bool TryEnableNameEdit(Vector2 mousePosition)
        {
            if (!nameEdit.GetRect().HasPoint(mousePosition))
                return false;
            EnableNameEdit(true);
            return true;
        }
    }
}