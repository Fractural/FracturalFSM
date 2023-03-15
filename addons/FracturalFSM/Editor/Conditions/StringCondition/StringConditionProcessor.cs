using Godot;

namespace Fractural.StateMachine
{
    [Tool]
    public class StringConditionProcessor : ConditionProcessor<StringCondition>
    {
        public override string ConditionName => "String";
    }
}