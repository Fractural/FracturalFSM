
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Fractural.Flowchart;

namespace Fractural.StateMachine
{
    [CSharpScript]
    [Tool]
    public class StateMachine : State
    {
        [Signal] delegate void TransitionAdded(Transition transition);// Transition added
        [Signal] delegate void TransitionRemoved(Transition transition);// Transition removed

        /// <summary>
        /// States within this StateMachine, keyed by State.Name
        /// [State.Name] = State
        /// </summary>
        [Export]
        public GDC.Dictionary States { get; set; } = new GDC.Dictionary();

        /// <summary>
        /// Transitions from this state, keyed by Transition.To
        /// [State.Name] = [Transition.To] = Transition
        /// </summary>
        [Export]
        private GDC.Dictionary transitions = new GDC.Dictionary();

        #region Transitions Accessors
        public Transition GetTransition(ConnectionPair pair)
        {
            return transitions.Get<Transition>($"{pair.From}.{pair.To}");
        }

        public Transition GetTransition(string fromNode, string toNode)
        {
            return transitions.Get<Transition>($"{fromNode}.{toNode}");
        }

        public IList<Transition> GetNodeTransitions(string fromNode)
        {
            var transitionsDict = GetNodeTransitionsDict(fromNode);
            return new List<Transition>(transitionsDict.Values.Cast<Transition>());
        }

        public GDC.Dictionary GetNodeTransitionsDict(string fromNode)
        {
            return transitions.Get<GDC.Dictionary>(fromNode);
        }

        public GDC.Dictionary GetNodeTransitionsDictOrNew(string fromNode)
        {
            var result = transitions.Get<GDC.Dictionary>(fromNode);
            if (result == null)
                return new GDC.Dictionary();
            return result;
        }
        #endregion

        public StateMachine() : this("", null, null) { }
        public StateMachine(string name = "", GDC.Dictionary transitions = null, GDC.Dictionary states = null) : base(name)
        {
            if (transitions != null)
                this.transitions = transitions;
            if (states != null)
                States = states;
        }

        /// <summary>
        /// Attempt to transit with global/local parameters, where localParams override params
        /// </summary>
        /// <param name="currentState"></param>
        /// <param name="transitParams"></param>
        /// <param name="localParams"></param>
        /// <returns>Name of the next state</returns>
        public string Transit(string currentState, GDC.Dictionary transitParams = null, GDC.Dictionary localParams = null)
        {
            if (transitParams == null) transitParams = new GDC.Dictionary();
            if (localParams == null) localParams = new GDC.Dictionary();

            var nestedStates = currentState.Split("/");
            var isNested = nestedStates.Length > 1;
            var endStateMachine = this;
            string basePath = "";
            for (int i = 0; i < nestedStates.Length - 1; i++) // Ignore last one, to get its parent StateMachine
            {
                var state = nestedStates[i];
                // Construct absolute base path
                basePath = JoinPath(basePath, state);
                if (endStateMachine != this)
                    endStateMachine = endStateMachine.States.Get<StateMachine>(state);
                else
                    endStateMachine = States.Get<StateMachine>(state); // First level state
            }
            // Nested StateMachine in Exit state
            if (isNested)
            {
                var isNestedExit = nestedStates[nestedStates.Length - 1] == ExitState;
                if (isNestedExit)
                {
                    // Normalize path to transit again with parent of endStateMachine
                    string endStateMachineParentPath = "";
                    for (int i = 0; i < nestedStates.Length - 2; i++) // Ignore last two State(which is endStateMachine/end_state
                        endStateMachineParentPath = JoinPath(endStateMachineParentPath, nestedStates[i]);

                    var endStateMachineParent = GetState(endStateMachineParentPath) as StateMachine;
                    var normalizedCurrentState = endStateMachine.Name;
                    var nextState = endStateMachineParent.Transit(normalizedCurrentState, transitParams);
                    if (nextState != null)
                        // Construct next state into absolute path
                        nextState = JoinPath(endStateMachineParentPath, nextState);

                    return nextState;

                }
            }
            // Transit with current running nested state machine
            var fromTransitions = endStateMachine.transitions.Get<GDC.Dictionary>(nestedStates[nestedStates.Length - 1]);
            if (fromTransitions != null)
            {
                var fromTransitionsArray = fromTransitions.Values.Cast<Transition>().ToList();
                fromTransitionsArray.Sort();

                foreach (var transition in fromTransitionsArray)
                {
                    var nextState = transition.Transit(transitParams, localParams);
                    if (nextState != null)
                    {
                        if (endStateMachine.States.Get<State>(nextState) is StateMachine)
                            // Next state is a StateMachine, return entry state of the state machine in absolute path
                            nextState = JoinPath(basePath, nextState, State.EntryState);
                        else
                            // Construct next state into absolute path
                            nextState = JoinPath(basePath, nextState);

                        return nextState;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get state from absolute path, for exmaple, "path/to/state" (root == empty string)
        /// *It is impossible to get parent state machine with path like "../sibling", as StateMachine is not structured as a Tree
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public State GetState(string path)
        {
            if (path.Empty())
                return this;

            State state = null;
            var nestedStates = path.Split("/");
            foreach (var dir in nestedStates)
            {
                if (state != null && state is StateMachine stateMachine)
                    state = stateMachine.States.Get<State>(dir);
                else
                    state = States.Get<State>(dir); // First level state
            }
            return state;

        }

        /// <summary>
        /// Add state, state name must be unique within this StateMachine, return state succeed if added else return null
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public State AddState(State state)
        {
            if (state == null || States.Contains(state.Name))
                return null;
            States[state.Name] = state;
            return state;
        }

        /// <summary>
        /// Remove state by its name
        /// </summary>
        /// <param name="stateName"></param>
        /// <returns></returns>
        public bool RemoveState(string stateName)
        {
            if (States.Contains(stateName))
            {
                States.Remove(stateName);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Change existing state key in States(GDC.Dictionary), return true if success
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public bool ChangeStateName(string from, string to)
        {
            if (!States.Contains(from) || States.Contains(to))
                return false;

            foreach (string stateKey in States.Keys)
            {
                var state = States.Get<State>(stateKey);
                var isNameChangingState = stateKey == from;
                if (isNameChangingState)
                {
                    state.Name = to;
                    States[to] = state;
                    States.Remove(from);
                }
                foreach (string fromKey in transitions.Keys)
                {
                    var fromTransitions = transitions.Get<GDC.Dictionary>(fromKey);
                    if (fromKey == from)
                    {
                        transitions.Remove(from);
                        transitions[to] = fromTransitions;
                    }
                    foreach (string toKey in fromTransitions.Keys)
                    {
                        var transition = fromTransitions.Get<Transition>(toKey);
                        if (transition.From == from)
                        {
                            transition.From = to;
                        }
                        else if (transition.To == from)
                        {
                            transition.To = to;
                            if (!isNameChangingState)
                            {
                                // Transitions to name changed state needs to be updated
                                fromTransitions.Remove(from);
                                fromTransitions[to] = transition;
                            }
                        }
                    }
                }
            }
            return true;

        }

        /// <summary>
        /// Add transition, Transition.From must be equal to this state's name && Transition.To !added yet
        /// </summary>
        public void AddTransition(Transition transition)
        {
            if (transition.From == null || transition.To == null)
            {
                GD.PushWarning($"Transition missing from/to ({transition.From}/{transition.To})");
                return;
            }
            GDC.Dictionary fromTransitions;
            if (transitions.Contains(transition.From))
                fromTransitions = transitions.Get<GDC.Dictionary>(transition.From);
            else
            {
                fromTransitions = new GDC.Dictionary() { };
                transitions[transition.From] = fromTransitions;
            }
            fromTransitions[transition.To] = transition;
            EmitSignal(nameof(TransitionAdded), transition);
        }

        /// <summary>
        /// Remove transition with Transition.To(name of state transiting to)
        /// </summary>
        /// <param name="fromState"></param>
        /// <param name="toState"></param>
        public void RemoveTransition(string fromState, string toState)
        {
            var fromTransitions = transitions.Get<GDC.Dictionary>(fromState);
            if (fromTransitions != null && fromTransitions.Contains(toState))
            {
                fromTransitions.Remove(toState);
                if (fromTransitions.Count == 0)
                    // There are no transitions going out from "fromState", so we remove
                    // it's entry from the Transitions' dict.
                    transitions.Remove(fromState);
                EmitSignal(nameof(TransitionRemoved), fromState, toState);
            }
        }

        public IEnumerable<State> GetEntries()
        {
            return transitions.Get<GDC.Dictionary>(EntryState).Values.Cast<State>();
        }

        public IEnumerable<State> GetExits()
        {
            return transitions.Get<GDC.Dictionary>(ExitState).Values.Cast<State>();
        }

        public bool HasEntry => States?.Contains(EntryState) ?? false;
        public bool HasExit => States?.Contains(ExitState) ?? false;

        private string JoinPath(params string[] dirs)
        {
            return string.Join("/", dirs);
        }

        /// <summary>
        /// Validate state machine resource to identify && fix error
        /// </summary>
        /// <param name="stateMachine"></param>
        /// <returns></returns>
        public static bool Validate(StateMachine stateMachine)
        {
            bool validated = false;
            foreach (var fromKey in stateMachine.transitions.Keys)
            {
                // Non-existing state found in StateMachine.transitions
                // See https://github.com/imjp94/gd-YAFSM/issues/6
                if (!stateMachine.States.Contains(fromKey))
                {
                    validated = true;
                    GD.PushWarning($"gd-YAFSM Non-existing ValidationError State({fromKey}) found in transition");
                    stateMachine.transitions.Remove(fromKey);
                    continue;
                }
                var fromTransition = stateMachine.transitions.Get<GDC.Dictionary>(fromKey);
                foreach (string toKey in fromTransition.Keys)
                {
                    // Non-existing state found in StateMachine.transitions
                    // See https://github.com/imjp94/gd-YAFSM/issues/6
                    if (!stateMachine.States.Contains(toKey))
                    {
                        validated = true;
                        GD.PushWarning($"gd-YAFSM Non-existing ValidationError State({toKey}) found in Transition({fromKey} -> {toKey})");
                        fromTransition.Remove(toKey);
                        continue;

                        // Mismatch of StateMachine.transitions with Transition.To 
                        // See https://github.com/imjp94/gd-YAFSM/issues/6
                    }
                    var toTransition = fromTransition.Get<Transition>(toKey);
                    if (toKey != toTransition.To)
                    {
                        validated = true;
                        GD.PushWarning($"gd-YAFSM Mismatch ValidationError of StateMachine.transitions Key({toKey}) with Transition.To({toTransition.To})");
                        toTransition.To = toKey;

                        // Self connecting transition
                        // See https://github.com/imjp94/gd-YAFSM/issues/5
                    }
                    if (toTransition.From == toTransition.To)
                    {
                        validated = true;
                        GD.PushWarning($"gd-YAFSM Self ValidationError connecting Transition({toTransition.From} -> {toTransition.To})");
                        fromTransition.Remove(toKey);
                    }
                }
            }
            return validated;
        }
    }
}