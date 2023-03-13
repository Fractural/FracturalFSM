
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;
using Fractural.GodotCodeGenerator.Attributes;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class StateMachineEditor : FlowChart
    {
        [Signal] public delegate void InspectorChanged(string property);// Inform plugin to refresh inspector
        [Signal] public delegate void DebugModeChanged(bool newDebugMode);

        public static readonly Dictionary ENTRYStateMissingMsg = new Dictionary()
        {
            ["key"] = "entry_state_missing",
            ["text"] = "Entry State missing}, it will never get started. Right-click -> \"Add Entry\"."
        };
        public static readonly Dictionary EXITStateMissingMsg = new Dictionary()
        {
            ["key"] = "exit_state_missing",
            ["text"] = "Exit State missing}, it will never exit from nested state. Right-click -> \"Add Exit\"."
        };
        public static readonly Dictionary DEBUGModeMsg = new Dictionary()
        {
            ["key"] = "debug_mode",
            ["text"] = "Debug Mode"
        };

        [Export]
        private PackedScene stateNodePrefab;
        [Export]
        private PackedScene transitionLinePrefab;

        [OnReadyGet("ContextMenu")]
        private PopupMenu contextMenu;
        [OnReadyGet("StateNodeContextMenu")]
        private PopupMenu stateNodeContextMenu;
        [OnReadyGet("ConvertToStateConfirmation")]
        private ConfirmationDialog convertToStateConfirmation;
        [OnReadyGet("SaveDialog")]
        private ConfirmationDialog saveDialog;
        [OnReadyGet("MarginContainer")]
        private MarginContainer createNewStateMachineContainer;
        [OnReadyGet("MarginContainer/CreateNewStateMachine")]
        private Button createNewStateMachine;
        [OnReadyGet("ParametersPanel")]
        private MarginContainer paramPanel;

        private PathViewer pathViewer;
        private TextureButton conditionVisibility = new TextureButton();
        private Label unsavedIndicator = new Label();
        private VBoxContainer messageBox = new VBoxContainer();

        private Color editorAccentColor = Colors.White;
        private Texture transitionArrowIcon;

        private UndoRedo undoRedo;

        private bool debugMode = false;
        public bool DebugMode
        {
            get => debugMode;
            set
            {
                if (debugMode != value)
                {
                    debugMode = value;
                    _OnDebugModeChanged(value);
                    EmitSignal("debug_mode_changed", debugMode);

                }
            }
        }
        private StateMachinePlayer stateMachinePlayer;
        private StateMachinePlayer StateMachinePlayer
        {
            get => stateMachinePlayer;
            set
            {
                if (stateMachinePlayer != value)
                {
                    stateMachinePlayer = value;
                    _OnStateMachinePlayerChanged(value);
                }
            }
        }
        private StateMachine stateMachine;
        private StateMachine StateMachine
        {
            get => stateMachine;
            set
            {
                if (stateMachine != value)
                {
                    stateMachine = value;
                    _OnStateMachineChanged(value);
                }
            }
        }
        public bool canGuiNameEdit = true;
        public bool canGuiContextMenu = true;

        private __TYPE _reconnectingConnection;
        private int _lastIndex = 0;
        private string _lastPath = "";
        private __TYPE _messageBoxDict = new Dictionary() { };
        private Control _contextNode;
        private string _currentState = "";
        private Array _lastStack = new Array() { };


        public void _Init()
        {
            pathViewer = new PathViewer();
            pathViewer.MouseFilter = MouseFilterEnum.Ignore;
            pathViewer.Connect(nameof(PathViewer.DirPressed), this, nameof(_OnPathViewerDirPressed));
            topBar.AddChild(pathViewer);

            conditionVisibility.hint_tooltip = "Hide/Show Conditions on Transition Line";
            conditionVisibility.stretch_mode = TextureButton.STRETCH_KEEP_ASPECT_CENTERED;
            conditionVisibility.toggle_mode = true;
            conditionVisibility.size_flags_vertical = SIZEShrinkCenter;
            conditionVisibility.focus_mode = FOCUSNone;
            conditionVisibility.Connect("pressed", this, "_on_condition_visibility_pressed");
            conditionVisibility.pressed = true;
            gadget.AddChild(conditionVisibility);

            unsavedIndicator.size_flags_vertical = SIZEShrinkCenter;
            unsavedIndicator.focus_mode = FOCUSNone;
            gadget.AddChild(unsavedIndicator);

            messageBox.SetAnchorsAndMarginsPreset(PRESETBottomWide);
            messageBox.grow_vertical = GROWDirectionBegin;
            AddChild(messageBox);

            content.GetChild(0).name = "root";

            SetProcess(false);

        }

        public void _Ready()
        {
            createNewStateMachineContainer.Visible = false;
            createNewStateMachine.Connect("pressed", this, "_on_create_new_state_machine_pressed");
            contextMenu.Connect("index_pressed", this, "_on_context_menu_index_pressed");
            stateNodeContextMenu.Connect("index_pressed", this, "_on_state_node_context_menu_index_pressed");
            convertToStateConfirmation.Connect("confirmed", this, "_on_convert_to_state_confirmation_confirmed");
            saveDialog.Connect("confirmed", this, "_on_save_dialog_confirmed");

        }

        public void _Process(__TYPE delta)
        {
            if (!debug_mode)
            {
                SetProcess(false);
                return;
            }
            if (!is_instance_valid(stateMachinePlayer))
            {
                SetProcess(false);
                SetDebugMode(false);
                return;
            }
            var stack = stateMachinePlayer.Get("Members/StackPlayer.gd/stack");
            if (!stack)
            {
                SetProcess(false);
                SetDebugMode(false);
                return;

            }
            if (stack.Size() == 1)
            {
                SetCurrentState(stateMachinePlayer.Get("Members/StackPlayer.gd/current"));
            }
            else
            {
                var stackMaxIndex = stack.Size() - 1;
                var prevIndex = stack.Find(_currentState);
                if (prevIndex == -1)
                {
                    if (_lastStack.Size() < stack.Size())
                    {
                        // Reproduce transition, for example:
                        // [Entry, Idle, Walk]
                        // [Entry, Idle, Jump, Fall]
                        // Walk -> Idle
                        // Idle -> Jump
                        // Jump -> Fall
                        int commonIndex = -1;
                        foreach (var i in _lastStack.Size())
                        {
                            if (_lastStack[i] == stack[i])
                            {
                                commonIndex = i;
                                break;
                            }
                        }
                        if (commonIndex > -1)
                        {
                            var countFromLastStack = _lastStack.Size() - 1 - commonIndex - 1;
                            _lastStack.Invert();
                            // Transit back to common state
                            foreach (var i in countFromLastStack)
                            {
                                SetCurrentState(_lastStack[i + 1]);
                                // Transit to all missing state in current stack
                            }
                            foreach (var i in GD.Range(commonIndex + 1, stack.Size()))
                            {
                                SetCurrentState(stack[i]);
                            }
                        }
                        else
                        {
                            SetCurrentState(stack.Back());
                        }
                    }
                    else
                    {
                        SetCurrentState(stack.Back());
                    }
                }
                else
                {
                    // Set every skipped state
                    var missingCount = stackMaxIndex - prevIndex;
                    foreach (var i in GD.Range(1, missingCount + 1))
                    {
                        SetCurrentState(stack[prevIndex + i]);
                    }
                }
            }
            _lastStack = stack;
            var params = stateMachinePlayer.Get("Members/_parameters");
            var localParams = stateMachinePlayer.Get("Members/_local_parameters");
            paramPanel.UpdateParams(params, localParams);
            GetFocusedLayer(_currentState).DebugUpdate(_currentState, params, localParams);

        }

        public void _OnPathViewerDirPressed(__TYPE dir, __TYPE index)
        {
            var path = pathViewer.SelectDir(dir);
            SelectLayer(GetLayer(path));

            if (_lastIndex > index)
            {
                // Going backward
                var endStateParentPath = StateMachinePlayer.PathBackward(_lastPath);
                var endStateName = StateMachinePlayer.PathEndDir(_lastPath);
                var layer = content.GetNodeOrNull(endStateParentPath);
                if (layer)
                {
                    var node = layer.content_nodes.GetNodeOrNull(endStateName);
                    if (node)
                    {
                        if (!node.state.states)
                        {
                            // Convert state machine node back to state node
                            ConvertToState(layer, node);

                        }
                    }
                }
            }
            _lastIndex = index;
            _lastPath = path;

        }

        public void _OnContextMenuIndexPressed(__TYPE index)
        {
            var newNode = StateNode.Instance();
            newNode.theme.GetStylebox("focus", "FlowChartNode").border_color = editorAccentColor;
            switch (index)
            {
                case 0: // Add State
                    newNode.name = "State";
                    break;
                case 1: // Add Entry
                    if (State.ENTRY_STATE in currentLayer.state_machine.states)
				{
                        GD.PushWarning("Entry node already exist");
                        return;
                    }
                    newNode.name = State.ENTRY_STATE;
                    break;
                case 2: // Add Exit
                    if (State.EXIT_STATE in currentLayer.state_machine.states)
				{
                        GD.PushWarning("Exit node already exist");
                        return;
                    }
                    newNode.name = State.EXIT_STATE;
                    break;
            }
            newNode.rect_position = ContentPosition(GetLocalMousePosition());
            AddNode(currentLayer, newNode);

        }

        public void _OnStateNodeContextMenuIndexPressed(__TYPE index)
        {
            if (!_context_node)
            {
                return;

            }
            switch (index)
            {
                case 0: // Copy
                    _copyingNodes = new Array() { _contextNode };
                    _contextNode = null;
                    break;
                case 1: // Duplicate
                    DuplicateNodes(currentLayer, new Array() { _contextNode });
                    _contextNode = null;
                    break;
                case 2: // Separator
                    _contextNode = null;
                    break;
                case 3: // Convert
                    convertToStateConfirmation.PopupCentered();

                    break;
            }
        }

        public void _OnConvertToStateConfirmationConfirmed()
        {
            ConvertToState(currentLayer, _contextNode);
            _contextNode.Update();// Update outlook of node
                                  // Remove layer
            var path = GD.Str(pathViewer.GetCwd(), "/", _contextNode.name);
            var layer = GetLayer(path);
            if (layer)
            {
                layer.QueueFree();
            }
            _contextNode = null;

        }

        public void _OnSaveDialogConfirmed()
        {
            Save();

        }

        public void _OnCreateNewStateMachinePressed()
        {
            var newStateMachine = new StateMachine()


        stateMachinePlayer.state_machine = newStateMachine;
            SetStateMachine(newStateMachine);
            createNewStateMachineContainer.Visible = false;
            CheckHasEntry();
            EmitSignal("inspector_changed", "state_machine");

        }

        public void _OnConditionVisibilityPressed()
        {
            foreach (var line in currentLayer.content_lines.GetChildren())
            {
                line.vbox.Visible = conditionVisibility.pressed;

            }
        }

        public void _OnDebugModeChanged(__TYPE newDebugMode)
        {
            if (newDebugMode)
            {
                paramPanel.Show();
                AddMessage(DEBUGModeMsg.key, DEBUGModeMsg.text);
                SetProcess(true);
                // mouseFilter = MOUSEFilterIgnore;
                canGuiSelectNode = false;
                canGuiDeleteNode = false;
                canGuiConnectNode = false;
                canGuiNameEdit = false;
                canGuiContextMenu = false;
            }
            else
            {
                paramPanel.ClearParams();
                paramPanel.Hide();
                RemoveMessage(DEBUGModeMsg.key);
                SetProcess(false);
                canGuiSelectNode = true;
                canGuiDeleteNode = true;
                canGuiConnectNode = true;
                canGuiNameEdit = true;
                canGuiContextMenu = true;

            }
        }

        public void _OnStateMachinePlayerChanged(StateMachinePlayer newStateMachinePlayer)
        {
            if (StateMachinePlayer == null)
                return;

            // TODO: Figure out what this means
            if (newStateMachinePlayer.GetClass() == "ScriptEditorDebuggerInspectedObject")
                return;

            if (newStateMachinePlayer != null && newStateMachinePlayer.StateMachine != null)
                createNewStateMachineContainer.Visible = true;
            else
                createNewStateMachineContainer.Visible = false;
        }

        public void _OnStateMachineChanged(StateMachine newStateMachine)
        {
            var rootLayer = GetLayer("root");
            pathViewer.SelectDir("root");// Before selectLayer, so pathViewer will be updated in _onLayerSelected
            SelectLayer(rootLayer);
            ClearGraph(rootLayer);
            // Reset layers & path viewer
            foreach (Node child in rootLayer.GetChildren())
            {
                if (child is FlowChartLayer layer)
                {
                    rootLayer.RemoveChild(child);
                    child.QueueFree();
                }
            }
            if (newStateMachine != null)
            {
                rootLayer.StateMachine = stateMachine;
                var validated = StateMachine.Validate(newStateMachine);
                if (validated)
                {
                    GD.Print("Corrupted gd-YAFSM StateMachine Resource fixed, save to apply the fix.");
                }
                DrawGraph(rootLayer);
                CheckHasEntry();

            }
        }

        public override void _GuiInput(InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseButton mouseButtonEvent)
            {
                switch (mouseButtonEvent.ButtonIndex)
                {
                    case (int)ButtonList.Right:
                        if (mouseButtonEvent.Pressed && canGuiContextMenu)
                        {
                            contextMenu.SetItemDisabled(1, currentLayer.StateMachine.HasEntry());
                            contextMenu.SetItemDisabled(2, currentLayer.StateMachine.HasExit());
                            contextMenu.rect_position = GetViewport().GetMousePosition();
                            contextMenu.Popup();

                        }
                        break;
                }
            }
        }

        public void _Input(__TYPE event)
{
            // Intercept save action
            if (visible && event is InputEventKey)

        {
            switch ( event.scancode)

            {
				case KEYS:
            if (event.control && event.pressed)

                    {
            SaveRequest();

        }
        break;
        }
    }
}

public __TYPE CreateLayer(__TYPE node)
{
    // Create/Move to new layer
    var newStateMachine = ConvertToStateMachine(currentLayer, node);
    // Determine current layer path
    var parentPath = pathViewer.GetCwd();
    var path = GD.Str(parentPath, "/", node.name);
    var layer = GetLayer(path);
    pathViewer.AddDir(node.state.name);// Before selectLayer, so pathViewer will be updated in _onLayerSelected
    if (!layer)
    {
        // New layer to spawn
        layer = AddLayerTo(GetLayer(parentPath));
        layer.name = node.state.name;
        layer.state_machine = newStateMachine;
        DrawGraph(layer);
    }
    _lastIndex = pathViewer.GetChildCount() - 1;
    _lastPath = path;
    return layer;

}

public __TYPE OpenLayer(__TYPE path)
{
    var dir = StateDirectory.new(path)

        dir.Goto(dir.GetEndIndex());
    dir.Back();
    var nextLayer = GetNextLayer(dir, GetLayer("root"));
    SelectLayer(nextLayer);
    return nextLayer;

    // Recursively get next layer
}

public __TYPE GetNextLayer(__TYPE dir, __TYPE baseLayer)
{
    var nextLayer = baseLayer;
    var np = dir.Next();
    if (np)
    {
        nextLayer = baseLayer.GetNodeOrNull(np);
        if (nextLayer)
        {
            nextLayer = GetNextLayer(dir, nextLayer);
        }
        else
        {
            var toDir = StateDirectory.new(dir.GetCurrent())

                toDir.Goto(toDir.GetEndIndex());
            toDir.Back();
            var node = baseLayer.content_nodes.GetNodeOrNull(toDir.GetCurrentEnd());
            nextLayer = GetNextLayer(dir, CreateLayer(node));
        }
    }
    return nextLayer;

}

public __TYPE GetFocusedLayer(__TYPE state)
{
    var currentDir = StateDirectory.new(state)

        currentDir.Goto(currentDir.GetEndIndex());
    currentDir.Back();
    return GetLayer(GD.Str("root/", currentDir.GetCurrent()));

}

public void _OnStateNodeGuiInput(__TYPE event, __TYPE node)
{
    if (node.state.IsEntry() || node.state.IsExit())
    {
        return;

    }
    if (event is InputEventMouseButton)
		{
    switch ( event.button_index)
			{
				case BUTTONLeft:
        if (event.pressed)
					{
            if (event.doubleclick)
						{
                if (node.name_edit.GetRect().HasPoint(event.position) && canGuiNameEdit)
                {
                    // Edit State name if within LineEdit
                    node.EnableNameEdit(true);
                    AcceptEvent();
                }
                else
                {
                    var layer = CreateLayer(node);
                    SelectLayer(layer);
                    AcceptEvent();
                }
            }
        }
        break;
				case BUTTONRight:
        if (event.pressed)
					{
            // State node context menu
            _contextNode = node;
            stateNodeContextMenu.rect_position = GetViewport().GetMousePosition();
            stateNodeContextMenu.Popup();
            stateNodeContextMenu.SetItemDisabled(3, !(node.state is StateMachine));
            AcceptEvent();

        }
        break;
    }
}
	}
	
	public __TYPE ConvertToStateMachine(FlowChartLayer layer, __TYPE node)
{
    // Convert State to StateMachine
    var newStateMachine;
    if (node.state is StateMachine)
    {
        newStateMachine = node.state;
    }
    else
    {
        newStateMachine = new StateMachine()

            newStateMachine.name = node.state.name;
        newStateMachine.graph_offset = node.state.graph_offset;
        layer.state_machine.RemoveState(node.state.name);
        layer.state_machine.AddState(newStateMachine);
        node.state = newStateMachine;
    }
    return newStateMachine;

}

public __TYPE ConvertToState(FlowChartLayer layer, __TYPE node)
{
    // Convert StateMachine to State
    var newState;
    if (node.state is StateMachine)
    {
        newState = new State()

            newState.name = node.state.name;
        newState.graph_offset = node.state.graph_offset;
        layer.state_machine.RemoveState(node.state.name);
        layer.state_machine.AddState(newState);
        node.state = newState;
    }
    else
    {
        newState = node.state;
    }
    return newState;

}

public __TYPE CreateLayerInstance()
{
    var layer = new Control()

        layer.SetScript(StateMachineEditorLayer);
    layer.editor_accent_color = editorAccentColor;
    return layer;

}

public __TYPE CreateLineInstance()
{
    var line = TransitionLine.Instance();
    line.theme.GetStylebox("focus", "FlowChartLine").shadow_color = editorAccentColor;
    line.theme.SetIcon("arrow", "FlowChartLine", transitionArrowIcon);
    return line;

    // Request to save current editing StateMachine
}

public void SaveRequest()
{
    if (!can_save())
    {
        return;

    }
    saveDialog.dialog_text = "Saving StateMachine to %s" % stateMachine.resource_path;
    saveDialog.PopupCentered();

    // Save current editing StateMachine
}

public void Save()
{
    if (!can_save())
    {
        return;

    }
    unsavedIndicator.text = "";
    ResourceSaver.Save(stateMachine.resource_path, stateMachine);

    // Clear editor
}

public void ClearGraph(FlowChartLayer layer)
{
    ClearConnections();
    foreach (var child in layer.content_nodes.GetChildren())
    {
        if (child is StateNodeScript)
        {
            layer.content_nodes.RemoveChild(child);
            child.QueueFree();
        }
    }
    unsavedIndicator.text = "";// Clear graph is !action by user

    // Intialize editor with current editing StateMachine
}

public void DrawGraph(FlowChartLayer layer)
{
    foreach (var stateKey in layer.state_machine.states.Keys())
    {
        var state = layer.state_machine.states[stateKey];
        var newNode = StateNode.Instance();
        newNode.theme.GetStylebox("focus", "FlowChartNode").border_color = editorAccentColor;
        newNode.name = stateKey;// Set before addNode to let engine handle duplicate name
        AddNode(layer, newNode);
        // Set after addNode to make sure UIs are initialized
        newNode.state = state;
        newNode.state.name = stateKey;
        newNode.rect_position = state.graph_offset;
    }
    foreach (var stateKey in layer.state_machine.states.Keys())
    {
        var fromTransitions = layer.state_machine.transitions.Get(stateKey);
        if (fromTransitions)
        {
            foreach (var transition in fromTransitions.Values())
            {
                ConnectNode(layer, transition.from, transition.to);
                layer._connections[transition.from][transition.to].line.transition = transition;
            }
        }
    }
    Update();
    unsavedIndicator.text = "";// Draw graph is !action by user

    // Add message to MessageBox(overlay text at bottom of editor)
}

public __TYPE AddMessage(__TYPE key, __TYPE text)
{
    var label = new Label()

        label.text = text;
    _messageBoxDict[key] = label;
    messageBox.AddChild(label);
    return label;

    // Remove message from messageBox
}

public __TYPE RemoveMessage(__TYPE key)
{
    var control = _messageBoxDict.Get(key);
    if (control)
    {
        _messageBoxDict.Erase(key);
        messageBox.RemoveChild(control);
        // Weird behavior of VBoxContainer, only sort children properly after changing growDirection
        messageBox.grow_vertical = GROWDirectionEnd;
        messageBox.grow_vertical = GROWDirectionBegin;
        return true;
    }
    return false;

    // Check if current editing StateMachine has entry, warns user if entry state missing
}

public void CheckHasEntry()
{
    if (!current_layer.state_machine)
    {
        return;
    }
    if (!current_layer.state_machine.HasEntry())
    {
        if (!(ENTRYStateMissingMsg.key in _messageBoxDict))
			{
    AddMessage(ENTRYStateMissingMsg.key, ENTRYStateMissingMsg.text);
}
		}

        else
{
    if (ENTRYStateMissingMsg.key in  _messageBoxDict)
			{
        RemoveMessage(ENTRYStateMissingMsg.key);

        // Check if current editing StateMachine is nested && has exit, warns user if exit state missing
    }
}
	}
	
	public void CheckHasExit()
{
    if (!current_layer.state_machine)
    {
        return;
    }
    if (!path_viewer.GetCwd() == "root") // Nested state
    {
        if (!current_layer.state_machine.HasExit())
        {
            if (!(EXITStateMissingMsg.key in _messageBoxDict))
				{
    AddMessage(EXITStateMissingMsg.key, EXITStateMissingMsg.text);
}
return;
			}
		}
		if (EXITStateMissingMsg.key in _messageBoxDict)
		{
    RemoveMessage(EXITStateMissingMsg.key);

}
	}
	
	public void _OnLayerSelected(FlowChartLayer layer)
{
    if (layer)
    {
        layer.ShowContent();
        CheckHasEntry();
        CheckHasExit();

    }
}

public void _OnLayerDeselected(FlowChartLayer layer)
{
    if (layer)
    {
        layer.HideContent();

    }
}

public void _OnNodeDragged(FlowChartLayer layer, Node node, bool dragged)
{
    node.state.graph_offset = node.rect_position;
    _OnEdited();

}

public void _OnNodeAdded(FlowChartLayer layer, Node newNode)
{
    newNode.undo_redo = undoRedo;
    newNode.state.name = newNode.name;
    newNode.state.graph_offset = newNode.rect_position;
    newNode.Connect("name_edit_entered", this, "_on_node_name_edit_entered", new Array() { newNode })

        newNode.Connect("gui_input", this, "_on_state_node_gui_input", new Array() { newNode })

        layer.state_machine.AddState(newNode.state);
    CheckHasEntry();
    CheckHasExit();
    _OnEdited();

}

public __TYPE _OnNodeRemoved(FlowChartLayer layer, __TYPE nodeName)
{
    var path = GD.Str(pathViewer.GetCwd(), "/", nodeName);
    var layerToRemove = GetLayer(path);
    if (layerToRemove)
    {
        layerToRemove.GetParent().RemoveChild(layerToRemove);
        layerToRemove.QueueFree();
    }
    var result = layer.state_machine.RemoveState(nodeName);
    CheckHasEntry();
    CheckHasExit();
    _OnEdited();
    return result;

}

public void _OnNodeConnected(FlowChartLayer layer, __TYPE from, __TYPE to)
{
    if (_reconnectingConnection)
    {
        // Reconnection will trigger _onNodeConnected after _onNodeReconnectEnd/_on_node_reconnect_failed
        if (_reconnectingConnection.from_node.name == from && _reconnectingConnection.to_node.name == to)
        {
            _reconnectingConnection = null;
            return;
        }
    }
    if (layer.state_machine.transitions.Has(from))
    {
        if (layer.state_machine.transitions[from].Has(to))
        {
            return; // Already existed as it is loaded from file

        }
    }
    var line = layer._connections[from][to].line;
    var newTransition = Transition.new(from, to)

        line.transition = newTransition;
    layer.state_machine.AddTransition(newTransition);
    ClearSelection();
    Select(line);
    _OnEdited();

}

public void _OnNodeDisconnected(FlowChartLayer layer, string from, string to)
{
    layer.state_machine.RemoveTransition(from, to);
    _OnEdited();

}

public void _OnNodeReconnectBegin(FlowChartLayer layer, string from, string to)
{
    _reconnectingConnection = layer._connections[from][to];
    layer.state_machine.RemoveTransition(from, to);

}

public void _OnNodeReconnectEnd(FlowChartLayer layer, string from, string to)
{
    var transition = _reconnectingConnection.line.transition;
    transition.to = to;
    layer.state_machine.AddTransition(transition);
    ClearSelection();
    Select(_reconnectingConnection.line);

}

public void _OnNodeReconnectFailed(FlowChartLayer layer, string from, string to)
{
    var transition = _reconnectingConnection.line.transition;
    layer.state_machine.AddTransition(transition);
    ClearSelection();
    Select(_reconnectingConnection.line);

}

public bool _RequestConnectFrom(FlowChartLayer layer, string from)
{
    if (from == State.ExitState)
    {
        return false;
    }
    return true;

}

public bool _RequestConnectTo(FlowChartLayer layer, string to)
{
    if (to == State.EntryState)
    {
        return false;
    }
    return true;

}

public void _OnDuplicated(FlowChartLayer layer, __TYPE oldNodes, __TYPE newNodes)
{
    // Duplicate condition as well
    foreach (var i in oldNodes.Size())
    {
        var fromNode = oldNodes[i];
        foreach (var connectionPair in GetConnectionList())
        {
            if (fromNode.name == connectionPair.from)
            {
                foreach (var j in oldNodes.Size())
                {
                    var toNode = oldNodes[j];
                    if (toNode.name == connectionPair.to)
                    {
                        var oldConnection = layer._connections[connectionPair.from][connectionPair.to];
                        var newConnection = layer._connections[newNodes[i].name][newNodes[j].name];
                        foreach (var condition in oldConnection.line.transition.conditions.Values())
                        {
                            newConnection.line.transition.AddCondition(condition.Duplicate())

                            }
                    }
                }
            }
        }
    }
    _OnEdited();

}

public void _OnNodeNameEditEntered(__TYPE newName, __TYPE node)
{
    var old = node.state.name;
    var new = newName

        if old == new:
			return;
if "/" in new || "\\" in new: // No back/forward-slash
			GD.PushWarning("Illegal State Name: / && \\ are !allowed in State Name(%s)" % new);
node.name_edit.text = old;
return;

if (currentLayer.state_machine.ChangeStateName(old, new))
{
    RenameNode(currentLayer, node.name, new);
    node.name = new
    // Rename layer as well
    var path = GD.Str(pathViewer.GetCwd(), "/", node.name);
    var layer = GetLayer(path);
    if (layer)
    {
        layer.name = new

            }
    foreach (var child in pathViewer.GetChildren())
    {
        if (child.text == old)
        {
            child.text = new

                    break;
        }
    }
    _OnEdited();
}
else
{
    node.name_edit.text = old;

}
	}
	
	public void _OnEdited()
{
    unsavedIndicator.text = "*";

}

public void _OnRemoteTransited(__TYPE from, __TYPE to)
{
    var fromDir = StateDirectory.new(from)

        var toDir = StateDirectory.new(to)

        var focusedLayer = GetFocusedLayer(from);
    if (from)
    {
        if (focusedLayer)
        {
            focusedLayer.DebugTransitOut(from, to);
        }
    }
    if (to)
    {
        if (fromDir.IsNested() && fromDir.IsExit())
        {
            if (focusedLayer)
            {
                var path = pathViewer.Back();
                SelectLayer(GetLayer(path));
            }
        }
        else if (toDir.IsNested())
        {
            if (toDir.IsEntry() && focusedLayer)
            {
                // Open into next layer
                toDir.Goto(toDir.GetEndIndex());
                toDir.Back();
                var node = focusedLayer.content_nodes.GetNodeOrNull(toDir.GetCurrentEnd());
                if (node)
                {
                    var layer = CreateLayer(node);
                    SelectLayer(layer);
                    // In case where, "from" state is nested yet !an exit state,
                    // while "to" state is on different level, then jump to destination layer directly.
                    // This happens when StateMachinePlayer transit to state that existing in the stack,
                    // which trigger StackPlayer.Reset() && cause multiple states removed from stack within one frame
                }
            }
        }
        else if (fromDir.IsNested() && !from_dir.IsExit())
        {
            if (toDir._dirs.Size() != fromDir._dirs.Size())
            {
                toDir.Goto(toDir.GetEndIndex());
                var n = toDir.Back();
                if (!n)
                {
                    n = "root";
                }
                var layer = GetLayer(n);
                pathViewer.SelectDir(layer.name);
                SelectLayer(layer);

            }
        }
        focusedLayer = GetFocusedLayer(to);
        if (!focused_layer)
        {
            focusedLayer = OpenLayer(to);
        }
        focusedLayer.DebugTransitIn(from, to);

        // Return if current editing StateMachine can be saved, ignore built-in resource
    }
}

public __TYPE CanSave()
{
    if (!state_machine)
    {
        return false;
    }
    var resourcePath = stateMachine.resource_path;
    if (resourcePath.Empty())
    {
        return false;
    }
    if (".scn" in resourcePath || ".tscn" in resourcePath) // Built-in resource will be saved by scene
		{
    return false;
}
return true;
	
	}

public void SetCurrentState(__TYPE v)
{
    if (_currentState != v)
    {
        var from = _currentState;
        var to = v;
        _currentState = v;
        _OnRemoteTransited(from, to);


    }
}
	
	
	
}
}