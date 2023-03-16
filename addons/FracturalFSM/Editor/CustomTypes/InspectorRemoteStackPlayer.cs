using Fractural.Utils;
using GDC = Godot.Collections;

namespace Fractural.StateMachine
{
    public class InspectorRemoteStackPlayer : GDScriptWrapper
    {
        public virtual string Previous
        {
            get
            {
                var stack = Stack;
                return stack.Length > 1 ? stack[1] : null;
            }
        }
        public virtual string Current
        {
            get
            {
                var stack = Stack;
                return stack.Length > 0 ? stack[0] : null;
            }
        }
        public string[] Stack => Source.GetRemote<string[]>("stack");

        public InspectorRemoteStackPlayer() { }
        public InspectorRemoteStackPlayer(Godot.Object source) : base(source) { }
    }
}