
using System;
using Godot;

namespace GodotRollbackNetcode.StateMachine
{
	[Tool]
	public class Condition : Resource
	{
		[Signal] public delegate void NameChanged(string oldName, string newName);
		[Signal] public delegate void DisplayStringChanged(string newString);

		// Name of condition, unique to Transition
		private string name;
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
	
		public virtual void _Init(string name="")
		{  
			this.name = name;
		}
	
		public virtual string DisplayString()
		{  
			return name;
		}
	}
}