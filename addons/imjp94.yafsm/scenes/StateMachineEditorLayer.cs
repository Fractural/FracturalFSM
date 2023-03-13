
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public class StateMachineEditorLayer : FlowChartLayer
    {
        private Color editorAccentColor = Colors.White;
        public Color EditorAccentColor
        {
            get => editorAccentColor;
            set
            {
                editorAccentColor = value;
                editorComplementaryColor = Utils.GetComplementaryColor(value);
            }
        }
        public Color editorComplementaryColor = Colors.White;

        public StateMachine StateMachine { get; set; }
        public Tween tween = new Tween();

        public StateMachineEditorLayer()
        {
            AddChild(tween);
            tween.Connect("tree_entered", this, "_on_tween_tree_entered");
        }

        public void _OnTweenTreeEntered()
        {
            tween.Start();
        }

        // TODO: Refactor this hacky mess it's soo coupled... :(
        public void DebugUpdate(string currentState, GDC.Dictionary parameters, GDC.Dictionary localParameters)
        {
            if (StateMachine == null)
                return;

            var currentDir = new StateDirectory(currentState);

            var transitions = StateMachine.Transitions.Get(currentState, new GDC.Dictionary() { });
            if (currentDir.IsNested)
                transitions = StateMachine.Transitions.Get(currentDir.End, new GDC.Dictionary() { });

            foreach (Transition transition in transitions.Values)
            {
                var line = ContentLines.GetNodeOrNull<TransitionLine>($"{transition.From}>{transition.To}");
                if (line != null)
                {
                    // Blinking alpha of TransitionLine
                    var color1 = Colors.White;
                    color1.a = 0.1f;
                    var color2 = Colors.White;
                    color2.a = 0.5f;
                    if (line.SelfModulate == color1)
                        tween.InterpolateProperty(line, "self_modulate", null, color2, 1);
                    else if (line.SelfModulate == color2)
                        tween.InterpolateProperty(line, "self_modulate", null, color1, 1);
                    else if (line.SelfModulate == Colors.White)
                        tween.InterpolateProperty(line, "self_modulate", null, color2, 1);

                    // Update TransitionLine condition labels
                    foreach (Condition condition in transition.Conditions.Values)
                    {
                        if (!(condition is ValueCondition valueCondition)) // Ignore trigger
                            continue;

                        var value = parameters.Get<object>(condition.Name);
                        value = value != null ? value : "?";

                        var label = line.GetLabelForCondition(condition.Name);
                        var overrideDetails = line.ConditionDisplayDetailOverrides.GetValue(valueCondition.Name) as ValueConditionDisplayDetails;

                        if (overrideDetails == null)
                        {
                            overrideDetails = new ValueConditionDisplayDetails();
                            line.ConditionDisplayDetailOverrides[valueCondition.Name] = overrideDetails;
                        }

                        overrideDetails.Value = GD.Str(value);
                        line.UpdateLabel();
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
            tween.Start();
        }

        public async void DebugTransitOut(string from, string to)
        {
            var fromDir = new StateDirectory(from);
            var toDir = new StateDirectory(to);

            var fromNode = ContentNodes.GetNodeOrNull<Control>(fromDir.End);
            if (fromNode != null)
            {
                fromNode.SelfModulate = editorComplementaryColor;
                tween.InterpolateProperty(fromNode, "self_modulate", null, Colors.White, 0.5f);
            }
            var transitions = StateMachine.Transitions.Get(from, new GDC.Dictionary());
            if (fromDir.IsNested)
            {
                transitions = StateMachine.Transitions.Get(fromDir.End, new GDC.Dictionary());
                // Fade out color of StateNode
            }
            foreach (Transition transition in transitions.Values)
            {
                var line = ContentLines.GetNodeOrNull<TransitionLine>(TransitionLine.GetUniqueNodeName(transition));
                if (line != null)
                {
                    line.UpdateLabel();
                    tween.Remove(line, "self_modulate");
                    if (transition.To == toDir.End)
                    {
                        line.SelfModulate = editorComplementaryColor;
                        tween.InterpolateProperty(line, "self_modulate", null, Colors.White, 2, Tween.TransitionType.Expo, Tween.EaseType.In);
                    }
                    else
                    {
                        tween.InterpolateProperty(line, "self_modulate", null, Colors.White, 0.1f);
                    }
                    // Revert color of TransitionLine condition labels
                    foreach (Condition condition in transition.Conditions.Values)
                    {
                        if (!(condition is ValueCondition valueCondition)) // Ignore trigger
                            continue;

                        var label = line.GetLabelForCondition(condition.Name);
                        if (label.SelfModulate != Colors.White)
                        {
                            tween.InterpolateProperty(label, "self_modulate", null, Colors.White, 0.5f);
                        }
                    }
                }
            }
            if (fromDir.IsNested && fromDir.IsExit)
            {
                // Transition from nested state
                transitions = StateMachine.Transitions.Get(fromDir.Base, new GDC.Dictionary());
                foreach (Transition transition in transitions.Values)
                {
                    var line = ContentLines.GetNodeOrNull(TransitionLine.GetUniqueNodeName(transition));
                    if (line != null)
                        tween.InterpolateProperty(line, "self_modulate", null, editorComplementaryColor.Lightened(0.5f), 0.5f);
                }

                // TODO: Maybe have to refactor this if we want to support rollback. What if we rollback while waiting for tween to complete?
                await ToSignal(tween, "tween_completed");

                foreach (Transition transition in transitions.Values)
                {
                    var line = ContentLines.GetNodeOrNull(TransitionLine.GetUniqueNodeName(transition));
                    if (line != null)
                        tween.InterpolateProperty(line, "self_modulate", null, Colors.White, 0.5f);
                }
            }
            tween.Start();
        }

        public void DebugTransitIn(string from, string to)
        {
            var toDir = new StateDirectory(to);
            var toNode = ContentNodes.GetNodeOrNull(toDir.End);
            if (toNode != null)
                tween.InterpolateProperty(toNode, "self_modulate", null, editorComplementaryColor, 0.5);

            var transitions = StateMachine.Transitions.Get(to, new GDC.Dictionary());
            if (toDir.IsNested)
                transitions = StateMachine.Transitions.Get(toDir.End, new GDC.Dictionary());

            // Change string template for current TransitionLines
            foreach (Transition transition in transitions.Values)
            {
                var line = ContentLines.GetNodeOrNull<TransitionLine>(TransitionLine.GetUniqueNodeName(transition));
                line.Template = "{conditionName} {conditionComparation} {conditionValue}({value})";
                // TODO: Reimplment template b/c that's most flexible way of controlling output for label.
            }
            tween.Start();
        }
    }
}