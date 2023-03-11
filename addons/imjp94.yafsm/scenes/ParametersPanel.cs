
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class ParametersPanel : MarginContainer
{
	 
	public onready var grid = GetNode("PanelContainer/MarginContainer/VBoxContainer/GridContainer");
	public onready var button = GetNode("PanelContainer/MarginContainer/VBoxContainer/MarginContainer/Button");
	
	
	public void _Ready()
	{  
		button.Connect("pressed", this, "_on_button_pressed");
	
	}
	
	public void UpdateParams(__TYPE params, __TYPE localParams)
	{  
		// Remove erased parameters from param panel
		foreach(var param in grid.GetChildren())
		{
			if(!(param.name in params))
			{
				RemoveParam(param.name);
			}
		}
		foreach(var param in params)
		{
			var value = params[param];
			if(value == null) // Ignore trigger
			{
				continue;
			}
			SetParam(param, GD.Str(value));
	
		// Remove erased local parameters from param panel
		}
		foreach(var param in grid.GetChildren())
		{
			if(!(param.name in localParams) && !(param.name in params))
			{
				RemoveParam(param.name);
			}
		}
		foreach(var param in localParams)
		{
			var nestedParams = localParams[param];
			foreach(var nestedParam in nestedParams)
			{
				var value = nestedParams[nestedParam];
				if(value == null) // Ignore trigger
				{
					continue;
				}
				SetParam(GD.Str(param, "/", nestedParam), GD.Str(value));
	
			}
		}
	}
	
	public void SetParam(__TYPE param, __TYPE value)
	{  
		var label = grid.GetNodeOrNull(param);
		if(!label)
		{
			label = new Label()
			label.name = param;
			grid.AddChild(label);
	
		}
		label.text = "%s = %s" % [param, value]
	
	}
	
	public void RemoveParam(__TYPE param)
	{  
		var label = grid.GetNodeOrNull(param);
		if(label)
		{
			grid.RemoveChild(label);
			label.QueueFree();
			SetAnchorsPreset(PRESETBottomRight);
	
		}
	}
	
	public void ClearParams()
	{  
		foreach(var child in grid.GetChildren())
		{
			grid.RemoveChild(child);
			child.QueueFree();
	
		}
	}
	
	public void _OnButtonPressed()
	{  
		grid.visible = !grid.visible;
		
		SetAnchorsPreset(PRESETBottomRight);
	
	
	}
	
	
	
}