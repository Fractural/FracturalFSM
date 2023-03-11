
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public class StateMachine : State
    {
        [Signal] delegate void TransitionAdded(Transition transition);// Transition added
        [Signal] delegate void TransitionRemoved(Transition transition);// Transition removed

        /// <summary>
        /// States within this StateMachine, keyed by State.name
        /// </summary>
        [Export]
        public GDC.Dictionary States { get => states.Duplicate(); set => states = value; }
        private GDC.Dictionary states = new GDC.Dictionary();
        public State GetState(string state) => states.Get<State>(state);
        public StateMachine GetStateMachine(string state) => GetState(state) as StateMachine;

        // TODO NOW: Fix this.

        /// <summary>
        /// Transitions from this state, keyed by Transition.to
        /// </summary>
        [Export]
        public GDC.Dictionary Transitions { get => transitions.Duplicate(); set => transitions = value; }
        private GDC.Dictionary transitions = new GDC.Dictionary();
        public GDC.Array<Transition> GetTransitions(string state) => transitions.Get<GDC.Array<Transition>>(state);


        public void _Init(string name = "", GDC.Dictionary transitions = null, GDC.Dictionary states = null)
        {
            base._Init(name);
            if (transitions != null)
                Transitions = transitions;
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
                {
                    endStateMachine = endStateMachine.GetState(state);
                }
                else
                {
                    endStateMachine = States.Get<StateMachine>(state); // First level state
                }
            }
            // Nested StateMachine in Exit state
            if (isNested)
            {
                var isNestedExit = nestedStates[nestedStates.Length - 1] == ExitState;
                if (isNestedExit)
                {
                    // Normalize path to transit again with parent of endStateMachine
                    string endStateMachineParentPath = "";
                    for (int i = 0; i < nestedStates.Length - 2; i++) // Ignore last two State(which is endStateMachine/end_state)
                    {
                        endStateMachineParentPath = JoinPath(endStateMachineParentPath, nestedStates[i]);
                    }
                    var endStateMachineParent = GetState(endStateMachineParentPath) as StateMachine;
                    var normalizedCurrentState = endStateMachine.Name;
                    var nextState = endStateMachineParent.Transit(normalizedCurrentState, transitParams);
                    if (nextState != null)
                    {
                        // Construct next state into absolute path
                        nextState = JoinPath(endStateMachineParentPath, nextState);
                    }
                    return nextState;

                }
            }
            // Transit with current running nested state machine
            var fromTransitions = endStateMachine.Transitions.Get<GDC.Dictionary>(nestedStates[nestedStates.Length - 1]);
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
                        {
                            // Next state is a StateMachine, return entry state of the state machine in absolute path
                            nextState = JoinPath(basePath, nextState, State.EntryState);
                        }

                        else
                        {
                            // Construct next state into absolute path
                            nextState = JoinPath(basePath, nextState);
                        }
                        return nextState;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get state from absolute path, for exmaple, "path/to/state" (root == empty string)
        /// *It is impossible to get parent state machine with path like "../sibling", as StateMachine is !structed as a Tree
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public State GetState(string path)
        {
            State state = null;
            if (path.Empty())
            {
                state = this;
            }
            else
            {
                var nestedStates = path.Split("/");
                for (int i = 0; i < nestedStates.Length; i++)
                {
                    var dir = nestedStates[i];
                    if (state != null)
                    {
                        state = state.states[dir];
                    }
                    else
                    {
                        state = States.get[dir]; // First level state
                    }
                }
            }
            return state;

        }

        /// <summary>
        /// Add state, state name must be unique within this StateMachine, return state succeed ? added : reutrn null
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        public __TYPE AddState(__TYPE state)
        {
            if (!state)
            {
                return null;
            }
            if (state.name in States)
		{
                return null;

            }
            States[state.name] = state;
            return state;

            // Remove state by its name
        }

        public __TYPE RemoveState(__TYPE state)
        {
            return States.Erase(state);

            // Change existing state key in States(GDC.Dictionary), return true if success
        }

        public __TYPE ChangeStateName(__TYPE from, __TYPE to)
        {
            if (!(from in States) || to in States)
		{
                return false;

            }
            foreach (var stateKey in States.Keys())
            {
                var state = States[stateKey];
                var isNameChangingState = stateKey == from;
                if (isNameChangingState)
                {
                    state.name = to;
                    States[to] = state;
                    States.Erase(from);
                }
                foreach (var fromKey in Transitions.Keys())
                {
                    var fromTransitions = Transitions[fromKey];
                    if (fromKey == from)
                    {
                        Transitions.Erase(from);
                        Transitions[to] = fromTransitions;
                    }
                    foreach (var toKey in fromTransitions.Keys())
                    {
                        var transition = fromTransitions[toKey];
                        if (transition.from == from)
                        {
                            transition.from = to;
                        }
                        else if (transition.to == from)
                        {
                            transition.to = to;
                            if (!is_name_changing_state)
                            {
                                // Transitions to name changed state needs to be updated
                                fromTransitions.Erase(from);
                                fromTransitions[to] = transition;
                            }
                        }
                    }
                }
            }
            return true;

        }

        /// <summary>
        /// Add transition, Transition.from must be equal to this state's name && Transition.to !added yet
        /// </summary>
        public void AddTransition(__TYPE transition)
        {
            if (!(transition.from || transition.to))
            {
                GD.PushWarning("Transition missing from/to (%s/%s)" % [transition.from, transition.to]);
                return;

            }
            var fromTransitions;
            if (transition.from in Transitions)
		{
                fromTransitions = Transitions[transition.from];
            }

        else
            {
                fromTransitions = new GDC.Dictionary() { };
                Transitions[transition.from] = fromTransitions;

            }
            fromTransitions[transition.to] = transition;
            EmitSignal("transition_added", transition);

        }

        // Remove transition with Transition.To(name of state transiting to)
        public void RemoveTransition(__TYPE fromState, __TYPE toState)
        {
            var fromTransitions = Transitions.Get(fromState);
            if (fromTransitions)
            {
                if (toState in fromTransitions)
			{
                    fromTransitions.Erase(toState);
                    if (fromTransitions.Empty())
                    {
                        Transitions.Erase(fromState);
                    }
                    EmitSignal("transition_removed", fromState, toState);

                }
            }
        }

        public IEnumerable<State> GetEntries()
        {
            return Transitions.Get<GDC.Dictionary>(EntryState).Values.Cast<State>();
        }

        public IEnumerable<State> GetExits()
        {
            return Transitions.Get<GDC.Dictionary>(ExitState).Values.Cast<State>();
        }

        public bool HasEntry => States.Contains(EntryState);
        public bool HasExit => States.Contains(ExitState);

        private string JoinPath(params string[] dirs)
        {
            return string.Join("/", dirs);
        }

        // Validate state machine resource to identify && fix error
        public bool Validate(StateMachine stateMachine)
        {
            bool validated = false;
            foreach (var fromKey in stateMachine.transitions.Keys)
            {
                // Non-existing state found in StateMachine.transitions
                // See https://github.com/imjp94/gd-YAFSM/issues/6
                if (!stateMachine.states.Contains(fromKey))
                {
                    validated = true;
                    GD.PushWarning($"gd-YAFSM Non-existing ValidationError State({fromKey}) found in transition");
                    stateMachine.transitions.Erase(fromKey);
                    continue;

                }
                var fromTransition = stateMachine.transitions[fromKey];
                foreach (var toKey in fromTransition.Keys)
                {
                    // Non-existing state found in StateMachine.transitions
                    // See https://github.com/imjp94/gd-YAFSM/issues/6
                    if (!(toKey in stateMachine.states))
				{
                        validated = true;
                        GD.PushWarning($"gd-YAFSM Non-existing ValidationError State({toKey}) found in Transition({fromKey} -> {toKey})");
                        fromTransition.Erase(toKey);
                        continue;

                        // Mismatch of StateMachine.transitions with Transition.to 
                        // See https://github.com/imjp94/gd-YAFSM/issues/6
                    }
                    var toTransition = fromTransition[toKey];
                    if (toKey != toTransition.to)
                    {
                        validated = true;
                        GD.PushWarning($"gd-YAFSM Mismatch ValidationError of StateMachine.transitions Key({toKey}) with Transition.To({toTransition.to})");
                        toTransition.to = toKey;

                        // Self connecting transition
                        // See https://github.com/imjp94/gd-YAFSM/issues/5
                    }
                    if (toTransition.from == toTransition.to)
                    {
                        validated = true;
                        GD.PushWarning($"gd-YAFSM Self ValidationError connecting Transition({toTransition.from} -> {toTransition.to})");
                        fromTransition.Erase(toKey);
                    }
                }
            }
            return validated;


        }
    }
}