
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Godot;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class StringConditionEditor : ValueConditionEditor<string>
    {
        [OnReadyGet("MarginContainer/StringValue")]
        public LineEdit stringValue;

        protected override string TypeEditorIcon => "String";
        private string oldValue = "";

        [OnReady]
        public new void RealReady()
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

        protected override void InitializeCondition()
        {
            base.InitializeCondition();
            stringValue.Text = TypedValueCondition.TypedValue;
        }

        private void OnStringValueTextEntered(string newText)
        {
            ChangeValueAction(oldValue, newText);
            stringValue.ReleaseFocus();
        }

        private void OnStringValueFocusEntered()
        {
            SetProcessInput(true);
            oldValue = stringValue.Text;
        }

        private void OnStringValueFocusExited()
        {
            SetProcessInput(false);
            ChangeValueAction(oldValue, stringValue.Text);
        }
    }
}