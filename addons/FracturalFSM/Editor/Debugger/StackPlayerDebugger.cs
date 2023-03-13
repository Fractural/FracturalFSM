
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;
using Fractural.GodotCodeGenerator.Attributes;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class StackPlayerDebugger : Control
    {
        [Export]
        private PackedScene stackItem;

        [OnReadyGet("MarginContainer/Stack")]
        public VBoxContainer Stack;
        public StackPlayer ParentStackPlayer => GetParent() as StackPlayer;


        public override string _GetConfigurationWarning()
        {
            if (!(GetParent() is StackPlayer))
            {
                return "Debugger must be child of StackPlayer";
            }
            return "";
        }

        [OnReady]
        public void RealReady()
        {
            if (Engine.EditorHint)
            {
                return;

            }
            ParentStackPlayer.Connect(nameof(StackPlayer.Pushed), this, nameof(OnStackPlayerPushed));
            ParentStackPlayer.Connect(nameof(StackPlayer.Popped), this, nameof(OnStackPlayerPopped));
            SyncStack();

        }

        // TODO: Remove this if unused
        // TODO: Maybe make a dedicateed script for stack items

        /// <summary>
        /// Override to handle custom object presentation
        /// </summary>
        /// <param name="label"></param>
        /// <param name="obj"></param>
        private void OnSetLabel(Label label, string obj)
        {
            label.Text = obj;
        }

        private void OnStackPlayerPushed(string to)
        {
            var stackItem = this.stackItem.Instance();
            OnSetLabel(stackItem.GetNode<Label>("Label"), to);
            Stack.AddChild(stackItem);
            Stack.MoveChild(stackItem, 0);

        }

        private void OnStackPlayerPopped(string from)
        {
            // Sync whole stack instead of just popping top item, as ResetEventTrigger passed to Reset() may be varied
            SyncStack();
        }

        public void SyncStack()
        {
            int diff = Stack.GetChildCount() - ParentStackPlayer.Stack.Count;
            int diffCount = Mathf.Abs(diff);
            for (int i = 0; i < diffCount; i++)
            {
                if (diff < 0)
                {
                    var stackItem = this.stackItem.Instance();
                    Stack.AddChild(stackItem);
                }
                else
                {
                    var child = Stack.GetChild(0);
                    Stack.RemoveChild(child);
                    child.QueueFree();
                }
            }
            var stack = ParentStackPlayer.Stack;
            for (int i = 0; i < stack.Count; i++)
            {
                var obj = stack[stack.Count - 1 - i]; // Descending order, to list from bottom to top in VBoxContainer
                var child = Stack.GetChild(i);
                OnSetLabel(child.GetNode<Label>("Label"), obj);
            }
        }
    }
}