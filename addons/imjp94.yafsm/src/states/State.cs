
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;
using Fractural.Utils;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public class State : Resource
    {

        [Signal] delegate void NameChanged(string newName);

        // Reserved state name for Entry/Exit
        public const string EntryState = "Entry";
        public const string ExitState = "Exit";

        /// <summary>
        /// Meta key for graphOffset
        /// </summary>
        public const string MetaGraphOffset = "graph_offset";

        private string name;
        [Export]
        public string Name
        {
            get => name;
            set
            {
                if (name != value)
                {
                    name = value;
                    EmitSignal(nameof(NameChanged), name);
                }
            }
        }

        /// <summary> 
        /// Position in FlowChart stored as meta, for editor only
        /// </summary>
        public Vector2 graphOffset
        {
            get => HasMeta(MetaGraphOffset) ? this.GetMeta<Vector2>(MetaGraphOffset) : Vector2.Zero;
            set => SetMeta(MetaGraphOffset, value);
        }

        public void _Init(string name = "")
        {
            this.name = name;
        }

        public bool IsEntry => name == EntryState;
        public bool IsExit => name == ExitState;
    }
}