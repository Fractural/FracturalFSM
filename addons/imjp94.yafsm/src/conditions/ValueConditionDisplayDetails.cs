using System;

namespace GodotRollbackNetcode.StateMachine
{
    /// <summary>
    /// Holds the details needed to display a conditon
    /// Is also used by the StateMachine editor to overwrite the display of a condition
    /// when visualizing a remote state machine.
    /// </summary>
    public class ConditionDisplayDetails
    {
        public string Name { get; set; }

        public static ConditionDisplayDetails From(ValueCondition details) => new ConditionDisplayDetails
        {
            Name = details.Name
        };

        public virtual string DisplayString() => Name;

        /// <summary>
        /// Copies non-null values in <paramref name="other"/> over to this CodnitionDetails instance, 
        /// overwriting what was originally set.
        /// </summary>
        /// <param name="other"></param>
        public virtual void CopySetValuesFrom(ConditionDisplayDetails other)
        {
            if (other.Name != null)
                Name = other.Name;
        }
    }

    public class ValueConditionDisplayDetails : ConditionDisplayDetails
    {
        public string ComparationSymbol { get; set; }
        public string Value { get; set; }

        public static new ValueConditionDisplayDetails From(ValueCondition details) => new ValueConditionDisplayDetails
        {
            Name = details.Name,
            ComparationSymbol = ValueCondition.ComparationSymbols[(int)details.Comparation],
            Value = details.GetValueString()
        };

        public override string DisplayString() => $"{Name} {ComparationSymbol} {Value}";
        public override void CopySetValuesFrom(ConditionDisplayDetails other)
        {
            base.CopySetValuesFrom(other);
            if (!(other is ValueConditionDisplayDetails casted))
                return;
            if (casted.ComparationSymbol != null)
                ComparationSymbol = casted.ComparationSymbol;
            if (casted.Value != null)
                Value = casted.ComparationSymbol;
        }
    }
}