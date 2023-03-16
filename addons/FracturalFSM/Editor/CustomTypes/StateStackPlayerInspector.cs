using Godot;

namespace Fractural.StateMachine
{
    /// <summary>
    /// Hides exported properties that are meant for remote debugging
    /// </summary>
    [Tool]
    public class StateStackPlayerInspector : EditorInspectorPlugin
    {
        public StateStackPlayerInspector() { }

        public override bool CanHandle(Godot.Object @object)
        {
            return @object is StateStackPlayer;
        }

        public override bool ParseProperty(Godot.Object @object, int type, string path, int hint, string hintText, int usage)
        {
            if (path == "stack")
                return true;
            return false;
        }
    }
}