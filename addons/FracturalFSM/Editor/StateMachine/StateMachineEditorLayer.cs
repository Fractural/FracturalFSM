
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;
using Fractural.FlowChart;

namespace Fractural.StateMachine
{
    /// <summary>
    /// Currently only provides debugging visualizatinon for state machine layers.
    /// </summary>
    [Tool]
    public class StateMachineEditorLayer : FlowChartLayer
    {
        private Color editorAccentColor = Colors.White;
        private Color editorComplementaryColor = Colors.White;

        public StateMachine StateMachine { get; set; }
        public Tween tween = new Tween();

        public StateMachineEditorLayer()
        {
            AddChild(tween);
        }

        public override void _Ready()
        {
            base._Ready();
            tween.Start();
        }

        public void Construct(Color editorAccentColor)
        {
            this.editorAccentColor = editorAccentColor;
            editorComplementaryColor = Utils.GetComplementaryColor(editorAccentColor);
        }

        #region Debug Display
        // TODO: Refactor this hacky mess it's soo coupled... :(
        public void DebugUpdate(string currentState, GDC.Dictionary parameters, GDC.Dictionary localParameters)
        {
            if (StateMachine == null)
                return;

            var currentDir = new StateDirectory(currentState);

            var transitions = StateMachine.GetNodeTransitionsDictOrNew(currentState);
            if (currentDir.IsNested)
                transitions = StateMachine.GetNodeTransitionsDictOrNew(currentDir.End);

            // Debug update all transitions for the current state.
            foreach (Transition transition in transitions.Values)
            {
                var line = ContentLines.GetNodeOrNull<TransitionLine>(TransitionLine.GetTransitionLineName(transition));
                line.DebugUpdate(tween, parameters, localParameters);
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

            // TODO: Maybe replace these with a single call to 
            // transitions = StateMachine.Transitions.Get(fromDir.End, new GDC.Dictionary());
            //
            // Not sure why the first call of StateMachine.Transitions.Get(from, new GDC.Dictionary());
            // is necesary...
            var transitions = StateMachine.GetNodeTransitionsDictOrNew(from);
            if (fromDir.IsNested)
                transitions = StateMachine.GetNodeTransitionsDictOrNew(fromDir.End);

            // Fade out color of StateNode
            foreach (Transition transition in transitions.Values)
            {
                // Transition out all the transition lines for the current state's transitions
                var line = ContentLines.GetNodeOrNull<TransitionLine>(TransitionLine.GetTransitionLineName(transition));
                bool isTransitionToEndState = transition.To == toDir.End;
                line.DebugTransitOut(tween, isTransitionToEndState, editorComplementaryColor);
            }
            if (fromDir.IsNested && fromDir.IsExit)
            {
                // Transition from nested state
                transitions = StateMachine.GetNodeTransitionsDictOrNew(fromDir.Base);
                foreach (Transition transition in transitions.Values)
                {
                    var line = ContentLines.GetNodeOrNull(TransitionLine.GetTransitionLineName(transition));
                    if (line != null)
                        tween.InterpolateProperty(line, "self_modulate", null, editorComplementaryColor.Lightened(0.5f), 0.5f);
                }

                // TODO: Maybe have to refactor this if we want to support rollback. What if we rollback while waiting for tween to complete?
                await ToSignal(tween, "tween_completed");

                foreach (Transition transition in transitions.Values)
                {
                    var line = ContentLines.GetNodeOrNull(TransitionLine.GetTransitionLineName(transition));
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
                tween.InterpolateProperty(toNode, "self_modulate", null, editorComplementaryColor, 0.5f);

            tween.Start();
        }
        #endregion
    }
}