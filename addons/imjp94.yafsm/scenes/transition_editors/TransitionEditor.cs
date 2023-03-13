using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.GodotCodeGenerator.Attributes;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class TransitionEditor : VBoxContainer
    {
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
                    _OnTransitionChanged(value);
                }
            }
        }

        private GDC.Array<Node> _toFree;

        public TransitionEditor()
        {
            _toFree = new GDC.Array<Node>();
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
            header.Connect("gui_input", this, nameof(_OnHeaderGuiInput));
            prioritySpinbox.Connect("value_changed", this, nameof(_OnPrioritySpinboxValueChanged));
            add.Connect("pressed", this, nameof(_OnAddPressed));
            addPopupMenu.Connect("index_pressed", this, nameof(_OnAddPopupMenuIndexPressed));

            priorityIcon.Texture = GetIcon("AnimationTrackList", "EditorIcons");

            // Manually invoke transition changed to update everything
            if (transition != null)
                _OnTransitionChanged(transition);
        }

        public override void _ExitTree()
        {
            FreeNodeFromUndoRedo(); // Managed by EditorInspector
        }

        public void _OnHeaderGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButtonEvent &&
                mouseButtonEvent.ButtonIndex == (int)ButtonList.Left
                && mouseButtonEvent.Pressed)
            {
                ToggleConditions();
            }
        }

        public void _OnPrioritySpinboxValueChanged(int val)
        {
            SetPriority(val);
        }

        public void _OnAddPressed()
        {
            Utils.PopupOnTarget(addPopupMenu, add);

        }

        public void _OnAddPopupMenuIndexPressed(__TYPE index)
        {
            var condition;
            switch (index)
            {
                case 0: // Trigger
                    condition = new Condition()


                    break;
                case 1: // Boolean
                    condition = new BooleanCondition()


                    break;
                case 2: // Integer
                    condition = new IntegerCondition()


                    break;
                case 3: // Float
                    condition = new FloatCondition()


                    break;
                case 4: // String
                    condition = new StringCondition()


                    break;
                case _:
                    GD.PushError("Unexpected Index(%d) from PopupMenu" % index);
                    break;
            }
            var editor = CreateConditionEditor(condition);
            condition.name = transition.GetUniqueName("Param");
            AddConditionEditorAction(editor, condition);

        }

        public void _OnConditionEditorRemovePressed(__TYPE editor)
        {
            RemoveConditionEditorAction(editor);

        }

        public void _OnTransitionChanged(Transition newTransition)
        {
            if (!new_transition)
            {
                return;

            }
            foreach (var condition in transition.Conditions.Values())
            {
                var editor = CreateConditionEditor(condition);
                AddConditionEditor(editor, condition);
            }
            UpdateTitle();
            UpdateConditionCount();
            UpdatePrioritySpinboxValue();

        }

        public void _OnConditionEditorAdded(__TYPE editor)
        {
            editor.undo_redo = undoRedo;
            if (!editor.remove.IsConnected("pressed", this, "_on_ConditionEditorRemove_pressed"))
            {
                editor.remove.Connect("pressed", this, "_on_ConditionEditorRemove_pressed", new Array() { editor });
            }
            transition.AddCondition(editor.condition);
            UpdateConditionCount();

        }

        public void AddConditionEditor(__TYPE editor, __TYPE condition)
        {
            conditionList.AddChild(editor);
            editor.condition = condition;// Must be assigned after enter tree, as assignment would trigger ui code
            _OnConditionEditorAdded(editor);

        }

        public void RemoveConditionEditor(__TYPE editor)
        {
            transition.RemoveCondition(editor.condition.name);
            conditionList.RemoveChild(editor);
            _toFree.Append(editor);// Freeing immediately after removal will break undo/redo
            UpdateConditionCount();

        }

        public void UpdateTitle()
        {
            from.Text = transition.From;
            to.Text = transition.To;

        }

        public void UpdateConditionCount()
        {
            var count = transition.Conditions.Size();
            conditionCountLabel.Text = GD.Str(count);
            if (count == 0)
            {
                HideConditions();
            }
            else
            {
                ShowConditions();
            }
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
            contentContainer.visible = !content_container.visible;

        }

        public __TYPE CreateConditionEditor(__TYPE condition)
        {
            var editor;
            if (condition is BooleanCondition)
            {
                editor = BoolConditionEditor.Instance();
            }
            else if (condition is IntegerCondition)
            {
                editor = IntegerConditionEditor.Instance();
            }
            else if (condition is FloatCondition)
            {
                editor = FloatConditionEditor.Instance();
            }
            else if (condition is StringCondition)
            {
                editor = StringConditionEditor.Instance();
            }
            else
            {
                editor = ConditionEditor.Instance();
            }
            return editor;

        }

        public void AddConditionEditorAction(__TYPE editor, __TYPE condition)
        {
            undoRedo.CreateAction("Add Transition Condition");
            undoRedo.AddDoMethod(this, "add_condition_editor", editor, condition);
            undoRedo.AddUndoMethod(this, "remove_condition_editor", editor);
            undoRedo.CommitAction();

        }

        public void RemoveConditionEditorAction(__TYPE editor)
        {
            undoRedo.CreateAction("Remove Transition Condition");
            undoRedo.AddDoMethod(this, "remove_condition_editor", editor);
            undoRedo.AddUndoMethod(this, "add_condition_editor", editor, editor.condition);
            undoRedo.CommitAction();

        }

        // Free nodes cached in UndoRedo stack
        public void FreeNodeFromUndoRedo()
        {
            foreach (var node in _toFree)
            {
                if (IsInstanceValid(node))
                {
                    node.QueueFree();
                }
            }
            _toFree.Clear();
            undoRedo.ClearHistory(false);// TODO: Should be handled by plugin.Gd (Temporary solution as only TransitionEditor support undo/redo)


        }
    }
}