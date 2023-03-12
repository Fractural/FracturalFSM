
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.GodotCodeGenerator.Attributes;
using System.Collections.Generic;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class TransitionLine : FlowChartLine
    {
        [Export] public float uprightAngleRange = 10.0f;

        [OnReadyGet("MarginContainer")]
        public MarginContainer labelMargin;
        [OnReadyGet("MarginContainer/VBoxContainer")]
        public VBoxContainer conditionLabelContainer;

        public UndoRedo undoRedo;

        private Transition transition;
        public Transition Transition
        {
            get => transition;
            set
            {
                if (transition != value)
                {
                    if (transition != null)
                    {
                        if (transition.IsConnected(nameof(Transition.ConditionAdded), this, nameof(_OnTransitionConditionAdded)))
                            transition.Disconnect(nameof(Transition.ConditionAdded), this, nameof(_OnTransitionConditionAdded));
                    }
                    transition = value;
                    _OnTransitionChanged(transition);
                }
            }
        }
        public Dictionary<string, ConditionDetails> conditionNameToDetailsDict { get; private set; } = new Dictionary<string, ConditionDetails>();

        public TransitionLine()
        {
            Transition = new Transition();
        }

        public override void _Draw()
        {
            base._Draw();

            var absRectRotation = Mathf.Abs(RectRotation);
            var isFlip = absRectRotation > 90.0;
            var isUpright = absRectRotation > 90.0 - uprightAngleRange && absRectRotation < 90.0 + uprightAngleRange;
            if (isUpright)
            {
                var xOffset = labelMargin.RectSize.x / 2f;
                var yOffset = -labelMargin.RectSize.y;

                labelMargin.RectRotation = -RectRotation;

                if (RectRotation > 0)
                {
                    labelMargin.RectPosition = new Vector2((RectSize.x - xOffset) / 2, 0);
                }
                else
                {
                    labelMargin.RectPosition = new Vector2((RectSize.x + xOffset) / 2, yOffset * 2);
                }
            }
            else
            {
                var xOffset = labelMargin.RectSize.x;
                var yOffset = -labelMargin.RectSize.y;

                if (isFlip)
                {
                    labelMargin.RectRotation = 180;
                    labelMargin.RectPosition = new Vector2((RectSize.x + xOffset) / 2, 0);
                }
                else
                {
                    labelMargin.RectRotation = 0;
                    labelMargin.RectPosition = new Vector2((RectSize.x - xOffset) / 2, yOffset);
                }
            }
        }

        /// <summary>
        /// Update overlay text
        /// </summary>
        public void UpdateLabel()
        {
            if (transition != null)
            {
                foreach (Label label in conditionLabelContainer.GetChildren())
                {
                    if (!transition.Conditions.Contains(label.Name))
                    {
                        conditionLabelContainer.RemoveChild(label);
                        label.QueueFree();
                    }
                }
                foreach (Condition condition in transition.Conditions.Values)
                {
                    var label = conditionLabelContainer.GetNodeOrNull<Label>(condition.Name);
                    if (label == null)
                    {
                        label = new Label();
                        label.Align = Label.AlignEnum.Center;
                        label.Name = condition.Name;
                        conditionLabelContainer.AddChild(label);
                    }
                    if (condition is ValueCondition valueCondition)
                    {
                        var details = valueCondition.ConditionDetails;
                        if (conditionNameToDetailsDict.ContainsKey(condition.Name))
                            label.Text = label.Text.Format(overrideTemplateVar);
                        else label.Text = $"{valueCondition.Name} {valueCondition.GetComparationSymbol()} {}";
                    }
                    else
                        label.Text = condition.Name;
                }
            }
            Update();

        }

        public void _OnTransitionChanged(__TYPE newTransition)
        {
            if (!is_inside_tree())
            {
                return;

            }
            if (newTransition)
            {
                newTransition.Connect("condition_added", this, "_on_transition_condition_added")


            newTransition.Connect("condition_removed", this, "_on_transition_condition_removed")


            foreach (var condition in newTransition.conditions.Values())
                {
                    condition.Connect("name_changed", this, "_on_condition_name_changed");
                    condition.Connect("display_string_changed", this, "_on_condition_display_string_changed");
                }
            }
            UpdateLabel();

        }

        public void _OnTransitionConditionAdded(__TYPE condition)
        {
            condition.Connect("name_changed", this, "_on_condition_name_changed");
            condition.Connect("display_string_changed", this, "_on_condition_display_string_changed");
            UpdateLabel();

        }

        public void _OnTransitionConditionRemoved(__TYPE condition)
        {
            condition.Disconnect("name_changed", this, "_on_condition_name_changed");
            condition.Disconnect("display_string_changed", this, "_on_condition_display_string_changed");
            UpdateLabel();

        }

        public void _OnConditionNameChanged(__TYPE from, __TYPE to)
        {
            var label = conditionLabelContainer.GetNodeOrNull(from);
            if (label)
            {
                label.name = to;
            }
            UpdateLabel();

        }

        public void _OnConditionDisplayStringChanged(__TYPE displayString)
        {
            UpdateLabel();

        }


    }

}