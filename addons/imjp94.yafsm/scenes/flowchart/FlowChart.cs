
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class FlowChart : Control
{
	 
	public const var Utils = GD.Load("res://addons/imjp94.yafsm/scripts/Utils.gd");
	public const var CohenSutherland = Utils.CohenSutherland;
	public const var FlowChartNode = GD.Load("FlowChartNode.gd");
	public const var FlowChartNodeScene = GD.Load("FlowChartNode.tscn");
	public const var FlowChartLine = GD.Load("FlowChartLine.gd");
	public const var FlowChartLineScene = GD.Load("FlowChartLine.tscn");
	public const var FlowChartLayer = GD.Load("FlowChartLayer.gd");
	public const var Connection = FlowChartLayer.Connection;
	
	[Signal] delegate void Connection(from, to, line);// When a connection established
	[Signal] delegate void Disconnection(from, to, line);// When a connection broken
	[Signal] delegate void NodeSelected(node);// When a node selected
	[Signal] delegate void NodeDeselected(node);// When a node deselected
	[Signal] delegate void Dragged(node, distance);// When a node dragged
	
	// Margin of content from edge of FlowChart
	[Export] public int scrollMargin = 100 ;
	// Offset between two line that interconnecting
	[Export] public int interconnectionOffset = 10;
	// Snap amount
	[Export] public int snap = 20;
	// Zoom amount
	[Export] public float zoom = 1.0f {set{SetZoom(value);}}
	
	public __TYPE content = new Control() // Root node that hold anything drawn in the flowchart
	public __TYPE currentLayer;
	public __TYPE hScroll = new HScrollBar()
	public __TYPE vScroll = new VScrollBar()
	public __TYPE topBar = new VBoxContainer()
	public __TYPE gadget = new HBoxContainer() // Root node of top overlay controls
	public __TYPE zoomMinus = new Button()
	public __TYPE zoomReset = new Button()
	public __TYPE zoomPlus = new Button()
	public __TYPE snapButton = new Button()
	public __TYPE snapAmount = new SpinBox()
	
	public __TYPE isSnapping = true;
	public __TYPE canGuiSelectNode = true;
	public __TYPE canGuiDeleteNode = true;
	public __TYPE canGuiConnectNode = true;
	
	private __TYPE _isConnecting = false;
	private __TYPE _currentConnection;
	private __TYPE _isDragging = false;
	private __TYPE _isDraggingNode = false;
	private __TYPE _dragStartPos = Vector2.ZERO;
	private __TYPE _dragEndPos = Vector2.ZERO;
	private __TYPE _dragOrigins = new Array(){};
	private __TYPE _selection = new Array(){};
	private __TYPE _copyingNodes = new Array(){};
	
	public __TYPE selectionStylebox = new StyleBoxFlat()
	public __TYPE gridMajorColor = new Color(1, 1, 1, 0.15);
	public __TYPE gridMinorColor = new Color(1, 1, 1, 0.07);
		
	
	public void _Init()
	{  
		focusMode = FOCUSAll;
		selectionStylebox.bg_color = new Color(0, 0, 0, 0.3);
		selectionStylebox.SetBorderWidthAll(1);
	
		content.mouse_filter = MOUSEFilterIgnore;
		AddChild(content);
	
		AddChild(hScroll);
		hScroll.SetAnchorsAndMarginsPreset(PRESETBottomWide);
		hScroll.Connect("value_changed", this, "_on_h_scroll_changed");
		hScroll.Connect("gui_input", this, "_on_h_scroll_gui_input");
	
		AddChild(vScroll);
		vScroll.SetAnchorsAndMarginsPreset(PRESETRightWide);
		vScroll.Connect("value_changed", this, "_on_v_scroll_changed");
		vScroll.Connect("gui_input", this, "_on_v_scroll_gui_input");
	
		hScroll.margin_right = -v_scroll.rect_size.x
		vScroll.margin_bottom = -h_scroll.rect_size.y
	
		AddLayerTo(content);
		SelectLayerAt(0);
	
		topBar.SetAnchorsAndMarginsPreset(PRESETTopWide);
		topBar.mouse_filter = MOUSEFilterIgnore;
		AddChild(topBar);
	
		gadget.mouse_filter = MOUSEFilterIgnore;
		topBar.AddChild(gadget);
	
		zoomMinus.flat = true;
		zoomMinus.hint_tooltip = "Zoom Out";
		zoomMinus.Connect("pressed", this, "_on_zoom_minus_pressed");
		zoomMinus.focus_mode = FOCUSNone;
		gadget.AddChild(zoomMinus);
	
		zoomReset.flat = true;
		zoomReset.hint_tooltip = "Zoom Reset";
		zoomReset.Connect("pressed", this, "_on_zoom_reset_pressed");
		zoomReset.focus_mode = FOCUSNone;
		gadget.AddChild(zoomReset);
	
		zoomPlus.flat = true;
		zoomPlus.hint_tooltip = "Zoom In";
		zoomPlus.Connect("pressed", this, "_on_zoom_plus_pressed");
		zoomPlus.focus_mode = FOCUSNone;
		gadget.AddChild(zoomPlus);
	
		snapButton.flat = true;
		snapButton.toggle_mode = true;
		snapButton.hint_tooltip = "Enable snap && show grid";
		snapButton.Connect("pressed", this, "_on_snap_button_pressed");
		snapButton.pressed = true;
		snapButton.focus_mode = FOCUSNone;
		gadget.AddChild(snapButton);
	
		snapAmount.value = snap;
		snapAmount.Connect("value_changed", this, "_on_snap_amount_value_changed");
		gadget.AddChild(snapAmount);
	
	}
	
	public void _OnHScrollGuiInput(__TYPE event)
	{  
		if(event is InputEventMouseButton)
		{
			var v = (hScroll.max_value - hScroll.min_value) * 0.01 ;// Scroll at 0.1% step
			switch( event.button_index)
			{
				case BUTTONWheelUp:
					hScroll.value -= v;
					break;
				case BUTTONWheelDown:
					hScroll.value += v;
	
					break;
			}
		}
	}
	
	public void _OnVScrollGuiInput(__TYPE event)
	{  
		if(event is InputEventMouseButton)
		{
			var v = (vScroll.max_value - vScroll.min_value) * 0.01 ;// Scroll at 0.1% step
			switch( event.button_index)
			{
				case BUTTONWheelUp:
					vScroll.value -= v ;// scroll left
					break;
				case BUTTONWheelDown:
					vScroll.value += v ;// scroll right
	
					break;
			}
		}
	}
	
	public void _OnHScrollChanged(__TYPE value)
	{  
		content.rect_position.x = -value
	
	}
	
	public void _OnVScrollChanged(__TYPE value)
	{  
		content.rect_position.y = -value
	
	}
	
	public void SetZoom(__TYPE v)
	{  
		zoom = v;
		content.rect_scale = Vector2.ONE * zoom;
	
	}
	
	public void _OnZoomMinusPressed()
	{  
		SetZoom(zoom - 0.1);
		Update();
	
	}
	
	public void _OnZoomResetPressed()
	{  
		SetZoom(1.0);
		Update();
	
	}
	
	public void _OnZoomPlusPressed()
	{  
		SetZoom(zoom + 0.1);
		Update();
	
	}
	
	public void _OnSnapButtonPressed()
	{  
		isSnapping = snapButton.pressed;
		Update();
	
	}
	
	public void _OnSnapAmountValueChanged(__TYPE value)
	{  
		snap = value;
		Update();
	
	}
	
	public void _Draw()
	{  
		// Update scrolls
		public __TYPE contentRect = GetScrollRect();
		content.rect_pivot_offset = GetScrollRect().size / 2.0 ;// Scale from center
		if(!get_rect().Encloses(contentRect))
		{
			public __TYPE hMin = contentRect.position.x;
			public __TYPE hMax = contentRect.size.x + contentRect.position.x - rectSize.x;
			public __TYPE vMin = contentRect.position.y;
			public __TYPE vMax = contentRect.size.y + contentRect.position.y - rectSize.y;
			if(hMin == hMax) // Otherwise scroll bar will complain no ratio
			{
				hMin -= 0.1;
				hMax += 0.1;
			}
			if(vMin == vMax) // Otherwise scroll bar will complain no ratio
			{
				vMin -= 0.1;
				vMax += 0.1;
			}
			hScroll.min_value = hMin;
			hScroll.max_value = hMax;
			hScroll.page = contentRect.size.x / 100;
			vScroll.min_value = vMin;
			vScroll.max_value = vMax;
			vScroll.page = contentRect.size.y / 100;
	
		// Draw selection box
		}
		if(!_is_dragging_node && !_is_connecting)
		{
			public __TYPE selectionBoxRect = GetSelectionBoxRect();
			DrawStyleBox(selectionStylebox, selectionBoxRect);
	
		// Draw grid
		// Refer GraphEdit(https://github.com/godotengine/godot/blob/6019dab0b45e1291e556e6d9e01b625b5076cc3c/scene/gui/graph_edit.cpp#L442)
		}
		if(isSnapping)
		{
			public __TYPE scrollOffset = new Vector2(hScroll.GetValue(), vScroll.GetValue());
			public __TYPE offset = scrollOffset / zoom;
			public __TYPE size = rectSize / zoom;
	
			public __TYPE from = (offset / (float)(snap)).Floor();
			public __TYPE l = (size / (float)(snap)).Floor() + new Vector2(1, 1);
	
			public __TYPE  gridMinor = gridMinorColor;
			public __TYPE  gridMajor = gridMajorColor;
	
			// for (int i = from.x; i < from.x + len.x; i++) {
			foreach(var i in GD.Range(from.x, from.x + l.x))
			{
				public __TYPE color;
	
				if(((int)(Mathf.Abs(i)) % 10 == 0))
				{
					color = gridMajor;
				}
				else
				{
					color = gridMinor;
	
				}
				public __TYPE baseOfs = i * snap * zoom - offset.x * zoom;
				DrawLine(new Vector2(baseOfs, 0), new Vector2(baseOfs, rectSize.y), color);
	
			// for (int i = from.y; i < from.y + len.y; i++) {
			}
			foreach(var i in GD.Range(from.y, from.y + l.y))
			{
				public __TYPE color;
	
				if(((int)(Mathf.Abs(i)) % 10 == 0))
				{
					color = gridMajor;
				}
				else
				{
					color = gridMinor;
	
				}
				public __TYPE baseOfs = i * snap * zoom - offset.y * zoom;
				DrawLine(new Vector2(0, baseOfs), new Vector2(rectSize.x, baseOfs), color);
	
		// Debug draw
		// for node in contentNodes.GetChildren():
		// 	var rect = GetTransform().Xform(content.GetTransform().Xform(node.GetRect()));
		// 	DrawStyleBox(selectionStylebox, rect)
	
		// var connectionList = GetConnectionList();
		// for i in connectionList.Size():
		// 	var connection = _connections[connectionList[i].from][connectionList[i].to];
		// 	# Line's offset along its down-vector
		// 	var lineLocalUpOffset = connection.line.rect_position - connection.line.GetTransform().Xform(Vector2.UP * connection.offset);
		// 	var fromPos = content.GetTransform().Xform(connection.GetFromPos() + lineLocalUpOffset);
		// 	var toPos = content.GetTransform().Xform(connection.GetToPos() + lineLocalUpOffset);
		// 	DrawLine(fromPos, toPos, Color.yellow)
	
			}
		}
	}
	
	public void _GuiInput(__TYPE event)
	{  
		if(event is InputEventKey)
		{
			switch( event.scancode)
			{
				case KEYDelete:
					if(event.pressed && canGuiDeleteNode)
					{
						// Delete nodes
						foreach(var node in _selection.Duplicate())
						{
							if(node is FlowChartLine)
							{
								// TODO: More efficient way to get connection from Line node
								foreach(var connectionsFrom in currentLayer._connections.Duplicate().Values())
								{
									foreach(var connection in connectionsFrom.Duplicate().Values())
									{
										if(connection.line == node)
										{
											DisconnectNode(currentLayer, connection.from_node.name, connection.to_node.name).QueueFree();
										}
									}
								}
							}
							else if(node is FlowChartNode)
							{
								RemoveNode(currentLayer, node.name);
								foreach(var connectionPair in currentLayer.GetConnectionList())
								{
									if(connectionPair.from == node.name || connectionPair.to == node.name)
									{
										DisconnectNode(currentLayer, connectionPair.from, connectionPair.to).QueueFree();
									}
								}
							}
						}
						AcceptEvent();
					}
					break;
				case KEYC:
					if(event.pressed && event.control)
					{
						// Copy node
						_copyingNodes = _selection.Duplicate();
						AcceptEvent();
					}
					break;
				case KEYD:
					if(event.pressed && event.control)
					{
						// Duplicate node directly from selection
						DuplicateNodes(currentLayer, _selection.Duplicate());
						AcceptEvent();
					}
					break;
				case KEYV:
					if(event.pressed && event.control)
					{
						// Paste node from _copyingNodes
						DuplicateNodes(currentLayer, _copyingNodes);
						AcceptEvent();
	
					}
					break;
			}
		}
		if(event is InputEventMouseMotion)
		{
			switch( event.button_mask)
			{
				case BUTTONMaskMiddle:
					// Panning
					hScroll.value -= event.relative.x;
					vScroll.value -= event.relative.y;
					Update();
					break;
				case BUTTONLeft:
					// Dragging
					if(_isDragging)
					{
						if(_isConnecting)
						{
							// Connecting
							if(_currentConnection)
							{
								var pos = ContentPosition(GetLocalMousePosition());
								Array clipRects = new Array(){_currentConnection.from_node.GetRect()};
								// Snapping connecting line
								foreach(var i in currentLayer.content_nodes.GetChildCount())
								{
									var child = currentLayer.content_nodes.GetChild(currentLayer.content_nodes.GetChildCount()-1 - i) ;// Inverse order to check from top to bottom of canvas
									if(child is FlowChartNode && child.name != _currentConnection.from_node.name)
									{
										if(_RequestConnectTo(currentLayer, child.name))
										{
											if(child.GetRect().HasPoint(pos))
											{
												pos = child.rect_position + child.rect_size / 2;
												clipRects.Append(child.GetRect());
												break;
											}
										}
									}
								}
								_currentConnection.line.Join(_currentConnection.GetFromPos(), pos, Vector2.ZERO, clipRects);
							}
						}
						else if(_isDraggingNode)
						{
							// Dragging nodes
							var dragged = ContentPosition(_dragEndPos) - ContentPosition(_dragStartPos);
							foreach(var i in _selection.Size())
							{
								var selected = _selection[i];
								if(!(selected is FlowChartNode))
								{
									continue;
								}
								selected.rect_position = (_dragOrigins[i] + selected.rect_size / 2.0 + dragged);
								selected.modulate.a = 0.3;
								if(isSnapping)
								{
									selected.rect_position = selected.rect_position.Snapped(Vector2.ONE * snap);
								}
								selected.rect_position -= selected.rect_size / 2.0 ;
								_OnNodeDragged(currentLayer, selected, dragged);
								EmitSignal("dragged", selected, dragged);
								// Update connection pos
								foreach(var from in currentLayer._connections)
								{
									var connectionsFrom = currentLayer._connections[from];
									foreach(var to in connectionsFrom)
									{
										if(from == selected.name || to == selected.name)
										{
											var connection = currentLayer._connections[from][to];
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
		if(event is InputEventMouseButton)
		{
			switch( event.button_index)
			{
				case BUTTONMiddle:
					// Reset zoom
					if(event.doubleclick)
					{
						SetZoom(1.0);
						Update();
					}
					break;
				case BUTTONWheelUp:
					// Zoom in
					SetZoom(zoom + 0.01);
					Update();
					break;
				case BUTTONWheelDown:
					// Zoom out
					SetZoom(zoom - 0.01);
					Update();
					break;
				case BUTTONLeft:
					// Hit detection
					var hitNode;
					foreach(var i in currentLayer.content_nodes.GetChildCount())
					{
						var child = currentLayer.content_nodes.GetChild(currentLayer.content_nodes.GetChildCount()-1 - i) ;// Inverse order to check from top to bottom of canvas
						if(child is FlowChartNode)
						{
							if(child.GetRect().HasPoint(ContentPosition(event.position)))
							{
								hitNode = child;
								break;
							}
						}
					}
					if(!hit_node)
					{
						// Test Line
						// Refer https://github.com/godotengine/godot/blob/master/editor/plugins/animation_state_machine_editor.cpp#L187
						int closest = -1;
						int closestD = 1e20
						var connectionList = GetConnectionList();
						foreach(var i in connectionList.Size())
						{
							var connection = currentLayer._connections[connectionList[i].from][connectionList[i].to];
							// Line's offset along its down-vector
							var lineLocalUpOffset = connection.line.rect_position - connection.line.GetTransform().Xform(Vector2.DOWN * connection.offset);
							var fromPos = connection.GetFromPos() + lineLocalUpOffset;
							var toPos = connection.GetToPos() + lineLocalUpOffset;
							var cp = Geometry.GetClosestPointToSegment2d(ContentPosition(event.position), fromPos, toPos);
							var d = cp.DistanceTo(ContentPosition(event.position));
							if(d > connection.line.rect_size.y * 2)
							{
								continue;
							}
							if(d < closestD)
							{
								closest = i;
								closestD = d;
							}
						}
						if(closest >= 0)
						{
							hitNode = currentLayer._connections[connectionList[closest].from][connectionList[closest].to].line;
	
						}
					}
					if(event.pressed)
					{
						if(!(hitNode in _selection) && !event.shift)
						{
							// Click on empty space
							ClearSelection();
						}
						if(hitNode)
						{
							// Click on Node(can be a line)
							_isDraggingNode = true;
							if(hitNode is FlowChartLine)
							{
								currentLayer.content_lines.MoveChild(hitNode, currentLayer.content_lines.GetChildCount()-1) ;// Raise selected line to top
								if(event.shift && canGuiConnectNode)
								{
									// Reconnection Start
									foreach(var from in currentLayer._connections.Keys())
									{
										var fromConnections = currentLayer._connections[from];
										foreach(var to in fromConnections.Keys())
										{
											var connection = fromConnections[to];
											if(connection.line == hitNode)
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
							if(hitNode is FlowChartNode)
							{
								currentLayer.content_nodes.MoveChild(hitNode, currentLayer.content_nodes.GetChildCount()-1) ;// Raise selected node to top
								if(event.shift && canGuiConnectNode)
								{
									// Connection start
									if(_RequestConnectFrom(currentLayer, hitNode.name))
									{
										_isConnecting = true;
										_isDraggingNode = false;
										var line = CreateLineInstance();
										var connection = Connection.new(line, hitNode, null)
										currentLayer._ConnectNode(connection);
										_currentConnection = connection;
										_currentConnection.line.Join(_currentConnection.GetFromPos(), ContentPosition(event.position));
									}
								}
								AcceptEvent();
							}
							if(_isConnecting)
							{
								ClearSelection();
							}
							else
							{
								if(canGuiSelectNode)
								{
									Select(hitNode);
								}
							}
						}
						if(!_is_dragging)
						{
							// Drag start
							_isDragging = true;
							_dragStartPos = event.position;
							_dragEndPos = event.position;
						}
					}
					else
					{
						var wasConnecting = _isConnecting;
						var wasDraggingNode = _isDraggingNode;
						if(_currentConnection)
						{
							// Connection end
							var from = _currentConnection.from_node.name;
							var to = hitNode ? hitNode.name : null
							if(hitNode is FlowChartNode && _RequestConnectTo(currentLayer, to) && from != to)
							{
								// Connection success
								var line;
								if(_currentConnection.to_node)
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
								if(_currentConnection.to_node)
								{
									// Reconnection
									_currentConnection.Join();
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
						if(_isDragging)
						{
							// Drag end
							_isDragging = false;
							_isDraggingNode = false;
							if(!(wasConnecting || wasDraggingNode) && canGuiSelectNode)
							{
								var selectionBoxRect = GetSelectionBoxRect();
								// Select node
								foreach(var node in currentLayer.content_nodes.GetChildren())
								{
									var rect = GetTransform().Xform(content.GetTransform().Xform(node.GetRect()));
									if(selectionBoxRect.Intersects(rect))
									{
										if(node is FlowChartNode)
										{
											Select(node);
								// Select line
										}
									}
								}
								var connectionList = GetConnectionList();
								foreach(var i in connectionList.Size())
								{
									var connection = currentLayer._connections[connectionList[i].from][connectionList[i].to];
									// Line's offset along its down-vector
									var lineLocalUpOffset = connection.line.rect_position - connection.line.GetTransform().Xform(Vector2.UP * connection.offset);
									var fromPos = content.GetTransform().Xform(connection.GetFromPos() + lineLocalUpOffset);
									var toPos = content.GetTransform().Xform(connection.GetToPos() + lineLocalUpOffset);
									if(CohenSutherland.LineIntersectRectangle(fromPos, toPos, selectionBoxRect))
									{
										Select(connection.line);
									}
								}
							}
							if(wasDraggingNode)
							{
								// Update _dragOrigins with new position after dragged
								foreach(var i in _selection.Size())
								{
									var selected = _selection[i];
									_dragOrigins[i] = selected.rect_position;
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
		var size = (_dragEndPos - _dragStartPos).Abs();
		return new Rect2(pos, size);
	
	// Get required scroll rect base on content
	}
	
	public __TYPE GetScrollRect(__TYPE layer=currentLayer)
	{  
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
		var node = layer.content_nodes.GetNodeOrNull(nodeName);
		if(node)
		{
			Deselect(node) ;// Must deselct before remove to make sure _dragOrigins synced with _selections
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
		if(line in _selection)
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
	
	public void ConnectNode(__TYPE layer, __TYPE from, __TYPE to, __TYPE line=null)
	{  
		if(!line)
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
		Deselect(line) ;// Since line is selectable as well
		_OnNodeDisconnected(layer, from, to);
		EmitSignal("disconnection", from, to);
		return line;
	
	// Clear all connections
	}
	
	public void ClearConnections(__TYPE layer=currentLayer)
	{  
		layer.ClearConnections();
	
	// Select a Node(can be a line)
	}
	
	public void Select(__TYPE node)
	{  
		if(node in _selection)
		{
			return;
	
		}
		_selection.Append(node);
		node.selected = true;
		_dragOrigins.Append(node.rect_position);
		EmitSignal("node_selected", node);
	
	// Deselect a node
	}
	
	public void Deselect(__TYPE node)
	{  
		_selection.Erase(node);
		if(IsInstanceValid(node))
		{
			node.selected = false;
		}
		_dragOrigins.PopBack();
		EmitSignal("node_deselected", node);
	
	// Clear all selection
	}
	
	public void ClearSelection()
	{  
		foreach(var node in _selection.Duplicate()) // duplicate _selection array as Deselect() edit array
		{
			if(!node)
			{
				continue;
			}
			Deselect(node);
		}
		_selection.Clear();
	
	// Duplicate given nodes in editor
	}
	
	public void DuplicateNodes(__TYPE layer, __TYPE nodes)
	{  
		ClearSelection();
		Array newNodes = new Array(){};
		foreach(var i in nodes.Size())
		{
			var node = nodes[i];
			if(!(node is FlowChartNode))
			{
				continue;
			}
			var newNode = node.Duplicate(DUPLICATESignals + DUPLICATEScripts);
			var offset = ContentPosition(GetLocalMousePosition()) - ContentPosition(_dragEndPos);
			newNode.rect_position = newNode.rect_position + offset;
			newNodes.Append(newNode)
			AddNode(layer, newNode);
			Select(newNode);
		// Duplicate connection within selection
		}
		foreach(var i in nodes.Size())
		{
			var fromNode = nodes[i];
			foreach(var connectionPair in GetConnectionList())
			{
				if(fromNode.name == connectionPair.from)
				{
					foreach(var j in nodes.Size())
					{
						var toNode = nodes[j];
						if(toNode.name == connectionPair.to)
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
	
	// Convert position in FlowChart space to Content(takes translation/scale of content into account)
	}
	
	public __TYPE ContentPosition(__TYPE pos)
	{  
		return (pos - content.rect_position - content.rect_pivot_offset * (Vector2.ONE - content.rect_scale)) * 1.0/content.rect_scale;
	
	// Return array of dictionary of connection as such [new Dictionary(){{"from1", "to1"}}, new Dictionary(){{"from2", "to2"}}]
	}
	
	public __TYPE GetConnectionList(__TYPE layer=currentLayer)
	{  
		return layer.GetConnectionList();
	
	
	}
	
	
	
}