
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

            AddManagedCustomType(nameof(StackPlayer), nameof(Node), GD.Load<CSharpScript>("res://addons/FracturalFSM/CustomTypes/StackPlayer.cs"), stackPlayerIcon);
            AddManagedCustomType(nameof(StateMachinePlayer), nameof(Node), GD.Load<CSharpScript>("res://addons/FracturalFSM/CustomTypes/StateMachinePlayer.cs"), stateMachinePlayerIcon);
            AddManagedCustomType(nameof(StateMachine), nameof(Resource), GD.Load<CSharpScript>("res://addons/FracturalFSM/CustomTypes/StateMachine.cs"), stateMachineIcon);

            PackedScene stateMachineEditorPrefab = GD.Load<PackedScene>("res://addons/FracturalFSM/Editor/StateMachine/StateMachineEditor.tscn");
            stateMachineEditor = stateMachineEditorPrefab.Instance<StateMachineEditor>();
            stateMachineEditor.Connect(nameof(StateMachineEditor.InspectorChanged), this, nameof(OnInspectorChanged));
            stateMachineEditor.Connect(nameof(StateMachineEditor.NodeSelected), this, nameof(OnStateMachineEditorNodeSelected));
            stateMachineEditor.Connect(nameof(StateMachineEditor.NodeDeselected), this, nameof(OnStateMachineEditorNodeDeselected));
            stateMachineEditor.Connect(nameof(StateMachineEditor.DebugModeChanged), this, nameof(OnStateMachineEditorDebugModeChanged));

            transitionInspector = new TransitionInspector(
                GetUndoRedo(),
                theme.GetIcon("ToolConnect", "EditorIcons"),
                GD.Load<PackedScene>("res://addons/FracturalFSM/Editor/Transition/TransitionEditor.tscn")
            );
            stateInspector = new StateInspector();

            AddManagedInspectorPlugin(transitionInspector);
            AddManagedInspectorPlugin(stateInspector);
        }

        public override bool Handles(Godot.Object @object)
        {
            if (@object is StateMachine)
                return true;

            if (@object is StateMachinePlayer)
            {
                if (@object.GetClass() == "ScriptEditorDebuggerInspectedObject")
                {
                    FocusedObject = @object;
                    stateMachineEditor.DebugMode = true;
                    return false;
                }
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
                stateMachineEditor.StateMachine = null;
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
                StateMachine stateMachine = null;
                if (FocusedObject is StateMachinePlayer focusedStateMachinePlayer)
                {
                    if (GDUtils.IsRemoteInspectedObject(FocusedObject))
                        stateMachine = FocusedObject.GetRemote<StateMachine>(nameof(StateMachinePlayer.StateMachine));
                    else
                        stateMachine = focusedStateMachinePlayer.StateMachine;
                    stateMachineEditor.StateMachinePlayer = focusedStateMachinePlayer;
                }
                else if (FocusedObject is StateMachine focusedStateMachine)
                {
                    stateMachine = focusedStateMachine;
                    stateMachineEditor.StateMachinePlayer = null;
                }
                stateMachineEditor.StateMachine = stateMachine;
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

        private void OnStateMachineEditorNodeDeselected(Control node)
        {
            GetEditorInterface().InspectObject(stateMachineEditor.StateMachine);
        }

        private void OnStateMachineEditorDebugModeChanged(bool newDebugMode)
        {
            if (!newDebugMode)
            {
                stateMachineEditor.DebugMode = false;
                stateMachineEditor.StateMachinePlayer = null;
                FocusedObject = null;
                HideStateMachineEditor();
            }
        }
        #endregion
    }
}