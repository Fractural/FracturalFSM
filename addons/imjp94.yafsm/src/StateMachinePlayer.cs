
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class StateMachinePlayer : "StackPlayer.gd"
{
	 
	public const var State = GD.Load("states/State.gd");
	
	[Signal] delegate void Transited(from, to);// Transition of state
	[Signal] delegate void Entered(to);// Entry of state Machine(including nested), empty string equals to root
	[Signal] delegate void Exited(from);// Exit of state Machine(including nested, empty string equals to root
	[Signal] delegate void Updated(state, delta);// Time to Update(based on processMode), up to user to handle any logic, for example, update movement of KinematicBody
	
	// Enum to define how state machine should be updated
	enum ProcessMode {
		PHYSICS,
		IDLE,
		MANUAL
	}
	
	Export(Resource) var stateMachine // StateMachine being played 
	[Export]  public bool active = true {set{SetActive(value);}} // Activeness of player
	[Export]  public bool autostart = true ;// Automatically enter Entry state on ready if true
	[Export]  public ProcessMode processMode = ProcessMode.IDLE {set{SetProcessMode(value);}} // ProcessMode of player
	
	private __TYPE _isStarted = false;
	var _parameters // Parameters to be passed to condition
	private __TYPE _localParameters;
	private __TYPE _isUpdateLocked = true;
	private __TYPE _wasTransited = false ;// If last transition was successful
	private __TYPE _isParamEdited = false;
	
	
	public void _Init()
	{  
		if(Engine.editor_hint)
		{
			return;
	
		}
		_parameters = new Dictionary(){};
		_localParameters = new Dictionary(){};
		_wasTransited = true ;// Trigger _transit on _ready
	
	}
	
	public __TYPE _GetConfigurationWarning()
	{  
		if(stateMachine)
		{
			if(!state_machine.HasEntry())
			{
				return "State Machine will !function properly without Entry node";
			}
		}
		else
		{
			return "State Machine Player is !going anywhere without default State Machine";
		}
		return "";
	
	}
	
	public void _Ready()
	{  
		if(Engine.editor_hint)
		{
			return;
	
		}
		SetProcess(false);
		SetPhysicsProcess(false);
		CallDeferred("_initiate") ;// Make sure connection of signals can be done in _ready to receive all signal callback
	
	}
	
	public void _Initiate()
	{  
		if(autostart)
		{
			Start();
		}
		_OnActiveChanged();
		_OnProcessModeChanged();
	
	}
	
	public void _Process(__TYPE delta)
	{  
		if(Engine.editor_hint)
		{
			return;
	
		}
		_UpdateStart();
		Update(delta);
		_UpdateEnd();
	
	}
	
	public void _PhysicsProcess(__TYPE delta)
	{  
		if(Engine.editor_hint)
		{
			return;
	
		}
		_UpdateStart();
		Update(delta);
		_UpdateEnd();
	
	// Only get called in 2 condition, _parameters edited || last transition was successful
	}
	
	public void _Transit()
	{  
		if(!active)
		{
			return;
		// Attempt to transit if parameter edited || last transition was successful
		}
		if(!_is_param_edited && !_was_transited)
		{
			return;
	
		}
		var from = GetCurrent();
		var localParams = _localParameters.Get(PathBackward(from), new Dictionary(){});
		var nextState = stateMachine.Transit(GetCurrent(), _parameters, localParams);
		if(nextState)
		{
			if(stack.Has(nextState))
			{
				Reset(stack.Find(nextState));
			}
			else
			{
				Push(nextState);
			}
		}
		var to = nextState;
		_wasTransited = !!next_state;
		_isParamEdited = false;
		_FlushTrigger(_parameters);
		_FlushTrigger(_localParameters, true);
	
		if(_wasTransited)
		{
			_OnStateChanged(from, to);
	
		}
	}
	
	public void _OnStateChanged(__TYPE from, __TYPE to)
	{  
		switch( to)
		{
			case State.ENTRY_STATE:
				EmitSignal("entered", "");
				break;
			case State.EXIT_STATE:
				SetActive(false) ;// Disable on exit
				EmitSignal("exited", "");
		
				break;
		}
		if(to.EndsWith(State.ENTRY_STATE) && to.Length() > State.ENTRY_STATE.Length())
		{
			// Nexted Entry state
			var state = PathBackward(GetCurrent());
			EmitSignal("entered", state);
		}
		else if(to.EndsWith(State.EXIT_STATE) && to.Length() > State.EXIT_STATE.Length())
		{
			// Nested Exit state, clear "local" params
			var state = PathBackward(GetCurrent());
			ClearParam(state, false) ;// Clearing params internally, do !update
			EmitSignal("exited", state);
	
		}
		EmitSignal("transited", from, to);
	
	// Called internally if processMode is PHYSICS/IDLE to unlock Update()
	}
	
	public void _UpdateStart()
	{  
		_isUpdateLocked = false;
	
	// Called internally if processMode is PHYSICS/IDLE to lock Update() from external call
	}
	
	public void _UpdateEnd()
	{  
		_isUpdateLocked = true;
	
	// Called after Update() which is dependant on processMode, override to process current state
	}
	
	public void _OnUpdated(__TYPE delta, __TYPE state)
	{  
	
	}
	
	public void _OnProcessModeChanged()
	{  
		if(!active)
		{
			return;
	
		}
		switch( processMode)
		{
			case ProcessMode.PHYSICS:
				SetPhysicsProcess(true);
				SetProcess(false);
				break;
			case ProcessMode.IDLE:
				SetPhysicsProcess(false);
				SetProcess(true);
				break;
			case ProcessMode.MANUAL:
				SetPhysicsProcess(false);
				SetProcess(false);
	
				break;
		}
	}
	
	public void _OnActiveChanged()
	{  
		if(Engine.editor_hint)
		{
			return;
	
		}
		if(active)
		{
			_OnProcessModeChanged();
			_Transit();
		}
		else
		{
			SetPhysicsProcess(false);
			SetProcess(false);
	
	// Remove all Trigger(param with null value) from provided params, only get called after _transit
	// Trigger another call of _flushTrigger on first layer of dictionary if nested is true
		}
	}
	
	public void _FlushTrigger(__TYPE params, bool nested=false)
	{  
		foreach(var paramKey in params.Keys())
		{
			var value = params[paramKey];
			if(nested && value is Dictionary)
			{
				_FlushTrigger(value);
			}
			if(value == null) // Param with null as value is treated as trigger
			{
				params.Erase(paramKey);
	
			}
		}
	}
	
	public void Reset(int to=-1, __TYPE event=ResetEventTrigger.LAST_TO_DEST)
	{  
		base.Reset(to, event);
		_wasTransited = true ;// Make sure to call _transit on next update
	
	// Manually start the player, automatically called if autostart is true
	}
	
	public void Start()
	{  
		Push(State.ENTRY_STATE);
		EmitSignal("entered", "");
		_wasTransited = true;
		_isStarted = true;
	
	// Restart player
	}
	
	public void Restart(bool isActive=true, bool preserveParams=false)
	{  
		Reset();
		SetActive(isActive);
		if(!preserve_params)
		{
			ClearParam("", false);
		}
		Start();
	
	// Update player to, first initiate transition, then call _onUpdated, finally emit "update" signal, delta will be given based on processMode.
	// Can only be called manually if processMode is MANUAL, otherwise, assertion error will be raised.
	// *delta provided will be reflected in [Signal] delegate void Updated(state, delta);
	}
	
	public void Update(__TYPE delta=GetPhysicsProcessDeltaTime())
	{  
		if(!active)
		{
			return;
		}
		if(processMode != ProcessMode.MANUAL)
		{
			System.Diagnostics.Debug.Assert(!_is_update_locked, "Attempting to update manually with ProcessMode.%s" % ProcessMode.Keys()[processMode]);
	
		}
		_Transit();
		var currentState = GetCurrent();
		_OnUpdated(currentState, delta);
		EmitSignal("updated", currentState, delta);
		if(processMode == ProcessMode.MANUAL)
		{
			// Make sure to auto advance even in MANUAL mode
			if(_wasTransited)
			{
				CallDeferred("update");
	
	// Set trigger to be tested with condition, then trigger _transit on next update, 
	// automatically call Update() if processMode set to MANUAL && autoUpdate true
	// Nested trigger can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
			}
		}
	}
	
	public void SetTrigger(__TYPE name, bool autoUpdate=true)
	{  
		SetParam(name, null, autoUpdate);
	
	}
	
	public void SetNestedTrigger(__TYPE path, __TYPE name, bool autoUpdate=true)
	{  
		SetNestedParam(path, name, null, autoUpdate);
	
	// Set Param(null value treated as trigger) to be tested with condition, then trigger _transit on next update, 
	// automatically call Update() if processMode set to MANUAL && autoUpdate true
	// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
	}
	
	public void SetParam(__TYPE name, __TYPE value, bool autoUpdate=true)
	{  
		string path = "";
		if("/" in name)
		{
			path = PathBackward(name);
			name = PathEndDir(name);
		}
		SetNestedParam(path, name, value, autoUpdate);
	
	}
	
	public void SetNestedParam(__TYPE path, __TYPE name, __TYPE value, bool autoUpdate=true)
	{  
		if(path.Empty())
		{
			_parameters[name] = value;
		}
		else
		{
			var localParams = _localParameters.Get(path);
			if(localParams is Dictionary)
			{
				localParams[name] = value;
			}
			else
			{
				localParams = new Dictionary(){};
				localParams[name] = value;
				_localParameters[path] = localParams;
			}
		}
		_OnParamEdited(autoUpdate);
	
	// Remove param, then trigger _transit on next update, 
	// automatically call Update() if processMode set to MANUAL && autoUpdate true
	// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
	}
	
	public __TYPE EraseParam(__TYPE name, bool autoUpdate=true)
	{  
		string path = "";
		if("/" in name)
		{
			path = PathBackward(name);
			name = PathEndDir(name);
		}
		return EraseNestedParam(path, name, autoUpdate);
	
	}
	
	public __TYPE EraseNestedParam(__TYPE path, __TYPE name, bool autoUpdate=true)
	{  
		bool result = false;
		if(path.Empty())
		{
			result = _parameters.Erase(name);
		}
		else
		{
			result = _localParameters.Get(path, new Dictionary(){}).Erase(name);
		}
		_OnParamEdited(autoUpdate);
		return result;
	
	// Clear params from specified path, empty string to clear all, then trigger _transit on next update, 
	// automatically call Update() if processMode set to MANUAL && autoUpdate true
	// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
	}
	
	public void ClearParam(string path="", bool autoUpdate=true)
	{  
		if(path.Empty())
		{
			_parameters.Clear();
		}
		else
		{
			_localParameters.Get(path, new Dictionary(){}).Clear();
			// Clear nested params
			foreach(var paramKey in _localParameters.Keys())
			{
				if(paramKey.BeginsWith(path))
				{
					_localParameters.Erase(paramKey);
	
	// Called when param edited, automatically call Update() if processMode set to MANUAL && autoUpdate true
				}
			}
		}
	}
	
	public void _OnParamEdited(bool autoUpdate=true)
	{  
		_isParamEdited = true;
		if(processMode == ProcessMode.MANUAL && autoUpdate && _isStarted)
		{
			Update();
	
	// Get value of param
	// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
		}
	}
	
	public __TYPE GetParam(__TYPE name, __TYPE default=null)
	{  
		string path = "";
		if("/" in name)
		{
			path = PathBackward(name);
			name = PathEndDir(name);
		}
		return GetNestedParam(path, name, default);
	
	}
	
	public __TYPE GetNestedParam(__TYPE path, __TYPE name, __TYPE default=null)
	{  
		if(path.Empty())
		{
			return _parameters.Get(name, default);
		}
		else
		{
			var localParams = _localParameters.Get(path, new Dictionary(){});
			return localParams.Get(name, default);
	
	// Get duplicate of whole parameter dictionary
		}
	}
	
	public __TYPE GetParams()
	{  
		return _parameters.Duplicate();
	
	// Return true if param exists
	// Nested param can be accessed through path "path/to/param_name", for example, "App/Game/is_playing"
	}
	
	public __TYPE HasParam(__TYPE name)
	{  
		string path = "";
		if("/" in name)
		{
			path = PathBackward(name);
			name = PathEndDir(name);
		}
		return HasNestedParam(path, name);
	
	}
	
	public __TYPE HasNestedParam(__TYPE path, __TYPE name)
	{  
		if(path.Empty())
		{
			return name in _parameters;
		}
		else
		{
			var localParams = _localParameters.Get(path, new Dictionary(){});
			return name in localParams;
	
	// Return if player started
		}
	}
	
	public __TYPE IsEntered()
	{  
		return State.ENTRY_STATE in stack;
	
	// Return if player ended
	}
	
	public __TYPE IsExited()
	{  
		return GetCurrent() == State.EXIT_STATE;
	
	}
	
	public void SetActive(__TYPE v)
	{  
		if(active != v)
		{
			if(v)
			{
				if(IsExited())
				{
					GD.PushWarning("Attempting to make exited StateMachinePlayer active, call Reset() then SetActive() instead");
					return;
				}
			}
			active = v;
			_OnActiveChanged();
	
		}
	}
	
	public void SetProcessMode(__TYPE mode)
	{  
		if(processMode != mode)
		{
			processMode = mode;
			_OnProcessModeChanged();
	
		}
	}
	
	public __TYPE GetCurrent()
	{  
		var v = base.GetCurrent();
		return v ? v : ""
	
	}
	
	public __TYPE GetPrevious()
	{  
		var v = base.GetPrevious();
		return v ? v : ""
	
	// Convert node path to state path that can be used to query state with StateMachine.get_state.
	// Node path, "root/path/to/state", equals to State path, "path/to/state"
	}
	
	public __TYPE NodePathToStatePath(__TYPE nodePath)
	{  
		var p = nodePath.Replace("root", "");
		if(p.BeginsWith("/"))
		{
			p = p.Substr(1);
		}
		return p;
	
	// Convert state path to node path that can be used for query node in scene tree.
	// State path, "path/to/state", equals to Node path, "root/path/to/state"
	}
	
	public __TYPE StatePathToNodePath(__TYPE statePath)
	{  
		var path = statePath;
		if(path.Empty())
		{
			path = "root";
		}
		else
		{
			path = GD.Str("root/", path);
		}
		return path;
	
	// Return parent path, "path/to/state" return "path/to"
	}
	
	public __TYPE PathBackward(__TYPE path)
	{  
		return path.Substr(0, path.Rfind("/"));
	
	// Return end directory of path, "path/to/state" returns "state"
	}
	
	public __TYPE PathEndDir(__TYPE path)
	{  
		return path.Right(path.Rfind("/") + 1);
	}
	
	
	
}