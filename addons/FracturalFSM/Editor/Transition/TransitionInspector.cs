
using System;
using Godot;
using Fractural.Utils;

namespace Fractural.StateMachine
{
    [Tool]
    public class TransitionInspector : EditorInspectorPlugin
    {
        private UndoRedo undoRedo;
        private Texture transitionIcon;
        private PackedScene transitionEditorPrefab;

        public TransitionInspector() { }
        public TransitionInspector(UndoRedo undoRedo, Texture transitionIcon, PackedScene transitionEditorPrefab)
        {
            this.undoRedo = undoRedo;
            this.transitionIcon = transitionIcon;
            this.transitionEditorPrefab = transitionEditorPrefab;
        }

        public override bool CanHandle(Godot.Object @object) => @object is Transition;

        public override bool ParseProperty(Godot.Object @object, int type, string path, int hint, string hintText, int usage)
        {
            Transition transition = @object as Transition;
            switch (path)
            {
                case nameof(Transition.From):
                    return true;
                case nameof(Transition.To):
                    return true;
                case nameof(Transition.Conditions):
                    var transitionEditor = transitionEditorPrefab.Instance<TransitionEditor>(); // Will be freed by editor
                    transitionEditor.Construct(undoRedo, transition, transitionIcon);
                    AddCustomControl(transitionEditor);
                    return true;
                case nameof(Transition.priority):
                    return true;
            }
            return false;
        }
    }
}