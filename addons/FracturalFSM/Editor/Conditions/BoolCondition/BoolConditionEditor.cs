
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;
using Fractural.GodotCodeGenerator.Attributes;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class BoolConditionEditor : ValueConditionEditor<bool>
    {
        [OnReadyGet("MarginContainer/BooleanValue")]
        private CheckButton booleanValue;

        protected override string ConditionPrefixEditorIcon => "bool";

        [OnReady]
        public new void RealReady()
        {
            booleanValue.Connect("pressed", this, nameof(OnBooleanValuePressed));
        }

        protected override void OnTypedValueChanged(bool newValue)
        {
            if (booleanValue.Pressed != newValue)
                booleanValue.Pressed = newValue;
        }

        protected override void InitializeCondition()
        {
            base.InitializeCondition();
            booleanValue.Pressed = TypedValueCondition.TypedValue;
        }

        private void OnBooleanValuePressed()
        {
            ChangeValueAction(TypedValueCondition.Value, booleanValue.Pressed);
        }
    }
}