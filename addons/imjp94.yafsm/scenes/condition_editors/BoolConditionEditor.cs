
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;
using Fractural.GodotCodeGenerator.Attributes;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class BoolConditionEditor : ValueConditionEditor<bool>
    {
        [OnReadyGet("MarginContainer/BooleanValue")]
        public CheckButton booleanValue;

        [OnReady]
        public void RealReady()
        {
            booleanValue.Connect("pressed", this, nameof(OnBooleanValuePressed));
        }

        protected override void OnTypedValueChanged(bool newValue)
        {
            if (booleanValue.Pressed != newValue)
                booleanValue.Pressed = newValue;
        }

        protected override void OnConditionChanged(Condition newCondition)
        {
            base.OnConditionChanged(newCondition);
            if (newCondition != null)
                booleanValue.Pressed = TypedValueCondition.TypedValue;
        }

        private void OnBooleanValuePressed()
        {
            ChangeValueAction(TypedValueCondition.Value, booleanValue.Pressed);
        }
    }
}