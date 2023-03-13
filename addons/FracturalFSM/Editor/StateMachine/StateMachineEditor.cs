
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Utils;
using System.Collections.Generic;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public partial class StateMachineEditor : FlowChart
    {
        [Signal] public delegate void InspectorChanged(string property);// Inform plugin to refresh inspector
        [Signal] public delegate void DebugModeChanged(bool newDebugMode);

        #region MessageBox
        public class MessageBoxMessage
        {
            public MessageBoxMessage(string key, string text)
            {
                Key = key;
                Text = text;
            }

            public string Key { get; set; }
            public string Text { get; set; }
        }

        // TODO: Refactor the messaging system to be better
        public static readonly MessageBoxMessage EntryStateMissingMsg = new MessageBoxMessage(
            "entry_state_missing",
            "Entry State missing, it will never get started. Right-click -> \"Add Entry\"."
            );
        public static readonly MessageBoxMessage ExitStateMissingMsg = new MessageBoxMessage(
            "exit_state_missing",
            "Exit State missing, it will never exit from nested state. Right-click -> \"Add Exit\"."
            );
        public static readonly MessageBoxMessage DebugModeMsg = new MessageBoxMessage(
            "debug_mode",
            "Debug Mode"
            );

        private GDC.Dictionary messageBoxDict = new GDC.Dictionary() { };
        #endregion

        #region Dependencies
        [Export]
        private PackedScene stateNodePrefab;
        [Export]
        private PackedScene transitionLinePrefab;
        [Export]
        private PackedScene stateMachineEditorLayerPrefab;

        /// <summary>
        /// Context menu for creating new nodes
        /// </summary>
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
        private ParametersPanel paramPanel;

        private PathViewer pathViewer;
        private TextureButton conditionVisibility = new TextureButton();
        private Label unsavedIndicator = new Label();
        private VBoxContainer messageBox = new VBoxContainer();

        private Color editorAccentColor = Colors.White;
        private Texture transitionArrowIcon;

        private UndoRedo undoRedo;
        #endregion

        #region Public properties
        private bool debugMode = false;
        public bool DebugMode
        {
            get => debugMode;
            set
            {
                if (debugMode != value)
                {
                    debugMode = value;
                    OnDebugModeChanged(value);
                    EmitSignal(nameof(DebugModeChanged), debugMode);

                }
            }
        }
        private StateMachinePlayer stateMachinePlayer;
        public StateMachinePlayer StateMachinePlayer
        {
            get => stateMachinePlayer;
            set
            {
                if (stateMachinePlayer != value)
                {
                    stateMachinePlayer = value;
                    OnStateMachinePlayerChanged(value);
                }
            }
        }
        private StateMachine stateMachine;
        public StateMachine StateMachine
        {
            get => stateMachine;
            set
            {
                if (stateMachine != value)
                {
                    stateMachine = value;
                    OnStateMachineChanged(value);
                }
            }
        }
        public bool CanGuiNameEdit { get; set; } = true;
        public bool CanGuiContextMenu { get; set; } = true;
        #endregion

        #region Private fields
        private Connection reconnectingConnection;
        private int lastIndex = 0;
        private string lastPath = "";
        private StateNode contextNode;
        private string currentState = "";
        private IList<string> lastStack = new List<string>();
        #endregion

        #region FlowChart Method/Property Hiding
        // We need to change methods to return StateMachineEditorLayer
        // rather than FlowChartLayer

        protected new StateMachineEditorLayer currentLayer
        {
            get => base.currentLayer as StateMachineEditorLayer;
            set => base.currentLayer = value;
        }

        public new StateMachineEditorLayer AddLayerTo(Control target)
            => base.AddLayerTo(target) as StateMachineEditorLayer;

        public new StateMachineEditorLayer GetLayer(NodePath nodePath)
            => base.GetLayer(nodePath) as StateMachineEditorLayer;
        #endregion

        public StateMachineEditor()
        {
            pathViewer = new PathViewer();
            pathViewer.MouseFilter = MouseFilterEnum.Ignore;
            pathViewer.Connect(nameof(PathViewer.DirPressed), this, nameof(OnPathViewerDirPressed));
            topBar.AddChild(pathViewer);

            conditionVisibility.HintTooltip = "Hide/Show Conditions on Transition Line";
            conditionVisibility.StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered;
            conditionVisibility.ToggleMode = true;
            conditionVisibility.SizeFlagsVertical = (int)SizeFlags.ShrinkCenter;
            conditionVisibility.FocusMode = FocusModeEnum.None;
            conditionVisibility.Connect("pressed", this, nameof(OnConditionVisibilityPressed));
            conditionVisibility.Pressed = true;
            gadget.AddChild(conditionVisibility);

            unsavedIndicator.SizeFlagsVertical = (int)SizeFlags.ShrinkCenter;
            unsavedIndicator.FocusMode = FocusModeEnum.None;
            gadget.AddChild(unsavedIndicator);

            messageBox.SetAnchorsAndMarginsPreset(LayoutPreset.BottomWide);
            messageBox.GrowVertical = GrowDirection.Begin;
            AddChild(messageBox);

            content.GetChild(0).Name = "root";

            SetProcess(false);
        }

        [OnReady]
        public void RealReady()
        {
            createNewStateMachineContainer.Visible = false;
            createNewStateMachine.Connect("pressed", this, nameof(OnCreateNewStateMachinePressed));
            contextMenu.Connect("index_pressed", this, nameof(OnContextMenuIndexPressed));
            stateNodeContextMenu.Connect("index_pressed", this, nameof(OnStateNodeContextMenuIndexPressed));
            convertToStateConfirmation.Connect("confirmed", this, nameof(OnConvertToStateConfirmationConfirmed));
            saveDialog.Connect("confirmed", this, nameof(OnSaveDialogConfirmed));
        }

        public override void _Process(float delta)
        {
            // Process is only used by the debug
            if (!DebugMode)
            {
                SetProcess(false);
                return;
            }
            if (!IsInstanceValid(stateMachinePlayer))
            {
                SetProcess(false);
                DebugMode = false;
                return;
            }
            var stack = stateMachinePlayer.Stack;
            if (stack.Count > 0)
            {
                SetProcess(false);
                DebugMode = false;
                return;

            }
            if (stack.Count == 1)
            {
                SetCurrentState(stateMachinePlayer.Current);
            }
            else
            {
                var stackMaxIndex = stack.Count - 1;
                var prevIndex = stack.IndexOf(currentState);
                if (prevIndex == -1)
                {
                    if (lastStack.Count < stack.Count)
                    {
                        // Reproduce transition, for example:
                        // [Entry, Idle, Walk]
                        // [Entry, Idle, Jump, Fall]
                        // Walk -> Idle
                        // Idle -> Jump
                        // Jump -> Fall
                        int commonIndex = -1;
                        for (int i = 0; i < lastStack.Count; i++)
                        {
                            if (lastStack[i] == stack[i])
                            {
                                commonIndex = i;
                                break;
                            }
                        }
                        if (commonIndex > -1)
                        {
                            var countFromLastStack = lastStack.Count - 1 - commonIndex - 1;
                            lastStack.Reverse();
                            // Transit back to common state
                            for (int i = 0; i < countFromLastStack; i++)
                                SetCurrentState(lastStack[i + 1]);

                            // Transit to all missing state in current stack
                            for (int i = commonIndex + 1; i < stack.Count; i++)
                                SetCurrentState(stack[i]);
                        }
                        else
                            SetCurrentState(stack.PeekBack());
                    }
                    else
                        SetCurrentState(stack.PeekBack());
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
            lastStack = new List<string>(stack);
            var globalParams = stateMachinePlayer.Parameters;
            var localParams = stateMachinePlayer.LocalParamters;
            paramPanel.UpdateParams(globalParams, localParams);
            GetFocusedLayer(currentState).DebugUpdate(currentState, globalParams, localParams);
        }

        private void OnPathViewerDirPressed(string dir, int index)
        {
            var path = pathViewer.SelectDir(dir);
            SelectLayer(GetLayer(path));

            if (lastIndex > index)
            {
                // Going backward
                var endStateParentPath = StateMachinePlayer.PathBackward(lastPath);
                var endStateName = StateMachinePlayer.PathEndDir(lastPath);
                var layer = content.GetNodeOrNull<StateMachineEditorLayer>(endStateParentPath);
                if (layer != null)
                {
                    var node = layer.ContentNodes.GetNodeOrNull<StateNode>(endStateName);
                    if (node != null && node.State is StateMachine)
                        // Convert state machine node back to state node
                        ConvertToState(layer, node);
                }
            }
            lastIndex = index;
            lastPath = path;
        }

        /// <summary>
        /// Handles instancings nodes
        /// </summary>
        /// <param name="index"></param>
        private void OnContextMenuIndexPressed(int index)
        {
            var newNode = stateNodePrefab.Instance<StateNode>();
            newNode.Theme.GetStylebox("focus", "FlowChartNode").Set("border_color", editorAccentColor);
            switch (index)
            {
                case 0: // Add State
                    newNode.Name = "State";
                    break;
                case 1: // Add Entry
                    if (currentLayer.StateMachine.States.Contains(State.EntryState))
                    {
                        GD.PushWarning("Entry node already exist");
                        return;
                    }
                    newNode.Name = State.EntryState;
                    break;
                case 2: // Add Exit
                    if (currentLayer.StateMachine.States.Contains(State.ExitState))
                    {
                        GD.PushWarning("Exit node already exist");
                        return;
                    }
                    newNode.Name = State.ExitState;
                    break;
            }
            newNode.RectPosition = ContentPosition(GetLocalMousePosition());
            AddNode(currentLayer, newNode);
        }

        private void OnStateNodeContextMenuIndexPressed(int index)
        {
            if (contextNode == null)
                return;

            switch (index)
            {
                case 0: // Copy
                    copyingNodes = new GDC.Array<Control>() { contextNode };
                    contextNode = null;
                    break;
                case 1: // Duplicate
                    DuplicateNodes(currentLayer, new GDC.Array<Control>() { contextNode });
                    contextNode = null;
                    break;
                case 2: // Separator
                    contextNode = null;
                    break;
                case 3: // Convert
                    convertToStateConfirmation.PopupCentered();
                    break;
            }
        }

        private void OnConvertToStateConfirmationConfirmed()
        {
            ConvertToState(currentLayer, contextNode);
            contextNode.Update(); // Update display of node

            // Remove layer
            var path = GD.Str(pathViewer.GetCwd(), "/", contextNode.Name);
            var layer = GetLayer(path);
            if (layer != null)
                layer.QueueFree();
            contextNode = null;
        }

        private void OnSaveDialogConfirmed()
        {
            Save();
        }

        private void OnCreateNewStateMachinePressed()
        {
            var newStateMachine = new StateMachine();
            stateMachinePlayer.StateMachine = newStateMachine;
            StateMachine = newStateMachine;
            createNewStateMachineContainer.Visible = false;
            CheckHasEntry();
            EmitSignal(nameof(InspectorChanged), "StateMachine");

        }

        private void OnConditionVisibilityPressed()
        {
            foreach (TransitionLine line in currentLayer.ContentLines.GetChildren())
                line.ConditionVisibility = conditionVisibility.Pressed;
        }

        private void OnDebugModeChanged(bool newDebugMode)
        {
            if (newDebugMode)
            {
                paramPanel.Show();
                AddMessage(DebugModeMsg);
                SetProcess(true);
                // mouseFilter = MOUSEFilterIgnore;
                CanGuiSelectNode = false;
                CanGuiDeleteNode = false;
                CanGuiConnectNode = false;
                CanGuiNameEdit = false;
                CanGuiContextMenu = false;
            }
            else
            {
                paramPanel.ClearParams();
                paramPanel.Hide();
                RemoveMessage(DebugModeMsg);
                SetProcess(false);
                CanGuiSelectNode = true;
                CanGuiDeleteNode = true;
                CanGuiConnectNode = true;
                CanGuiNameEdit = true;
                CanGuiContextMenu = true;

            }
        }

        private void OnStateMachinePlayerChanged(StateMachinePlayer newStateMachinePlayer)
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

        private void OnStateMachineChanged(StateMachine newStateMachine)
        {
            var rootLayer = GetLayer("root");
            pathViewer.SelectDir("root");// Before selectLayer, so pathViewer will be updated in OnLayerSelected
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
                        if (mouseButtonEvent.Pressed && CanGuiContextMenu)
                        {
                            contextMenu.SetItemDisabled(1, currentLayer.StateMachine.HasEntry);
                            contextMenu.SetItemDisabled(2, currentLayer.StateMachine.HasExit);
                            contextMenu.RectPosition = GetViewport().GetMousePosition();
                            contextMenu.Popup_();
                        }
                        break;
                }
            }
        }

        public override void _Input(InputEvent inputEvent)
        {
            // Intercept save action
            if (Visible && inputEvent is InputEventKey keyEvent)
            {
                switch (keyEvent.Scancode)
                {
                    case (int)ButtonList.Right:
                        if (keyEvent.Control && keyEvent.Pressed)
                            SaveRequest();
                        break;
                }
            }
        }

        public StateMachineEditorLayer CreateLayer(StateNode node)
        {
            // Create/Move to new layer
            var newStateMachine = ConvertToStateMachine(currentLayer, node);
            // Determine current layer path
            var parentPath = pathViewer.GetCwd();
            var path = GD.Str(parentPath, "/", node.Name);
            var layer = GetLayer(path);
            pathViewer.AddDir(node.State.Name);// Before selectLayer, so pathViewer will be updated in OnLayerSelected
            if (layer == null)
            {
                // New layer to spawn
                layer = AddLayerTo(GetLayer(parentPath));
                layer.Name = node.State.Name;
                layer.StateMachine = newStateMachine;
                DrawGraph(layer);
            }
            lastIndex = pathViewer.GetChildCount() - 1;
            lastPath = path;
            return layer;

        }

        public StateMachineEditorLayer OpenLayer(string path)
        {
            var dir = new StateDirectory(path);
            dir.Goto(dir.EndIndex);
            dir.Back();
            var nextLayer = GetNextLayer(dir, GetLayer("root"));
            SelectLayer(nextLayer);
            return nextLayer;
        }

        /// <summary>
        /// Recursively get next layer
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="baseLayer"></param>
        /// <returns></returns>
        public StateMachineEditorLayer GetNextLayer(StateDirectory dir, StateMachineEditorLayer baseLayer)
        {
            var nextLayer = baseLayer;
            var nextLayerName = dir.Next();
            if (nextLayerName != null)
            {
                nextLayer = baseLayer.GetNodeOrNull<StateMachineEditorLayer>(nextLayerName);
                if (nextLayer != null)
                {
                    nextLayer = GetNextLayer(dir, nextLayer);
                }
                else
                {
                    var toDir = new StateDirectory(dir.Current);

                    toDir.Goto(toDir.EndIndex);
                    toDir.Back();
                    var node = baseLayer.ContentNodes.GetNodeOrNull<StateNode>(toDir.CurrentEnd);
                    nextLayer = GetNextLayer(dir, CreateLayer(node));
                }
            }
            return nextLayer;
        }

        public StateMachineEditorLayer GetFocusedLayer(string state)
        {
            var currentDir = new StateDirectory(state);

            currentDir.Goto(currentDir.EndIndex);
            currentDir.Back();
            return GetLayer(GD.Str("root/", currentDir.Current));

        }

        private void OnStateNodeGuiInput(InputEvent inputEvent, StateNode node)
        {
            if (node.State.IsEntry || node.State.IsExit)
                return;

            if (inputEvent is InputEventMouseButton mouseButtonEvent)
            {
                switch (mouseButtonEvent.ButtonIndex)
                {
                    case (int)ButtonList.Left:
                        if (mouseButtonEvent.Pressed)
                        {
                            if (mouseButtonEvent.Doubleclick)
                            {
                                if (node.NameEdit.GetRect().HasPoint(mouseButtonEvent.Position) && CanGuiNameEdit)
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
                    case (int)ButtonList.Right:
                        if (mouseButtonEvent.Pressed)
                        {
                            // State node context menu
                            contextNode = node;
                            stateNodeContextMenu.RectPosition = GetViewport().GetMousePosition();
                            stateNodeContextMenu.Popup_();
                            stateNodeContextMenu.SetItemDisabled(3, !(node.State is StateMachine));
                            AcceptEvent();

                        }
                        break;
                }
            }
        }

        private StateMachine ConvertToStateMachine(StateMachineEditorLayer layer, StateNode stateNnode)
        {
            // Convert State to StateMachine
            StateMachine newStateMachine;
            if (stateNnode.State is StateMachine stateMachine)
                newStateMachine = stateMachine;
            else
            {
                newStateMachine = new StateMachine();

                newStateMachine.Name = stateNnode.State.Name;
                newStateMachine.GraphOffset = stateNnode.State.GraphOffset;
                layer.StateMachine.RemoveState(stateNnode.State.Name);
                layer.StateMachine.AddState(newStateMachine);
                stateNnode.State = newStateMachine;
            }
            return newStateMachine;
        }

        private State ConvertToState(StateMachineEditorLayer layer, StateNode statNnode)
        {
            // Convert StateMachine to State
            State newState;
            if (statNnode.State is StateMachine)
            {
                newState = new State();
                newState.Name = statNnode.State.Name;
                newState.GraphOffset = statNnode.State.GraphOffset;
                layer.StateMachine.RemoveState(statNnode.State.Name);
                layer.StateMachine.AddState(newState);
                statNnode.State = newState;
            }
            else
            {
                newState = statNnode.State;
            }
            return newState;
        }

        public override FlowChartLayer CreateLayerInstance()
        {
            var layer = stateMachineEditorLayerPrefab.Instance<StateMachineEditorLayer>();
            layer.Construct(editorAccentColor);
            return layer;
        }

        public override FlowChartLine CreateLineInstance()
        {
            var line = transitionLinePrefab.Instance<TransitionLine>();
            line.Theme.GetStylebox("focus", "FlowChartLine").Set("shadow_color", editorAccentColor);
            line.Theme.SetIcon("arrow", "FlowChartLine", transitionArrowIcon);
            return line;
        }

        /// <summary>
        /// Request to save current editing StateMachine
        /// </summary>
        public void SaveRequest()
        {
            if (!CanSave())
                return;

            saveDialog.DialogText = $"Saving StateMachine to {stateMachine.ResourcePath}";
            saveDialog.PopupCentered();
        }

        /// <summary>
        /// Save current editing StateMachine
        /// </summary>
        public void Save()
        {
            if (!CanSave())
                return;

            unsavedIndicator.Text = "";
            ResourceSaver.Save(stateMachine.ResourcePath, stateMachine);
        }

        /// <summary>
        /// Clear editor
        /// </summary>
        /// <param name="layer"></param>
        public void ClearGraph(FlowChartLayer layer)
        {
            ClearConnections();
            foreach (Control child in layer.ContentNodes.GetChildren())
            {
                if (child is StateNode)
                {
                    layer.ContentNodes.RemoveChild(child);
                    child.QueueFree();
                }
            }
            unsavedIndicator.Text = ""; // Clear graph is not action by user
        }

        /// <summary>
        /// Intialize editor with current editing StateMachine
        /// </summary>
        /// <param name="layer"></param>
        public void DrawGraph(FlowChartLayer layer)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;

            foreach (string stateKey in stateLayer.StateMachine.States.Keys)
            {
                var state = stateLayer.StateMachine.States.Get<State>(stateKey);
                var newNode = stateNodePrefab.Instance<StateNode>();
                newNode.Theme.GetStylebox("focus", "FlowChartNode").Set("border_color", editorAccentColor);
                newNode.Name = stateKey; // Set before addNode to let engine handle duplicate name
                AddNode(stateLayer, newNode);
                // Set after addNode to make sure UIs are initialized
                newNode.State = state;
                newNode.State.Name = stateKey;
                newNode.RectPosition = state.GraphOffset;
            }
            foreach (string stateKey in stateLayer.StateMachine.States.Keys)
            {
                var fromTransitions = stateLayer.StateMachine.GetNodeTransitionsDict(stateKey);
                if (fromTransitions != null)
                {
                    foreach (Transition transition in fromTransitions.Values)
                    {
                        ConnectNode(stateLayer, transition.From, transition.To);
                        var transitionLine = stateLayer.GetConnection(transition.From, transition.To).Line as TransitionLine;
                        transitionLine.Transition = transition;
                    }
                }
            }
            Update();
            unsavedIndicator.Text = ""; // Draw graph is not an action by user
        }

        /// <summary>
        /// Add message to MessageBox (overlay text at bottom of editor)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public Label AddMessage(MessageBoxMessage message)
        {
            var label = new Label();
            label.Text = message.Text;
            messageBoxDict[message.Key] = label;
            messageBox.AddChild(label);
            return label;
        }

        /// <summary>
        /// Returns whether the MessageBox already has <paramref name="message"/> or not.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public bool HasMessage(MessageBoxMessage message)
        {
            return messageBoxDict.Contains(message.Key);
        }

        /// <summary>
        /// Remove message from the MessageBox
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool RemoveMessage(MessageBoxMessage message)
        {
            var control = messageBoxDict.Get<Label>(message.Key);
            if (control != null)
            {
                messageBoxDict.Remove(message.Key);
                messageBox.RemoveChild(control);
                // Weird behavior of VBoxContainer, only sort children properly after changing growDirection
                messageBox.GrowVertical = GrowDirection.End;
                messageBox.GrowVertical = GrowDirection.Begin;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Check if current editing StateMachine has entry, warns user if entry state missing
        /// </summary>
        public void CheckHasEntry()
        {
            if (currentLayer.StateMachine == null)
                return;

            if (currentLayer.StateMachine.HasEntry)
            {
                // Has entry so remove any entry missing messages
                if (HasMessage(EntryStateMissingMsg))
                    RemoveMessage(EntryStateMissingMsg);
            }
            else
            {
                // Doesn't have entry, so add entry state missing message
                if (!HasMessage(EntryStateMissingMsg))
                    AddMessage(EntryStateMissingMsg);
            }
        }

        /// <summary>
        /// Check if current editing StateMachine is nested && has exit, warns user if exit state missing
        /// </summary>
        public void CheckHasExit()
        {
            if (currentLayer.StateMachine == null)
                return;

            if (pathViewer.GetCwd() != "root") // Nested state
            {
                if (!currentLayer.StateMachine.HasExit && !HasMessage(ExitStateMissingMsg))
                    AddMessage(ExitStateMissingMsg);

            }
            else
            {
                if (HasMessage(ExitStateMissingMsg))
                    RemoveMessage(ExitStateMissingMsg);
            }
        }

        #region Flowchart Lifetime Calls
        protected override void OnLayerSelected(FlowChartLayer layer)
        {
            if (layer != null)
            {
                layer.ShowContent();
                CheckHasEntry();
                CheckHasExit();
            }
        }

        protected override void OnLayerDeselected(FlowChartLayer layer)
        {
            if (layer != null)
                layer.HideContent();
        }

        protected override void OnNodeDragged(FlowChartLayer layer, Control node, Vector2 dragDelta)
        {
            if (!(node is StateNode stateNode)) return;

            stateNode.State.GraphOffset = stateNode.RectPosition;
            OnEdited();
        }

        protected override void OnNodeAdded(FlowChartLayer layer, Control newNode)
        {
            if (!(newNode is StateNode stateNode) || !(layer is StateMachineEditorLayer stateLayer)) return;

            stateNode.Construct(undoRedo);
            stateNode.State.Name = stateNode.Name;
            stateNode.State.GraphOffset = stateNode.RectPosition;
            stateNode.Connect(nameof(StateNode.NameEditEntered), this, nameof(OnNodeNameEditEntered), GDUtils.GDParams(stateNode));
            stateNode.Connect("gui_input", this, nameof(OnStateNodeGuiInput), GDUtils.GDParams(stateNode));

            stateLayer.StateMachine.AddState(stateNode.State);
            CheckHasEntry();
            CheckHasExit();
            OnEdited();
        }

        protected override void OnNodeRemoved(FlowChartLayer layer, Control node)
        {
            var path = GD.Str(pathViewer.GetCwd(), "/", node.Name);
            var layerToRemove = GetLayer(path);
            if (layerToRemove != null)
            {
                layerToRemove.GetParent().RemoveChild(layerToRemove);
                layerToRemove.QueueFree();
            }
            //var result = layer.StateMachine.RemoveState(nodeName);
            CheckHasEntry();
            CheckHasExit();
            OnEdited();
            //return result;

        }

        protected override void OnNodeConnected(FlowChartLayer layer, string from, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;

            if (reconnectingConnection != null)
            {
                // Reconnection will trigger OnNodeConnected after OnNodeReconnectEnd/On_node_reconnect_failed
                if (reconnectingConnection.FromNode.Name == from && reconnectingConnection.ToNode.Name == to)
                {
                    reconnectingConnection = null;
                    return;
                }
            }

            if (stateLayer.StateMachine.GetTransition(from, to) != null)
                return; // Transition already exists as it is loaded from file

            var line = stateLayer.GetConnection(from, to).Line as TransitionLine;
            var newTransition = new Transition(from, to);
            line.Transition = newTransition;
            stateLayer.StateMachine.AddTransition(newTransition);
            ClearSelection();
            Select(line);
            OnEdited();

        }

        protected override void OnNodeDisconnected(FlowChartLayer layer, string from, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;
            stateLayer.StateMachine.RemoveTransition(from, to);
            OnEdited();

        }

        protected override void OnNodeReconnectBegin(FlowChartLayer layer, string from, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;
            reconnectingConnection = stateLayer.GetConnection(from, to);
            stateLayer.StateMachine.RemoveTransition(from, to);
        }

        protected override void OnNodeReconnectEnd(FlowChartLayer layer, string from, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;
            var transition = (reconnectingConnection.Line as TransitionLine).Transition;
            transition.To = to;
            stateLayer.StateMachine.AddTransition(transition);
            ClearSelection();
            Select(reconnectingConnection.Line);
        }

        protected override void OnNodeReconnectFailed(FlowChartLayer layer, string from, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;
            var transition = (reconnectingConnection.Line as TransitionLine).Transition;
            stateLayer.StateMachine.AddTransition(transition);
            ClearSelection();
            Select(reconnectingConnection.Line);

        }

        protected override bool _RequestConnectFrom(FlowChartLayer layer, string from)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return false;
            if (from == State.ExitState)
                return false;
            return true;

        }

        protected override bool _RequestConnectTo(FlowChartLayer layer, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return false;
            if (to == State.EntryState)
                return false;
            return true;

        }

        protected override void OnDuplicated(FlowChartLayer layer, GDC.Array<Control> oldNodes, GDC.Array<Control> newNodes)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;
            // Duplicate condition as well
            for (int i = 0; i < oldNodes.Count; i++)
            {
                var fromNode = oldNodes[i];
                foreach (var connectionPair in GetConnectionList())
                {
                    if (fromNode.Name == connectionPair.From)
                    {
                        for (int j = 0; j < oldNodes.Count; j++)
                        {
                            var toNode = oldNodes[j];
                            if (toNode.Name == connectionPair.To)
                            {
                                var oldConnection = stateLayer.GetConnection(connectionPair);
                                var newConnection = stateLayer.GetConnection(newNodes[i].Name, newNodes[j].Name);
                                var oldConnectionTransitionLine = oldConnection.Line as TransitionLine;
                                var newConnectionTransitionLine = newConnection.Line as TransitionLine;
                                foreach (Condition condition in oldConnectionTransitionLine.Transition.Conditions.Values)
                                    newConnectionTransitionLine.Transition.AddCondition(condition.Duplicate() as Condition);
                            }
                        }
                    }
                }
            }
            OnEdited();
        }
        #endregion

        public void OnNodeNameEditEntered(string newName, StateNode node)
        {
            var old = node.State.Name;

            // TODO: Refactor this to not edit name edit directly from the state node (seems like an intrusion of responsibility)
            if (old == newName)
                return;
            if (newName.Contains("/") || newName.Contains("\\")) // No back/forward-slash
            {
                GD.PushWarning($"Illegal State Name: / && \\ are !allowed in State Name({newName})");
                node.NameEdit.Text = old;
                return;
            }

            if (currentLayer.StateMachine.ChangeStateName(old, newName))
            {
                RenameNode(currentLayer, node.Name, newName);
                node.Name = newName;
                // Rename layer as well
                var path = GD.Str(pathViewer.GetCwd(), "/", node.Name);
                var layer = GetLayer(path);
                if (layer == null)
                    layer.Name = newName;

                // TODO: Don't pull children directly from path viewer, that's path viewer's responsibility
                foreach (Label child in pathViewer.GetChildren())
                {
                    if (child.Text == old)
                    {
                        child.Text = newName;
                        break;
                    }
                }
                OnEdited();
            }
            else
                node.NameEdit.Text = old;
        }

        public void OnEdited()
        {
            unsavedIndicator.Text = "*";

        }

        public void OnRemoteTransited(string from, string to)
        {
            var fromDir = new StateDirectory(from);
            var toDir = new StateDirectory(to);

            var focusedLayer = GetFocusedLayer(from);
            if (from != null)
            {
                if (focusedLayer != null)
                    focusedLayer.DebugTransitOut(from, to);
            }

            if (to != null)
            {
                if (fromDir.IsNested && fromDir.IsExit)
                {
                    if (focusedLayer != null)
                    {
                        var path = pathViewer.Back();
                        SelectLayer(GetLayer(path));
                    }
                }
                else if (toDir.IsNested)
                {
                    if (toDir.IsEntry && focusedLayer != null)
                    {
                        // Open into next layer
                        toDir.Goto(toDir.EndIndex);
                        toDir.Back();
                        var node = focusedLayer.ContentNodes.GetNodeOrNull<StateNode>(toDir.CurrentEnd);
                        if (node != null)
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
                else if (fromDir.IsNested && !fromDir.IsExit)
                {
                    if (toDir.Dirs.Length != fromDir.Dirs.Length)
                    {
                        toDir.Goto(toDir.EndIndex);
                        var n = toDir.Back();
                        if (n == null)
                        {
                            n = "root";
                        }
                        var layer = GetLayer(n);
                        pathViewer.SelectDir(layer.Name);
                        SelectLayer(layer);

                    }
                }
                focusedLayer = GetFocusedLayer(to);
                if (focusedLayer == null)
                    focusedLayer = OpenLayer(to);

                focusedLayer.DebugTransitIn(from, to);
            }
        }

        /// <summary>
        /// Return true if current editing StateMachine can be saved, ignore built-in resource
        /// </summary>
        /// <returns></returns>
        public bool CanSave()
        {
            if (StateMachine == null)
                return false;

            var resourcePath = stateMachine.ResourcePath;
            if (resourcePath.Empty())
                return false;

            if (resourcePath.Contains(".scn") || resourcePath.Contains(".tscn")) // Built-in resource will be saved by scene
                return false;
            return true;
        }

        public void SetCurrentState(string v)
        {
            if (currentState != v)
            {
                var from = currentState;
                var to = v;
                currentState = v;
                OnRemoteTransited(from, to);
            }
        }
    }
}