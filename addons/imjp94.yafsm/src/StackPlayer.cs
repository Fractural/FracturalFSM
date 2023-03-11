
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;


public class StackPlayer : Node
{
	 
	[Signal] delegate void Pushed(to);// When item pushed to stack
	[Signal] delegate void Popped(from);// When item popped from stack
	
	// Enum to specify how reseting state stack should trigger Event(transit, push, pop etc.)
	enum ResetEventTrigger {
		NONE = -1, // No event
		ALL = 0, // All removed state will emit event
		LASTToDest = 1 ;// Only last state && destination will emit event
	}
	
	var current {get{return GetCurrent();}} // Current item on top of stack
	var stack {get{return GetStack();} set{SetStack(value);}}
	
	
	public void _Init()
	{  
		stack = new Array(){};
	
	// Push an item to the top of stack
	}
	
	public void Push(__TYPE to)
	{  
		var from = GetCurrent();
		stack.PushBack(to);
		_OnPushed(from, to);
		EmitSignal("pushed", to);
	
	// Remove the current item on top of stack
	}
	
	public void Pop()
	{  
		var to = GetPrevious();
		var from = stack.PopBack();
		_OnPopped(from, to);
		EmitSignal("popped", from);
	
	// Called when item pushed
	}
	
	public void _OnPushed(__TYPE from, __TYPE to)
	{  
	
	// Called when item popped
	}
	
	public void _OnPopped(__TYPE from, __TYPE to)
	{  
	
	// Reset stack to given index, -1 to clear all item by default
	// Use ResetEventTrigger to define how _onPopped should be called
	}
	
	public void Reset(int to=-1, __TYPE event=ResetEventTrigger.ALL)
	{  
		System.Diagnostics.Debug.Assert(to > -2 && to < stack.Size(), "Reset to Index(%d) out of Bounds(%d)" % [to, stack.Size()]);
		var lastIndex = stack.Size() - 1;
		string firstState = "";
		var numToPop = lastIndex - to;
	
		if(numToPop > 0)
		{
			foreach(var i in GD.Range(numToPop))
			{
				firstState = i == 0 ? GetCurrent() : firstState
				switch( event)
				{
					case ResetEventTrigger.LAST_TO_DEST:
						stack.PopBack();
						if(i == numToPop - 1)
						{
							stack.PushBack(firstState);
							Pop();
						}
						break;
					case ResetEventTrigger.ALL:
						Pop();
						break;
					case _:
						stack.PopBack();
						break;
				}
			}
		}
		else if(numToPop == 0)
		{
			switch( event)
			{
				case ResetEventTrigger.NONE:
					stack.PopBack();
					break;
				case _:
					Pop();
	
					break;
			}
		}
	}
	
	public void SetStack(__TYPE stack)
	{  
		GD.PushWarning("Attempting to edit read-only state stack directly. " \
			+ "Control state machine from setting parameters || call Update() instead");
	
	// Get duplicate of the stack being played
	}
	
	public __TYPE GetStack()
	{  
		return stack.Duplicate();
	
	}
	
	public __TYPE GetCurrent()
	{  
		return !stack.Empty() ? stack.Back() : null
	
	}
	
	public __TYPE GetPrevious()
	{  
		return stack.Size() > 1 ? stack[stack.Size() - 2] : null
	
	
	}
	
	
	
}