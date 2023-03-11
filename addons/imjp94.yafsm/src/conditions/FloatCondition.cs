
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

namespace GodotRollbackNetcode.StateMachine
{
	[Tool]
	public class FloatCondition : ValueCondition<float>
	{
		public override string GetValueString() => Mathf.Stepify(TypedValue, 0.01f).ToString().PadDecimals(2);
	}
}