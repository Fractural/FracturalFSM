
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;

namespace GodotRollbackNetcode.StateMachine
{

    [Tool]
    public class StateMachineEditorLayer : FlowChartLayer
    {
        // TODO: Finish fixi 
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

        public StateMachine stateMachine;
        public Tween tween = new Tween();

        public void _Init()
        {
            AddChild(tween);
            tween.Connect("tree_entered", this, "_on_tween_tree_entered");

        }

        public void _OnTweenTreeEntered()
        {
            tween.Start();

        }

        public void DebugUpdate(string currentState, GDC.Dictionary parameters, GDC.Dictionary localParameters)
        {
            if (stateMachine == null)
                return;

            var currentDir = new StateDirectory(currentState);

            var transitions = stateMachine.Transitions.Get(currentState, new GDC.Dictionary() { });
            if (currentDir.IsNested)
                transitions = stateMachine.Transitions.Get(currentDir.End, new GDC.Dictionary() { });

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
                    {
                        tween.InterpolateProperty(line, "self_modulate", null, color2, 1);
                    }
                    else if (line.SelfModulate == color2)
                    {
                        tween.InterpolateProperty(line, "self_modulate", null, color1, 1);
                    }
                    else if (line.SelfModulate == Colors.White)
                    {
                        tween.InterpolateProperty(line, "self_modulate", null, color2, 1);
                        // Update TransitionLine condition labels
                    }
                    foreach (Condition condition in transition.Conditions.Values)
                    {
                        if (!(condition is ValueCondition)) // Ignore trigger
                            continue;

                        var value = parameters.Get(condition.Name);
                        value = value != null ? GD.Str(value) : "?";


                        var label = line.Vbox.GetNodeOrNull(condition.Name);
                        var overrideTemplateVar = line._template_var.Get(condition.name);


                        if (overrideTemplateVar == null)
                        {

                            overrideTemplateVar = new GDC.Dictionary() { };


                            line._template_var[condition.name] = overrideTemplateVar;
                        }


                        overrideTemplateVar["value"] = GD.Str(value)


                    line.UpdateLabel();
                        // Condition label color based on comparation
                        if (condition.Compare(parameters.Get(condition.name)) || condition.Compare(localParameters.Get(condition.name)))
                        {
                            if (label.self_modulate != Color.green)
                            {
                                tween.InterpolateProperty(label, "self_modulate", null, Color.green.Lightened(0.5), 0.1);
                            }
                        }
                        else
                        {
                            if (label.self_modulate != Color.red)
                            {
                                tween.InterpolateProperty(label, "self_modulate", null, Color.red.Lightened(0.5), 0.1);
                            }
                        }
                    }
                }
            }
            tween.Start();

        }

        public async void DebugTransitOut(__TYPE from, __TYPE to)
        {
            var fromDir = StateDirectory.new(from)


        var toDir = StateDirectory.new(to)


        var fromNode = ContentNodes.GetNodeOrNull(fromDir.GetEnd());
            if (fromNode)
            {
                fromNode.self_modulate = editorComplementaryColor;
                tween.InterpolateProperty(fromNode, "self_modulate", null, Colors.White, 0.5);
            }
            var transitions = stateMachine.Transitions.Get(from, new GDC.Dictionary() { });
            if (fromDir.IsNested())
            {
                transitions = stateMachine.Transitions.Get(fromDir.GetEnd(), new GDC.Dictionary() { });
                // Fade out color of StateNode
            }
            foreach (var transition in transitions.Values())
            {
                var line = ContentLines.GetNodeOrNull("%s>%s" % [transition.from, transition.to]);
                if (line)
                {
                    line.template = "{conditionName} {conditionComparation} {conditionValue}";
                    line.UpdateLabel();
                    tween.Remove(line, "self_modulate");
                    if (transition.to == toDir.GetEnd())
                    {
                        line.self_modulate = editorComplementaryColor;
                        tween.InterpolateProperty(line, "self_modulate", null, Colors.White, 2, Tween.TRANS_EXPO, Tween.EASE_IN);
                    }
                    else
                    {
                        tween.InterpolateProperty(line, "self_modulate", null, Colors.White, 0.1);
                        // Revert color of TransitionLine condition labels
                    }
                    foreach (var condition in transition.conditions.Values())
                    {
                        if (!(condition is ValueCondition)) // Ignore trigger
                        {
                            continue;
                        }
                        var label = line.vbox.GetNodeOrNull(condition.name);
                        if (label.self_modulate != Colors.White)
                        {
                            tween.InterpolateProperty(label, "self_modulate", null, Colors.White, 0.5);
                        }
                    }
                }
            }
            if (fromDir.IsNested() && fromDir.IsExit())
            {
                // Transition from nested state
                transitions = stateMachine.Transitions.Get(fromDir.GetBase(), new GDC.Dictionary() { });
                foreach (var transition in transitions.Values())
                {
                    var line = ContentLines.GetNodeOrNull("%s>%s" % [transition.from, transition.to]);
                    if (line)
                    {
                        tween.InterpolateProperty(line, "self_modulate", null, editorComplementaryColor.Lightened(0.5), 0.5);
                    }
                }
                await ToSignal(tween, "tween_completed")


            foreach (var transition in transitions.Values())
                {
                    var line = ContentLines.GetNodeOrNull("%s>%s" % [transition.from, transition.to]);
                    if (line)
                    {
                        tween.InterpolateProperty(line, "self_modulate", null, Colors.White, 0.5);
                    }
                }
            }
            tween.Start();

        }

        public void DebugTransitIn(__TYPE from, __TYPE to)
        {
            var toDir = StateDirectory.new(to)


        var toNode = ContentNodes.GetNodeOrNull(toDir.GetEnd());
            if (toNode)
            {
                tween.InterpolateProperty(toNode, "self_modulate", null, editorComplementaryColor, 0.5);
            }
            var transitions = stateMachine.Transitions.Get(to, new GDC.Dictionary() { });
            if (toDir.IsNested())
            {
                transitions = stateMachine.Transitions.Get(toDir.GetEnd(), new GDC.Dictionary() { });
                // Change string template for current TransitionLines
            }
            foreach (var transition in transitions.Values())
            {
                var line = ContentLines.GetNodeOrNull("%s>%s" % [transition.from, transition.to]);
                line.template = "{conditionName} {conditionComparation} {conditionValue}(new GDC.Dictionary(){value})";
            }
            tween.Start();

        }

    }
}