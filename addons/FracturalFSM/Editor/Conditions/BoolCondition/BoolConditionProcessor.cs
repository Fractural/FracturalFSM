using Godot;

namespace Fractural.StateMachine
{
    [Tool]
    public class BoolConditionProcessor : ConditionProcessor<BoolCondition>
    {
        public override string ConditionName => "Boolean";
    }
}