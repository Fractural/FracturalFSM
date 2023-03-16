
using System;
using Godot;
using Fractural.GodotCodeGenerator.Attributes;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class FloatConditionEditor : ValueConditionEditor<float>
    {
        [OnReadyGet("MarginContainer/FloatValue")]
        private LineEdit floatValue;

        protected override string TypeEditorIcon => "float";
        private float oldValue = 0f;

        [OnReady]
        public new void RealReady()
        {
            floatValue.Connect("text_entered", this, nameof(OnFloatValueTextEntered));
            floatValue.Connect("focus_entered", this, nameof(OnFloatValueFocusEntered));
            floatValue.Connect("focus_exited", this, nameof(OnFloatValueFocusExited));
            SetProcessInput(false);
        }

        public override void _Input(InputEvent inputEvent)
        {
            base._Input(inputEvent);
            floatValue.TryReleaseFocusWithMouseClick(inputEvent);
        }

        protected override void OnTypedValueChanged(float newValue)
        {
            floatValue.Text = Mathf.Stepify(newValue, 0.01f).ToString().PadDecimals(2);
        }

        protected override void InitializeCondition()
        {
            base.InitializeCondition();
            floatValue.Text = TypedValueCondition.TypedValue.ToString(2);
        }

        private void OnFloatValueTextEntered(string newText)
        {
            if (float.TryParse(newText, out float result))
                ChangeValueAction(oldValue, result);
            else
                floatValue.Text = oldValue.ToString();
            floatValue.ReleaseFocus();
        }

        private void OnFloatValueFocusEntered()
        {
            SetProcessInput(true);
            oldValue = float.Parse(floatValue.Text);
        }

        private void OnFloatValueFocusExited()
        {
            SetProcessInput(false);
            OnFloatValueTextEntered(floatValue.Text);
        }
    }
}