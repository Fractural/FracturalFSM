
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;
using Fractural.Utils;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public class Transition : Resource, IComparable<Transition>
    {
        [Signal] public delegate void ConditionAdded(Condition condition);
        [Signal] public delegate void ConditionRemoved(Condition condition);

        /// <summary>
        /// // Name of state transiting from
        /// </summary>
        [Export] public string From { get; set; }
        /// <summary>
        /// Name of state transiting to
        /// </summary>
        [Export] public string To { get; set; }
        /// <summary>
        /// Conditions to transit successfuly, keyed by Condition.name
        /// </summary>
        [Export] public Dictionary Conditions { get; private set; }
        /// <summary>
        /// Higher the number, higher the priority
        /// </summary>
        [Export] public int priority = 0;

        // TODO: Replace Godot Dictionaries with C# Generic Dictionaries


        public void _Init(string from = "", string to = "", Dictionary conditions = null)
        {
            From = from;
            To = to;
            Conditions = conditions;
        }

        /// <summary>
        /// Attempt to transit with parameters given, return name of next succeeded ? state : null
        /// </summary>
        /// <param name="transitParams"></param>
        /// <param name="localParams"></param>
        /// <returns></returns>
        public string Transit(Dictionary transitParams = null, Dictionary localParams = null)
        {
            if (transitParams == null) transitParams = new Dictionary();
            if (localParams == null) localParams = new Dictionary();

            if (Conditions.Count > 0)
            {
                // Make sure we pass every condition
                foreach (Condition condition in Conditions.Values)
                {
                    var hasParam = transitParams.Contains(condition.Name);
                    var hasLocalParam = localParams.Contains(condition.Name);
                    if (hasParam || hasLocalParam)
                    {
                        // localParams > params
                        var value = hasLocalParam ? localParams.Contains(condition.Name) : transitParams.Get<object>(condition.Name);
                        // null value is treated as trigger
                        if (!(value == null || (condition is ValueCondition valueCondition && valueCondition.Compare(value))))
                            return null;
                    }
                    else
                        return null; // There are no params, bail
                }
            }
            return To;
        }

        /// <summary>
        /// Add condition, return true if succeeded
        /// </summary>
        /// <param name="condition"></param>
        /// <returns></returns>
        public bool AddCondition(Condition condition)
        {
            if (Conditions.Contains(condition.Name))
            {
                return false;
            }
            Conditions[condition.Name] = condition;
            EmitSignal(nameof(ConditionAdded), condition);
            return true;
        }

        /// <summary>
        /// Remove condition by name of condition
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool RemoveCondition(string name)
        {
            var condition = Conditions.Get<Condition>(name);
            if (condition != null)
            {
                Conditions.Remove(name);
                EmitSignal("condition_removed", condition);
                return true;
            }
            return false;

        }

        /// <summary>
        /// Change condition name, return true if succeeded
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public bool ChangeConditionName(string from, string to)
        {
            if (!Conditions.Contains(from) || Conditions.Contains(to))
            {
                return false;
            }
            var condition = Conditions.Get<Condition>(from);
            condition.Name = to;
            Conditions.Remove(from);
            Conditions[to] = condition;
            return true;

        }

        public string GetUniqueName(string name)
        {
            var newName = name;
            int i = 1;
            while (Conditions.Contains(newName))
            {
                newName = name + i;
                i += 1;
            }
            return newName;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is Transition trans))
                return false;

            return From == trans.From && To == trans.To;
        }

        public override int GetHashCode()
        {
            return GeneralUtils.CombineHashCodes(From.GetHashCode(), To.GetHashCode());
        }

        public int CompareTo(Transition other)
        {
            return priority - other.priority;
        }
    }
}