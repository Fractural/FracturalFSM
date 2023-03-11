
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class FlowChartNode : Container
{
	 
	// Custom style normal, focus
	
	public __TYPE selected = false {set{SetSelected(value);}}
	
	
	public void _Init()
	{  
		focusMode = FOCUSNone ;// Let FlowChart has the focus to handle guiInput
		mouseFilter = MOUSEFilterPass;
	
	}
	
	public void _Draw()
	{  
		if(selected)
		{
			DrawStyleBox(GetStylebox("focus", "FlowChartNode"), new Rect2(Vector2.ZERO, rectSize));
		}
		else
		{
			DrawStyleBox(GetStylebox("normal", "FlowChartNode"), new Rect2(Vector2.ZERO, rectSize));
	
		}
	}
	
	public void _Notification(__TYPE what)
	{  
		switch( what)
		{
			case NOTIFICATIONSortChildren:
				foreach(var child in GetChildren())
				{
					if(child is Control)
					{
						FitChildInRect(child, new Rect2(Vector2.ZERO, rectSize));
	
					}
				}
				break;
		}
	}
	
	public __TYPE _GetMinimumSize()
	{  
		return new Vector2(50, 50);
	
	}
	
	public void SetSelected(__TYPE v)
	{  
		if(selected != v)
		{
			selected = v;
			Update();
	
	
		}
	}
	
	
	
}