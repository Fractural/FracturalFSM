using Godot;

namespace Fractural.StateMachine
{
    [Tool]
    public class TriggerConditionProcessor : ConditionProcessor<TriggerCondition>
    {
        public override string ConditionName => "Trigger";
    }
}