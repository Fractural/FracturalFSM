
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class TransitionLine : "res://addons/imjp94.yafsm/scenes/flowchart/FlowChartLine.gd"
{
	 
	public const var Transition = GD.Load("../../src/transitions/Transition.gd");
	public const var ValueCondition = GD.Load("../../src/conditions/ValueCondition.gd");
	
	[Export] public float uprightAngleRange = 10.0f;
	
	public onready var labelMargin = GetNode("MarginContainer");
	public onready var vbox = GetNode("MarginContainer/VBoxContainer");
	
	public __TYPE undoRedo;
	
	var transition {set{SetTransition(value);}}
	public __TYPE template = "{conditionName} {conditionComparation} {conditionValue}";
	
	var _templateVar = new Dictionary(){}
	
	public void _Init()
	{  
		SetTransition(new Transition());
	
	}
	
	public void _Draw()
	{  
		base._Draw();
	
		var absRectRotation = Mathf.Abs(rectRotation);
		var isFlip = absRectRotation > 90.0;
		var isUpright = absRectRotation > 90.0 - uprightAngleRange && absRectRotation < 90.0 + uprightAngleRange;
		if(isUpright)
		{
			var xOffset = labelMargin.rect_size.x / 2;
			var yOffset = -label_margin.rect_size.y
			labelMargin.rect_rotation = -rect_rotation
			if(rectRotation > 0)
			{
				labelMargin.rect_position = new Vector2((rectSize.x - xOffset) / 2, 0);
			}
			else
			{
				labelMargin.rect_position = new Vector2((rectSize.x + xOffset) / 2, yOffset * 2);
			}
		}
		else
		{
			var xOffset = labelMargin.rect_size.x;
			var yOffset = -label_margin.rect_size.y
			if(isFlip)
			{
				labelMargin.rect_rotation = 180;
				labelMargin.rect_position = new Vector2((rectSize.x + xOffset) / 2, 0);
			}
			else
			{
				labelMargin.rect_rotation = 0;
				labelMargin.rect_position = new Vector2((rectSize.x - xOffset) / 2, yOffset);
	
	// Update overlay text
			}
		}
	}
	
	public void UpdateLabel()
	{  
		if(transition)
		{
			var templateVar = new Dictionary(){{"condition_name", ""}, {"condition_comparation", ""}, {"condition_value", null}}
			foreach(var label in vbox.GetChildren())
			{
				if(!(label.name in transition.conditions.Keys()))
				{
					vbox.RemoveChild(label);
					label.QueueFree();
				}
			}
			foreach(var condition in transition.conditions.Values())
			{
				var label = vbox.GetNodeOrNull(condition.name);
				if(!label)
				{
					label = new Label()
					label.align = label.ALIGN_CENTER;
					label.name = condition.name;
					vbox.AddChild(label);
				}
				if(condition is ValueCondition)
				{
					templateVar["condition_name"] = condition.name
					templateVar["condition_comparation"] = ValueCondition.COMPARATION_SYMBOLS[condition.comparation]
					templateVar["condition_value"] = condition.GetValueString()
					label.text = template.Format(templateVar);
					var overrideTemplateVar = _templateVar.Get(condition.name)
					if overrideTemplateVar:
						label.text = label.text.Format(overrideTemplateVar);
				}
				else
				{
					label.text = condition.name;
				}
			}
		}
		Update();
	
	}
	
	public void _OnTransitionChanged(__TYPE newTransition)
	{  
		if(!is_inside_tree())
		{
			return;
	
		}
		if(newTransition)
		{
			newTransition.Connect("condition_added", this, "_on_transition_condition_added")
			newTransition.Connect("condition_removed", this, "_on_transition_condition_removed")
			foreach(var condition in newTransition.conditions.Values())
			{
				condition.Connect("name_changed", this, "_on_condition_name_changed");
				condition.Connect("display_string_changed", this, "_on_condition_display_string_changed");
			}
		}
		UpdateLabel();
	
	}
	
	public void _OnTransitionConditionAdded(__TYPE condition)
	{  
		condition.Connect("name_changed", this, "_on_condition_name_changed");
		condition.Connect("display_string_changed", this, "_on_condition_display_string_changed");
		UpdateLabel();
	
	}
	
	public void _OnTransitionConditionRemoved(__TYPE condition)
	{  
		condition.Disconnect("name_changed", this, "_on_condition_name_changed");
		condition.Disconnect("display_string_changed", this, "_on_condition_display_string_changed");
		UpdateLabel();
	
	}
	
	public void _OnConditionNameChanged(__TYPE from, __TYPE to)
	{  
		var label = vbox.GetNodeOrNull(from);
		if(label)
		{
			label.name = to;
		}
		UpdateLabel();
	
	}
	
	public void _OnConditionDisplayStringChanged(__TYPE displayString)
	{  
		UpdateLabel();
	
	}
	
	public void SetTransition(__TYPE t)
	{  
		if(transition != t)
		{
			if(transition)
			{
				if(transition.IsConnected("condition_added", this, "_on_transition_condition_added"))
				{
					transition.Disconnect("condition_added", this, "_on_transition_condition_added");
				}
			}
			transition = t;
			_OnTransitionChanged(transition);
	
	
		}
	}
	
	
	
}