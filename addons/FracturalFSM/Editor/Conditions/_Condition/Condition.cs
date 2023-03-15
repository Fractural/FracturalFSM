
using System;
using Godot;

namespace Fractural.StateMachine
{
    [Tool]
    public class Condition : Resource
    {
        /// <summary>
        /// Emitted when the name is set
        /// </summary>
        /// <param name="oldName"></param>
        /// <param name="newName"></param>
        [Signal] public delegate void NameChanged(string oldName, string newName);
        /// <summary>
        /// Emitted when any fields that need displaying is changed.
        /// For conditions this is the Name.
        /// For value conditions this include the value and the comparation symbol.
        /// </summary>
        /// <param name="newString"></param>
        [Signal] public delegate void DisplayStringChanged(string newString);

        private string name;
        /// <summary>
        /// Name of condition, unique to Transition. 
        /// </summary>
        [Export]
        public string Name
        {
            get => name;
            set
            {
                if (name != value)
                {
                    var old = name;
                    name = value;
                    EmitSignal(nameof(NameChanged), old, value);
                    EmitSignal(nameof(DisplayStringChanged), DisplayString());
                }
            }
        }

        public Condition() { }
        public Condition(string name = "")
        {
            this.name = name;
        }

        public virtual string DisplayString()
        {
            return name;
        }
    }
}