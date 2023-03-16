
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Utils;
using System.Collections.Generic;
using Fractural.Flowchart;
using System.Linq;

namespace Fractural.StateMachine
{
    // TODO: Refactor out messaging from the editor
    [Tool]
    public partial class StateMachineEditor : Flowchart.Flowchart
    {
        /// <summary>
        /// Emitted to inform plugin to refresh inspector
        /// </summary>
        /// <param name="property"></param>
        [Signal] public delegate void InspectorChanged(string property);
        /// <summary>
        /// Emitted when the user selects the background without selecting any nodes
        /// </summary>
        [Signal] public delegate void BackgroundSelected();

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

        private PackedScene stateNodePrefab;
        private PackedScene StateNodePrefab
        {
            get
            {
                if (stateNodePrefab == null)
                    stateNodePrefab = GD.Load<PackedScene>("res://addons/FracturalFSM/Editor/State/StateNode.tscn");
                return stateNodePrefab;
            }
        }

        private PackedScene transitionLinePrefab;
        private PackedScene TransitionLinePrefab
        {
            get
            {
                if (transitionLinePrefab == null)
                    transitionLinePrefab = GD.Load<PackedScene>("res://addons/FracturalFSM/Editor/Transition/TransitionLine.tscn");
                return transitionLinePrefab;
            }
        }

        private PackedScene stateLayerPrefab;
        private PackedScene StateLayerPrefab
        {
            get
            {
                if (stateLayerPrefab == null)
                    stateLayerPrefab = GD.Load<PackedScene>("res://addons/FracturalFSM/Editor/StateMachine/StateMachineEditorLayer.tscn");
                return stateLayerPrefab;
            }
        }
        #endregion

        #region Public properties
        public bool DebugMode { get; private set; }
        public StateMachinePlayer StateMachinePlayer { get; private set; }
        public InspectorRemoteStateMachinePlayer RemoteStateMachinePlayer { get; private set; }
        public StateMachine StateMachine { get; private set; }
        public bool CanGuiNameEdit { get; private set; } = true;
        public bool CanGuiContextMenu { get; private set; } = true;
        #endregion

        #region Private fields
        private Connection reconnectingConnection;
        private int previousIndex = 0;
        private string previousPath = "";
        private StateNode contextNode;
        private string currentDebugState = "";
        private IList<string> lastStack = new List<string>();
        #endregion

        #region Flowchart Method/Property Hiding
        // We need to change methods to return StateMachineEditorLayer
        // rather than FlowchartLayer

        public new StateMachineEditorLayer CurrentLayer
        {
            get => base.CurrentLayer as StateMachineEditorLayer;
            set => base.CurrentLayer = value;
        }

        public new StateMachineEditorLayer AddLayerTo(Control target)
            => base.AddLayerTo(target) as StateMachineEditorLayer;

        public new StateMachineEditorLayer GetLayer(NodePath nodePath)
            => base.GetLayer(nodePath) as StateMachineEditorLayer;
        #endregion





        #region Loading StateMachines, StateMachinePlayer, or RemoteStateMachinePlayers
        /// <summary>
        /// Load in a StateMachine for editing. Returns true if successfully loaded.
        /// </summary>
        /// <param name="stateMachine"></param>
        /// <returns></returns>
        public bool TryLoad(StateMachine stateMachine)
        {
            // Don't reload if we're already loaded. Otherwise this can cause weird bugs with
            // node selection, since mid way through processing the _GuiInput the entire editor gets refreshed.
            if (!DebugMode && StateMachinePlayer == null && StateMachine == stateMachine && RemoteStateMachinePlayer == null) return false;
            if (!IsUnloaded) Unload();
            ConfigureDebugMode(false);
            ConfigureNewStateMachinePlayer(null);
            ConfigureNewStateMachine(stateMachine);
            RemoteStateMachinePlayer = null;
            return true;
        }

        /// <summary>
        /// Load in a StateMachinePlayer for editing. Returns true if successfully loaded.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool TryLoad(StateMachinePlayer player)
        {
            // Don't reload if we're already loaded. Otherwise this can cause weird bugs with
            // node selection, since mid way through processing the _GuiInput the entire editor gets refreshed.
            if (!DebugMode && StateMachinePlayer == player && StateMachine == null && RemoteStateMachinePlayer == null) return false;
            if (!IsUnloaded) Unload();
            ConfigureDebugMode(false);
            ConfigureNewStateMachinePlayer(player);
            ConfigureNewStateMachine(player.StateMachine);
            RemoteStateMachinePlayer = null;
            return true;
        }

        /// <summary>
        /// Load in a InspectorRemoteStateMachinePlayer for debug viewing. Returns true if successfully loaded.
        /// </summary>
        /// <param name="remotePlayer"></param>
        /// <returns></returns>
        public bool TryLoad(InspectorRemoteStateMachinePlayer remotePlayer)
        {
            // Don't reload if we're already loaded. Otherwise this can cause weird bugs with
            // node selection, since mid way through processing the _GuiInput the entire editor gets refreshed.
            if (DebugMode && StateMachinePlayer == null && StateMachine == null && RemoteStateMachinePlayer == remotePlayer) return false;
            if (!IsUnloaded) Unload();
            // Turn on debugging mode
            ConfigureDebugMode(true);
            ConfigureNewStateMachinePlayer(null);
            ConfigureNewStateMachine(remotePlayer.StateMachine);
            RemoteStateMachinePlayer = remotePlayer;
            return true;
        }

        public bool IsUnloaded => StateMachine == null && StateMachinePlayer == null && RemoteStateMachinePlayer == null;

        /// <summary>
        /// Unload whatever's currently loaded (ie. state machine, state machine player, or remote state machine)
        /// </summary>
        public void Unload()
        {
            ClearSelection();
            ConfigureDebugMode(false);
            ConfigureNewStateMachine(null);
            ConfigureNewStateMachinePlayer(null);
            RemoteStateMachinePlayer = null;
            ResetLayersBackToRoot();
            currentDebugState = "";
            currentConnection = null;
            isConnecting = false;
            isDragging = false;
            isDraggingNode = false;
            dragStartPos = Vector2.Zero;
            dragEndPos = Vector2.Zero;
        }

        public void ResetLayersBackToRoot()
        {
            var rootLayer = GetLayer("root");
            SelectLayer(rootLayer);
            ClearGraph(rootLayer);
            foreach (Node child in rootLayer.ContentNodes.GetChildren())
                if (child is FlowchartLayer)
                    child.QueueFree();
        }

        /// <summary>
        /// Called once to initialize the StateMachineEditor
        /// </summary>
        /// <param name="undoRedo"></param>
        public void Construct(UndoRedo undoRedo)
        {
            this.undoRedo = undoRedo;
        }
        #endregion

        #region Godot Lifecycle Methods
        [OnReady]
        public void RealReady()
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
            toolbar.AddChild(conditionVisibility);

            unsavedIndicator.SizeFlagsVertical = (int)SizeFlags.ShrinkCenter;
            unsavedIndicator.FocusMode = FocusModeEnum.None;
            toolbar.AddChild(unsavedIndicator);

            messageBox.SetAnchorsAndMarginsPreset(LayoutPreset.BottomWide);
            messageBox.GrowVertical = GrowDirection.Begin;
            AddChild(messageBox);

            // Name first layer root.
            GetLayerAt(0).Name = "root";
            SetProcess(false);

            createNewStateMachineContainer.Visible = false;
            createNewStateMachine.Connect("pressed", this, nameof(OnCreateNewStateMachinePressed));
            contextMenu.Connect("index_pressed", this, nameof(OnContextMenuIndexPressed));
            stateNodeContextMenu.Connect("index_pressed", this, nameof(OnStateNodeContextMenuIndexPressed));
            convertToStateConfirmation.Connect("confirmed", this, nameof(OnConvertToStateConfirmationConfirmed));
            saveDialog.Connect("confirmed", this, nameof(OnSaveDialogConfirmed));

            var theme = this.GetThemeFromAncestor(true);
            SelectionStylebox.BgColor = theme.GetColor("box_selection_fill_color", "Editor");
            SelectionStylebox.BorderColor = theme.GetColor("box_selection_stroke_color", "Editor");
            zoomMinus.Icon = theme.GetIcon("ZoomLess", "EditorIcons");
            zoomReset.Icon = theme.GetIcon("ZoomReset", "EditorIcons");
            zoomPlus.Icon = theme.GetIcon("ZoomMore", "EditorIcons");
            snapButton.Icon = theme.GetIcon("SnapGrid", "EditorIcons");
            conditionVisibility.TexturePressed = theme.GetIcon("GuiVisibilityVisible", "EditorIcons");
            conditionVisibility.TextureNormal = theme.GetIcon("GuiVisibilityHidden", "EditorIcons");
            editorAccentColor = theme.GetColor("accent_color", "Editor");
            transitionArrowIcon = theme.GetIcon("TransitionImmediateBig", "EditorIcons");

            // CurrentLayer should be set by now, since it's automatically set in the constructor of Flowchart with Select
            // However the layer would have been created with the old accent color, so we're going to inject
            // the new accent color we just got from the theme into it.
            CurrentLayer.Construct(editorAccentColor);
        }

        public override void _EnterTree()
        {
            base._EnterTree();
            // Adjust ScrollMargin based on window size
            ScrollMargin = (int)(Mathf.Max(OS.WindowSize.x, OS.WindowSize.y) / 2f);
        }

        public override void _ExitTree()
        {
            // Clear selection when the state editor is hidden
            base._ExitTree();
            ClearSelection();
        }

        public override void _GuiInput(InputEvent inputEvent)
        {
            base._GuiInput(inputEvent);
            if (inputEvent is InputEventMouseButton mouseButtonEvent)
            {
                switch (mouseButtonEvent.ButtonIndex)
                {
                    case (int)ButtonList.Right:
                        if (mouseButtonEvent.Pressed && CanGuiContextMenu)
                        {
                            contextMenu.SetItemDisabled(1, CurrentLayer.StateMachine.HasEntry);
                            contextMenu.SetItemDisabled(2, CurrentLayer.StateMachine.HasExit);
                            contextMenu.RectPosition = GetViewport().GetMousePosition();
                            contextMenu.Popup_();
                        }
                        break;
                }
            }
        }

        public override void _Input(InputEvent inputEvent)
        {
            base._Input(inputEvent);
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


        // Process is only used by the debug
        public override void _Process(float delta)
        {
            DebugProcess();
        }
        #endregion

        #region Signal Wiring
        private void OnPathViewerDirPressed(string dir, int index)
        {
            var path = pathViewer.SelectDir(dir);
            SelectLayer(GetLayer(path));

            if (previousIndex > index)
            {
                // Going backward
                // rootState --> someState --> someOtherState --> endState
                //                     ^                            ^
                //                     index                        lastIndex
                // '--------------------------------.-------'     '---.--'
                //                              endStateParentPath  endStateName
                //                      
                var endStateBaseDirectory = StateDirectory.GetBaseDirectoryFromPath(previousPath);
                var endStateName = StateDirectory.GetStateFromPath(previousPath);
                var layer = content.GetNodeOrNull<StateMachineEditorLayer>(endStateBaseDirectory);
                if (layer != null)
                {
                    var node = layer.ContentNodes.GetNodeOrNull<StateNode>(endStateName);
                    if (node != null && node.State is StateMachine stateMachine && stateMachine.States.Count == 0)
                    {
                        // Convert empty state machine node back to state node
                        ConvertToStateAndRemoveLayer(previousPath);
                    }
                }
            }
            previousIndex = index;
            previousPath = path;
        }

        /// <summary>
        /// Handles instancings nodes
        /// </summary>
        /// <param name="index"></param>
        private void OnContextMenuIndexPressed(int index)
        {
            var newNode = StateNodePrefab.Instance<StateNode>();
            newNode.Theme.GetStylebox<StyleBoxFlat>("focus", "FlowchartNode").BorderColor = editorAccentColor;
            switch (index)
            {
                case 0: // Add State
                    newNode.Name = "State";
                    break;
                case 1: // Add Entry
                    if (CurrentLayer.StateMachine.States.Contains(State.EntryState))
                    {
                        GD.PushWarning("Entry node already exist");
                        return;
                    }
                    newNode.Name = State.EntryState;
                    break;
                case 2: // Add Exit
                    if (CurrentLayer.StateMachine.States.Contains(State.ExitState))
                    {
                        GD.PushWarning("Exit node already exist");
                        return;
                    }
                    newNode.Name = State.ExitState;
                    break;
            }
            newNode.RectPosition = ContentPosition(GetLocalMousePosition());
            AddNode(CurrentLayer, newNode);
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
                    DuplicateNodes(CurrentLayer, new GDC.Array<Control>() { contextNode });
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
            ConvertToStateAndRemoveLayer(pathViewer.GetCwd() + "/" + contextNode.Name);
            contextNode = null;
        }

        private void OnSaveDialogConfirmed()
        {
            Save();
        }

        private void OnCreateNewStateMachinePressed()
        {
            var newStateMachine = CSharpScript<StateMachine>.New();
            StateMachinePlayer.StateMachine = newStateMachine;
            StateMachine = newStateMachine;
            createNewStateMachineContainer.Visible = false;
            CheckHasEntry();
            EmitSignal(nameof(InspectorChanged), "StateMachine");
        }

        private void OnConditionVisibilityPressed()
        {
            foreach (TransitionLine line in CurrentLayer.ContentLines.GetChildren())
                line.ConditionVisibility = conditionVisibility.Pressed;
        }

        private void ConfigureDebugMode(bool debugMode)
        {
            DebugMode = debugMode;
            if (debugMode)
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

        private void ConfigureNewStateMachinePlayer(StateMachinePlayer newStateMachinePlayer)
        {
            StateMachinePlayer = newStateMachinePlayer;

            if (newStateMachinePlayer != null)
                createNewStateMachineContainer.Visible = newStateMachinePlayer.StateMachine == null;
            else
                createNewStateMachineContainer.Visible = false;
        }

        private void ConfigureNewStateMachine(StateMachine newStateMachine)
        {
            StateMachine = newStateMachine;
            pathViewer.SelectDir("root");

            if (newStateMachine != null)
            {
                var rootLayer = GetLayer("root");
                rootLayer.StateMachine = StateMachine;
                var corrected = StateMachine.Validate(newStateMachine);
                if (corrected)
                {
                    GD.Print("Corrupted FracturalFSM StateMachine Resource fixed, save to apply the fix.");
                }
                PopulateStateLayer(rootLayer);
                CheckHasEntry();
            }
        }

        public void OnNodeNewNameEntered(string newName, StateNode node)
        {
            var oldName = node.State.Name;
            if (oldName == newName)
                return;

            if (newName.Empty())
            {
                GD.PushWarning($"Illegal State Name: State name cannot be empty!");
                node.RevertStateName();
                return;
            }

            if (newName.Contains("/") || newName.Contains("\\")) // No back/forward-slash
            {
                GD.PushWarning($"Illegal State Name: / && \\ are !allowed in State Name({newName})");
                node.RevertStateName();
                return;
            }

            if (!CurrentLayer.StateMachine.ChangeStateName(oldName, newName))
            {
                GD.PushWarning($"State Name: {newName} already exists!");
                node.RevertStateName();
                return;
            }

            RenameNode(CurrentLayer, node.Name, newName);
            node.State.Name = newName;
            node.Name = newName;

            UpdateConnectionLines(node.Name);

            // Rename layer as well
            var path = GD.Str(pathViewer.GetCwd(), "/", node.Name);
            var layer = GetLayer(path);
            if (layer != null)
                layer.Name = newName;

            pathViewer.RenameDirectory(oldName, newName);
            OnEdited();
        }

        public void OnEdited()
        {
            unsavedIndicator.Text = "*";
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
                                // Edit State name if within LineEdit
                                if (CanGuiNameEdit && node.TryEnableNameEdit(mouseButtonEvent.Position))
                                    AcceptEvent();
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
        #endregion

        #region Utils
        private StateMachineEditorLayer CreateLayer(StateNode node)
        {
            var path = pathViewer.GetCwd() + "/" + node.Name;
            var result = TryConvertToStateMachineAndAddLayer(path);
            previousIndex = pathViewer.GetChildCount() - 1;
            previousPath = path;
            return result;
        }

        private StateMachineEditorLayer OpenLayer(string path)
        {
            var dir = new StateDirectory(path);
            dir.Goto(dir.EndIndex);
            dir.GotoPrevious();
            var nextLayer = GetNextLayer(dir, GetLayer("root"));
            SelectLayer(nextLayer);
            return nextLayer;
        }

        // TODO: Figure out what "next layer" means???
        /// <summary>
        /// Recursively get next layer
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="baseLayer"></param>
        /// <returns></returns>
        private StateMachineEditorLayer GetNextLayer(StateDirectory dir, StateMachineEditorLayer baseLayer)
        {
            var nextLayer = baseLayer;
            var nextLayerName = dir.GotoNext();
            if (nextLayerName != null)
            {
                nextLayer = baseLayer.GetNodeOrNull<StateMachineEditorLayer>(nextLayerName);
                if (nextLayer != null)
                {
                    nextLayer = GetNextLayer(dir, nextLayer);
                }
                else
                {
                    var toDir = new StateDirectory(dir.CurrentPath);

                    toDir.Goto(toDir.EndIndex);
                    toDir.GotoPrevious();
                    var node = baseLayer.ContentNodes.GetNodeOrNull<StateNode>(toDir.CurrentEnd);
                    nextLayer = GetNextLayer(dir, CreateLayer(node));
                }
            }
            return nextLayer;
        }

        private StateMachineEditorLayer GetFocusedLayer(string state)
        {
            var currentDir = new StateDirectory(state);

            currentDir.Goto(currentDir.EndIndex);
            currentDir.GotoPrevious();
            return GetLayer(GD.Str("root/", currentDir.CurrentPath));
        }

        /// <summary>
        /// Converts a <paramref name="stateNode"/> inside of <paramref name="layer"/> from a State to a StateMachine.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="stateNode"></param>
        /// <returns></returns>
        private StateMachineEditorLayer TryConvertToStateMachineAndAddLayer(string pathToState)
        {
            var stateBaseDirectory = StateDirectory.GetBaseDirectoryFromPath(pathToState);
            var stateName = StateDirectory.GetStateFromPath(pathToState);

            var stateBaseDirectoryLayer = GetLayer(stateBaseDirectory);
            if (stateBaseDirectoryLayer == null) return null;
            var stateNode = stateBaseDirectoryLayer.ContentNodes.GetNodeOrNull<StateNode>(stateName);

            // Convert State to StateMachine only if the node's State is not a StateMachine
            // NOTE: We can't use a !(stateNode.State is State) check here because
            //       StateMachine is a subclass of State, so this condition would mistake
            //       StateMachine for a State
            if (!(stateNode.State is StateMachine))
            {
                StateMachine newStateMachine = CSharpScript<StateMachine>.New();
                newStateMachine.Name = stateNode.State.Name;
                newStateMachine.GraphOffset = stateNode.State.GraphOffset;
                stateBaseDirectoryLayer.StateMachine.RemoveState(stateNode.State.Name);
                stateBaseDirectoryLayer.StateMachine.AddState(newStateMachine);
                stateNode.State = newStateMachine;
            }

            var stateMachineLayer = GetLayer(pathToState);
            pathViewer.AddDir(stateNode.State.Name);
            if (stateMachineLayer == null)
            {
                // New layer to spawn
                stateMachineLayer = AddLayerTo(stateBaseDirectoryLayer);
                stateMachineLayer.Name = stateNode.State.Name;
                stateMachineLayer.StateMachine = stateNode.State as StateMachine;
                PopulateStateLayer(stateMachineLayer);
            }
            return stateMachineLayer;
        }

        /// <summary>
        /// Converts a <paramref name="stateNode"/> inside of <paramref name="layer"/> from a StateMachine to a  State.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="stateNode"></param>
        /// <returns></returns>
        private void ConvertToStateAndRemoveLayer(string pathToStateMachine)
        {
            var stateBaseDirectory = StateDirectory.GetBaseDirectoryFromPath(pathToStateMachine);
            var stateName = StateDirectory.GetStateFromPath(pathToStateMachine);

            // Layer that contains the state as a node
            var stateBaseDirectoryLayer = GetLayer(stateBaseDirectory);
            if (stateBaseDirectoryLayer == null) return;
            var stateNode = stateBaseDirectoryLayer.ContentNodes.GetNodeOrNull<StateNode>(stateName);
            // TODO: Refactor accessing flowchart node through method instead of directory fetching ContentNodes

            // Convert State to StateMachine only if the node's State is not a State
            if (!(stateNode.State is StateMachine))
                return;

            State newState = CSharpScript<State>.New();
            newState.Name = stateNode.State.Name;
            newState.GraphOffset = stateNode.State.GraphOffset;
            stateBaseDirectoryLayer.StateMachine.RemoveState(stateNode.State.Name);
            stateBaseDirectoryLayer.StateMachine.AddState(newState);
            stateNode.State = newState;

            // Remove layer that represents this state
            // Note that this layer is a child of dirLayer.
            var stateMachineLayer = GetLayer(pathToStateMachine);
            if (stateMachineLayer != null)
                stateMachineLayer.QueueFree();
        }

        protected override FlowchartLayer CreateLayerInstance()
        {
            var layer = StateLayerPrefab.Instance<StateMachineEditorLayer>();
            layer.Construct(editorAccentColor);
            return layer;
        }

        protected override FlowchartLine CreateLineInstance()
        {
            var line = TransitionLinePrefab.Instance<TransitionLine>();
            line.Theme.GetStylebox<StyleBoxFlat>("focus", "FlowchartLine").ShadowColor = editorAccentColor;
            line.Theme.SetIcon("arrow", "FlowchartLine", transitionArrowIcon);
            return line;
        }

        /// <summary>
        /// Request to save current editing StateMachine
        /// </summary>
        private void SaveRequest()
        {
            if (!CanSave())
                return;

            saveDialog.DialogText = $"Saving StateMachine to {StateMachine.ResourcePath}";
            saveDialog.PopupCentered();
        }

        /// <summary>
        /// Save current editing StateMachine
        /// </summary>
        private void Save()
        {
            if (!CanSave())
                return;

            unsavedIndicator.Text = "";
            ResourceSaver.Save(StateMachine.ResourcePath, StateMachine);
        }

        /// <summary>
        /// Clears a graph layer
        /// </summary>
        /// <param name="layer"></param>
        public override void ClearGraph(FlowchartLayer layer)
        {
            base.ClearGraph(layer);
            unsavedIndicator.Text = ""; // Clear graph is not action by user
        }

        /// <summary>
        /// Intialize the node and connections for a state layer using its StateMachine
        /// </summary>
        /// <param name="layer"></param>
        private void PopulateStateLayer(StateMachineEditorLayer layer)
        {
            foreach (string stateKey in layer.StateMachine.States.Keys)
            {
                var state = layer.StateMachine.States.Get<State>(stateKey);
                var newNode = StateNodePrefab.Instance<StateNode>();
                newNode.Theme.GetStylebox<StyleBoxFlat>("focus", "FlowchartNode").BorderColor = editorAccentColor;
                newNode.Name = stateKey; // Set before addNode to let engine handle duplicate name
                AddNode(layer, newNode);
                // Set after addNode to make sure UIs are initialized
                newNode.State = state;
                newNode.State.Name = stateKey;
                newNode.RectPosition = state.GraphOffset;
            }
            foreach (string stateKey in layer.StateMachine.States.Keys)
            {
                var fromTransitions = layer.StateMachine.GetNodeTransitionsDict(stateKey);
                if (fromTransitions != null)
                {
                    foreach (Transition transition in fromTransitions.Values)
                    {
                        ConnectNode(layer, transition.From, transition.To);
                        var transitionLine = layer.GetConnection(transition.From, transition.To).Line as TransitionLine;
                        transitionLine.Transition = transition;
                    }
                }
            }
            Update();
            unsavedIndicator.Text = ""; // Draw graph is not an action by user
        }


        /// <summary>
        /// Check if current editing StateMachine has entry, warns user if entry state missing
        /// </summary>
        private void CheckHasEntry()
        {
            if (CurrentLayer.StateMachine == null)
                return;

            if (CurrentLayer.StateMachine.HasEntry)
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
            if (CurrentLayer.StateMachine == null)
                return;

            if (pathViewer.GetCwd() != "root") // Nested state
            {
                if (!CurrentLayer.StateMachine.HasExit)
                {
                    if (!HasMessage(ExitStateMissingMsg))
                        AddMessage(ExitStateMissingMsg);
                }
                else
                {
                    if (HasMessage(ExitStateMissingMsg))
                        RemoveMessage(ExitStateMissingMsg);
                }
            }
            else
            {
                if (HasMessage(ExitStateMissingMsg))
                    RemoveMessage(ExitStateMissingMsg);
            }
        }


        /// <summary>
        /// Return true if current editing StateMachine can be saved, ignore built-in resource
        /// </summary>
        /// <returns></returns>
        private bool CanSave()
        {
            if (StateMachine == null)
                return false;

            var resourcePath = StateMachine.ResourcePath;
            if (resourcePath.Empty())
                return false;

            if (resourcePath.Contains(".scn") || resourcePath.Contains(".tscn")) // Built-in resource will be saved by scene
                return false;
            return true;
        }
        #endregion

        #region MessageBox Methods
        // TODO: Might want to refactor out messaging, since the StateMachineEditor is a bit cramped now

        /// <summary>
        /// Add message to MessageBox (overlay text at bottom of editor)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        protected Label AddMessage(MessageBoxMessage message)
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
        protected bool HasMessage(MessageBoxMessage message)
        {
            return messageBoxDict.Contains(message.Key);
        }

        /// <summary>
        /// Remove message from the MessageBox
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected bool RemoveMessage(MessageBoxMessage message)
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
        #endregion

        #region Flowchart Lifetime Calls
        protected override void OnLayerSelected(FlowchartLayer layer)
        {
            if (layer != null)
            {
                layer.ShowContent();
                CheckHasEntry();
                CheckHasExit();
            }
        }

        protected override void OnLayerDeselected(FlowchartLayer layer)
        {
            if (layer != null)
                layer.HideContent();
        }

        protected override void OnNodeDragged(FlowchartLayer layer, Control node, Vector2 dragDelta)
        {
            if (!(node is StateNode stateNode)) return;

            stateNode.State.GraphOffset = stateNode.RectPosition;
            OnEdited();
        }

        protected override void OnNodeAdded(FlowchartLayer layer, Control newNode)
        {
            if (!(newNode is StateNode stateNode) || !(layer is StateMachineEditorLayer stateLayer)) return;

            stateNode.Construct(undoRedo);
            stateNode.State.Name = stateNode.Name;
            stateNode.State.GraphOffset = stateNode.RectPosition;
            stateNode.Connect(nameof(StateNode.NewNameEntered), this, nameof(OnNodeNewNameEntered), GDUtils.GDParams(stateNode));
            stateNode.Connect("gui_input", this, nameof(OnStateNodeGuiInput), GDUtils.GDParams(stateNode));

            stateLayer.StateMachine.AddState(stateNode.State);
            CheckHasEntry();
            CheckHasExit();
            OnEdited();
        }

        protected override void OnNodeRemoved(FlowchartLayer layer, Control node)
        {
            if (!(node is StateNode stateNode) || !(layer is StateMachineEditorLayer stateLayer)) return;

            var path = GD.Str(pathViewer.GetCwd(), "/", stateNode.Name); // stateNode.Name == stateNode.State.Name
            var layerToRemove = GetLayer(path);
            if (layerToRemove != null)
            {
                layerToRemove.GetParent().RemoveChild(layerToRemove);
                layerToRemove.QueueFree();
            }
            stateLayer.StateMachine.RemoveState(stateNode.Name);
            CheckHasEntry();
            CheckHasExit();
            OnEdited();
        }

        protected override void OnNodeConnected(FlowchartLayer layer, string from, string to)
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
            var newTransition = CSharpScript<Transition>.New(from, to, null); // NOTE: Constructor matching is stingy :( You can't use constructors with optional paramters
            line.Transition = newTransition;
            stateLayer.StateMachine.AddTransition(newTransition);
            ClearSelection();
            Select(line);
            OnEdited();
        }

        protected override void OnNodeDisconnected(FlowchartLayer layer, string from, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;
            stateLayer.StateMachine.RemoveTransition(from, to);
            OnEdited();

        }

        protected override void OnNodeReconnectBegin(FlowchartLayer layer, string from, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;
            reconnectingConnection = stateLayer.GetConnection(from, to);
            stateLayer.StateMachine.RemoveTransition(from, to);
        }

        protected override void OnNodeReconnectEnd(FlowchartLayer layer, string from, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;
            var transition = (reconnectingConnection.Line as TransitionLine).Transition;
            transition.To = to;
            stateLayer.StateMachine.AddTransition(transition);
            ClearSelection();
            Select(reconnectingConnection.Line);
        }

        protected override void OnNodeReconnectFailed(FlowchartLayer layer, string from, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return;
            var transition = (reconnectingConnection.Line as TransitionLine).Transition;
            stateLayer.StateMachine.AddTransition(transition);
            ClearSelection();
            Select(reconnectingConnection.Line);

        }

        protected override bool RequestConnectFrom(FlowchartLayer layer, string from)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return false;
            if (from == State.ExitState)
                return false;
            return true;

        }

        protected override bool RequestConnectTo(FlowchartLayer layer, string to)
        {
            if (!(layer is StateMachineEditorLayer stateLayer)) return false;
            if (to == State.EntryState)
                return false;
            return true;

        }

        protected override void OnDuplicated(FlowchartLayer layer, GDC.Array<Control> oldNodes, GDC.Array<Control> newNodes)
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

        #region Debug
        public void DebugProcess()
        {
            if (!DebugMode)
            {
                SetProcess(false);
                return;
            }
            // We only want to debug as long as we have a valid RemoteStateMachinePlayer
            if (RemoteStateMachinePlayer == null || !IsInstanceValid(RemoteStateMachinePlayer.Source))
            {
                SetProcess(false);
                DebugMode = false;
                return;
            }
            var stack = RemoteStateMachinePlayer.Stack;
            if (stack.Length == 0)
            {
                SetProcess(false);
                DebugMode = false;
                return;
            }
            if (stack.Length == 1)
            {
                SetCurrentDebugState(RemoteStateMachinePlayer.Current);
            }
            else
            {
                var stackMaxIndex = stack.Length - 1;
                var prevIndex = stack.IndexOf(currentDebugState);
                if (prevIndex == -1)
                {
                    if (lastStack.Count < stack.Length)
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
                                SetCurrentDebugState(lastStack[i + 1]);

                            // Transit to all missing state in current stack
                            for (int i = commonIndex + 1; i < stack.Length; i++)
                                SetCurrentDebugState(stack[i]);
                        }
                        else
                            SetCurrentDebugState(stack.PeekBack());
                    }
                    else
                        SetCurrentDebugState(stack.PeekBack());
                }
                else
                {
                    // Set every skipped state
                    var missingCount = stackMaxIndex - prevIndex;
                    foreach (var i in GD.Range(1, missingCount + 1))
                        SetCurrentDebugState(stack[prevIndex + i]);
                }
            }
            lastStack = new List<string>(stack);
            var globalParams = RemoteStateMachinePlayer.Parameters;
            var localParams = RemoteStateMachinePlayer.LocalParameters;
            paramPanel.UpdateParams(globalParams, localParams);
            GetFocusedLayer(currentDebugState).DebugUpdate(currentDebugState, globalParams, localParams);
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
                        toDir.GotoPrevious();
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
                        var n = toDir.GotoPrevious();
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

        private void SetCurrentDebugState(string v)
        {
            if (currentDebugState != v)
            {
                var from = currentDebugState;
                var to = v;
                currentDebugState = v;
                OnRemoteTransited(from, to);
            }
        }
        #endregion
    }
}