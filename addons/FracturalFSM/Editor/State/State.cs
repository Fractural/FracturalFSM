
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;

namespace Fractural.StateMachine
{
    [CSharpScript]
    [Tool]
    public class State : Resource
    {
        [Signal] public delegate void NameChanged(string newName);

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
        /// Position in Flowchart stored as meta, for editor only
        /// </summary>
        public Vector2 GraphOffset
        {
            get => HasMeta(MetaGraphOffset) ? this.GetMeta<Vector2>(MetaGraphOffset) : Vector2.Zero;
            set => SetMeta(MetaGraphOffset, value);
        }

        public State() : this("") { }
        public State(string name = "")
        {
            this.name = name;
        }

        public bool IsEntry => name == EntryState;
        public bool IsExit => name == ExitState;
    }
}