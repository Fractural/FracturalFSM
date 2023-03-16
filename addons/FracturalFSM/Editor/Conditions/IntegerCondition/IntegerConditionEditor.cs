
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;
using Fractural.GodotCodeGenerator.Attributes;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class IntegerConditionEditor : ValueConditionEditor<int>
    {
        [OnReadyGet("MarginContainer/IntegerValue")]
        private LineEdit integerValue;

        protected override string TypeEditorIcon => "int";
        private int oldValue = 0;

        [OnReady]
        public new void RealReady()
        {
            integerValue.Connect("text_entered", this, nameof(OnIntegerValueTextEntered));
            integerValue.Connect("focus_entered", this, nameof(OnIntegerValueFocusEntered));
            integerValue.Connect("focus_exited", this, nameof(OnIntegerValueFocusExited));
            SetProcessInput(false);
        }

        public override void _Input(InputEvent inputEvent)
        {
            base._Input(inputEvent);
            integerValue.TryReleaseFocusWithMouseClick(inputEvent);
        }

        protected override void OnTypedValueChanged(int newValue)
        {
            integerValue.Text = newValue.ToString();
        }

        protected override void InitializeCondition()
        {
            base.InitializeCondition();
            integerValue.Text = TypedValueCondition.TypedValue.ToString();
        }

        private void OnIntegerValueTextEntered(string newText)
        {
            if (int.TryParse(newText, out int result))
                ChangeValueAction(oldValue, result);
            else
                integerValue.Text = oldValue.ToString();
            integerValue.ReleaseFocus();
        }

        private void OnIntegerValueFocusEntered()
        {
            SetProcessInput(true);
            oldValue = int.Parse(integerValue.Text);
        }

        private void OnIntegerValueFocusExited()
        {
            SetProcessInput(false);
            OnIntegerValueTextEntered(integerValue.Text);
        }
    }
}