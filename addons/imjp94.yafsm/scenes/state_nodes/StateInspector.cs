
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class StateInspector : EditorInspectorPlugin
{
	 
	public const var State = GD.Load("res://addons/imjp94.yafsm/src/states/State.gd");
	
	public __TYPE CanHandle(__TYPE object)
	{  
		return object is State;
	
	}
	
	public __TYPE ParseProperty(__TYPE object, __TYPE type, __TYPE path, __TYPE hint, __TYPE hintText, __TYPE usage)
	{  
		// Hide all property
		return true;
	
	
	}
	
	
	
}