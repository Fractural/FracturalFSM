
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class StackPlayerDebugger : Control
{
	 
	public const var StackPlayer = GD.Load("../StackPlayer.gd");
	public const var StackItem = GD.Load("StackItem.tscn");
	
	public onready var Stack = GetNode("MarginContainer/Stack");
	
	
	public __TYPE _GetConfigurationWarning()
	{  
		if(!(GetParent() is StackPlayer))
		{
			return "Debugger must be child of StackPlayer";
		}
		return "";
	
	}
	
	public void _Ready()
	{  
		if(Engine.editor_hint)
		{
			return;
	
		}
		GetParent().Connect("pushed", this, "_on_StackPlayer_pushed");
		GetParent().Connect("popped", this, "_on_StackPlayer_popped");
		SyncStack();
	
	// Override to handle custom object presentation
	}
	
	public void _OnSetLabel(__TYPE label, __TYPE obj)
	{  
		label.text = obj;
	
	}
	
	public void _OnStackPlayerPushed(__TYPE to)
	{  
		var stackItem = StackItem.Instance();
		_OnSetLabel(stackItem.GetNode("Label"), to);
		Stack.AddChild(stackItem);
		Stack.MoveChild(stackItem, 0);
	
	}
	
	public void _OnStackPlayerPopped(__TYPE from)
	{  
		// Sync whole stack instead of just popping top item, as ResetEventTrigger passed to Reset() may be varied
		SyncStack();
	
	}
	
	public void SyncStack()
	{  
		var diff = Stack.GetChildCount() - GetParent().stack.Size();
		foreach(var i in Mathf.Abs(diff))
		{
			if(diff < 0)
			{
				var stackItem = StackItem.Instance();
				Stack.AddChild(stackItem);
			}
			else
			{
				var child = Stack.GetChild(0);
				Stack.RemoveChild(child);
				child.QueueFree();
			}
		}
		var stack = GetParent().stack;
		foreach(var i in stack.Size())
		{
			var obj = stack[stack.Size()-1 - i] ;// Descending order, to list from bottom to top in VBoxContainer
			var child = Stack.GetChild(i);
			_OnSetLabel(child.GetNode("Label"), obj);
	
	
		}
	}
	
	
	
}