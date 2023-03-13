
using System;
using Godot;
using Fractural.GodotCodeGenerator.Attributes;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class FloatConditionEditor : ValueConditionEditor<float>
    {
        [OnReadyGet("MarginContainer/FloatValue")]
        private LineEdit floatValue;

        private float _oldValue = 0f;


        public override void RealReady()
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

        protected override void OnConditionChanged(Condition newCondition)
        {
            base.OnConditionChanged(newCondition);
            if (newCondition != null)
                floatValue.Text = TypedValueCondition.TypedValue.ToString(2);
        }

        private void OnFloatValueTextEntered(string newText)
        {
            ChangeValueAction(_oldValue, float.Parse(newText));
            floatValue.ReleaseFocus();
        }

        private void OnFloatValueFocusEntered()
        {
            SetProcessInput(true);
            _oldValue = float.Parse(floatValue.Text);
        }

        private void OnFloatValueFocusExited()
        {
            SetProcessInput(false);
            ChangeValueAction(_oldValue, float.Parse(floatValue.Text));
        }
    }
}