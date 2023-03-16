
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.GodotCodeGenerator.Attributes;
using System.Collections.Generic;
using Fractural.Utils;
using Fractural.Flowchart;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class TransitionLine : FlowchartLine
    {
        [Export] private float uprightAngleRange = 10.0f;

        [OnReadyGet("MarginContainer")]
        private MarginContainer labelMargin;
        [OnReadyGet("MarginContainer/VBoxContainer")]
        private VBoxContainer conditionLabelContainer;

        private Transition transition = CSharpScript<Transition>.New();
        public Transition Transition
        {
            get => transition;
            set
            {
                if (transition != value)
                {
                    if (transition != null)
                    {
                        transition.TryDisconnect(nameof(Transition.ConditionAdded), this, nameof(OnTransitionConditionAdded));
                    }
                    transition = value;
                    OnTransitionChanged(transition);
                }
            }
        }

        /// <summary>
        /// Maps condition names to a ConditionDetail object for that condition.
        /// This allows you to add to the display string of a the condition.
        /// This is used by the debugger to visualize a remote StateMachine when the game is running.
        /// </summary>
        public Dictionary<string, ConditionExtraDetail> ConditionExtraDetails { get; private set; } = new Dictionary<string, ConditionExtraDetail>();
        private bool conditionVisibility = true;
        public bool ConditionVisibility
        {
            get => conditionVisibility;
            set
            {
                conditionVisibility = value;
                conditionLabelContainer.Visible = value;
            }
        }

        public override void _Draw()
        {
            base._Draw();

            var absRectRotation = Mathf.Abs(RectRotation);
            var isFlip = absRectRotation > 90.0;
            // We are +/- upRightAngleRange from 90 degrees (which is vertical)
            var isUpright = absRectRotation > (90.0 - uprightAngleRange) && absRectRotation < (90.0 + uprightAngleRange);
            if (isUpright)
            {
                labelMargin.RectRotation = -RectRotation;

                // Because we're rotated 90 degrees:
                // - x = vertical
                // - y = horizontal

                if (RectRotation > 0)
                {
                    // RectSize = line's rect size
                    // RectSize.x = line width
                    // RectSize.x / 2 = half of line width

                    // We are pointing up, so show on right
                    labelMargin.RectPosition = new Vector2((RectSize.x - labelMargin.RectSize.y) / 2, 0);
                }
                else
                {
                    // We are pointing down, so show on left
                    // -labelMargin.RectSize.x moves the entire box over to the left
                    labelMargin.RectPosition = new Vector2((RectSize.x + labelMargin.RectSize.y) / 2, -labelMargin.RectSize.x);
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
                    GD.Print("\tTry adding label for " + condition.DisplayString());
                    Label label = conditionLabelContainer.GetNodeOrNull<Label>(condition.Name);
                    if (label == null)
                    {
                        label = new Label();
                        label.Align = Label.AlignEnum.Center;
                        label.Name = condition.Name;
                        conditionLabelContainer.AddChild(label);
                    }
                    string valueDetails = condition.DisplayString();

                    if (ConditionExtraDetails.TryGetValue(condition.Name, out ConditionExtraDetail moreDetails))
                        valueDetails += moreDetails.DisplayString();

                    label.Text = valueDetails;
                }
            }
            // Force the label margin container to be the min size
            labelMargin.RectSize = Vector2.Zero;
            Update();
        }

        public Label GetLabelForCondition(string conditionName)
        {
            return conditionLabelContainer.GetNodeOrNull<Label>(conditionName);
        }

        private void OnTransitionChanged(Transition newTransition)
        {
            GD.Print("Transition changed");
            if (!IsInsideTree())
                return;

            GD.Print("Transition changed 2");
            if (newTransition != null)
            {
                GD.Print("Transition changed 3");
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
            GD.Print("Added condition, updating label");
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
        /// Used for looking up the transition line of a given transition using GetNode
        /// </summary>
        /// <param name="transition"></param>
        /// <returns></returns>
        public static string GetTransitionLineName(Transition transition) => Flowchart.Flowchart.GetFlowchartLineName(transition.From, transition.To);

        #region Debug Display
        public void DebugUpdate(Tween tween, GDC.Dictionary parameters, GDC.Dictionary localParameters)
        {
            // Blinking alpha of TransitionLine
            var color1 = Colors.White;
            color1.a = 0.1f;
            var color2 = Colors.White;
            color2.a = 0.5f;
            if (this.SelfModulate == color1)
                tween.InterpolateProperty(this, "self_modulate", null, color2, 1);
            else if (this.SelfModulate == color2)
                tween.InterpolateProperty(this, "self_modulate", null, color1, 1);
            else if (this.SelfModulate == Colors.White)
                tween.InterpolateProperty(this, "self_modulate", null, color2, 1);

            // Update TransitionLine condition labels
            foreach (Condition condition in transition.Conditions.Values)
            {
                if (condition == null) // Ignore trigger
                    continue;

                if (condition is ValueCondition valueCondition)
                {
                    var paramValue = parameters.Get<object>(condition.Name);
                    paramValue = paramValue != null ? paramValue : "?";

                    var label = GetLabelForCondition(condition.Name);
                    var extraDetails = ConditionExtraDetails.GetValue(valueCondition.Name) as ValueConditionExtraDetail;

                    if (extraDetails == null)
                    {
                        extraDetails = new ValueConditionExtraDetail();
                        this.ConditionExtraDetails[valueCondition.Name] = extraDetails;
                    }

                    extraDetails.ParameterValue = GD.Str(paramValue);
                    this.UpdateLabel();

                    // Condition label color based on comparation with parameter values -- green if comparation is true, red if it's false
                    if (valueCondition.Compare(parameters.Get<Condition>(valueCondition.Name)) || valueCondition.Compare(localParameters.Get<Condition>(valueCondition.Name)))
                    {
                        if (label.SelfModulate != Colors.Green)
                            tween.InterpolateProperty(label, "self_modulate", null, Colors.Green.Lightened(0.5f), 0.1f);
                    }
                    else
                    {
                        if (label.SelfModulate != Colors.Red)
                            tween.InterpolateProperty(label, "self_modulate", null, Colors.Red.Lightened(0.5f), 0.1f);
                    }
                }
            }
        }

        public void DebugTransitOut(Tween tween, bool isTransitionToEndState, Color isTransitionToEndStateColor)
        {
            ConditionExtraDetails.Clear();
            UpdateLabel();
            tween.Remove(this, "self_modulate");
            if (isTransitionToEndState)
            {
                // If the condition is the one that activated and caused this transit out to occur in the first place,
                // then we color it with the special isTransitionToEndStateColor to make the transitioning visible in the editor.
                SelfModulate = isTransitionToEndStateColor;
                tween.InterpolateProperty(this, "self_modulate", null, Colors.White, 2, Tween.TransitionType.Expo, Tween.EaseType.In);
            }
            else
                tween.InterpolateProperty(this, "self_modulate", null, Colors.White, 0.1f);

            // Revert color of TransitionLine condition labels
            foreach (Condition condition in transition.Conditions.Values)
            {
                if (condition == null) // Ignore trigger
                    continue;

                var label = GetLabelForCondition(condition.Name);
                if (label.SelfModulate != Colors.White)
                    tween.InterpolateProperty(label, "self_modulate", null, Colors.White, 0.5f);
            }
        }
        #endregion
    }
}