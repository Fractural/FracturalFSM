
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;
using Fractural.GodotCodeGenerator.Attributes;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class IntegerConditionEditor : ValueConditionEditor<int>
    {
        [OnReadyGet("MarginContainer/IntegerValue")]
        private LineEdit integerValue;

        private int _oldValue = 0;


        public override void RealReady()
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
            integerValue.Text = GD.Str(newValue);
        }

        protected override void OnConditionChanged(Condition newCondition)
        {
            base.OnConditionChanged(newCondition);
            if (newCondition != null)
                integerValue.Text = TypedValueCondition.TypedValue.ToString();
        }

        private void OnIntegerValueTextEntered(string newText)
        {
            ChangeValueAction(_oldValue, int.Parse(newText));
            integerValue.ReleaseFocus();
        }

        private void OnIntegerValueFocusEntered()
        {
            SetProcessInput(true);
            _oldValue = int.Parse(integerValue.Text);
        }

        private void OnIntegerValueFocusExited()
        {
            SetProcessInput(false);
            ChangeValueAction(_oldValue, int.Parse(integerValue.Text));
        }
    }
}