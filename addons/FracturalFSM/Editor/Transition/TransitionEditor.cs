using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Utils;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class TransitionEditor : VBoxContainer
    {
        [Export]
        private Resource[] conditionProcessors;

        [OnReadyGet("HeaderContainer/Header")]
        private HBoxContainer header;
        [OnReadyGet("HeaderContainer/Header/Title")]
        private HBoxContainer title;
        [OnReadyGet("HeaderContainer/Header/Title/Icon")]
        private TextureRect titleIcon;
        [OnReadyGet("HeaderContainer/Header/Title/From")]
        private Label from;
        [OnReadyGet("HeaderContainer/Header/Title/To")]
        private Label to;
        [OnReadyGet("HeaderContainer/Header/ConditionCount/Icon")]
        private TextureRect conditionCountIcon;
        [OnReadyGet("HeaderContainer/Header/ConditionCount/Label")]
        private Label conditionCountLabel;
        [OnReadyGet("HeaderContainer/Header/Priority/Icon")]
        private TextureRect priorityIcon;
        [OnReadyGet("HeaderContainer/Header/Priority/SpinBox")]
        private SpinBox prioritySpinbox;
        [OnReadyGet("HeaderContainer/Header/HBoxContainer/Add")]
        private Button add;
        [OnReadyGet("HeaderContainer/Header/HBoxContainer/Add/PopupMenu")]
        private PopupMenu addPopupMenu;
        [OnReadyGet("MarginContainer")]
        private MarginContainer contentContainer;
        [OnReadyGet("MarginContainer/Conditions")]
        private VBoxContainer conditionList;

        private UndoRedo undoRedo;

        private Transition transition;
        public Transition Transition
        {
            get => Transition;
            set
            {
                if (transition != value)
                {
                    transition = value;
                    OnTransitionChanged(value);
                }
            }
        }

        private GDC.Array<Node> toFree;

        public TransitionEditor()
        {
            toFree = new GDC.Array<Node>();
        }

        public void Construct(UndoRedo undoRedo, Transition transition, Texture transitionIcon)
        {
            this.undoRedo = undoRedo;
            this.transition = transition;
            this.titleIcon.Texture = transitionIcon;
        }

        [OnReady]
        public void RealReady()
        {
            int nextFreeId = 0;
            foreach (ConditionProcessor processor in conditionProcessors)
            {
                processor.ID = nextFreeId;
                addPopupMenu.AddItem(processor.ConditionName, nextFreeId);
                nextFreeId++;
            }

            header.Connect("gui_input", this, nameof(OnHeaderGuiInput));
            prioritySpinbox.Connect("value_changed", this, nameof(OnPrioritySpinboxValueChanged));
            add.Connect("pressed", this, nameof(OnAddPressed));
            addPopupMenu.Connect("index_pressed", this, nameof(OnAddPopupMenuIndexPressed));

            priorityIcon.Texture = GetIcon("AnimationTrackList", "EditorIcons");

            // Manually invoke transition changed to update everything
            if (transition != null)
                OnTransitionChanged(transition);
        }

        public override void _ExitTree()
        {
            FreeNodeFromUndoRedo(); // Managed by EditorInspector
        }

        public void OnHeaderGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButtonEvent &&
                mouseButtonEvent.ButtonIndex == (int)ButtonList.Left
                && mouseButtonEvent.Pressed)
            {
                ToggleConditions();
            }
        }

        public void OnPrioritySpinboxValueChanged(int val)
        {
            SetPriority(val);
        }

        public void OnAddPressed()
        {
            Utils.PopupOnTarget(addPopupMenu, add);
        }

        public void OnAddPopupMenuIndexPressed(int index)
        {
            Condition condition = null;
            foreach (ConditionProcessor processor in conditionProcessors)
            {
                if (processor.ID == index)
                {
                    condition = processor.CreateConditionInstance();
                    break;
                }
            }
            if (condition == null)
            {
                GD.PushError($"Unexpected Index({index}) from PopupMenu");
                return;
            }
            var editor = CreateConditionEditor(condition);
            if (editor == null)
                return;
            condition.Name = transition.GetUniqueName("Param");
            AddConditionEditorAction(editor, condition);
        }

        public void OnConditionEditorRemoved(ConditionEditor editor)
        {
            RemoveConditionEditorAction(editor);
        }

        public void OnTransitionChanged(Transition newTransition)
        {
            if (newTransition == null)
                return;

            foreach (Condition condition in transition.Conditions.Values)
            {
                var editor = CreateConditionEditor(condition);
                if (editor == null)
                    return;
                AddConditionEditor(editor, condition);
            }
            UpdateTitle();
            UpdateConditionCount();
            UpdatePrioritySpinboxValue();

        }

        public void OnConditionEditorAdded(ConditionEditor editor)
        {
            editor.Construct(undoRedo);
            if (!editor.IsConnected(nameof(ConditionEditor.Removed), this, nameof(OnConditionEditorRemoved)))
                editor.Connect(nameof(ConditionEditor.Removed), this, nameof(OnConditionEditorRemoved), GDUtils.GDParams(editor));

            transition.AddCondition(editor.Condition);
            UpdateConditionCount();
        }

        public void AddConditionEditor(ConditionEditor editor, Condition condition)
        {
            conditionList.AddChild(editor);
            editor.Condition = condition; // Must be assigned after enter tree, as assignment would trigger ui code
            OnConditionEditorAdded(editor);
        }

        public void RemoveConditionEditor(ConditionEditor editor)
        {
            transition.RemoveCondition(editor.Condition.Name);
            conditionList.RemoveChild(editor);
            toFree.Add(editor); // Freeing immediately after removal will break undo/redo
            UpdateConditionCount();
        }

        public void UpdateTitle()
        {
            from.Text = transition.From;
            to.Text = transition.To;
        }

        public void UpdateConditionCount()
        {
            var count = transition.Conditions.Count;
            conditionCountLabel.Text = GD.Str(count);
            if (count == 0)
                HideConditions();
            else
                ShowConditions();
        }

        public void UpdatePrioritySpinboxValue()
        {
            prioritySpinbox.Value = transition.priority;
            prioritySpinbox.Apply();
        }

        public void SetPriority(int value)
        {
            transition.priority = value;
        }

        public void ShowConditions()
        {
            contentContainer.Visible = true;
        }

        public void HideConditions()
        {
            contentContainer.Visible = false;
        }

        public void ToggleConditions()
        {
            contentContainer.Visible = !contentContainer.Visible;
        }

        public ConditionEditor CreateConditionEditor(Condition condition)
        {
            foreach (ConditionProcessor processor in conditionProcessors)
            {
                if (processor.CanHandle(condition))
                    return processor.ConditionEditorPrefab.Instance<ConditionEditor>();
            }
            GD.PushError($"TransitionEditor could not process condition of type \"{condition.GetType().Name}\", did you add a ConditionProcessor for this type?");
            return null;
        }

        public void AddConditionEditorAction(ConditionEditor editor, Condition condition)
        {
            undoRedo.CreateAction("Add Transition Condition");
            undoRedo.AddDoMethod(this, "add_condition_editor", editor, condition);
            undoRedo.AddUndoMethod(this, "remove_condition_editor", editor);
            undoRedo.CommitAction();

        }

        public void RemoveConditionEditorAction(ConditionEditor editor)
        {
            undoRedo.CreateAction("Remove Transition Condition");
            undoRedo.AddDoMethod(this, "remove_condition_editor", editor);
            undoRedo.AddUndoMethod(this, "add_condition_editor", editor, editor.Condition);
            undoRedo.CommitAction();

        }

        /// <summary>
        /// Free nodes cached in UndoRedo stack
        /// </summary>
        public void FreeNodeFromUndoRedo()
        {
            foreach (var node in toFree)
            {
                if (IsInstanceValid(node))
                {
                    node.QueueFree();
                }
            }
            toFree.Clear();
            undoRedo.ClearHistory(false);// TODO: Should be handled by plugin.Gd (Temporary solution as only TransitionEditor support undo/redo)
        }
    }
}