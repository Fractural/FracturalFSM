
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class StateMachineEditorLayer : "res://addons/imjp94.yafsm/scenes/flowchart/FlowChartLayer.gd"
{
	 
	public const var Utils = GD.Load("res://addons/imjp94.yafsm/scripts/Utils.gd");
	public const var StateNode = GD.Load("res://addons/imjp94.yafsm/scenes/state_nodes/StateNode.tscn");
	public const var StateNodeScript = GD.Load("res://addons/imjp94.yafsm/scenes/state_nodes/StateNode.gd");
	public const var StateDirectory = GD.Load("../src/StateDirectory.gd");
	
	public __TYPE editorAccentColor = Color.white {set{SetEditorAccentColor(value);}}
	public __TYPE editorComplementaryColor = Color.white;
	
	public __TYPE stateMachine;
	public __TYPE tween = new Tween()
	
	
	public void _Init()
	{  
		AddChild(tween);
		tween.Connect("tree_entered", this, "_on_tween_tree_entered");
	
	}
	
	public void _OnTweenTreeEntered()
	{  
		tween.Start();
	
	}
	
	public void DebugUpdate(__TYPE currentState, __TYPE parameters, __TYPE localParameters)
	{  
		if(!state_machine)
		{
			return;
		}
		var currentDir = StateDirectory.new(currentState)
		var transitions = stateMachine.transitions.Get(currentState, new Dictionary(){});
		if(currentDir.IsNested())
		{
			transitions = stateMachine.transitions.Get(currentDir.GetEnd(), new Dictionary(){});
		}
		foreach(var transition in transitions.Values())
		{
			var line = contentLines.GetNodeOrNull("%s>%s" % [transition.from, transition.to]);
			if(line)
			{
				// Blinking alpha of TransitionLine
				var color1 = Color.white;
				color1.a = 0.1;
				var color2 = Color.white;
				color2.a = 0.5;
				if(line.self_modulate == color1)
				{
					tween.InterpolateProperty(line, "self_modulate", null, color2, 1);
				}
				else if(line.self_modulate == color2)
				{
					tween.InterpolateProperty(line, "self_modulate", null, color1, 1);
				}
				else if(line.self_modulate == Color.white)
				{
					tween.InterpolateProperty(line, "self_modulate", null, color2, 1);
				// Update TransitionLine condition labels
				}
				foreach(var condition in transition.conditions.Values())
				{
					if(!(condition is ValueCondition)) // Ignore trigger
					{
						continue;
					}
					var value = parameters.Get(condition.name);
					value = value != null ? GD.Str(value) : "?"
					var label = line.vbox.GetNodeOrNull(condition.name);
					var overrideTemplateVar = line._template_var.Get(condition.name)
					if overrideTemplateVar == null:
						overrideTemplateVar = new Dictionary(){}
						line._template_var[condition.name] = overrideTemplateVar
					overrideTemplateVar["value"] = GD.Str(value)
					line.UpdateLabel();
					// Condition label color based on comparation
					if(condition.Compare(parameters.Get(condition.name)) || condition.Compare(localParameters.Get(condition.name)))
					{
						if(label.self_modulate != Color.green)
						{
							tween.InterpolateProperty(label, "self_modulate", null, Color.green.Lightened(0.5), 0.1);
						}
					}
					else
					{
						if(label.self_modulate != Color.red)
						{
							tween.InterpolateProperty(label, "self_modulate", null, Color.red.Lightened(0.5), 0.1);
						}
					}
				}
			}
		}
		tween.Start();
	
	}
	
	public async void DebugTransitOut(__TYPE from, __TYPE to)
	{  
		var fromDir = StateDirectory.new(from)
		var toDir = StateDirectory.new(to)
		var fromNode = contentNodes.GetNodeOrNull(fromDir.GetEnd());
		if(fromNode)
		{
			fromNode.self_modulate = editorComplementaryColor;
			tween.InterpolateProperty(fromNode, "self_modulate", null, Color.white, 0.5);
		}
		var transitions = stateMachine.transitions.Get(from, new Dictionary(){});
		if(fromDir.IsNested())
		{
			transitions = stateMachine.transitions.Get(fromDir.GetEnd(), new Dictionary(){});
		// Fade out color of StateNode
		}
		foreach(var transition in transitions.Values())
		{
			var line = contentLines.GetNodeOrNull("%s>%s" % [transition.from, transition.to]);
			if(line)
			{
				line.template = "{conditionName} {conditionComparation} {conditionValue}";
				line.UpdateLabel();
				tween.Remove(line, "self_modulate");
				if(transition.to == toDir.GetEnd())
				{
					line.self_modulate = editorComplementaryColor;
					tween.InterpolateProperty(line, "self_modulate", null, Color.white, 2, Tween.TRANS_EXPO, Tween.EASE_IN);
				}
				else
				{
					tween.InterpolateProperty(line, "self_modulate", null, Color.white, 0.1);
				// Revert color of TransitionLine condition labels
				}
				foreach(var condition in transition.conditions.Values())
				{
					if(!(condition is ValueCondition)) // Ignore trigger
					{
						continue;
					}
					var label = line.vbox.GetNodeOrNull(condition.name);
					if(label.self_modulate != Color.white)
					{
						tween.InterpolateProperty(label, "self_modulate", null, Color.white, 0.5);
					}
				}
			}
		}
		if(fromDir.IsNested() && fromDir.IsExit())
		{
			// Transition from nested state
			transitions = stateMachine.transitions.Get(fromDir.GetBase(), new Dictionary(){});
			foreach(var transition in transitions.Values())
			{
				var line = contentLines.GetNodeOrNull("%s>%s" % [transition.from, transition.to]);
				if(line)
				{
					tween.InterpolateProperty(line, "self_modulate", null, editorComplementaryColor.Lightened(0.5), 0.5);
				}
			}
			await ToSignal(tween, "tween_completed")
			foreach(var transition in transitions.Values())
			{
				var line = contentLines.GetNodeOrNull("%s>%s" % [transition.from, transition.to]);
				if(line)
				{
					tween.InterpolateProperty(line, "self_modulate", null, Color.white, 0.5);
				}
			}
		}
		tween.Start();
	
	}
	
	public void DebugTransitIn(__TYPE from, __TYPE to)
	{  
		var toDir = StateDirectory.new(to)
		var toNode = contentNodes.GetNodeOrNull(toDir.GetEnd());
		if(toNode)
		{
			tween.InterpolateProperty(toNode, "self_modulate", null, editorComplementaryColor, 0.5);
		}
		var transitions = stateMachine.transitions.Get(to, new Dictionary(){});
		if(toDir.IsNested())
		{
			transitions = stateMachine.transitions.Get(toDir.GetEnd(), new Dictionary(){});
		// Change string template for current TransitionLines
		}
		foreach(var transition in transitions.Values())
		{
			var line = contentLines.GetNodeOrNull("%s>%s" % [transition.from, transition.to]);
			line.template = "{conditionName} {conditionComparation} {conditionValue}(new Dictionary(){value})";
		}
		tween.Start();
	
	}
	
	public void SetEditorAccentColor(__TYPE color)
	{  
		editorAccentColor = color;
		editorComplementaryColor = Utils.GetComplementaryColor(color);
	
	
	}
	
	
	
}