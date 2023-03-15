
using System;
using System.Linq;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.StateMachine
{
    [Tool]
    public partial class StateMachinePlayer : StackPlayer
    {
        [Signal] public delegate void Transited(string from, string to);   // Transition of state
        [Signal] public delegate void Entered(string to);                  // Entry of state Machine(including nested), empty string equals to root
        [Signal] public delegate void Exited(string from);                 // Exit of state Machine(including nested, empty string equals to root
        [Signal] public delegate void Updated(string state, float delta);  // Time to Update(based on processMode), up to user to handle any logic, for example, update movement of KinematicBody

        /// <summary>
        /// Enum to define how state machine should be updated
        /// </summary>
        public enum ProcessModeType
        {
            PHYSICS,
            IDLE,
            MANUAL
        }

        [OnReadyGet(OrNull = true)]
        public StateMachine StateMachine { get; set; } // StateMachine being played 
        private bool active = true;
        /// <summary>
        /// Whether the state machine player is enabled (active) or not
        /// </summary>
        [Export]
        public bool Active
        {
            get => true;
            set
            {
                if (active != value)
                {
                    if (value && IsExited)
                    {
                        GD.PushWarning("Attempting to make exited StateMachinePlayer active, call Reset() then SetActive() instead");
                        return;
                    }
                    active = value;
                    OnActiveChanged();

                }
            }
        }
        [Export]
        public bool Autostart { get; set; } = true; // Automatically enter Entry state on ready if true

        /// <summary>
        /// ProcessMode of the state machine player
        /// </summary>
        [Export]
        public ProcessModeType ProcessMode
        {
            get => processMode;
            set
            {
                if (processMode != value)
                {
                    processMode = value;
                    OnProcessModeChanged();
                }
            }
        }
        private ProcessModeType processMode = ProcessModeType.IDLE;

        /// <summary>
        /// If state machine player has started
        /// </summary>
        public bool IsEntered => Stack.Contains(State.EntryState);

        /// <summary>
        /// If state machine player has ended
        /// </summary>
        public bool IsExited => Current == State.ExitState;

        public override string Current => base.Current != null ? base.Current : "";
        public override string Previous => base.Previous != null ? base.Previous : "";

        // TODO: Figure out if duplicating is necessary
        private GDC.Dictionary parameters; // Parameters to be passed to condition
        /// <summary>
        /// Get duplicate of whole parameter dictionary
        /// </summary>
        /// <returns></returns>
        public GDC.Dictionary Parameters => parameters.Duplicate();

        private GDC.Dictionary localParameters;
        public GDC.Dictionary LocalParamters => localParameters.Duplicate();

        private bool isStarted = false;
        private bool isUpdateLocked = true;
        private bool wasTransited = false;// If last transition was successful
        private bool isParamEdited = false;


        public StateMachinePlayer() : base()
        {
            if (Engine.EditorHint)
            {
                return;
            }
            parameters = new GDC.Dictionary() { };
            localParameters = new GDC.Dictionary() { };
            wasTransited = true;// Trigger _transit on _ready
        }

        public override string _GetConfigurationWarning()
        {
            base._GetConfigurationWarning();
            if (StateMachine != null)
            {
                if (!StateMachine.HasEntry)
                    return "State Machine will !function properly without Entry node";
            }
            else
                return "State Machine Player is !going anywhere without default State Machine";
            return "";

        }

        [OnReady]
        public virtual void RealReady()
        {
            if (Engine.EditorHint)
            {
                return;
            }
            SetProcess(false);
            SetPhysicsProcess(false);
            CallDeferred(nameof(_Initiate)); // Make sure connection of signals can be done in _ready to receive all signal callback
        }

        public void _Initiate()
        {
            if (Autostart)
            {
                Start();
            }
            OnActiveChanged();
            OnProcessModeChanged();
        }

        public override void _Process(float delta)
        {
            if (Engine.EditorHint)
            {
                return;
            }
            UpdateStart();
            Update(delta);
            UpdateEnd();

        }

        public override void _PhysicsProcess(float delta)
        {
            if (Engine.EditorHint)
            {
                return;
            }
            UpdateStart();
            Update(delta);
            UpdateEnd();

        }

        // Only get called in 2 condition, _parameters edited || last transition was successful
        private void Transit()
        {
            if (!active)
                return;
            // Attempt to transit if parameter edited || last transition was successful
            if (!isParamEdited && !wasTransited)
                return;
            var from = Current;
            var localParams = localParameters.Get(StateDirectory.GetBaseDirectoryFromPath(from), new GDC.Dictionary());
            var nextState = StateMachine.Transit(Current, parameters, localParams);
            if (nextState != null)
            {
                if (Stack.Contains(nextState))
                    Reset(Stack.IndexOf(nextState));
                else
                    Push(nextState);
            }
            var to = nextState;
            wasTransited = nextState != null;
            isParamEdited = false;
            FlushTrigger(parameters);
            FlushTrigger(localParameters, true);

            if (wasTransited)
                OnStateChanged(from, to);
        }

        private void OnStateChanged(string from, string to)
        {
            switch (to)
            {
                case State.EntryState:
                    EmitSignal(nameof(Entered), "");
                    break;
                case State.ExitState:
                    Active = false; // Disable on exit
                    EmitSignal(nameof(Exited), "");

                    break;
            }
            if (to.EndsWith(State.EntryState) && to.Length() > State.EntryState.Length())
            {
                // Nexted Entry state
                var state = StateDirectory.GetBaseDirectoryFromPath(Current);
                EmitSignal(nameof(Entered), state);
            }
            else if (to.EndsWith(State.ExitState) && to.Length() > State.ExitState.Length())
            {
                // Nested Exit state, clear "local" params
                var state = StateDirectory.GetBaseDirectoryFromPath(Current);
                ClearParam(state, false); // Clearing params internally, do !update
                EmitSignal(nameof(Exited), state);

            }
            EmitSignal(nameof(Transited), from, to);
        }

        /// <summary>
        /// Called internally if processMode is PHYSICS/IDLE to unlock Update()
        /// </summary>
        private void UpdateStart()
        {
            isUpdateLocked = false;
        }

        /// <summary>
        /// Called internally if processMode is PHYSICS/IDLE to lock Update() from external call
        /// </summary>
        private void UpdateEnd()
        {
            isUpdateLocked = true;
        }

        /// <summary>
        /// Called after Update() which is dependant on processMode, override to process current state
        /// </summary>
        /// <param name="delta"></param>
        /// <param name="state"></param>
        public virtual void OnUpdated(float delta, string state)
        {

        }

        private void OnProcessModeChanged()
        {
            if (!active)
                return;

            switch (processMode)
            {
                case ProcessModeType.PHYSICS:
                    SetPhysicsProcess(true);
                    SetProcess(false);
                    break;
                case ProcessModeType.IDLE:
                    SetPhysicsProcess(false);
                    SetProcess(true);
                    break;
                case ProcessModeType.MANUAL:
                    SetPhysicsProcess(false);
                    SetProcess(false);

                    break;
            }
        }

        private void OnActiveChanged()
        {
            if (Engine.EditorHint)
                return;

            if (active)
            {
                OnProcessModeChanged();
                Transit();
            }
            else
            {
                SetPhysicsProcess(false);
                SetProcess(false);
            }
        }

        /// <summary>
        /// Remove all Trigger(param with null value) from provided params, only get called after _transit
        /// Trigger another call of _flushTrigger on first layer of dictionary if nested is true
        /// </summary>
        /// <param name="triggerParams"></param>
        /// <param name="nested"></param>
        private void FlushTrigger(GDC.Dictionary triggerParams, bool nested = false)
        {
            foreach (string paramKey in triggerParams.Keys)
            {
                var value = triggerParams[paramKey];
                if (nested && value is GDC.Dictionary nestedParams)
                    FlushTrigger(nestedParams);
                if (value == null) // Param with null as value is treated as trigger
                    triggerParams.Remove(paramKey);
            }
        }

        public override void Reset(int to = -1, ResetEventTrigger resetEventTrigger = ResetEventTrigger.LastToDest)
        {
            base.Reset(to, resetEventTrigger);
            wasTransited = true; // Make sure to call _transit on next update
        }

        /// <summary>
        /// Manually start the player, automatically called if autostart is true
        /// </summary>
        public void Start()
        {
            Push(State.EntryState);
            EmitSignal(nameof(Entered), "");
            wasTransited = true;
            isStarted = true;

            // Restart player
        }

        public void Restart(bool isActive = true, bool preserveParams = false)
        {
            Reset();
            Active = isActive;
            if (!preserveParams)
            {
                ClearParam("", false);
            }
            Start();

            // Update player to, first initiate transition, then call OnUpdated, finally emit "update" signal, delta will be given based on processMode.
            // Can only be called manually if processMode is MANUAL, otherwise, assertion error will be raised.
            // *delta provided will be reflected in [Signal] delegate void Updated(state, delta);
        }

        public void Update() => Update(GetPhysicsProcessDeltaTime());
        public void Update(float delta)
        {
            if (!active)
                return;
            if (processMode != ProcessModeType.MANUAL)
                System.Diagnostics.Debug.Assert(!isUpdateLocked, $"Attempting to update manually with ProcessMode.{Enum.GetName(typeof(ProcessModeType), processMode)}");

            Transit();
            var currentState = Current;
            OnUpdated(delta, currentState);
            EmitSignal(nameof(Updated), currentState, delta);
            if (processMode == ProcessModeType.MANUAL)
            {
                // Make sure to auto advance even in MANUAL mode
                if (wasTransited)
                {
                    CallDeferred(nameof(Update));
                }
            }
        }

        /// <summary>
        /// Set trigger to be tested with condition, then trigger _transit on next update, 
        /// automatically call Update() if processMode set to MANUAL && autoUpdate true
        /// Nested trigger can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
        /// </summary>
        /// <param name="name"></param>
        /// <param name="autoUpdate"></param>
        public void SetTrigger(string name, bool autoUpdate = true)
        {
            SetParam(name, null, autoUpdate);
        }

        public void SetNestedTrigger(string path, string name, bool autoUpdate = true)
        {
            SetNestedParam(path, name, null, autoUpdate);
        }

        /// <summary> 
        /// Set Param(null value treated as trigger) to be tested with condition, then trigger _transit on next update, 
        /// automatically call Update() if processMode set to MANUAL && autoUpdate true
        /// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="autoUpdate"></param>
        public void SetParam(string name, object value, bool autoUpdate = true)
        {
            string path = "";
            if (name.Contains("/"))
            {
                path = StateDirectory.GetBaseDirectoryFromPath(name);
                name = StateDirectory.GetStateFromPath(name);
            }
            SetNestedParam(path, name, value, autoUpdate);

        }

        public void SetNestedParam(string path, string name, object value, bool autoUpdate = true)
        {
            if (path.Empty())
                parameters[name] = value;
            else
            {
                var localParams = localParameters.Get<GDC.Dictionary>(path);
                if (localParams != null)
                    localParams[name] = value;
                else
                {
                    localParams = new GDC.Dictionary() { };
                    localParams[name] = value;
                    localParameters[path] = localParams;
                }
            }
            OnParamEdited(autoUpdate);
        }

        /// <summary>
        /// Remove param, then trigger _transit on next update, 
        /// automatically call Update() if processMode set to MANUAL && autoUpdate true
        /// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
        /// </summary>
        /// <param name="name"></param>
        /// <param name="autoUpdate"></param>
        /// <returns></returns>
        public bool EraseParam(string name, bool autoUpdate = true)
        {
            string path = "";
            if (name.Contains("/"))
            {
                path = StateDirectory.GetBaseDirectoryFromPath(name);
                name = StateDirectory.GetStateFromPath(name);
            }
            return EraseNestedParam(path, name, autoUpdate);
        }

        public bool EraseNestedParam(string path, string name, bool autoUpdate = true)
        {
            bool successful = false;
            if (path.Empty())
            {
                successful = parameters.Contains(name);
                if (successful) parameters.Remove(name);
            }
            else
            {
                var nestedParams = localParameters.Get<GDC.Dictionary>(path);
                if (nestedParams != null)
                {
                    successful = nestedParams.Contains(name);
                    if (successful) nestedParams.Remove(name);
                }
            }
            OnParamEdited(autoUpdate);
            return successful;
        }

        /// <summary>
        /// Clear params from specified path, empty string to clear all, then trigger _transit on next update, 
        /// automatically call Update() if ProcessMode set to ProcessModeType.Manual && autoUpdate true
        /// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
        /// </summary>
        /// <param name="path"></param>
        /// <param name="autoUpdate"></param>
        public void ClearParam(string path = "", bool autoUpdate = true)
        {
            if (path.Empty())
                parameters.Clear();
            else
            {
                var nestedParams = localParameters.Get<GDC.Dictionary>(path);
                if (nestedParams != null)
                    nestedParams.Clear();

                // Clear nested params
                foreach (string paramKey in localParameters.Keys)
                {
                    if (paramKey.BeginsWith(path))
                        localParameters.Remove(paramKey);
                }
            }
        }

        /// <summary>
        /// Called when param edited, automatically call Update() if ProcessMode set to ProcessModeType.Manual && autoUpdate true
        /// </summary>
        /// <param name="autoUpdate"></param>
        private void OnParamEdited(bool autoUpdate = true)
        {
            isParamEdited = true;
            if (processMode == ProcessModeType.MANUAL && autoUpdate && isStarted)
                Update();
        }


        /// <summary>
        /// Get value of param
        /// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
        /// </summary>
        /// <param name="name"></param>
        /// <param name=""></param>
        /// <returns></returns>
        public T GetParam<T>(string name, T defaultReturn = default(T))
        {
            string path = "";
            if (name.Contains("/"))
            {
                path = StateDirectory.GetBaseDirectoryFromPath(name);
                name = StateDirectory.GetStateFromPath(name);
            }
            return GetNestedParam(path, name, defaultReturn);

        }

        public T GetNestedParam<T>(string path, string name, T defaultReturn = default(T))
        {
            if (path.Empty())
            {
                return parameters.Get(name, defaultReturn);
            }
            else
            {
                var nestedParams = localParameters.Get<GDC.Dictionary>(path);
                if (nestedParams == null) return defaultReturn;
                return nestedParams.Get(name, defaultReturn);

            }
        }

        /// <summary>
        /// Return true if param exists
        /// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool HasParam(string name)
        {
            string path = "";
            if (name.Contains("/"))
            {
                path = StateDirectory.GetBaseDirectoryFromPath(name);
                name = StateDirectory.GetStateFromPath(name);
            }
            return HasNestedParam(path, name);

        }

        public bool HasNestedParam(string path, string name)
        {
            if (path.Empty())
                return parameters.Contains(name);
            else
            {
                var nestedParams = localParameters.Get<GDC.Dictionary>(path);
                if (nestedParams == null)
                    return false;
                return nestedParams.Contains(name);
            }
        }

        #region Static Methods
        /// <summary>
        /// Convert node path to state path that can be used to query state with StateMachine.get_state.
        /// Node path, "root/path/to/state", equals to State path, "path/to/state"
        /// </summary>
        /// <param name="nodePath"></param>
        /// <returns></returns>
        public static string NodePathToStatePath(string nodePath)
        {
            var p = nodePath.Replace("root", "");
            if (p.BeginsWith("/"))
            {
                p = p.Substring(1);
            }
            return p;

        }

        /// <summary>
        /// Convert state path to node path that can be used for query node in scene tree.
        /// State path, "path/to/state", equals to Node path, "root/path/to/state"
        /// </summary>
        /// <param name="statePath"></param>
        /// <returns></returns>
        public static string StatePathToNodePath(string statePath)
        {
            var path = statePath;
            if (path.Empty())
            {
                path = "root";
            }
            else
            {
                path = GD.Str("root/", path);
            }
            return path;
        }
        #endregion
    }
}