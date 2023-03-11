
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class StateNode : "res://addons/imjp94.yafsm/scenes/flowchart/FlowChartNode.gd"
{
	 
	public const var State = GD.Load("../../src/states/State.gd");
	public const var StateMachine = GD.Load("../../src/states/StateMachine.gd");
	
	[Signal] delegate void NameEditEntered(newName);// Emits when focused exit || Enter pressed
	
	public onready var nameEdit = GetNode("MarginContainer/NameEdit");
	
	public __TYPE undoRedo;
	
	var state {set{SetState(value);}}
	
	
	public void _Init()
	{  
		SetState(State.new("State"));
	
	}
	
	public void _Ready()
	{  
		nameEdit.text = "State";
		nameEdit.Connect("focus_exited", this, "_on_NameEdit_focus_exited");
		nameEdit.Connect("text_entered", this, "_on_NameEdit_text_entered");
		SetProcessInput(false) ;// _input only required when nameEdit enabled to check mouse click outside
	
	}
	
	public void _Draw()
	{  
		if(state is StateMachine)
		{
			if(selected)
			{
				DrawStyleBox(GetStylebox("nested_focus", "StateNode"), new Rect2(Vector2.ZERO, rectSize));
			}
			else
			{
				DrawStyleBox(GetStylebox("nested_normal", "StateNode"), new Rect2(Vector2.ZERO, rectSize));
			}
		}
		else
		{
			base._Draw();
	
		}
	}
	
	public void _Input(__TYPE event)
	{  
		if(event is InputEventMouseButton)
		{
			if(event.pressed)
			{
				// Detect click outside rect
				if(GetFocusOwner() == nameEdit)
				{
					var localEvent = MakeInputLocal(event);
					if(!name_edit.GetRect().HasPoint(localEvent.position))
					{
						nameEdit.ReleaseFocus();
	
					}
				}
			}
		}
	}
	
	public void EnableNameEdit(__TYPE v)
	{  
		if(v)
		{
			SetProcessInput(true);
			nameEdit.editable = true;
			nameEdit.selecting_enabled = true;
			nameEdit.mouse_filter = MOUSEFilterPass;
			mouseDefaultCursorShape = CURSORIbeam;
			nameEdit.GrabFocus();
		}
		else
		{
			SetProcessInput(false);
			nameEdit.editable = false;
			nameEdit.selecting_enabled = false;
			nameEdit.mouse_filter = MOUSEFilterIgnore;
			mouseDefaultCursorShape = CURSORArrow;
			nameEdit.ReleaseFocus();
	
		}
	}
	
	public void _OnStateNameChanged(__TYPE newName)
	{  
		nameEdit.text = newName;
		rectSize.x = 0 ;// Force reset horizontal size
	
	}
	
	public void _OnStateChanged(__TYPE newState)
	{  
		if(state)
		{
			state.Connect("name_changed", this, "_on_state_name_changed");
			if(nameEdit)
			{
				nameEdit.text = state.name;
	
			}
		}
	}
	
	public void _OnNameEditFocusExited()
	{  
		EnableNameEdit(false);
		nameEdit.Deselect();
		EmitSignal("name_edit_entered", nameEdit.text);
	
	}
	
	public void _OnNameEditTextEntered(__TYPE newText)
	{  
		EnableNameEdit(false);
		EmitSignal("name_edit_entered", newText);
	
	}
	
	public void SetState(__TYPE s)
	{  
		if(state != s)
		{
			state = s;
			_OnStateChanged(s);
	
	
		}
	}
	
	
	
}