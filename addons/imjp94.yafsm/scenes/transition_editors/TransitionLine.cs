
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
        [Export] private float uprightAngleRange = 10.0f;

        [OnReadyGet("MarginContainer")]
        private MarginContainer labelMargin;
        [OnReadyGet("MarginContainer/VBoxContainer")]
        private VBoxContainer conditionLabelContainer;

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
                        if (transition.IsConnected(nameof(Transition.ConditionAdded), this, nameof(OnTransitionConditionAdded)))
                            transition.Disconnect(nameof(Transition.ConditionAdded), this, nameof(OnTransitionConditionAdded));
                    }
                    transition = value;
                    OnTransitionChanged(transition);
                }
            }
        }

        /// <summary>
        /// Maps condition names to a ConditionDetail object for that condition.
        /// This allows you to override the actual ConditionDetails of the condition with your own ConditionDetails.
        /// This is used by the debugger to visualize a remote StateMachine when the game is running.
        /// </summary>
        public Dictionary<string, ConditionDisplayDetails> ConditionDisplayDetailOverrides { get; private set; } = new Dictionary<string, ConditionDisplayDetails>();

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
                        var valueDetails = ValueConditionDisplayDetails.From(valueCondition);

                        if (ConditionDisplayDetailOverrides.TryGetValue(condition.Name, out ConditionDisplayDetails newDetails)
                            && newDetails is ValueConditionDisplayDetails newValueDetails)
                            valueDetails.CopySetValuesFrom(newValueDetails);

                        label.Text = valueDetails.DisplayString();
                    }
                    else
                        label.Text = condition.Name;
                }
            }
            Update();
        }

        public Label GetLabelForCondition(string conditionName)
        {
            return conditionLabelContainer.GetNodeOrNull<Label>(conditionName);
        }

        private void OnTransitionChanged(Transition newTransition)
        {
            if (!IsInsideTree())
                return;

            if (newTransition != null)
            {
                newTransition.Connect(nameof(Transition.ConditionAdded), this, nameof(OnTransitionConditionAdded));
                newTransition.Connect(nameof(Transition.ConditionRemoved), this, nameof(OnTransitionConditionRemoved));

                foreach (Condition condition in newTransition.Conditions.Values)
                {
                    condition.Connect(nameof(Condition.NameChanged), this, nameof(OnConditionNameChanged));
                    condition.Connect(nameof(Condition.DisplayStringChanged), this, nameof(OnConditionDisplayStringChanged));
                }
            }
            UpdateLabel();
        }

        #region Signal Listeners
        private void OnTransitionConditionAdded(Condition condition)
        {
            condition.Connect(nameof(Condition.NameChanged), this, nameof(OnConditionNameChanged));
            condition.Connect(nameof(Condition.DisplayStringChanged), this, nameof(OnConditionDisplayStringChanged));
            UpdateLabel();
        }

        private void OnTransitionConditionRemoved(Condition condition)
        {
            condition.Disconnect(nameof(Condition.NameChanged), this, nameof(OnConditionNameChanged));
            condition.Disconnect(nameof(Condition.DisplayStringChanged), this, nameof(OnConditionDisplayStringChanged));
            UpdateLabel();
        }

        private void OnConditionNameChanged(string from, string to)
        {
            var label = conditionLabelContainer.GetNodeOrNull<Label>(from);
            if (label != null)
                label.Name = to; // We want label name to equal conditon name for lookups using GetNode

            UpdateLabel();
        }

        private void OnConditionDisplayStringChanged(string displayString)
        {
            UpdateLabel();
        }
        #endregion

        /// <summary>
        /// Used for looking up the transition line using GetNode
        /// </summary>
        /// <param name="transitionLine"></param>
        /// <returns></returns>
        public static string GetUniqueNodeName(TransitionLine transitionLine) => GetUniqueNodeName(transitionLine.transition);
        public static string GetUniqueNodeName(Transition transition) => $"{transition.From}>{transition.To}";
    }
}