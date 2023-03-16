
using System;
using Godot;
using System.Collections.Generic;
using System.Linq;
using Fractural.Utils;

namespace Fractural.StateMachine
{
    public class StateStackPlayer : Node
    {

        [Signal] public delegate void Pushed(string to);    // When item pushed to stack
        [Signal] public delegate void Popped(string from);  // When item popped from stack

        /// <summary>
        /// Enum to specify how reseting state stack should trigger Event(transit, push, pop etc.)
        /// </summary>
        public enum ResetEventTrigger
        {
            None = -1,      // No event
            All = 0,        // All removed state will emit event
            LastToDest = 1  // Only last state && destination will emit event
        }

        /// <summary>
        /// State before the current state.
        /// </summary>
        public virtual string Previous => stack.Count > 1 ? stack.Skip(1).First() : null;
        /// <summary>
        /// Current item on top of stack
        /// </summary>
        public virtual string Current => stack.FirstOrDefault();
        // Exported but should not be set by the user.
        [Export]
        private List<string> stack = new List<string>(); // We use a list as a stack becasue we need to also access items by index in case of a refresh
        public IReadOnlyList<string> Stack => stack;

        /// <summary>
        /// Push an item to the top of stackz
        /// </summary>
        /// <param name="to"></param>
        public void Push(string to)
        {
            var from = Current;
            stack.PushBack(to);
            OnPushed(from, to);
            EmitSignal(nameof(Pushed), to);
        }

        /// <summary>
        /// Remove the current item on top of stack
        /// </summary>
        public void Pop()
        {
            var to = Previous;
            var from = stack.PopBack();
            OnPopped(from, to);
            EmitSignal(nameof(Popped), from);
        }

        /// <summary>
        /// Called when item pushed
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public virtual void OnPushed(string from, string to) { }

        /// <summary>
        /// Called when item popped
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public virtual void OnPopped(string from, string to) { }

        /// <summary>
        /// Reset stack back to a given index, -1 to clear all item by default.
        /// This index is inclusive, so the item at index will also get popped.
        /// This operation ONLY pops items from the stack, so index &lt; count
        /// Use ResetEventTrigger to define how OnPopped should be called
        /// </summary>
        /// <param name="index"></param>
        /// <param name="resetEventTrigger"></param>
        public virtual void Reset(int index = -1, ResetEventTrigger resetEventTrigger = ResetEventTrigger.All)
        {
            System.Diagnostics.Debug.Assert(index > -2 && index < stack.Count, $"Reset to Index({index}) out of Bounds({stack.Count})");
            var lastIndex = stack.Count - 1;
            string firstState = "";
            var numToPop = lastIndex - index;

            if (numToPop > 0)
            {
                for (int i = 0; i < numToPop; i++)
                {
                    firstState = i == 0 ? Current : firstState;

                    switch (resetEventTrigger)
                    {
                        case ResetEventTrigger.LastToDest:
                            stack.PopBack();
                            if (i == numToPop - 1)
                            {
                                stack.PushBack(firstState);
                                Pop();
                            }
                            break;
                        case ResetEventTrigger.All:
                            Pop();
                            break;
                        default:
                            stack.PopBack();
                            break;
                    }
                }
            }
            else if (numToPop == 0)
            {
                switch (resetEventTrigger)
                {
                    case ResetEventTrigger.None:
                        stack.PopBack();
                        break;
                    default:
                        Pop();
                        break;
                }
            }
        }
    }
}