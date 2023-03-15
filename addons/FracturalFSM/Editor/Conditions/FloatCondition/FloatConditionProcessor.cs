using Godot;

namespace Fractural.StateMachine
{
    [Tool]
    public class FloatConditionProcessor : ConditionProcessor<FloatCondition>
    {
        public override string ConditionName => "Float";
    }
}