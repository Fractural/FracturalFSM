
using System;
using Godot;
using Fractural.Utils;
using Fractural.Plugin;

namespace Fractural.StateMachine
{
    [Tool]
    public class FSMPlugin : ExtendedPlugin
    {
        public StateMachineEditor stateMachineEditor;
        public TransitionInspector transitionInspector;
        public StateStackPlayerInspector stateStackPlayerInspector;
        public StateMachinePlayerInspector stateMachinePlayerInspector;
        public StateInspector stateInspector;

        private Godot.Object focusedObject = null;
        /// <summary>
        /// Can be StateMachine/StateMachinePlayer
        /// </summary>
        public Godot.Object FocusedObject
        {
            get => focusedObject;
            set
            {
                if (focusedObject != value)
                {
                    focusedObject = value;
                    OnFocusedObjectChanged(value);
                }
            }
        }

        public override string PluginName => "Fractural Finite State Machine";

        public EditorSelection editorSelection;

        protected override void Load()
        {
            var theme = GetEditorInterface().GetBaseControl().Theme;
            // Force anti-alias for default font, so rotated text will looks smoother
            var font = theme.GetFont<DynamicFont>("main", "EditorFonts");
            font.UseFilter = true;

            editorSelection = GetEditorInterface().GetSelection();
            editorSelection.Connect("selection_changed", this, nameof(OnEditorSelectionSelectionChanged));

            Texture stackPlayerIcon = GD.Load<Texture>("res://addons/FracturalFSM/Assets/Icons/stack_player_icon.png");
            Texture stateMachinePlayerIcon = GD.Load<Texture>("res://addons/FracturalFSM/Assets/Icons/state_machine_player_icon.png");
            Texture stateMachineIcon = GD.Load<Texture>("res://addons/FracturalFSM/Assets/Icons/state_machine_icon.png");

            AddManagedCustomType(nameof(StateStackPlayer), nameof(Node), GD.Load<CSharpScript>("res://addons/FracturalFSM/CustomTypes/StateStackPlayer.cs"), stackPlayerIcon);
            AddManagedCustomType(nameof(StateMachinePlayer), nameof(Node), GD.Load<CSharpScript>("res://addons/FracturalFSM/CustomTypes/StateMachinePlayer.cs"), stateMachinePlayerIcon);
            AddManagedCustomType(nameof(StateMachine), nameof(Resource), GD.Load<CSharpScript>("res://addons/FracturalFSM/CustomTypes/StateMachine.cs"), stateMachineIcon);

            PackedScene stateMachineEditorPrefab = GD.Load<PackedScene>("res://addons/FracturalFSM/Editor/StateMachine/StateMachineEditor.tscn");
            stateMachineEditor = stateMachineEditorPrefab.Instance<StateMachineEditor>();
            stateMachineEditor.Connect(nameof(StateMachineEditor.InspectorChanged), this, nameof(OnInspectorChanged));
            stateMachineEditor.Connect(nameof(StateMachineEditor.NodeSelected), this, nameof(OnStateMachineEditorNodeSelected));
            stateMachineEditor.Connect(nameof(StateMachineEditor.NothingSelected), this, nameof(OnStateMachineEditorNothingSelected));
            //stateMachineEditor.Connect(nameof(StateMachineEditor.DebugModeChanged), this, nameof(OnStateMachineEditorDebugModeChanged));
            stateMachineEditor.Construct(GetUndoRedo(), AssetsRegistry);

            transitionInspector = new TransitionInspector(
                GetUndoRedo(),
                theme.GetIcon("ToolConnect", "EditorIcons"),
                GD.Load<PackedScene>("res://addons/FracturalFSM/Editor/Transition/TransitionEditor.tscn")
            );
            stateInspector = new StateInspector();
            stateStackPlayerInspector = new StateStackPlayerInspector();
            stateMachinePlayerInspector = new StateMachinePlayerInspector();

            AddManagedInspectorPlugin(transitionInspector);
            AddManagedInspectorPlugin(stateInspector);
            AddManagedInspectorPlugin(stateStackPlayerInspector);
            AddManagedInspectorPlugin(stateMachinePlayerInspector);
        }

        protected override void Unload()
        {
            editorSelection.Disconnect("selection_changed", this, nameof(OnEditorSelectionSelectionChanged));
            HideStateMachineEditor();
            stateMachineEditor.QueueFree();
        }

        public override bool Handles(Godot.Object @object)
        {
            if (@object is StateMachine)
                return true;

            if (GDUtils.IsRemoteInspectedObject<StateMachinePlayer>(@object))
            {
                // We want to focus on the remote object for debugging purposes
                FocusedObject = @object.AsWrapper<InspectorRemoteStateMachinePlayer>();
                return false;
            }
            return false;
        }

        public override void Edit(Godot.Object @object)
        {
            FocusedObject = @object;
        }

        public void ShowStateMachineEditor()
        {
            if (FocusedObject != null && stateMachineEditor != null)
            {
                if (!stateMachineEditor.IsInsideTree())
                    AddControlToBottomPanel(stateMachineEditor, "StateMachine");
                MakeBottomPanelItemVisible(stateMachineEditor);
            }
        }

        public void HideStateMachineEditor()
        {
            if (stateMachineEditor.IsInsideTree())
            {
                stateMachineEditor.Unload();
                RemoveControlFromBottomPanel(stateMachineEditor);
            }
        }

        #region Signal Listeners
        private void OnEditorSelectionSelectionChanged()
        {
            var selectedNodes = editorSelection.GetSelectedNodes();
            if (selectedNodes.Count == 1)
            {
                var selectedNode = selectedNodes[0];
                if (selectedNode is StateMachinePlayer stateMachinePLayer)
                {
                    FocusedObject = stateMachinePLayer;
                    return;
                }
            }
            FocusedObject = null;
        }

        private void OnFocusedObjectChanged(Godot.Object newObj)
        {
            if (newObj != null)
            {
                // Must be shown first, otherwise StateMachineEditor can't execute ui action as it is !added to scene tree
                ShowStateMachineEditor();
                if (FocusedObject is StateMachinePlayer focusedStateMachinePlayer)
                    stateMachineEditor.TryLoad(focusedStateMachinePlayer);
                else if (FocusedObject is StateMachine focusedStateMachine)
                    stateMachineEditor.TryLoad(focusedStateMachine);
                else if (FocusedObject is InspectorRemoteStateMachinePlayer remotePlayer)
                    stateMachineEditor.TryLoad(remotePlayer);
            }
            else
                HideStateMachineEditor();
        }

        private void OnInspectorChanged(string property)
        {
            GetEditorInterface().GetInspector().Refresh();
        }

        private void OnStateMachineEditorNodeSelected(Control node)
        {
            Godot.Object objectToInspect = null;
            if (node is StateNode stateNode)
            {
                if (stateNode.State is StateMachine) // Ignore, inspect state machine will trigger Edit()
                    return;

                objectToInspect = stateNode.State;
            }
            else if (node is TransitionLine transitionLine)
                objectToInspect = transitionLine.Transition;

            GetEditorInterface().InspectObject(objectToInspect);
        }

        private void OnStateMachineEditorNothingSelected()
        {
            // We're not looking at a state or line node, so we go back to inspecting the state machine
            GetEditorInterface().InspectObject(FocusedObject);
        }
        #endregion
    }
}