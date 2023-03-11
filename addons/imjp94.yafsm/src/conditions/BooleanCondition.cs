
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

namespace GodotRollbackNetcode.StateMachine
{
	[Tool]
	public class BooleanCondition : ValueCondition<bool>
	{
        public override string DisplayString()
        {
            return base.DisplayString();
        }
    }
}