using System;

namespace Fractural.StateMachine
{
    /// <summary>
    /// Holds extra details to append to a conditon's display string.
    /// Is also used by the StateMachine editor to 
    /// add more displays to the existing one when visualizing 
    /// a remote state machine.
    /// </summary>
    public abstract class ConditionExtraDetail
    {
        public string ConditionName { get; set; }

        public virtual string DisplayString() => "";
    }

    public class ValueConditionExtraDetail : ConditionExtraDetail
    {
        public string ParameterValue { get; set; }

        public override string DisplayString() => $"({ParameterValue})";
    }
}