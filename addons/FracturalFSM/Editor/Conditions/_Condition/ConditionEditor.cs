
using System;
using Godot;
using Fractural.GodotCodeGenerator.Attributes;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class ConditionEditor : HBoxContainer
    {
        [Signal] public delegate void Removed();

        [OnReadyGet("Name")]
        private Label nameEdit;
        [OnReadyGet("Remove")]
        private Button remove;

        protected UndoRedo undoRedo;

        private Condition condition;
        public Condition Condition
        {
            get => condition;
            set
            {
                if (Condition != value)
                {
                    Condition = value;
                    OnConditionChanged(value);
                }
            }
        }

        public void Construct(UndoRedo undoRedo)
        {
            this.undoRedo = undoRedo;
        }

        [OnReady]
        public virtual void RealReady()
        {
            nameEdit.Connect("text_entered", this, nameof(OnNameEditTextEntered));
            nameEdit.Connect("focus_entered", this, nameof(OnNameEditFocusEntered));
            nameEdit.Connect("focus_exited", this, nameof(OnNameEditFocusExited));
            nameEdit.Connect("text_changed", this, nameof(OnNameEditTextChanged));
            remove.Connect("pressed", this, nameof(OnEditorRemoved));
            SetProcessInput(false);
        }

        public override void _Input(InputEvent inputEvent)
        {
            nameEdit.TryReleaseFocusWithMouseClick(inputEvent);
        }

        public virtual bool CanHandle(Condition condition) => true;

        #region UI Wiring

        private void OnEditorRemoved()
        {
            EmitSignal(nameof(Removed));
        }

        private void OnNameEditTextEntered(string newText)
        {
            nameEdit.ReleaseFocus();
            if (Condition.Name == newText) // Avoid infinite loop
            {
                return;
            }
            RenameEditAction(newText);
        }

        private void OnNameEditFocusEntered()
        {
            SetProcessInput(true);
        }

        private void OnNameEditFocusExited()
        {
            SetProcessInput(false);
            if (Condition.Name == nameEdit.Text)
            {
                return;
            }
            RenameEditAction(nameEdit.Text);
        }

        private void OnNameEditTextChanged(string newText)
        {
            nameEdit.HintTooltip = newText;
        }

        #endregion

        #region Private Methods

        private void RenameEditAction(string newNameEdit)
        {
            var oldNameEdit = Condition.Name;
            undoRedo.CreateAction("Rename Edit Condition");
            undoRedo.AddDoMethod(this, nameof(UndoRedoRenameEdit), oldNameEdit, newNameEdit);
            undoRedo.AddUndoMethod(this, nameof(UndoRedoRenameEdit), newNameEdit, oldNameEdit);
            undoRedo.CommitAction();
        }

        private void UndoRedoRenameEdit(string fromText, string toText)
        {
            var transition = GetParent().GetParent().GetParent<TransitionEditor>().Transition;// TODO: Better way to get Transition object
            if (transition.ChangeConditionName(fromText, toText))
            {
                if (nameEdit.Text != toText) // Manually update nameEdit.text, in case called from undoRedo
                    nameEdit.Text = toText;
            }
            else
            {
                nameEdit.Text = fromText;
                GD.PushWarning($"Change Condition nameEdit From ({fromText}) To ({toText}) failed, nameEdit existed");
            }
        }

        protected virtual void OnConditionChanged(Condition newCondition)
        {
            if (newCondition != null)
            {
                nameEdit.Text = newCondition.Name;
                nameEdit.HintTooltip = nameEdit.Text;
            }
        }
        #endregion
    }
}