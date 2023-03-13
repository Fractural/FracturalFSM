
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

namespace Fractural.StateMachine
{
    [Tool]
    public class StringCondition : ValueCondition<string>
    {
        public override string GetValueString() => $"\"{TypedValue}\"";
    }
}