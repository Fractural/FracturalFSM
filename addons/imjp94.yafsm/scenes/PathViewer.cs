
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class PathViewer : HBoxContainer
{
	 
	[Signal] delegate void DirPressed(dir, index);
	
	
	public void _Init()
	{  
		AddDir("root");
	
	// Select parent dir & return its path
	}
	
	public __TYPE Back()
	{  
		return SelectDir(GetChild(Mathf.Max(GetChildCount()-1 - 1, 0)).name);
	
	// Select dir & return its path
	}
	
	public __TYPE SelectDir(__TYPE dir)
	{  
		foreach(var i in GetChildCount())
		{
			var child = GetChild(i);
			if(child.name == dir)
			{
				RemoveDirUntil(i);
				return GetDirUntil(i);
	
	// Add directory button
			}
		}
	}
	
	public __TYPE AddDir(__TYPE dir)
	{  
		var button = new Button()
		button.name = dir;
		button.flat = true;
		button.text = dir;
		AddChild(button);
		button.Connect("pressed", this, "_on_button_pressed", new Array(){button});
		return button;
	
	// Remove directory until Index(exclusive)
	}
	
	public void RemoveDirUntil(__TYPE index)
	{  
		Array toRemove = new Array(){};
		foreach(var i in GetChildCount())
		{
			if(index == GetChildCount()-1 - i)
			{
				break;
			}
			var child = GetChild(GetChildCount()-1 - i);
			toRemove.Append(child);
		}
		foreach(var n in toRemove)
		{
			RemoveChild(n);
			n.QueueFree();
	
	// Return current working directory
		}
	}
	
	public __TYPE GetCwd()
	{  
		return GetDirUntil(GetChildCount()-1);
	
	// Return path until Index(inclusive) of directory
	}
	
	public __TYPE GetDirUntil(__TYPE index)
	{  
		string path = "";
		foreach(var i in GetChildCount())
		{
			if(i > index)
			{
				break;
			}
			var child = GetChild(i);
			if(i == 0)
			{
				path = "root";
			}
			else
			{
				path = GD.Str(path, "/", child.text);
			}
		}
		return path;
	
	}
	
	public void _OnButtonPressed(__TYPE button)
	{  
		var index = button.GetIndex();
		var dir = button.name;
		EmitSignal("dir_pressed", dir, index);
	
	
	}
	
	
	
}