
using System;
using System.Collections;
using System.Collections.Generic;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.FlowChart
{
    // TODO: Add callbacks for selection drag finished, etc. to support undo and redo of graph
    [Tool]
    public class FlowChart : Control
    {
        #region Signals
        /// <summary>
        /// When a connection established
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="line"></param>
        [Signal] public delegate void ConnectionEstablished(string from, string to, FlowChartLine line);
        /// <summary>
        /// When a connection broken
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="line"></param>
        [Signal] public delegate void ConnectionBroken(string from, string to, FlowChartLine line);
        /// <summary>
        /// When a node selected. Could be a FlowChartNode or a FlowChartLine
        /// </summary>
        /// <param name="node"></param>
        [Signal] public delegate void NodeSelected(Control node);
        /// <summary>
        /// When a node deselected. Could be a FlowChartNode or a FlowChartLine
        /// </summary>
        /// <param name="node"></param>
        [Signal] public delegate void NodeDeselected(Control node);
        /// <summary>
        /// When a node dragged
        /// </summary>
        /// <param name="node"></param>
        /// <param name="distance"></param>
        [Signal] public delegate void Dragged(FlowChartNode node, float distance);
        #endregion

        #region Public Properties
        /// <summary>
        /// Margin of content from edge of FlowChart
        /// </summary>
        [Export] public int ScrollMargin { get; set; } = 300;
        /// <summary>
        /// Offset between two line that interconnecting
        /// </summary>
        [Export] public int InterconnectionOffset { get; set; } = 10;
        /// <summary>
        /// Snap amount
        /// </summary>
        [Export] public int Snap { get; set; } = 20;

        private float zoom = 1.0f;
        /// <summary> 
        /// Zoom amount
        /// </summary>
        [Export]
        public float Zoom
        {
            get => zoom;
            set
            {
                zoom = Mathf.Clamp(value, ZoomRange.x, ZoomRange.y);
                content.RectScale = Vector2.One * zoom;
            }
        }
        [Export]
        public Vector2 ZoomRange { get; set; } = new Vector2(0.25f, 3f);
        public bool IsSnapping { get; set; } = true;
        public bool CanGuiSelectNode { get; set; } = true;
        public bool CanGuiDeleteNode { get; set; } = true;
        public bool CanGuiConnectNode { get; set; } = true;
        public FlowChartLayer CurrentLayer { get; set; }
        public StyleBoxFlat SelectionStylebox = new StyleBoxFlat();
        public Color GridMajorColor = new Color(1, 1, 1, 0.15f);
        public Color GridMinorColor = new Color(1, 1, 1, 0.07f);
        #endregion

        #region Dependencies
        protected Control content = new Control(); // Root node that hold anything drawn in the flowchart
        protected HScrollBar hScroll = new HScrollBar();
        protected VScrollBar vScroll = new VScrollBar();
        protected VBoxContainer topBar = new VBoxContainer();
        protected HBoxContainer toolbar = new HBoxContainer(); // Root node of top overlay controls
        protected Button zoomMinus = new Button();
        protected Button zoomReset = new Button();
        protected Button zoomPlus = new Button();
        protected Button snapButton = new Button();
        protected SpinBox snapAmount = new SpinBox();
        #endregion

        #region Private Fields
        protected bool isConnecting = false;
        protected Connection currentConnection;
        protected bool isDragging = false;
        protected bool isDraggingNode = false;
        protected Vector2 dragStartPos = Vector2.Zero;
        protected Vector2 dragEndPos = Vector2.Zero;
        protected GDC.Array<Vector2> dragOrigins = new GDC.Array<Vector2>() { };
        /// <summary>
        /// Selection of FlowChartNodes and FlowChartLines
        /// </summary>
        protected GDC.Array<Control> selection = new GDC.Array<Control>() { };
        /// <summary>
        /// Copied list of FlowChartNodes and FlowChartLines
        /// </summary>
        protected GDC.Array<Control> copyingNodes = new GDC.Array<Control>() { };
        #endregion

        #region UI Wiring
        private void OnHScrollGuiInput(InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseButton mouseButtonEvent)
            {
                var v = (hScroll.MaxValue - hScroll.MinValue) * 0.01f;// Scroll at 0.1% step
                switch (mouseButtonEvent.ButtonIndex)
                {
                    case (int)ButtonList.WheelUp:
                        hScroll.Value -= v;
                        break;
                    case (int)ButtonList.WheelDown:
                        hScroll.Value += v;
                        break;
                }
            }
        }

        private void OnVScrollGuiInput(InputEvent inputEvent)
        {
            if (inputEvent is InputEventMouseButton mouseButtonEvent)
            {
                var v = (vScroll.MaxValue - vScroll.MinValue) * 0.01;// Scroll at 0.1% step
                switch (mouseButtonEvent.ButtonIndex)

                {
                    case (int)ButtonList.WheelUp:
                        vScroll.Value -= v;// scroll left
                        break;
                    case (int)ButtonList.WheelDown:
                        vScroll.Value += v;// scroll right
                        break;
                }
            }
        }

        private void OnHScrollChanged(float value)
        {
            content.RectPosition = new Vector2(-value, content.RectPosition.y);
        }

        private void OnVScrollChanged(float value)
        {
            content.RectPosition = new Vector2(content.RectPosition.x, -value);
        }

        private void OnZoomMinusPressed()
        {
            Zoom = zoom - 0.1f;
            Update();
        }

        private void OnZoomResetPressed()
        {
            Zoom = 1f;
            Update();
        }

        private void OnZoomPlusPressed()
        {
            Zoom = zoom + 0.1f;
            Update();
        }

        private void OnSnapButtonPressed()
        {
            IsSnapping = snapButton.Pressed;
            Update();
        }

        private void OnSnapAmountValueChanged(int value)
        {
            Snap = value;
            Update();
        }
        #endregion

        #region Godot Lifetime Methods
        public override void _Ready()
        {
            FocusMode = FocusModeEnum.All;
            SelectionStylebox.BgColor = new Color(0, 0, 0, 0.3f);
            SelectionStylebox.SetBorderWidthAll(1);

            content.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(content);

            AddChild(hScroll);
            hScroll.SetAnchorsAndMarginsPreset(LayoutPreset.BottomWide);
            hScroll.Connect("value_changed", this, nameof(OnHScrollChanged));
            hScroll.Connect("gui_input", this, nameof(OnHScrollGuiInput));

            AddChild(vScroll);
            vScroll.SetAnchorsAndMarginsPreset(LayoutPreset.RightWide);
            vScroll.Connect("value_changed", this, nameof(OnVScrollChanged));
            vScroll.Connect("gui_input", this, nameof(OnVScrollGuiInput));

            hScroll.MarginRight = -vScroll.RectSize.x;
            vScroll.MarginBottom = -hScroll.RectSize.y;

            AddLayerTo(content);
            SelectLayerAt(0);

            topBar.SetAnchorsAndMarginsPreset(LayoutPreset.TopWide);
            topBar.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(topBar);

            toolbar.MouseFilter = MouseFilterEnum.Ignore;
            topBar.AddChild(toolbar);

            zoomMinus.Flat = true;
            zoomMinus.HintTooltip = "Zoom Out";
            zoomMinus.Connect("pressed", this, nameof(OnZoomMinusPressed));
            zoomMinus.FocusMode = FocusModeEnum.None;
            toolbar.AddChild(zoomMinus);

            zoomReset.Flat = true;
            zoomReset.HintTooltip = "Zoom Reset";
            zoomReset.Connect("pressed", this, nameof(OnZoomResetPressed));
            zoomReset.FocusMode = FocusModeEnum.None;
            toolbar.AddChild(zoomReset);

            zoomPlus.Flat = true;
            zoomPlus.HintTooltip = "Zoom In";
            zoomPlus.Connect("pressed", this, nameof(OnZoomPlusPressed));
            zoomPlus.FocusMode = FocusModeEnum.None;
            toolbar.AddChild(zoomPlus);

            snapButton.Flat = true;
            snapButton.ToggleMode = true;
            snapButton.HintTooltip = "Enable snap && show grid";
            snapButton.Connect("pressed", this, nameof(OnSnapButtonPressed));
            snapButton.Pressed = true;
            snapButton.FocusMode = FocusModeEnum.None;
            toolbar.AddChild(snapButton);

            snapAmount.Value = Snap;
            snapAmount.Connect("value_changed", this, nameof(OnSnapAmountValueChanged));
            toolbar.AddChild(snapAmount);
        }
        // TODO: Figure out how graph edit work
        public override void _Draw()
        {
            // Update scrolls
            var contentRect = GetScrollRect();
            content.RectPivotOffset = GetScrollRect().Size / 2f;// Scale from center
            if (!GetRect().Encloses(contentRect))
            {
                var hMin = contentRect.Position.x;
                var hMax = contentRect.Size.x + contentRect.Position.x - RectSize.x;
                var vMin = contentRect.Position.y;
                var vMax = contentRect.Size.y + contentRect.Position.y - RectSize.y;
                if (hMin == hMax) // Otherwise scroll bar will complain no ratio
                {
                    hMin -= 0.1f;
                    hMax += 0.1f;
                }
                if (vMin == vMax) // Otherwise scroll bar will complain no ratio
                {
                    vMin -= 0.1f;
                    vMax += 0.1f;
                }
                hScroll.MinValue = hMin;
                hScroll.MaxValue = hMax;
                hScroll.Page = contentRect.Size.x / 100;
                vScroll.MinValue = vMin;
                vScroll.MaxValue = vMax;
                vScroll.Page = contentRect.Size.y / 100;

            }
            // Draw selection box
            if (!isDraggingNode && !isConnecting)
            {

                var selectionBoxRect = GetSelectionBoxRect();
                DrawStyleBox(SelectionStylebox, selectionBoxRect);

            }
            // Draw grid
            // Refer GraphEdit(https://github.com/godotengine/godot/blob/6019dab0b45e1291e556e6d9e01b625b5076cc3c/scene/gui/graph_edit.cpp#L442)
            if (IsSnapping)
            {
                var scrollOffset = new Vector2((float)hScroll.Value, (float)vScroll.Value);
                var offset = scrollOffset / zoom;
                var Size = RectSize / zoom;

                var from = (offset / (float)(Snap)).Floor();
                var len = (Size / (float)(Snap)).Floor() + new Vector2(1, 1);

                var gridMinor = GridMinorColor;
                var gridMajor = GridMajorColor;

                for (int i = (int)from.x; i < from.x + len.x; i++)
                {
                    Color color;

                    if (Mathf.Abs(i) % 10 == 0)
                        color = gridMajor;
                    else
                        color = gridMinor;

                    var baseOfs = i * Snap * zoom - offset.x * zoom;
                    DrawLine(new Vector2(baseOfs, 0), new Vector2(baseOfs, RectSize.y), color);
                }
                for (int i = (int)from.y; i < from.y + len.y; i++)
                {
                    Color color;

                    if (Mathf.Abs(i) % 10 == 0)
                        color = gridMajor;
                    else
                        color = gridMinor;

                    var baseOfs = i * Snap * zoom - offset.y * zoom;
                    DrawLine(new Vector2(0, baseOfs), new Vector2(RectSize.x, baseOfs), color);

                    // Debug draw
                    // for node in contentNodes.GetChildren():
                    // 	var rect = GetTransform().Xform(content.GetTransform().Xform(node.GetRect()));
                    // 	DrawStyleBox(selectionStylebox, rect)

                    // var connectionList = GetConnectionList();
                    // for i in connectionList.Size():
                    // 	var connection = _connections[connectionList[i].from][connectionList[i].to];
                    // 	# Line's offset along its down-vector
                    // 	var lineLocalUpOffset = connection.Line.RectPosition - connection.Line.GetTransform().Xform(Vector2.UP * connection.Offset);
                    // 	var fromPos = content.GetTransform().Xform(connection.GetFromPos() + lineLocalUpOffset);
                    // 	var toPos = content.GetTransform().Xform(connection.GetToPos() + lineLocalUpOffset);
                    // 	DrawLine(fromPos, toPos, Color.yellow)

                }
            }
        }

        public override void _GuiInput(InputEvent inputEvent)
        {
            if (inputEvent is InputEventKey keyEvent)
            {
                switch (keyEvent.Scancode)
                {
                    case (uint)KeyList.Delete:
                        if (keyEvent.Pressed && CanGuiDeleteNode)
                        {
                            // Delete nodes
                            foreach (Node node in selection.Duplicate())
                            {
                                if (node is FlowChartLine flowChartLine)
                                {
                                    // TODO: More efficient way to get connection from Line node
                                    foreach (GDC.Dictionary connectionsFrom in CurrentLayer.Connections.Duplicate().Values)
                                    {
                                        foreach (Connection connection in connectionsFrom.Duplicate().Values)
                                            if (connection.Line == flowChartLine)
                                                DisconnectNode(CurrentLayer, connection.FromNode.Name, connection.ToNode.Name).QueueFree();
                                    }
                                }
                                else if (node is FlowChartNode flowChartNode)
                                {
                                    RemoveNode(CurrentLayer, node.Name);
                                    foreach (var connectionPair in CurrentLayer.GetConnectionList())
                                        if (connectionPair.From == node.Name || connectionPair.To == node.Name)
                                            DisconnectNode(CurrentLayer, connectionPair.From, connectionPair.To).QueueFree();
                                }
                            }
                            AcceptEvent();
                        }
                        break;
                    case (uint)KeyList.C:
                        if (keyEvent.Pressed && keyEvent.Control)
                        {
                            // Copy node
                            copyingNodes = selection.Duplicate();
                            AcceptEvent();
                        }
                        break;
                    case (uint)KeyList.D:
                        if (keyEvent.Pressed && keyEvent.Control)
                        {
                            // Duplicate node directly from selection
                            DuplicateNodes(CurrentLayer, selection.Duplicate());
                            AcceptEvent();
                        }
                        break;
                    case (uint)KeyList.V:
                        if (keyEvent.Pressed && keyEvent.Control)
                        {
                            // Paste node from _copyingNodes
                            DuplicateNodes(CurrentLayer, copyingNodes);
                            AcceptEvent();

                        }
                        break;
                }
            }
            if (inputEvent is InputEventMouseMotion mouseMotionEvent)
            {
                switch (mouseMotionEvent.ButtonMask)
                {
                    case (int)ButtonList.MaskMiddle:
                        // Panning
                        hScroll.Value -= mouseMotionEvent.Relative.x;
                        vScroll.Value -= mouseMotionEvent.Relative.y;
                        Update();
                        break;
                    case (int)ButtonList.Left:
                        // Dragging
                        if (isDragging)
                        {
                            if (isConnecting)
                            {
                                // Connecting
                                if (currentConnection != null)
                                {
                                    var pos = ContentPosition(GetLocalMousePosition());
                                    GDC.Array<Rect2> clipRects = new GDC.Array<Rect2>() { currentConnection.FromNode.GetRect() };
                                    // Snapping connecting line
                                    int currentLayerChildCount = CurrentLayer.ContentNodes.GetChildCount();
                                    for (int i = 0; i < currentLayerChildCount; i++)
                                    {
                                        var child = CurrentLayer.ContentNodes.GetChild(CurrentLayer.ContentNodes.GetChildCount() - 1 - i);// Inverse order to check from top to bottom of canvas
                                        if (child is FlowChartNode flowChartNode && child.Name != currentConnection.FromNode.Name)
                                        {
                                            if (_RequestConnectTo(CurrentLayer, flowChartNode.Name))
                                            {
                                                if (flowChartNode.GetRect().HasPoint(pos))
                                                {
                                                    pos = flowChartNode.RectPosition + flowChartNode.RectSize / 2;
                                                    clipRects.Add(flowChartNode.GetRect());
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    currentConnection.Line.Join(currentConnection.GetFromPos(), pos, Vector2.Zero, clipRects);
                                }
                            }
                            else if (isDraggingNode)
                            {
                                // Dragging nodes
                                var dragDelta = ContentPosition(dragEndPos) - ContentPosition(dragStartPos);
                                for (int i = 0; i < selection.Count; i++)
                                {
                                    var selected = selection[i];
                                    if (!(selected is FlowChartNode selectedFlowChartNode))
                                        continue;

                                    selectedFlowChartNode.RectPosition = dragOrigins[i] + selectedFlowChartNode.RectSize / 2f + dragDelta;

                                    var color = selectedFlowChartNode.Modulate;
                                    color.a = 0.3f;
                                    selectedFlowChartNode.Modulate = color;

                                    if (IsSnapping)
                                    {
                                        selectedFlowChartNode.RectPosition = selectedFlowChartNode.RectPosition.Snapped(Vector2.One * Snap);
                                    }
                                    selectedFlowChartNode.RectPosition -= selectedFlowChartNode.RectSize / 2f;
                                    OnNodeDragged(CurrentLayer, selected, dragDelta);
                                    EmitSignal(nameof(Dragged), selected, dragDelta);

                                    // Update connection pos
                                    foreach (string from in CurrentLayer.Connections.Keys)
                                    {
                                        var connectionsFrom = CurrentLayer.Connections.Get<GDC.Dictionary>(from);
                                        foreach (string to in connectionsFrom.Keys)
                                        {
                                            if (from == selected.Name || to == selected.Name)
                                            {
                                                var connection = connectionsFrom.Get<Connection>(to);
                                                connection.Join();
                                            }
                                        }
                                    }
                                }
                            }
                            dragEndPos = GetLocalMousePosition();
                            Update();

                        }
                        break;
                }
            }
            if (inputEvent is InputEventMouseButton mouseButtonEvent)
            {
                switch (mouseButtonEvent.ButtonIndex)
                {
                    case (int)ButtonList.Middle:
                        // Reset zoom
                        if (mouseButtonEvent.Doubleclick)
                        {
                            Zoom = 1.0f;
                            Update();
                        }
                        break;
                    case (int)ButtonList.WheelUp:
                        // Zoom in
                        Zoom = zoom + 0.01f;
                        Update();
                        break;
                    case (int)ButtonList.WheelDown:
                        // Zoom out
                        Zoom = zoom - 0.01f;
                        Update();
                        break;
                    case (int)ButtonList.Left:
                        // Hit detection
                        Control hitNode = null;
                        int currentLayerChildCount = CurrentLayer.ContentNodes.GetChildCount();
                        for (int i = 0; i < currentLayerChildCount; i++)
                        {
                            var child = CurrentLayer.ContentNodes.GetChild(CurrentLayer.ContentNodes.GetChildCount() - 1 - i);// Inverse order to check from top to bottom of canvas
                            if (child is FlowChartNode flowChartNode &&
                                flowChartNode.GetRect().HasPoint(ContentPosition(mouseButtonEvent.Position)))
                            {
                                hitNode = flowChartNode;
                                break;
                            }
                        }

                        if (hitNode == null)
                        {
                            // Test Line
                            // Refer https://github.com/godotengine/godot/blob/master/editor/plugins/animation_state_machine_editor.cpp#L187
                            int closest = -1;
                            float closestD = 1e20f;

                            var connectionList = GetConnectionList();
                            for (int i = 0; i < connectionList.Count; i++)
                            {
                                var connection = CurrentLayer.GetConnection(connectionList[i]);
                                // Line's offset along its down-vector
                                var lineLocalUpOffset = connection.Line.RectPosition - connection.Line.GetTransform() * (Vector2.Down * connection.Offset);
                                var fromPos = connection.GetFromPos() + lineLocalUpOffset;
                                var toPos = connection.GetToPos() + lineLocalUpOffset;
                                var cp = Geometry.GetClosestPointToSegment2d(ContentPosition(mouseButtonEvent.Position), fromPos, toPos);
                                var d = cp.DistanceTo(ContentPosition(mouseButtonEvent.Position));
                                if (d > connection.Line.RectSize.y * 2)
                                {
                                    continue;
                                }
                                if (d < closestD)
                                {
                                    closest = i;
                                    closestD = d;
                                }
                            }
                            if (closest >= 0)
                            {
                                hitNode = CurrentLayer.GetConnection(connectionList[closest]).Line;
                            }
                        }
                        if (mouseButtonEvent.Pressed)
                        {
                            if (!selection.Contains(hitNode) && !mouseButtonEvent.Shift)
                            {
                                // Click on empty space
                                ClearSelection();
                            }
                            if (hitNode != null)
                            {
                                // Click on Node(can be a line)
                                isDraggingNode = true;
                                if (hitNode is FlowChartLine)
                                {
                                    CurrentLayer.ContentLines.MoveChild(hitNode, CurrentLayer.ContentLines.GetChildCount() - 1);// Raise selected line to top
                                    if (mouseButtonEvent.Shift && CanGuiConnectNode)
                                    {
                                        // Reconnection Start
                                        foreach (string from in CurrentLayer.Connections.Keys)
                                        {
                                            var fromConnections = CurrentLayer.Connections.Get<GDC.Dictionary>(from);
                                            foreach (string to in fromConnections.Keys)
                                            {
                                                var connection = fromConnections.Get<Connection>(to);
                                                if (connection.Line == hitNode)
                                                {
                                                    isConnecting = true;
                                                    isDraggingNode = false;
                                                    currentConnection = connection;
                                                    OnNodeReconnectBegin(CurrentLayer, from, to);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                if (hitNode is FlowChartNode flowChartNode)
                                {
                                    CurrentLayer.ContentNodes.MoveChild(hitNode, CurrentLayer.ContentNodes.GetChildCount() - 1); // Raise selected node to top
                                    if (mouseButtonEvent.Shift && CanGuiConnectNode)
                                    {
                                        // Connection start
                                        if (_RequestConnectFrom(CurrentLayer, hitNode.Name))
                                        {
                                            isConnecting = true;
                                            isDraggingNode = false;
                                            var line = CreateLineInstance();
                                            var connection = new Connection(line, flowChartNode, null);

                                            CurrentLayer.AfterConnectNode(connection);
                                            currentConnection = connection;
                                            currentConnection.Line.Join(currentConnection.GetFromPos(), ContentPosition(mouseButtonEvent.Position));
                                        }
                                    }
                                    AcceptEvent();
                                }
                                if (isConnecting)
                                {
                                    ClearSelection();
                                }
                                else
                                {
                                    if (CanGuiSelectNode)
                                    {
                                        Select(hitNode);
                                    }
                                }
                            }
                            if (!isDragging)
                            {
                                // Drag start
                                isDragging = true;
                                dragStartPos = mouseButtonEvent.Position;
                                dragEndPos = mouseButtonEvent.Position;
                            }
                        }
                        else
                        {
                            var wasConnecting = isConnecting;
                            var wasDraggingNode = isDraggingNode;
                            if (currentConnection != null)
                            {
                                // Connection end
                                var from = currentConnection.FromNode.Name;
                                var to = hitNode != null ? hitNode.Name : null;


                                if (hitNode is FlowChartNode flowChartNode && _RequestConnectTo(CurrentLayer, to) && from != to)
                                {
                                    // Connection success
                                    FlowChartLine line;
                                    if (currentConnection.ToNode != null)
                                    {
                                        // Reconnection
                                        line = DisconnectNode(CurrentLayer, from, currentConnection.ToNode.Name);
                                        currentConnection.ToNode = flowChartNode;
                                        OnNodeReconnectEnd(CurrentLayer, from, to);
                                        ConnectNode(CurrentLayer, from, to, line);
                                    }
                                    else
                                    {
                                        // New Connection
                                        CurrentLayer.ContentLines.RemoveChild(currentConnection.Line);
                                        line = currentConnection.Line;
                                        currentConnection.ToNode = flowChartNode;
                                        ConnectNode(CurrentLayer, from, to, line);
                                    }
                                }
                                else
                                {
                                    // Connection failed
                                    if (currentConnection.ToNode != null)
                                    {
                                        // Reconnection
                                        currentConnection.Join();
                                        OnNodeReconnectFailed(CurrentLayer, from, Name);
                                    }
                                    else
                                    {
                                        // New Connection
                                        currentConnection.Line.QueueFree();
                                        OnNodeConnectFailed(CurrentLayer, from);
                                    }
                                }
                                isConnecting = false;
                                currentConnection = null;
                                AcceptEvent();

                            }
                            if (isDragging)
                            {
                                // Drag end
                                isDragging = false;
                                isDraggingNode = false;
                                if (!(wasConnecting || wasDraggingNode) && CanGuiSelectNode)
                                {
                                    var selectionBoxRect = GetSelectionBoxRect();
                                    // Select node
                                    foreach (Control node in CurrentLayer.ContentNodes.GetChildren())
                                    {
                                        var rect = GetTransform() * (content.GetTransform() * node.GetRect());
                                        if (selectionBoxRect.Intersects(rect))
                                        {
                                            if (node is FlowChartNode)
                                            {
                                                Select(node);
                                            }
                                        }
                                    }
                                    // Select line
                                    var connectionList = GetConnectionList();
                                    for (int i = 0; i < connectionList.Count; i++)
                                    {
                                        var connection = CurrentLayer.GetConnection(connectionList[i]);
                                        // Line's offset along its down-vector
                                        var lineLocalUpOffset = connection.Line.RectPosition - connection.Line.GetTransform() * (Vector2.Up * connection.Offset);
                                        var fromPos = content.GetTransform() * (connection.GetFromPos() + lineLocalUpOffset);
                                        var toPos = content.GetTransform() * (connection.GetToPos() + lineLocalUpOffset);
                                        if (CohenSutherland.LineIntersectRectangle(fromPos, toPos, selectionBoxRect))
                                            Select(connection.Line);
                                    }
                                }
                                if (wasDraggingNode)
                                {
                                    // Update _dragOrigins with new Position after dragged
                                    for (int i = 0; i < selection.Count; i++)
                                    {
                                        var selected = selection[i];
                                        dragOrigins[i] = selected.RectPosition;
                                        var color = selected.Modulate;
                                        color.a = 1f;
                                        selected.Modulate = color;
                                    }
                                }
                                dragStartPos = dragEndPos;
                                Update();
                            }
                        }
                        break;
                }
            }
        }
        #endregion

        #region Public API
        public FlowChartLayer AddLayerTo(Control target)
        {
            var layer = CreateLayerInstance();
            target.AddChild(layer);
            return layer;
        }

        public FlowChartLayer GetLayer(NodePath nodePath)
        {
            return content.GetNodeOrNull<FlowChartLayer>(nodePath);
        }

        public void SelectLayerAt(int i)
        {
            SelectLayer(content.GetChild<FlowChartLayer>(i));
        }

        public void SelectLayer(FlowChartLayer layer)
        {
            var prevLayer = CurrentLayer;
            OnLayerDeselected(prevLayer);
            CurrentLayer = layer;
            OnLayerSelected(layer);
        }

        /// <summary>
        /// Add node
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="node"></param>
        public void AddNode(FlowChartLayer layer, Control node)
        {
            layer.AddNode(node);
            OnNodeAdded(layer, node);
        }

        /// <summary>
        /// Remove node
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="nodeName"></param>
        public void RemoveNode(FlowChartLayer layer, string nodeName)
        {
            var node = layer.ContentNodes.GetNodeOrNull<Control>(nodeName);
            if (node != null)
            {
                Deselect(node); // Must deselct before remove to make sure _dragOrigins synced with _selections
                layer.RemoveNode(node);
                OnNodeRemoved(layer, node);
            }
        }

        /// <summary>
        /// Rename node
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="oldName"></param>
        /// <param name=""></param>
        public virtual void RenameNode(FlowChartLayer layer, string oldName, string newName)
        {
            layer.RenameNode(oldName, newName);
        }

        /// <summary>
        /// Connect two nodes with a line
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="line"></param>
        public void ConnectNode(FlowChartLayer layer, string from, string to, FlowChartLine line = null)
        {
            if (line == null)
                line = CreateLineInstance();
            line.Name = GetFlowChartLineName(from, to);
            layer.ConnectNode(line, from, to, InterconnectionOffset);
            OnNodeConnected(layer, from, to);
            EmitSignal(nameof(ConnectionEstablished), from, to, line);
        }

        /// <summary>
        /// Break a connection between two node
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <returns></returns>
        public FlowChartLine DisconnectNode(FlowChartLayer layer, string from, string to)
        {
            var line = layer.DisconnectNode(from, to);
            Deselect(line);// Since line is selectable as well
            OnNodeDisconnected(layer, from, to);
            EmitSignal(nameof(ConnectionBroken), from, to);
            return line;
        }

        /// <summary>
        /// Clear all connections
        /// </summary>
        /// <param name="layer"></param>
        public void ClearConnections(FlowChartLayer layer = null)
        {
            if (layer == null) layer = CurrentLayer;
            layer.ClearConnections();
        }

        /// <summary>
        /// Select a FlowChartNode or a FlowChartLine
        /// </summary>
        /// <param name="node"></param>
        public void Select(Control node)
        {
            if (selection.Contains(node))
                return;
            if (!(node is ISelectable selectable))
                return;

            selection.Add(node);
            selectable.Selected = true;
            dragOrigins.Add(node.RectPosition);
            EmitSignal(nameof(NodeSelected), node);
        }

        /// <summary>
        /// Deselect a node
        /// </summary>
        /// <param name="node"></param>
        public void Deselect(Control node)
        {
            int index = selection.IndexOf(node);
            if (index < 0) return;
            if (IsInstanceValid(node) && node is ISelectable selectable)
                selectable.Selected = false;
            selection.RemoveAt(index);
            dragOrigins.RemoveAt(index);
            EmitSignal(nameof(NodeDeselected), node);
        }

        /// <summary>
        /// Clear all selection
        /// </summary>
        public void ClearSelection()
        {
            foreach (var node in selection.Duplicate()) // duplicate _selection GDC.Array as Deselect() edit GDC.Array
            {
                if (node == null)
                    continue;

                Deselect(node);
            }
            selection.Clear();
        }

        /// <summary>
        /// Duplicate given nodes in editor
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="controlNodes"></param>
        public void DuplicateNodes(FlowChartLayer layer, GDC.Array<Control> controlNodes)
        {
            ClearSelection();
            GDC.Array<Control> newNodes = new GDC.Array<Control>();
            for (int i = 0; i < controlNodes.Count; i++)
            {
                var node = controlNodes[i];
                if (!(node is FlowChartNode))
                {
                    continue;
                }
                var newNode = node.Duplicate((int)(DuplicateFlags.Signals | DuplicateFlags.Scripts)) as Control;
                var offset = ContentPosition(GetLocalMousePosition()) - ContentPosition(dragEndPos);
                newNode.RectPosition = newNode.RectPosition + offset;
                newNodes.Add(newNode);
                AddNode(layer, newNode);
                Select(newNode);
            }

            // Duplicate connection within selection
            for (int i = 0; i < controlNodes.Count; i++)
            {
                var fromNode = controlNodes[i];
                foreach (var connectionPair in GetConnectionList())
                {
                    if (fromNode.Name == connectionPair.From)
                    {
                        for (int j = 0; j < controlNodes.Count; j++)
                        {
                            var toNode = controlNodes[j];
                            if (toNode.Name == connectionPair.To)
                                ConnectNode(layer, newNodes[i].Name, newNodes[j].Name);
                        }
                    }
                }
            }
            OnDuplicated(layer, controlNodes, newNodes);
        }

        /// <summary>
        /// Return GDC.Array of dictionary of connection as such [new Dictionary(){{"from1", "to1"}}, new Dictionary(){{"from2", "to2"}}]
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        public IReadOnlyList<ConnectionPair> GetConnectionList(FlowChartLayer layer = null)
        {
            if (layer == null) layer = CurrentLayer;
            return layer.GetConnectionList();
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Get selection box rect
        /// </summary>
        /// <returns></returns>
        private Rect2 GetSelectionBoxRect()
        {
            Vector2 pos = new Vector2(Mathf.Min(dragStartPos.x, dragEndPos.x), Mathf.Min(dragStartPos.y, dragEndPos.y));
            var Size = (dragEndPos - dragStartPos).Abs();
            return new Rect2(pos, Size);
        }

        /// <summary>
        /// Get required scroll rect base on content
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        private Rect2 GetScrollRect(FlowChartLayer layer = null)
        {
            if (layer == null) layer = CurrentLayer;
            return layer.GetScrollRect((int)(ScrollMargin * zoom));
        }


        /// <summary>
        /// Convert Position in FlowChart space to Content(takes translation/scale of content into account)
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        protected Vector2 ContentPosition(Vector2 pos)
        {
            return (pos - content.RectPosition - content.RectPivotOffset * (Vector2.One - content.RectScale)) * 1f / content.RectScale;
        }
        #endregion

        #region Virtual Prefab Creation
        private PackedScene flowChartLayerPrefab;
        public PackedScene FlowChartLayerPrefab
        {
            get
            {
                if (flowChartLayerPrefab == null)
                    flowChartLayerPrefab = GD.Load<PackedScene>("res://addons/FracturalFSM/Editor/FlowChart/FlowChartLayer.tscn");
                return flowChartLayerPrefab;
            }
        }

        private PackedScene flowChartLinePrefab;
        public PackedScene FlowChartLinePrefab
        {
            get
            {
                if (flowChartLinePrefab == null)
                    flowChartLinePrefab = GD.Load<PackedScene>("res://addons/FracturalFSM/Editor/FlowChart/FlowChartLine.tscn");
                return flowChartLinePrefab;
            }
        }

        /// <summary>
        /// Return a new layer instance to use.
        /// </summary>
        /// <returns></returns>
        public virtual FlowChartLayer CreateLayerInstance()
        {
            return flowChartLayerPrefab.Instance<FlowChartLayer>();
        }

        /// <summary>
        /// Return new line instance to use, called when connecting node
        /// </summary>
        /// <returns></returns>
        public virtual FlowChartLine CreateLineInstance()
        {
            return FlowChartLinePrefab.Instance<FlowChartLine>();
        }
        #endregion

        #region Virtual Lifetime Methods
        /// <summary>
        /// Called after layer Selected(currentLayer changed)
        /// </summary>
        /// <param name="layer"></param>
        protected virtual void OnLayerSelected(FlowChartLayer layer)
        {

        }

        protected virtual void OnLayerDeselected(FlowChartLayer layer)
        {

        }

        /// <summary>
        /// Called after a node added
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="node"></param>
        protected virtual void OnNodeAdded(FlowChartLayer layer, Control node)
        {

        }

        /// <summary>
        /// Called after a node removed 
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="node"></param>
        protected virtual void OnNodeRemoved(FlowChartLayer layer, Control node)
        {

        }

        /// <summary>
        /// Called when a node dragged
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="node"></param>
        /// <param name="netDragDelta"></param>
        protected virtual void OnNodeDragged(FlowChartLayer layer, Control node, Vector2 netDragDelta)
        {

        }

        /// <summary>
        /// Called when connection established between two nodes
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        protected virtual void OnNodeConnected(FlowChartLayer layer, string from, string to)
        {

        }

        /// <summary>
        /// Called when connection broken
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        protected virtual void OnNodeDisconnected(FlowChartLayer layer, string from, string to)
        {

        }

        protected virtual void OnNodeConnectFailed(FlowChartLayer layer, string from)
        {

        }

        protected virtual void OnNodeReconnectBegin(FlowChartLayer layer, string from, string to)
        {

        }

        protected virtual void OnNodeReconnectEnd(FlowChartLayer layer, string from, string to)
        {

        }

        protected virtual void OnNodeReconnectFailed(FlowChartLayer layer, string from, string to)
        {

        }

        protected virtual bool _RequestConnectFrom(FlowChartLayer layer, string from)
        {
            return true;

        }

        protected virtual bool _RequestConnectTo(FlowChartLayer layer, string to)
        {
            return true;

        }

        /// <summary>
        /// Called when nodes duplicated
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="oldNodes"></param>
        /// <param name="newNodes"></param>
        protected virtual void OnDuplicated(FlowChartLayer layer, GDC.Array<Control> oldNodes, GDC.Array<Control> newNodes)
        {
        }
        #endregion

        #region Utils
        /// <summary>
        /// Used for looking up the transition line using GetNode
        /// </summary>
        /// <param name="transitionLine"></param>
        /// <returns></returns>
        public static string GetFlowChartLineName(string from, string to) => $"{from}>{to}";
        #endregion
    }
}