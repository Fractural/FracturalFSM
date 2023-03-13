
using System;
using Godot;

namespace Fractural.StateMachine
{
    public class StateInspector : EditorInspectorPlugin
    {
        public override bool CanHandle(Godot.Object @object)
        {
            return @object is State;
        }

        public override bool ParseProperty(Godot.Object @object, int type, string path, int hint, string hintText, int usage)
        {
            // Hide all properties
            return true;
        }
    }

}