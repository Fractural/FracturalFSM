
using System;
using Godot;
using Fractural.GodotCodeGenerator.Attributes;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public abstract partial class ValueConditionEditor<T> : ValueConditionEditor
    {
        public ValueCondition<T> TypedValueCondition
        {
            get => ValueCondition as ValueCondition<T>;
            set => ValueCondition = value;
        }
        protected override void OnValueChanged(object newValue) => OnTypedValueChanged((T)newValue);
        protected abstract void OnTypedValueChanged(T newValue);
    }

    [Tool]
    public abstract partial class ValueConditionEditor : ConditionEditor
    {
        [OnReadyGet("Comparation")]
        private Button comparationButton;
        [OnReadyGet("Comparation/PopupMenu")]
        private PopupMenu comparationPopupMenu;

        public ValueCondition ValueCondition { get => Condition as ValueCondition; set => Condition = value; }

        public override void RealReady()
        {
            comparationButton.Connect("pressed", this, nameof(OnComparationButtonPressed));
            comparationPopupMenu.Connect("id_pressed", this, nameof(OnComparationPopupMenuIdPressed));
        }

        private void OnComparationButtonPressed()
        {
            Utils.PopupOnTarget(comparationPopupMenu, comparationButton);
        }

        private void OnComparationPopupMenuIdPressed(int id)
        {
            ChangeComparationAction(id);
        }

        protected override void OnConditionChanged(Condition newCondition)
        {
            base.OnConditionChanged(newCondition);
            if (newCondition != null)
                comparationButton.Text = comparationPopupMenu.GetItemText((int)ValueCondition.Comparation);
        }

        private void ChangeComparationAction(int id)
        {
            var from = ValueCondition.Comparation;
            var to = id;
            undoRedo.CreateAction("Change Condition Comparation");
            undoRedo.AddDoMethod(this, nameof(UndoRedoChangeComparation), to);
            undoRedo.AddUndoMethod(this, nameof(UndoRedoChangeComparation), from);
            undoRedo.CommitAction();
        }

        private void UndoRedoChangeComparation(int id)
        {
            if (id > Enum.GetValues(typeof(ValueCondition.ComparationType)).Length - 1)
            {
                GD.PushError($"Unexpected Id({id}) from PopupMenu");
                return;
            }
            ValueCondition.Comparation = (ValueCondition.ComparationType)id;
            comparationButton.Text = comparationPopupMenu.GetItemText(id);
        }

        public void ChangeValueAction(object from, object to)
        {
            if (from == to)
            {
                return;
            }
            undoRedo.CreateAction("Change Condition Value");
            undoRedo.AddDoMethod(this, nameof(UndoRedoChangeValue), to);
            undoRedo.AddUndoMethod(this, nameof(UndoRedoChangeValue), from);
            undoRedo.CommitAction();
        }

        private void UndoRedoChangeValue(object value)
        {
            if (ValueCondition.Value != value)
            {
                ValueCondition.Value = value;
                OnValueChanged(value);
            }
        }

        protected abstract void OnValueChanged(object newValue);
    }
}