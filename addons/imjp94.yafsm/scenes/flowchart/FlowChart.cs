
using System;
using System.Collections;
using System.Collections.Generic;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace GodotRollbackNetcode.StateMachine
{
    [Tool]
    public class FlowChart : Control
    {
        [Signal] public delegate void ConnectionEstablished(string from, string to, FlowChartLine line);// When a connection established
        [Signal] public delegate void ConnectionBroken(string from, string to, FlowChartLine line);// When a connection broken
        [Signal] public delegate void NodeSelected(FlowChartNode node);// When a node selected
        [Signal] public delegate void NodeDeselected(FlowChartNode node);// When a node deselected
        [Signal] public delegate void Dragged(FlowChartNode node, float distance);// When a node dragged

        /// <summary>
        /// Margin of content from edge of FlowChart
        /// </summary>
        [Export] public int scrollMargin = 100;
        /// <summary>
        /// Offset between two line that interconnecting
        /// </summary>
        [Export] public int interconnectionOffset = 10;
        /// <summary>
        /// Snap amount
        /// </summary>
        [Export] public int snap = 20;
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
                zoom = value;
                content.RectScale = Vector2.One * zoom;
            }
        }

        public Control content = new Control(); // Root node that hold anything drawn in the flowchart

        public FlowChartLayer currentLayer;
        public HScrollBar hScroll = new HScrollBar();

        public VScrollBar vScroll = new VScrollBar();

        public VBoxContainer topBar = new VBoxContainer();

        public HBoxContainer gadget = new HBoxContainer(); // Root node of top overlay controls

        public Button zoomMinus = new Button();

        public Button zoomReset = new Button();

        public Button zoomPlus = new Button();

        public Button snapButton = new Button();

        public SpinBox snapAmount = new SpinBox();

        public bool isSnapping = true;
        public bool canGuiSelectNode = true;
        public bool canGuiDeleteNode = true;
        public bool canGuiConnectNode = true;

        private PackedScene flowChartNodePrefab = GD.Load<PackedScene>("res://addons/imjp94.yafsm/scenes/flowchart/FlowChartNode.tscn");
        private PackedScene flowChartLinePrefab = GD.Load<PackedScene>("res://addons/imjp94.yafsm/scenes/flowchart/FlowChartLine.tscn");

        private bool _isConnecting = false;
        private Connection _currentConnection;
        private bool _isDragging = false;
        private bool _isDraggingNode = false;
        private Vector2 _dragStartPos = Vector2.Zero;
        private Vector2 _dragEndPos = Vector2.Zero;
        private GDC.Array<Vector2> _dragOrigins = new GDC.Array<Vector2>() { };
        private GDC.Array<Control> _selection = new GDC.Array<Control>() { };
        private GDC.Array<Control> _copyingNodes = new GDC.Array<Control>() { };

        private StyleBoxFlat selectionStylebox = new StyleBoxFlat();

        private Color gridMajorColor = new Color(1, 1, 1, 0.15f);
        private Color gridMinorColor = new Color(1, 1, 1, 0.07f);


        public FlowChart()
        {
            FocusMode = FocusModeEnum.All;
            selectionStylebox.BgColor = new Color(0, 0, 0, 0.3f);
            selectionStylebox.SetBorderWidthAll(1);

            content.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(content);

            AddChild(hScroll);
            hScroll.SetAnchorsAndMarginsPreset(LayoutPreset.BottomWide);
            hScroll.Connect("value_changed", this, "_on_h_scroll_changed");
            hScroll.Connect("gui_input", this, "_on_h_scroll_gui_input");

            AddChild(vScroll);
            vScroll.SetAnchorsAndMarginsPreset(LayoutPreset.RightWide);
            vScroll.Connect("value_changed", this, "_on_v_scroll_changed");
            vScroll.Connect("gui_input", this, "_on_v_scroll_gui_input");

            hScroll.MarginRight = -vScroll.RectSize.x;
            vScroll.MarginBottom = -hScroll.RectSize.y;

            AddLayerTo(content);
            SelectLayerAt(0);

            topBar.SetAnchorsAndMarginsPreset(LayoutPreset.TopWide);
            topBar.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(topBar);

            gadget.MouseFilter = MouseFilterEnum.Ignore;
            topBar.AddChild(gadget);

            zoomMinus.Flat = true;
            zoomMinus.HintTooltip = "Zoom Out";
            zoomMinus.Connect("Pressed", this, "_on_zoom_minus_Pressed");
            zoomMinus.FocusMode = FocusModeEnum.None;
            gadget.AddChild(zoomMinus);

            zoomReset.Flat = true;
            zoomReset.HintTooltip = "Zoom Reset";
            zoomReset.Connect("Pressed", this, "_on_zoom_reset_Pressed");
            zoomReset.FocusMode = FocusModeEnum.None;
            gadget.AddChild(zoomReset);

            zoomPlus.Flat = true;
            zoomPlus.HintTooltip = "Zoom In";
            zoomPlus.Connect("Pressed", this, "_on_zoom_plus_Pressed");
            zoomPlus.FocusMode = FocusModeEnum.None;
            gadget.AddChild(zoomPlus);

            snapButton.Flat = true;
            snapButton.ToggleMode = true;
            snapButton.HintTooltip = "Enable snap && show grid";
            snapButton.Connect("Pressed", this, "_on_snap_button_Pressed");
            snapButton.Pressed = true;
            snapButton.FocusMode = FocusModeEnum.None;
            gadget.AddChild(snapButton);

            snapAmount.Value = snap;
            snapAmount.Connect("value_changed", this, "_on_snap_amount_value_changed");
            gadget.AddChild(snapAmount);

        }

        public void _OnHScrollGuiInput(InputEvent inputEvent)
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

        public void _OnVScrollGuiInput(InputEvent inputEvent)
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

        public void _OnHScrollChanged(float value)
        {
            content.RectPosition = new Vector2(-value, content.RectPosition.y);
        }

        public void _OnVScrollChanged(float value)
        {
            content.RectPosition = new Vector2(content.RectPosition.x, -value);
        }

        public void _OnZoomMinusPressed()
        {
            Zoom = zoom - 0.1f;
            Update();
        }

        public void _OnZoomResetPressed()
        {
            Zoom = 1f;
            Update();
        }

        public void _OnZoomPlusPressed()
        {
            Zoom = zoom + 0.1f;
            Update();
        }

        public void _OnSnapButtonPressed()
        {
            isSnapping = snapButton.Pressed;
            Update();
        }

        public void _OnSnapAmountValueChanged(int value)
        {
            snap = value;
            Update();
        }

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

                // Draw selection box
            }
            if (!_isDraggingNode && !_isConnecting)
            {

                var selectionBoxRect = GetSelectionBoxRect();
                DrawStyleBox(selectionStylebox, selectionBoxRect);

                // Draw grid
                // Refer GraphEdit(https://github.com/godotengine/godot/blob/6019dab0b45e1291e556e6d9e01b625b5076cc3c/scene/gui/graph_edit.cpp#L442)
            }
            if (isSnapping)
            {
                var scrollOffset = new Vector2((float)hScroll.Value, (float)vScroll.Value);
                var offset = scrollOffset / zoom;
                var Size = RectSize / zoom;

                var from = (offset / (float)(snap)).Floor();
                var len = (Size / (float)(snap)).Floor() + new Vector2(1, 1);

                var gridMinor = gridMinorColor;
                var gridMajor = gridMajorColor;

                for (int i = (int)from.x; i < from.x + len.x; i++)
                {
                    Color color;

                    if (Mathf.Abs(i) % 10 == 0)
                        color = gridMajor;
                    else
                        color = gridMinor;

                    var baseOfs = i * snap * zoom - offset.x * zoom;
                    DrawLine(new Vector2(baseOfs, 0), new Vector2(baseOfs, RectSize.y), color);
                }
                for (int i = (int)from.y; i < from.y + len.y; i++)
                {
                    Color color;

                    if (Mathf.Abs(i) % 10 == 0)
                        color = gridMajor;
                    else
                        color = gridMinor;

                    var baseOfs = i * snap * zoom - offset.y * zoom;
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
                        if (keyEvent.Pressed && canGuiDeleteNode)
                        {
                            // Delete nodes
                            foreach (Node node in _selection.Duplicate())
                            {
                                if (node is FlowChartLine flowChartLine)
                                {
                                    // TODO: More efficient way to get connection from Line node
                                    foreach (GDC.Dictionary connectionsFrom in currentLayer.Connections.Duplicate().Values)
                                    {
                                        foreach (Connection connection in connectionsFrom.Duplicate().Values)
                                            if (connection.Line == flowChartLine)
                                                DisconnectNode(currentLayer, connection.FromNode.Name, connection.ToNode.Name).QueueFree();
                                    }
                                }
                                else if (node is FlowChartNode flowChartNode)
                                {
                                    RemoveNode(currentLayer, node.Name);
                                    foreach (var connectionPair in currentLayer.GetConnectionList())
                                        if (connectionPair.From == node.Name || connectionPair.To == node.Name)
                                            DisconnectNode(currentLayer, connectionPair.From, connectionPair.To).QueueFree();
                                }
                            }
                            AcceptEvent();
                        }
                        break;
                    case (uint)KeyList.C:
                        if (keyEvent.Pressed && keyEvent.Control)
                        {
                            // Copy node
                            _copyingNodes = _selection.Duplicate();
                            AcceptEvent();
                        }
                        break;
                    case (uint)KeyList.D:
                        if (keyEvent.Pressed && keyEvent.Control)
                        {
                            // Duplicate node directly from selection
                            DuplicateNodes(currentLayer, _selection.Duplicate());
                            AcceptEvent();
                        }
                        break;
                    case (uint)KeyList.V:
                        if (keyEvent.Pressed && keyEvent.Control)
                        {
                            // Paste node from _copyingNodes
                            DuplicateNodes(currentLayer, _copyingNodes);
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
                        if (_isDragging)
                        {
                            if (_isConnecting)
                            {
                                // Connecting
                                if (_currentConnection != null)
                                {
                                    var pos = ContentPosition(GetLocalMousePosition());
                                    GDC.Array<Rect2> clipRects = new GDC.Array<Rect2>() { _currentConnection.FromNode.GetRect() };
                                    // Snapping connecting line
                                    int currentLayerChildCount = currentLayer.ContentNodes.GetChildCount();
                                    for (int i = 0; i < currentLayerChildCount; i++)
                                    {
                                        var child = currentLayer.ContentNodes.GetChild(currentLayer.ContentNodes.GetChildCount() - 1 - i);// Inverse order to check from top to bottom of canvas
                                        if (child is FlowChartNode flowChartNode && child.Name != _currentConnection.FromNode.Name)
                                        {
                                            if (_RequestConnectTo(currentLayer, flowChartNode.Name))
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
                                    _currentConnection.Line.Join(_currentConnection.GetFromPos(), pos, Vector2.Zero, clipRects);
                                }
                            }
                            else if (_isDraggingNode)
                            {
                                // Dragging nodes
                                var dragDelta = ContentPosition(_dragEndPos) - ContentPosition(_dragStartPos);
                                for (int i = 0; i < _selection.Count; i++)
                                {
                                    var selected = _selection[i];
                                    if (!(selected is FlowChartNode selectedFlowChartNode))
                                        continue;

                                    selectedFlowChartNode.RectPosition = _dragOrigins[i] + selectedFlowChartNode.RectSize / 2f + dragDelta;

                                    var color = selectedFlowChartNode.Modulate;
                                    color.a = 0.3f;
                                    selectedFlowChartNode.Modulate = color;

                                    if (isSnapping)
                                    {
                                        selectedFlowChartNode.RectPosition = selectedFlowChartNode.RectPosition.Snapped(Vector2.One * snap);
                                    }
                                    selectedFlowChartNode.RectPosition -= selectedFlowChartNode.RectSize / 2f;
                                    _OnNodeDragged(currentLayer, selected, dragDelta);
                                    EmitSignal(nameof(Dragged), selected, dragDelta);
                                    // Update connection pos
                                    foreach (string from in currentLayer.Connections)
                                    {
                                        var connectionsFrom = currentLayer.Connections.Get<GDC.Dictionary>(from);
                                        foreach (string to in connectionsFrom)
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
                            _dragEndPos = GetLocalMousePosition();
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
                        int currentLayerChildCount = currentLayer.ContentNodes.GetChildCount();
                        for (int i = 0; i < currentLayerChildCount; i++)
                        {
                            var child = currentLayer.ContentNodes.GetChild(currentLayer.ContentNodes.GetChildCount() - 1 - i);// Inverse order to check from top to bottom of canvas
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
                                var connection = currentLayer.Connections.Get<Connection>($"{connectionList[i].From}{connectionList[i].To}");
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
                                hitNode = currentLayer.Connections.Get<Connection>($"{connectionList[closest].From}{connectionList[closest].To}").Line;
                            }
                        }
                        if (mouseButtonEvent.Pressed)
                        {
                            if (!_selection.Contains(hitNode) && !mouseButtonEvent.Shift)
                            {
                                // Click on empty space
                                ClearSelection();
                            }
                            if (hitNode != null)
                            {
                                // Click on Node(can be a line)
                                _isDraggingNode = true;
                                if (hitNode is FlowChartLine)
                                {
                                    currentLayer.ContentLines.MoveChild(hitNode, currentLayer.ContentLines.GetChildCount() - 1);// Raise selected line to top
                                    if (mouseButtonEvent.Shift && canGuiConnectNode)
                                    {
                                        // Reconnection Start
                                        foreach (string from in currentLayer.Connections.Keys)
                                        {
                                            var fromConnections = currentLayer.Connections.Get<GDC.Dictionary>(from);
                                            foreach (string to in fromConnections.Keys)
                                            {
                                                var connection = fromConnections.Get<Connection>(to);
                                                if (connection.Line == hitNode)
                                                {
                                                    _isConnecting = true;
                                                    _isDraggingNode = false;
                                                    _currentConnection = connection;
                                                    _OnNodeReconnectBegin(currentLayer, from, to);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                if (hitNode is FlowChartNode flowChartNode)
                                {
                                    currentLayer.ContentNodes.MoveChild(hitNode, currentLayer.ContentNodes.GetChildCount() - 1); // Raise selected node to top
                                    if (mouseButtonEvent.Shift && canGuiConnectNode)
                                    {
                                        // Connection start
                                        if (_RequestConnectFrom(currentLayer, hitNode.Name))
                                        {
                                            _isConnecting = true;
                                            _isDraggingNode = false;
                                            var line = CreateLineInstance();
                                            var connection = new Connection(line, flowChartNode, null);

                                            currentLayer.AfterConnectNode(connection);
                                            _currentConnection = connection;
                                            _currentConnection.Line.Join(_currentConnection.GetFromPos(), ContentPosition(mouseButtonEvent.Position));
                                        }
                                    }
                                    AcceptEvent();
                                }
                                if (_isConnecting)
                                {
                                    ClearSelection();
                                }
                                else
                                {
                                    if (canGuiSelectNode)
                                    {
                                        Select(hitNode);
                                    }
                                }
                            }
                            if (!_isDragging)
                            {
                                // Drag start
                                _isDragging = true;
                                _dragStartPos = mouseButtonEvent.Position;
                                _dragEndPos = mouseButtonEvent.Position;
                            }
                        }
                        else
                        {
                            var wasConnecting = _isConnecting;
                            var wasDraggingNode = _isDraggingNode;
                            if (_currentConnection != null)
                            {
                                // Connection end
                                var from = _currentConnection.FromNode.Name;
                                var to = hitNode != null ? hitNode.Name : null;


                                if (hitNode is FlowChartNode flowChartNode && _RequestConnectTo(currentLayer, to) && from != to)
                                {
                                    // Connection success
                                    FlowChartLine line;
                                    if (_currentConnection.ToNode != null)
                                    {
                                        // Reconnection
                                        line = DisconnectNode(currentLayer, from, _currentConnection.ToNode.Name);
                                        _currentConnection.ToNode = flowChartNode;
                                        _OnNodeReconnectEnd(currentLayer, from, to);
                                        ConnectNode(currentLayer, from, to, line);
                                    }
                                    else
                                    {
                                        // New Connection
                                        currentLayer.ContentLines.RemoveChild(_currentConnection.Line);
                                        line = _currentConnection.Line;
                                        _currentConnection.ToNode = flowChartNode;
                                        ConnectNode(currentLayer, from, to, line);
                                    }
                                }
                                else
                                {
                                    // Connection failed
                                    if (_currentConnection.ToNode != null)
                                    {
                                        // Reconnection
                                        _currentConnection.Join();
                                        _OnNodeReconnectFailed(currentLayer, from, Name);
                                    }
                                    else
                                    {
                                        // New Connection
                                        _currentConnection.Line.QueueFree();
                                        _OnNodeConnectFailed(currentLayer, from);
                                    }
                                }
                                _isConnecting = false;
                                _currentConnection = null;
                                AcceptEvent();

                            }
                            if (_isDragging)
                            {
                                // Drag end
                                _isDragging = false;
                                _isDraggingNode = false;
                                if (!(wasConnecting || wasDraggingNode) && canGuiSelectNode)
                                {
                                    var selectionBoxRect = GetSelectionBoxRect();
                                    // Select node
                                    foreach (Control node in currentLayer.ContentNodes.GetChildren())
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
                                        var connection = currentLayer.Connections.Get<Connection>($"{connectionList[i].From}.{connectionList[i].To}");
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
                                    for (int i = 0; i < _selection.Count; i++)
                                    {
                                        var selected = _selection[i];
                                        _dragOrigins[i] = selected.RectPosition;
                                        var color = selected.Modulate;
                                        color.a = 1f;
                                        selected.Modulate = color;
                                    }
                                }
                                _dragStartPos = _dragEndPos;
                                Update();

                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Get selection box rect
        /// </summary>
        /// <returns></returns>
        public Rect2 GetSelectionBoxRect()
        {
            Vector2 pos = new Vector2(Mathf.Min(_dragStartPos.x, _dragEndPos.x), Mathf.Min(_dragStartPos.y, _dragEndPos.y));
            var Size = (_dragEndPos - _dragStartPos).Abs();
            return new Rect2(pos, Size);

        }

        /// <summary>
        /// Get required scroll rect base on content
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        public Rect2 GetScrollRect(FlowChartLayer layer = null)
        {
            if (layer == null) layer = currentLayer;
            return layer.GetScrollRect(scrollMargin);

        }

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
            var prevLayer = currentLayer;
            _OnLayerDeselected(prevLayer);
            currentLayer = layer;
            _OnLayerSelected(layer);

        }

        /// <summary>
        /// Add node
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="node"></param>
        public void AddNode(FlowChartLayer layer, Node node)
        {
            layer.AddNode(node);
            _OnNodeAdded(layer, node);
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
                _OnNodeRemoved(layer, nodeName);
            }
        }

        /// <summary>
        /// Called after connection established
        /// </summary>
        /// <param name="line"></param>
        /// <param name="fromPos"></param>
        /// <param name="toPos"></param>
        public void _ConnectNode(FlowChartLine line, Vector2 fromPos, Vector2 toPos)
        {
        }

        /// <summary>
        /// Called after connection broken
        /// </summary>
        /// <param name="line"></param>
        public void _DisconnectNode(FlowChartLine line)
        {
            if (_selection.Contains(line))
                Deselect(line);
        }

        /// <summary>
        /// Return a new layer instance to use.
        /// </summary>
        /// <returns></returns>
        public virtual FlowChartLayer CreateLayerInstance()
        {
            return new FlowChartLayer();
        }

        /// <summary>
        /// Return new line instance to use, called when connecting node
        /// </summary>
        /// <returns></returns>
        public virtual FlowChartLine CreateLineInstance()
        {
            return flowChartLinePrefab.Instance<FlowChartLine>();
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
            if (line != null)
            {
                line = CreateLineInstance();
            }
            line.Name = $"{from}>{to}"; // "From>To"

            layer.ConnectNode(line, from, to, interconnectionOffset);
            _OnNodeConnected(layer, from, to);
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
            _OnNodeDisconnected(layer, from, to);
            EmitSignal(nameof(ConnectionBroken), from, to);
            return line;
        }

        /// <summary>
        /// Clear all connections
        /// </summary>
        /// <param name="layer"></param>
        public void ClearConnections(FlowChartLayer layer = null)
        {
            if (layer == null) layer = currentLayer;
            layer.ClearConnections();
        }

        /// <summary>
        /// Select a Node(can be a line)
        /// </summary>
        /// <param name="node"></param>
        public void Select(Control node)
        {
            if (_selection.Contains(node))
                return;
            if (!(node is ISelectable selectable))
                return;

            _selection.Add(node);
            selectable.Selected = true;
            _dragOrigins.Add(node.RectPosition);
            EmitSignal(nameof(NodeSelected), node);

        }

        /// <summary>
        /// Deselect a node
        /// </summary>
        /// <param name="node"></param>
        public void Deselect(Control node)
        {
            _selection.Remove(node);
            if (IsInstanceValid(node) && node is ISelectable selectable)
            {
                selectable.Selected = false;
            }
            _dragOrigins.PopBack();
            EmitSignal(nameof(NodeDeselected), node);
        }

        /// <summary>
        /// Clear all selection
        /// </summary>
        public void ClearSelection()
        {
            foreach (var node in _selection.Duplicate()) // duplicate _selection GDC.Array as Deselect() edit GDC.Array
            {
                if (node == null)
                    continue;

                Deselect(node);
            }
            _selection.Clear();
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
                var offset = ContentPosition(GetLocalMousePosition()) - ContentPosition(_dragEndPos);
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
                        for (int j = 0; i < controlNodes.Count; i++)
                        {
                            var toNode = controlNodes[j];
                            if (toNode.Name == connectionPair.To)
                                ConnectNode(layer, newNodes[i].Name, newNodes[j].Name);
                        }
                    }
                }
            }
            _OnDuplicated(layer, controlNodes, newNodes);
        }

        #region Virtual Methods
        /// <summary>
        /// Called after layer Selected(currentLayer changed)
        /// </summary>
        /// <param name="layer"></param>
        public virtual void _OnLayerSelected(FlowChartLayer layer)
        {

        }

        public virtual void _OnLayerDeselected(FlowChartLayer layer)
        {

        }

        /// <summary>
        /// Called after a node added
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="node"></param>
        public virtual void _OnNodeAdded(FlowChartLayer layer, Control node)
        {

        }

        /// <summary>
        /// Called after a node removed 
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="node"></param>
        public virtual void _OnNodeRemoved(FlowChartLayer layer, Control node)
        {

        }

        /// <summary>
        /// Called when a node dragged
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="node"></param>
        /// <param name="dragDelta"></param>
        public virtual void _OnNodeDragged(FlowChartLayer layer, Control node, Vector2 dragDelta)
        {


        }

        /// <summary>
        /// Called when connection established between two nodes
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public virtual void _OnNodeConnected(FlowChartLayer layer, string from, string to)
        {

        }

        /// <summary>
        /// Called when connection broken
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public virtual void _OnNodeDisconnected(FlowChartLayer layer, string from, string to)
        {

        }

        public virtual void _OnNodeConnectFailed(FlowChartLayer layer, string from)
        {

        }

        public virtual void _OnNodeReconnectBegin(FlowChartLayer layer, string from, string to)
        {

        }

        public virtual void _OnNodeReconnectEnd(FlowChartLayer layer, string from, string to)
        {

        }

        public virtual void _OnNodeReconnectFailed(FlowChartLayer layer, string from, string to)
        {

        }

        public virtual bool _RequestConnectFrom(FlowChartLayer layer, string from)
        {
            return true;

        }

        public virtual bool _RequestConnectTo(FlowChartLayer layer, string to)
        {
            return true;

        }

        /// <summary>
        /// Called when nodes duplicated
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="oldNodes"></param>
        /// <param name="newNodes"></param>
        public virtual void _OnDuplicated(FlowChartLayer layer, GDC.Array<Control> oldNodes, GDC.Array<Control> newNodes)
        {
        }
        #endregion


        #region Helper Methods
        /// <summary>
        /// Convert Position in FlowChart space to Content(takes translation/scale of content into account)
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Vector2 ContentPosition(Vector2 pos)
        {
            return (pos - content.RectPosition - content.RectPivotOffset * (Vector2.One - content.RectScale)) * 1f / content.RectScale;
        }

        /// <summary>
        /// Return GDC.Array of dictionary of connection as such [new Dictionary(){{"from1", "to1"}}, new Dictionary(){{"from2", "to2"}}]
        /// </summary>
        /// <param name="layer"></param>
        /// <returns></returns>
        public IReadOnlyList<ConnectionPair> GetConnectionList(FlowChartLayer layer = null)
        {
            if (layer == null) layer = currentLayer;
            return layer.GetConnectionList();
        }
        #endregion
    }
}