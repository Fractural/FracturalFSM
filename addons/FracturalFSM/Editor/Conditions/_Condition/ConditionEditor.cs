using Fractural.GodotCodeGenerator.Attributes;
using Godot;
using System.Diagnostics;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class ConditionEditor : HBoxContainer
    {
        [Signal] public delegate void NewNameEntered(string newName);
        [Signal] public delegate void Removed();

        [OnReadyGet("Name")]
        private LineEdit nameEdit;
        [OnReadyGet("Remove")]
        private Button remove;

        protected UndoRedo undoRedo;

        private Condition condition;
        public Condition Condition => condition;

        public ConditionEditor() { }
        public void Construct(UndoRedo undoRedo, Condition condition)
        {
            this.undoRedo = undoRedo;
            this.condition = condition;
            Debug.Assert(condition != null, "Expected condition to not be null");
            InitializeCondition();
        }

        [OnReady]
        public void RealReady()
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

        /// <summary>
        /// Reverts the editor to display the old condition name
        /// </summary>
        public void RevertConditionName()
        {
            nameEdit.Text = condition.Name;
        }

        #region UI Wiring

        private void OnEditorRemoved()
        {
            EmitSignal(nameof(Removed));
        }

        private void OnNameEditTextEntered(string newText)
        {
            nameEdit.ReleaseFocus();
            if (Condition.Name == newText) // Avoid infinite loop
                return;
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
                return;
            RenameEditAction(nameEdit.Text);
        }

        private void OnNameEditTextChanged(string newText)
        {
            nameEdit.HintTooltip = newText;
        }

        #endregion

        #region Private Methods

        private void RenameEditAction(string newName)
        {
            var oldName = Condition.Name;
            undoRedo.CreateAction("Rename Edit Condition");
            undoRedo.AddDoMethod(this, nameof(UndoRedoRenameEdit), newName);
            undoRedo.AddUndoMethod(this, nameof(UndoRedoRenameEdit), oldName);
            undoRedo.CommitAction();
        }

        private void UndoRedoRenameEdit(string newName)
        {
            EmitSignal(nameof(NewNameEntered), newName);
        }

        /// <summary>
        /// Only called when the Condition's name is changed
        /// </summary>
        /// <param name="oldName"></param>
        /// <param name="newName"></param>
        private void OnConditionNameChanged(string oldName, string newName)
        {
            nameEdit.Text = newName;
        }

        /// <summary>
        /// Called when the condition is first set. Is overriden by 
        /// implementations of ConditionEditor to handle intiializing the edtior
        /// with newCondition's data.
        /// </summary>
        /// <param name="newCondition"></param>
        protected virtual void InitializeCondition()
        {
            Condition.Connect(nameof(Condition.NameChanged), this, nameof(OnConditionNameChanged));
            nameEdit.Text = Condition.Name;
            nameEdit.HintTooltip = nameEdit.Text;
        }
        #endregion
    }
}