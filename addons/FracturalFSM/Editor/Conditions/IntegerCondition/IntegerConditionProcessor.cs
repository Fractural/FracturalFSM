using Godot;

namespace Fractural.StateMachine
{
    [Tool]
    public class IntegerConditionProcessor : ConditionProcessor<IntegerCondition>
    {
        public override string ConditionName => "Integer";
    }
}