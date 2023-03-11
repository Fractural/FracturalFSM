
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class FlowChartLine : Container
{
	 
	// Custom style normal, focus, arrow
	
	public __TYPE selected = false {set{SetSelected(value);}}
	
	
	public void _Init()
	{  
		focusMode = FOCUSClick;
		mouseFilter = MOUSEFilterIgnore;
	
	}
	
	public void _Draw()
	{  
		PivotAtLineStart();
		var from = Vector2.ZERO;
		from.y += rectSize.y / 2.0;
		var to = rectSize;
		to.y -= rectSize.y / 2.0;
		var arrow = GetIcon("arrow", "FlowChartLine");
		var tint = Color.white;
		if(selected)
		{
			tint = GetStylebox("focus", "FlowChartLine").shadow_color;
			DrawStyleBox(GetStylebox("focus", "FlowChartLine"), new Rect2(Vector2.ZERO, rectSize));
		}
		else
		{
			DrawStyleBox(GetStylebox("normal", "FlowChartLine"), new Rect2(Vector2.ZERO, rectSize));
		
		
		}
		DrawTexture(arrow, Vector2.ZERO - arrow.GetSize() / 2 + rectSize / 2, tint);
	
	}
	
	public __TYPE _GetMinimumSize()
	{  
		return new Vector2(0, 5);
	
	}
	
	public void PivotAtLineStart()
	{  
		rectPivotOffset.x = 0;
		rectPivotOffset.y = rectSize.y / 2.0;
	
	}
	
	public void Join(__TYPE from, __TYPE to, __TYPE offset=Vector2.ZERO, Array clipRects=new Array(){})
	{  
		// Offset along perpendicular direction
		var perpDir = from.DirectionTo(to).Rotated(Mathf.Deg2Rad(90.0)).Normalized();
		from -= perpDir * offset;
		to -= perpDir * offset;
	
		var dist = from.DistanceTo(to);
		var dir = from.DirectionTo(to);
		var center = from + dir * dist / 2;
	
		// Clip line with provided Rect2 array
		Array clipped = new Array(){new Array(){from, to}};
		var lineFrom = from;
		var lineTo = to;
		foreach(var clipRect in clipRects)
		{
			if(clipped.Size() == 0)
			{
				break;
			
			}
			lineFrom = clipped[0][0];
			lineTo = clipped[0][1];
			clipped = Geometry.ClipPolylineWithPolygon2d(
					new Array(){lineFrom, lineTo}, 
					new Array(){clipRect.position, new Vector2(clipRect.position.x, clipRect.end.y), 
						clipRect.end, new Vector2(clipRect.end.x, clipRect.position.y)}
					);
	
		}
		if(clipped.Size() > 0)
		{
			from = clipped[0][0];
			to = clipped[0][1];
		}
		else // Line is totally overlapped
		{
			from = center;
			to = center + dir * 0.1;
	
		// Extends line by 2px to minimise ugly seam	
		}
		from -= dir * 2.0;
		to += dir * 2.0;
	
		rectSize.x = to.DistanceTo(from);
		// rectSize.y equals to the thickness of line
		rectPosition = from;
		rectPosition.y -= rectSize.y / 2.0;
		rectRotation = Mathf.Rad2Deg(Vector2.RIGHT.AngleTo(dir));
		PivotAtLineStart();
	
	}
	
	public void SetSelected(__TYPE v)
	{  
		if(selected != v)
		{
			selected = v;
			Update();
	
		}
	}
	
	public __TYPE GetFromPos()
	{  
		return GetTransform().Xform(rectPosition);
	
	}
	
	public __TYPE GetToPos()
	{  
		return GetTransform().Xform(rectPosition + rectSize);
	
	
	}
	
	
	
}