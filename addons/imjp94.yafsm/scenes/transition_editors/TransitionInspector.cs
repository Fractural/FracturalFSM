
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class TransitionInspector : EditorInspectorPlugin
{
	 
	public const var Transition = GD.Load("res://addons/imjp94.yafsm/src/transitions/Transition.gd");
	
	public const var TransitionEditor = GD.Load("res://addons/imjp94.yafsm/scenes/transition_editors/TransitionEditor.tscn");
	
	public __TYPE undoRedo;
	
	public __TYPE transitionIcon;
	
	public __TYPE CanHandle(__TYPE object)
	{  
		return object is Transition;
	
	}
	
	public __TYPE ParseProperty(__TYPE object, __TYPE type, __TYPE path, __TYPE hint, __TYPE hintText, __TYPE usage)
	{  
		switch( path)
		{
			{"from",
				return true;
			{"to",
				return true;
			{"conditions",
				var transitionEditor = TransitionEditor.Instance() ;// Will be freed by editor
				transitionEditor.undo_redo = undoRedo;
				AddCustomControl(transitionEditor);
				transitionEditor.Connect("ready"}}}, this, "_on_transition_editor_tree_entered", new Array(){transitionEditor, object});
				return true;
			{"priority",
				return true;
		}
		return false;
	
	}}
	
	public void _OnTransitionEditorTreeEntered(__TYPE editor, __TYPE transition)
	{  
		editor.transition = transition;
		if(transitionIcon)
		{
			editor.title_icon.texture = transitionIcon;
	
	
		}
	}
	
	
	
}