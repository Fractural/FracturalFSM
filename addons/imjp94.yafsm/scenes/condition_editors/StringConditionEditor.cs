
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Godot;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class StringConditionEditor : ValueConditionEditor<string>
    {
        [OnReadyGet("MarginContainer/StringValue")]
        public LineEdit stringValue;

        private string _oldValue = "";

        public override void RealReady()
        {
            stringValue.Connect("text_entered", this, nameof(OnStringValueTextEntered));
            stringValue.Connect("focus_entered", this, nameof(OnStringValueFocusEntered));
            stringValue.Connect("focus_exited", this, nameof(OnStringValueFocusExited));
            SetProcessInput(false);
        }

        public override void _Input(InputEvent inputEvent)
        {
            base._Input(inputEvent);
            stringValue.TryReleaseFocusWithMouseClick(inputEvent);
        }

        protected override void OnTypedValueChanged(string newValue)
        {
            stringValue.Text = newValue;
        }

        protected override void OnConditionChanged(Condition newCondition)
        {
            base.OnConditionChanged(newCondition);
            if (newCondition != null)
                stringValue.Text = TypedValueCondition.TypedValue;
        }

        private void OnStringValueTextEntered(string newText)
        {
            ChangeValueAction(_oldValue, newText);
            stringValue.ReleaseFocus();
        }

        private void OnStringValueFocusEntered()
        {
            SetProcessInput(true);
            _oldValue = stringValue.Text;
        }

        private void OnStringValueFocusExited()
        {
            SetProcessInput(false);
            ChangeValueAction(_oldValue, stringValue.Text);

        }
    }
}