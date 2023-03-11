
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class StateDirectory : Reference
{
	 
	public const var State = GD.Load("states/State.gd");
	
	public __TYPE path;
	var current {get{return GetCurrent();}}
	var base {get{return GetBase();}}
	var end {get{return GetEnd();}}
	
	private __TYPE _currentIndex = 0;
	private __TYPE _dirs = new Array(){""} ;// Empty string equals to root
	
	
	public void _Init(__TYPE p)
	{  
		path = p;
		_dirs += new Array(p.Split("/"));
	
	// Move to next level && return state if exists, else null
	}
	
	public __TYPE Next()
	{  
		if(HasNext())
		{
			_currentIndex += 1;
			return GetCurrentEnd();
	
		}
		return null;
	
	// Move to previous level && return state if exists, else null
	}
	
	public __TYPE Back()
	{  
		if(HasBack())
		{
			_currentIndex -= 1;
			return GetCurrentEnd();
		
		}
		return null;
	
	// Move to specified index && return state
	}
	
	public __TYPE Goto(__TYPE index)
	{  
		System.Diagnostics.Debug.Assert(index > -1 && index < _dirs.Size());
		_currentIndex = index;
		return GetCurrentEnd();
	
	// Check if directory has next level
	}
	
	public __TYPE HasNext()
	{  
		return _currentIndex < _dirs.Size() - 1;
	
	// Check if directory has previous level
	}
	
	public __TYPE HasBack()
	{  
		return _currentIndex > 0;
	
	// Get current full path
	}
	
	public __TYPE GetCurrent()
	{  
		return PoolStringArray(_dirs.Slice(GetBaseIndex(), _currentIndex)).Join("/");
	
	// Get current end state name of path
	}
	
	public __TYPE GetCurrentEnd()
	{  
		var currentPath = GetCurrent();
		return currentPath.Right(currentPath.Rfind("/") + 1);
	
	// Get index of base state
	}
	
	public __TYPE GetBaseIndex()
	{  
		return 1; // Root(empty string) at index 0, base at index 1
	
	// Get level index of end state
	}
	
	public __TYPE GetEndIndex()
	{  
		return _dirs.Size() - 1;
	
	// Get base state name
	}
	
	public __TYPE GetBase()
	{  
		return _dirs[GetBaseIndex()];
	
	// Get end state name
	}
	
	public __TYPE GetEnd()
	{  
		return _dirs[GetEndIndex()];
	
	// Get arrays of directories
	}
	
	public __TYPE GetDirs()
	{  
		return _dirs.Duplicate();
	
	// Check if it is Entry state
	}
	
	public __TYPE IsEntry()
	{  
		return GetEnd() == State.ENTRY_STATE;
	
	// Check if it is Exit state
	}
	
	public __TYPE IsExit()
	{  
		return GetEnd() == State.EXIT_STATE;
	
	// Check if it is nested. ("Base" is !nested, "Base/NextState" is nested)
	}
	
	public __TYPE IsNested()
	{  
		return _dirs.Size() > 2; // Root(empty string) & base taken 2 place
	
	
	}
	
	
	
}