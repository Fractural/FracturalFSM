
using System;
using System.Collections;
using System.Collections.Generic;
using Godot;
using GDC = Godot.Collections;

namespace GodotRollbackNetcode.StateMachine
{

    [Tool]
    public class FlowChart : Control
    {
        [Signal] delegate void ConnectionEstablished(string from, string to, FlowChartLine line);// When a connection established
        [Signal] delegate void ConnectionBroken(string from, string to, FlowChartLine line);// When a connection broken
        [Signal] delegate void NodeSelected(FlowChartNode node);// When a node selected
        [Signal] delegate void NodeDeselected(FlowChartNode node);// When a node deselected
        [Signal] delegate void Dragged(FlowChartNode node, float distance);// When a node dragged

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

        private bool _isConnecting = false;
        private bool _currentConnection;
        private bool _isDragging = false;
        private bool _isDraggingNode = false;
        private Vector2 _dragStartPos = Vector2.Zero;
        private Vector2 _dragEndPos = Vector2.Zero;
        private GDC.Array _dragOrigins = new GDC.Array() { };
        private GDC.Array _selection = new GDC.Array() { };
        private GDC.Array _copyingNodes = new GDC.Array() { };

        public StyleBoxFlat selectionStylebox = new StyleBoxFlat();

        public Color gridMajorColor = new Color(1, 1, 1, 0.15f);
        public Color gridMinorColor = new Color(1, 1, 1, 0.07f);


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
            content.RectPivotOffset = GetScrollRect().Size / 2.0;// Scale from center
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
                    // 	var lineLocalUpOffset = connection.line.RectPosition - connection.line.GetTransform().Xform(Vector2.UP * connection.offset);
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
                                    foreach ((string from, string to) connectionPair in currentLayer.GetConnectionList())
                                        if (connectionPair.from == node.Name || connectionPair.to == node.Name)
                                            DisconnectNode(currentLayer, connectionPair.from, connectionPair.to).QueueFree();
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
                                if (_currentConnection)
                                {
                                    var pos = ContentPosition(GetLocalMousePosition());
                                    GDC.Array clipRects = new GDC.Array() { _currentConnection.FromNode.GetRect() };
                                    // Snapping connecting line
                                    foreach (var i in currentLayer.ContentNodes.GetChildCount())
                                    {
                                        var child = currentLayer.ContentNodes.GetChild(currentLayer.ContentNodes.GetChildCount() - 1 - i);// Inverse order to check from top to bottom of canvas
                                        if (child is FlowChartNode && child.name != _currentConnection.FromNode.name)
                                        {
                                            if (_RequestConnectTo(currentLayer, child.name))
                                            {
                                                if (child.GetRect().HasPoint(pos))
                                                {
                                                    pos = child.RectPosition + child.rect_Size / 2;
                                                    clipRects.Append(child.GetRect());
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    _currentConnection.line.Join(_currentConnection.GetFromPos(), pos, Vector2.Zero, clipRects);
                                }
                            }
                            else if (_isDraggingNode)
                            {
                                // Dragging nodes
                                var dragged = ContentPosition(_dragEndPos) - ContentPosition(_dragStartPos);
                                foreach (var i in _selection.Size())
                                {
                                    var selected = _selection[i];
                                    if (!(selected is FlowChartNode))
                                    {
                                        continue;
                                    }
                                    selected.RectPosition = (_dragOrigins[i] + selected.rect_Size / 2.0 + dragged);
                                    selected.modulate.a = 0.3;
                                    if (isSnapping)
                                    {
                                        selected.RectPosition = selected.RectPosition.Snapped(Vector2.ONE * snap);
                                    }
                                    selected.RectPosition -= selected.rect_Size / 2.0;
                                    _OnNodeDragged(currentLayer, selected, dragged);
                                    EmitSignal("dragged", selected, dragged);
                                    // Update connection pos
                                    foreach (var from in currentLayer.Connections)
                                    {
                                        var connectionsFrom = currentLayer.Connections[from];
                                        foreach (var to in connectionsFrom)
                                        {
                                            if (from == selected.name || to == selected.name)
                                            {
                                                var connection = currentLayer.Connections[from][to];
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
            if (inputEvent is InputEventMouseButton)

            {
                switch (inputEvent.ButtonIndex)
                {
                    case (int)ButtonList.Middle:
                        // Reset zoom
                        if (inputEvent.doubleclick)
                        {
                            Zoom = 1.0);
                            Update();
                        }
                        break;
                    case (int)ButtonList.WheelUp:
                        // Zoom in
                        Zoom = zoom + 0.01);
                        Update();
                        break;
                    case (int)ButtonList.WheelDown:
                        // Zoom out
                        Zoom = zoom - 0.01);
                        Update();
                        break;
                    case (int)ButtonList.Left:
                        // Hit detection
                        var hitNode;
                        foreach (var i in currentLayer.ContentNodes.GetChildCount())
                        {
                            var child = currentLayer.ContentNodes.GetChild(currentLayer.ContentNodes.GetChildCount() - 1 - i);// Inverse order to check from top to bottom of canvas
                            if (child is FlowChartNode)
                            {
                                if (child.GetRect().HasPoint(ContentPosition(inputEvent.Position)))
                                {
                                    hitNode = child;
                                    break;
                                }
                            }
                        }
                        if (!hit_node)
                        {
                            // Test Line
                            // Refer https://github.com/godotengine/godot/blob/master/editor/plugins/animation_state_machine_editor.cpp#L187
                            int closest = -1;
                            int closestD = 1e20



                        var connectionList = GetConnectionList();
                            foreach (var i in connectionList.Size())
                            {
                                var connection = currentLayer.Connections[connectionList[i].from][connectionList[i].to];
                                // Line's offset along its down-vector
                                var lineLocalUpOffset = connection.line.RectPosition - connection.line.GetTransform().Xform(Vector2.DOWN * connection.offset);
                                var fromPos = connection.GetFromPos() + lineLocalUpOffset;
                                var toPos = connection.GetToPos() + lineLocalUpOffset;
                                var cp = Geometry.GetClosestPointToSegment2d(ContentPosition(inputEvent.Position), fromPos, toPos);
                                var d = cp.DistanceTo(ContentPosition(inputEvent.Position));
                                if (d > connection.line.rect_Size.y * 2)
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
                                hitNode = currentLayer.Connections[connectionList[closest].from][connectionList[closest].to].line;

                            }
                        }
                        if (inputEvent.Pressed)
                        {
                            if (!(hitNode in _selection) && !inputEvent.shift)
						{
                                // Click on empty space
                                ClearSelection();
                            }
                            if (hitNode)
                            {
                                // Click on Node(can be a line)
                                _isDraggingNode = true;
                                if (hitNode is FlowChartLine)
                                {
                                    currentLayer.content_lines.MoveChild(hitNode, currentLayer.content_lines.GetChildCount() - 1);// Raise selected line to top
                                    if (inputEvent.shift && canGuiConnectNode)
                                    {
                                        // Reconnection Start
                                        foreach (var from in currentLayer._connections.Keys())
                                        {
                                            var fromConnections = currentLayer.Connections[from];
                                            foreach (var to in fromConnections.Keys())
                                            {
                                                var connection = fromConnections[to];
                                                if (connection.line == hitNode)
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
                                if (hitNode is FlowChartNode)
                                {
                                    currentLayer.ContentNodes.MoveChild(hitNode, currentLayer.ContentNodes.GetChildCount() - 1);// Raise selected node to top
                                    if (inputEvent.shift && canGuiConnectNode)
                                    {
                                        // Connection start
                                        if (_RequestConnectFrom(currentLayer, hitNode.name))
                                        {
                                            _isConnecting = true;
                                            _isDraggingNode = false;
                                            var line = CreateLineInstance();
                                            var connection = Connection.new(line, hitNode, null)



                                        currentLayer._ConnectNode(connection);
                                            _currentConnection = connection;
                                            _currentConnection.line.Join(_currentConnection.GetFromPos(), ContentPosition(inputEvent.Position));
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
                                _dragStartPos = inputEvent.Position;
                                _dragEndPos = inputEvent.Position;
                            }
                        }

                        else
                        {
                            var wasConnecting = _isConnecting;
                            var wasDraggingNode = _isDraggingNode;
                            if (_currentConnection)
                            {
                                // Connection end
                                var from = _currentConnection.FromNode.name;
                                var to = hitNode ? hitNode.name : null


                            if (hitNode is FlowChartNode && _RequestConnectTo(currentLayer, to) && from != to)
                                {
                                    // Connection success
                                    var line;
                                    if (_currentConnection.to_node)
                                    {
                                        // Reconnection
                                        line = DisconnectNode(currentLayer, from, _currentConnection.to_node.name);
                                        _currentConnection.to_node = hitNode;
                                        _OnNodeReconnectEnd(currentLayer, from, to);
                                        ConnectNode(currentLayer, from, to, line);
                                    }
                                    else
                                    {
                                        // New Connection
                                        currentLayer.content_lines.RemoveChild(_currentConnection.line);
                                        line = _currentConnection.line;
                                        _currentConnection.to_node = hitNode;
                                        ConnectNode(currentLayer, from, to, line);
                                    }
                                }
                                else
                                {
                                    // Connection failed
                                    if (_currentConnection.to_node)
                                    {
                                        // Reconnection
                                        _currentConnection.



                                            ();
                                        _OnNodeReconnectFailed(currentLayer, from, name);
                                    }
                                    else
                                    {
                                        // New Connection
                                        _currentConnection.line.QueueFree();
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
                                    foreach (var node in currentLayer.ContentNodes.GetChildren())
                                    {
                                        var rect = GetTransform().Xform(content.GetTransform().Xform(node.GetRect()));
                                        if (selectionBoxRect.Intersects(rect))
                                        {
                                            if (node is FlowChartNode)
                                            {
                                                Select(node);
                                                // Select line
                                            }
                                        }
                                    }
                                    var connectionList = GetConnectionList();
                                    foreach (var i in connectionList.Size())
                                    {
                                        var connection = currentLayer.Connections[connectionList[i].from][connectionList[i].to];
                                        // Line's offset along its down-vector
                                        var lineLocalUpOffset = connection.line.RectPosition - connection.line.GetTransform().Xform(Vector2.UP * connection.offset);
                                        var fromPos = content.GetTransform().Xform(connection.GetFromPos() + lineLocalUpOffset);
                                        var toPos = content.GetTransform().Xform(connection.GetToPos() + lineLocalUpOffset);
                                        if (CohenSutherland.LineIntersectRectangle(fromPos, toPos, selectionBoxRect))
                                        {
                                            Select(connection.line);
                                        }
                                    }
                                }
                                if (wasDraggingNode)
                                {
                                    // Update _dragOrigins with new Position after dragged
                                    foreach (var i in _selection.Size())
                                    {
                                        var selected = _selection[i];
                                        _dragOrigins[i] = selected.RectPosition;
                                        selected.modulate.a = 1.0;
                                    }
                                }
                                _dragStartPos = _dragEndPos;
                                Update();

                                // Get selection box rect
                            }
                        }
                        break;
                }
            }
        }

        public __TYPE GetSelectionBoxRect()
        {
            Vector2 pos = new Vector2(Mathf.Min(_dragStartPos.x, _dragEndPos.x), Mathf.Min(_dragStartPos.y, _dragEndPos.y));
            var Size = (_dragEndPos - _dragStartPos).Abs();
            return new Rect2(pos, Size);

            // Get required scroll rect base on content
        }

        public Rect2 GetScrollRect(FlowChartLayer layer = null)
        {
            if (layer == null) layer = currentLayer;
            return layer.GetScrollRect(scrollMargin);

        }

        public __TYPE AddLayerTo(__TYPE target)
        {
            var layer = CreateLayerInstance();
            target.AddChild(layer);
            return layer;

        }

        public __TYPE GetLayer(__TYPE np)
        {
            return content.GetNodeOrNull(np);

        }

        public void SelectLayerAt(__TYPE i)
        {
            SelectLayer(content.GetChild(i));

        }

        public void SelectLayer(__TYPE layer)
        {
            var prevLayer = currentLayer;
            _OnLayerDeselected(prevLayer);
            currentLayer = layer;
            _OnLayerSelected(layer);

            // Add node
        }

        public void AddNode(__TYPE layer, __TYPE node)
        {
            layer.AddNode(node);
            _OnNodeAdded(layer, node);

            // Remove node
        }

        public void RemoveNode(__TYPE layer, __TYPE nodeName)
        {
            var node = layer.ContentNodes.GetNodeOrNull(nodeName);
            if (node)
            {
                Deselect(node);// Must deselct before remove to make sure _dragOrigins synced with _selections
                layer.RemoveNode(node);
                _OnNodeRemoved(layer, nodeName);

                // Called after connection established
            }
        }

        public void _ConnectNode(__TYPE line, __TYPE fromPos, __TYPE toPos)
        {

            // Called after connection broken
        }

        public void _DisconnectNode(__TYPE line)
        {
            if (line in _selection)
		{
                Deselect(line);

            }
        }

        public __TYPE CreateLayerInstance()
        {
            var layer = new Control()


        layer.SetScript(FlowChartLayer);
            return layer;

            // Return new line instance to use, called when connecting node
        }

        public __TYPE CreateLineInstance()
        {
            return FlowChartLineScene.Instance();

            // Rename node
        }

        public void RenameNode(__TYPE layer, __TYPE old, new)
	{
    layer.RenameNode(old, new);

    // Connect two nodes with a line
}

    public void ConnectNode(__TYPE layer, __TYPE from, __TYPE to, __TYPE line = null)
    {
        if (!line)
        {
            line = CreateLineInstance();
        }
        line.name = "%s>%s" % [from, to] // "From>To";

        layer.ConnectNode(line, from, to, interconnectionOffset);
        _OnNodeConnected(layer, from, to);
        EmitSignal("connection", from, to, line);

        // Break a connection between two node
    }

    public __TYPE DisconnectNode(__TYPE layer, __TYPE from, __TYPE to)
    {
        var line = layer.DisconnectNode(from, to);
        Deselect(line);// Since line is selectable as well
        _OnNodeDisconnected(layer, from, to);
        EmitSignal("disconnection", from, to);
        return line;

        // Clear all connections
    }

    public void ClearConnections(__TYPE layer = currentLayer)
    {
        layer.ClearConnections();

        // Select a Node(can be a line)
    }

    public void Select(__TYPE node)
    {
        if (node in _selection)
		{
            return;

        }
        _selection.Append(node);
        node.selected = true;
        _dragOrigins.Append(node.RectPosition);
        EmitSignal("node_selected", node);

        // Deselect a node
    }

    public void Deselect(__TYPE node)
    {
        _selection.Erase(node);
        if (IsInstanceValid(node))
        {
            node.selected = false;
        }
        _dragOrigins.PopBack();
        EmitSignal("node_deselected", node);
    }

    /// <summary>
    /// Clear all selection
    /// </summary>
    public void ClearSelection()
    {
        foreach (var node in _selection.Duplicate()) // duplicate _selection GDC.Array as Deselect() edit GDC.Array
        {
            if (!node)
            {
                continue;
            }
            Deselect(node);
        }
        _selection.Clear();

    }

    /// <summary>
    /// Duplicate given nodes in editor
    /// </summary>
    /// <param name="layer"></param>
    /// <param name="nodes"></param>
    public void DuplicateNodes(FlowChartLayer layer, IEnumerable<Node> nodes)
    {
        ClearSelection();
        GDC.Array newNodes = new GDC.Array() { };
        foreach (var i in nodes.Size())
        {
            var node = nodes[i];
            if (!(node is FlowChartNode))
            {
                continue;
            }
            var newNode = node.Duplicate(DUPLICATESignals + DUPLICATEScripts);
            var offset = ContentPosition(GetLocalMousePosition()) - ContentPosition(_dragEndPos);
            newNode.RectPosition = newNode.RectPosition + offset;
            newNodes.Append(newNode)


            AddNode(layer, newNode);
            Select(newNode);
            // Duplicate connection within selection
        }
        foreach (var i in nodes.Size())
        {
            var fromNode = nodes[i];
            foreach (var connectionPair in GetConnectionList())
            {
                if (fromNode.name == connectionPair.from)
                {
                    foreach (var j in nodes.Size())
                    {
                        var toNode = nodes[j];
                        if (toNode.name == connectionPair.to)
                        {
                            ConnectNode(layer, newNodes[i].name, newNodes[j].name);
                        }
                    }
                }
            }
        }
        _OnDuplicated(layer, nodes, newNodes);

        // Called after layer Selected(currentLayer changed)
    }

    public void _OnLayerSelected(__TYPE layer)
    {

    }

    public void _OnLayerDeselected(__TYPE layer)
    {

        // Called after a node added
    }

    public void _OnNodeAdded(__TYPE layer, __TYPE node)
    {

        // Called after a node removed
    }

    public void _OnNodeRemoved(__TYPE layer, __TYPE node)
    {

        // Called when a node dragged
    }

    public void _OnNodeDragged(__TYPE layer, __TYPE node, __TYPE dragged)
    {

        // Called when connection established between two nodes
    }

    public void _OnNodeConnected(__TYPE layer, __TYPE from, __TYPE to)
    {

        // Called when connection broken
    }

    public void _OnNodeDisconnected(__TYPE layer, __TYPE from, __TYPE to)
    {

    }

    public void _OnNodeConnectFailed(__TYPE layer, __TYPE from)
    {

    }

    public void _OnNodeReconnectBegin(__TYPE layer, __TYPE from, __TYPE to)
    {

    }

    public void _OnNodeReconnectEnd(__TYPE layer, __TYPE from, __TYPE to)
    {

    }

    public void _OnNodeReconnectFailed(__TYPE layer, __TYPE from, __TYPE to)
    {

    }

    public __TYPE _RequestConnectFrom(__TYPE layer, __TYPE from)
    {
        return true;

    }

    public __TYPE _RequestConnectTo(__TYPE layer, __TYPE to)
    {
        return true;

        // Called when nodes duplicated
    }

    public void _OnDuplicated(__TYPE layer, __TYPE oldNodes, __TYPE newNodes)
    {

        // Convert Position in FlowChart space to Content(takes translation/scale of content into account)
    }

    public __TYPE ContentPosition(__TYPE pos)
    {
        return (pos - content.RectPosition - content.RectPivotOffset * (Vector2.ONE - content.rect_scale)) * 1.0 / content.rect_scale;

        // Return GDC.Array of dictionary of connection as such [new Dictionary(){{"from1", "to1"}}, new Dictionary(){{"from2", "to2"}}]
    }

    public __TYPE GetConnectionList(__TYPE layer = currentLayer)
    {
        return layer.GetConnectionList();


    }



}
}
}