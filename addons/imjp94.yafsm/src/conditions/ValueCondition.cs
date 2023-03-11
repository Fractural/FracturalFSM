
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

namespace GodotRollbackNetcode.StateMachine
{
	[Tool]
	public abstract class ValueCondition<T> : ValueCondition
    {
		[Export]
		private T value;
		public T TypedValue { get => value; set => Value = value; }
        protected override object InternalValue { get => TypedValue; set => this.value = (T)value; }
    }

	[Tool]
	public abstract class ValueCondition : Condition
	{
		[Signal] public delegate void ComparationChanged(ComparationType newComparation); // Comparation hanged
		[Signal] public delegate void ValueChanged(object newValue); // Value changed
	
		// Enum to define how to compare value
		public enum ComparationType 
		{
			EQUAL = 0,
			INEQUAL,
			GREATER,
			LESSER,
			GREATER_OR_EQUAL,
			LESSER_OR_EQUAL
		}

		// Comparation symbols arranged in order as enum Comparation
		public static readonly string[] ComparationSymbols = new string[] {
			"==",
			"!=",
			">",
			"<",
			"≥",
			"≤"
		};
	
		private ComparationType comparation;
		[Export] 
		public ComparationType Comparation
		{
			get => comparation;
			set
			{
				if (comparation != value)
				{
					comparation = value;
					EmitSignal(nameof(ComparationChanged), value);
					EmitSignal(nameof(DisplayStringChanged), DisplayString());
				}
			}
		}
	
		public void _Init(string pName = "", ComparationType comparation = ComparationType.EQUAL)
		{  
			base._Init(pName);
			Comparation = comparation;
		}
		
		/// <summary>
		/// Overridde by subclass if you want finer grain control over when
		/// ValueChanged and DisplayStringChanged is emitted. See <see cref="InternalValue"/>.
		/// </summary>
		public virtual object Value 
		{ 
			get => InternalValue;
			set 
			{
				if (value.Equals(InternalValue))
					return;
				InternalValue = value;
				EmitSignal(nameof(ValueChanged), value);
				EmitSignal(nameof(DisplayStringChanged), DisplayString());
			}
		}

		/// <summary>
		/// Overridde by subclass if you satisfied with emitting value changed when !currentValue.Equals(newValue).
		/// </summary>
		protected abstract object InternalValue { get; set; }

		/// <summary>
		/// Returns the value as a string. Uses Value.ToString() by default, but can be overridden 
		/// to return a custom formatted value string.
		/// </summary>
		/// <returns></returns>
		public virtual string GetValueString() => Value.ToString();

		/// <summary>
		/// Compare value against this condition, return true if succeeded
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Compare(object other)
		{  
			if(other == null)
				return false;

			switch(Comparation)
			{
				case ComparationType.EQUAL:
					return other.Equals(Value);
				case ComparationType.INEQUAL:
					return !other.Equals(Value);
			}

			if (other is IComparable otherComp && Value is IComparable valueComp)
			{
				switch (Comparation)
				{
					case ComparationType.GREATER:
						return otherComp.CompareTo(Value) > 0;
					case ComparationType.LESSER:
						return otherComp.CompareTo(Value) < 0;
					case ComparationType.GREATER_OR_EQUAL:
						return otherComp.CompareTo(Value) >= 0;
					case ComparationType.LESSER_OR_EQUAL:
						return otherComp.CompareTo(Value) <= 0;
				}
			}
			return false;
		}

		/// <summary>
		/// Return human readable display string, for example, "condition_name == True"
		/// </summary>
		/// <returns></returns>
		public override string DisplayString()
		{  
			return $"{base.DisplayString()} {ComparationSymbols[(int)Comparation]} {GetValueString()}";
		}	
	}
}