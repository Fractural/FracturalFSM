
using System;
using Godot;
using Dictionary = Godot.Collections.Dictionary;
using Array = Godot.Collections.Array;

[Tool]
public class FlowChartLayer : Control
{
	 
	public const var FlowChartNode = GD.Load("res://addons/imjp94.yafsm/scenes/flowchart/FlowChartNode.gd");
	
	public __TYPE contentLines = new Control() // Node that hold all flowchart lines
	public __TYPE contentNodes = new Control() // Node that hold all flowchart nodes
	
	private __TYPE _connections = new Dictionary(){};
	
	public void _Init()
	{  
		name = "FlowChartLayer";
		mouseFilter = MOUSEFilterIgnore;
	
		contentLines.name = "content_lines";
		contentLines.mouse_filter = MOUSEFilterIgnore;
		AddChild(contentLines);
		MoveChild(contentLines, 0) ;// Make sure contentLines always behind nodes
	
		contentNodes.name = "content_nodes";
		contentNodes.mouse_filter = MOUSEFilterIgnore;
		AddChild(contentNodes);
	
	}
	
	public void HideContent()
	{  
		contentNodes.Hide();
		contentLines.Hide();
	
	}
	
	public void ShowContent()
	{  
		contentNodes.Show();
		contentLines.Show();
	
	// Get required scroll rect base on content
	}
	
	public __TYPE GetScrollRect(int scrollMargin=0)
	{  
		Rect2 rect = new Rect2();
		foreach(var child in contentNodes.GetChildren())
		{
			var childRect = child.GetRect();
			rect = rect.Merge(childRect);
		}
		return rect.Grow(scrollMargin);
	
	// Add node
	}
	
	public void AddNode(__TYPE node)
	{  
		contentNodes.AddChild(node);
	
	// Remove node
	}
	
	public void RemoveNode(__TYPE node)
	{  
		if(node)
		{
			contentNodes.RemoveChild(node);
	
	// Called after connection established
		}
	}
	
	public void _ConnectNode(__TYPE connection)
	{  
		contentLines.AddChild(connection.line);
		connection.Join();
	
	// Called after connection broken
	}
	
	public __TYPE _DisconnectNode(__TYPE connection)
	{  
		contentLines.RemoveChild(connection.line);
		return connection.line;
	
	// Rename node
	}
	
	public void RenameNode(__TYPE old, new)
	{  
		foreach(var from in _connections.Keys())
		{
			if(from == old) // Connection from
			{
				var fromConnections = _connections[from];
				_connections.Erase(old);
				_connections[new] = fromConnections;
			}
			else // Connection to
			{
				foreach(var to in _connections[from].Keys())
				{
					if(to == old)
					{
						var fromConnection = _connections[from];
						var value = fromConnection[old];
						fromConnection.Erase(old);
						fromConnection[new] = value;
	
	// Connect two nodes with a line
					}
				}
			}
		}
	}
	
	public void ConnectNode(__TYPE line, __TYPE from, __TYPE to, int interconnectionOffset=0)
	{  
		if(from == to)
		{
			return; // Connect to this
		}
		var connectionsFrom = _connections.Get(from);
		if(connectionsFrom)
		{
			if(to in connectionsFrom)
			{
				return; // Connection existed
			}
		}
		var connection = Connection.new(line, contentNodes.GetNode(from), contentNodes.GetNode(to))
		if(!connections_from)
		{
			connectionsFrom = new Dictionary(){};
			_connections[from] = connectionsFrom;
		}
		connectionsFrom[to] = connection;
		_ConnectNode(connection);
	
		// Check if connection in both ways
		connectionsFrom = _connections.Get(to);
		if(connectionsFrom)
		{
			var invConnection = connectionsFrom.Get(from);
			if(invConnection)
			{
				connection.offset = interconnectionOffset;
				invConnection.offset = interconnectionOffset;
				connection.Join();
				invConnection.Join();
	
	// Break a connection between two node
			}
		}
	}
	
	public __TYPE DisconnectNode(__TYPE from, __TYPE to)
	{  
		var connectionsFrom = _connections.Get(from);
		var connection = connectionsFrom.Get(to);
		if(!connection)
		{
			return;
	
		}
		_DisconnectNode(connection);
		if(connectionsFrom.Size() == 1)
		{
			_connections.Erase(from);
		}
		else
		{
			connectionsFrom.Erase(to);
	
		}
		connectionsFrom = _connections.Get(to);
		if(connectionsFrom)
		{
			var invConnection = connectionsFrom.Get(from);
			if(invConnection)
			{
				invConnection.offset = 0;
				invConnection.Join();
			}
		}
		return connection.line;
	
	// Clear all selection
	}
	
	public void ClearConnections()
	{  
		foreach(var connectionsFrom in _connections.Values())
		{
			foreach(var connection in connectionsFrom.Values())
			{
				connection.line.QueueFree();
			}
		}
		_connections.Clear();
				
	// Return array of dictionary of connection as such [new Dictionary(){{"from1", "to1"}}, new Dictionary(){{"from2", "to2"}}]
	}
	
	public __TYPE GetConnectionList()
	{  
		Array connectionList = new Array(){};
		foreach(var connectionsFrom in _connections.Values())
		{
			foreach(var connection in connectionsFrom.Values())
			{
				connectionList.Append(new Dictionary(){{"from", connection.from_node.name}, {"to", connection.to_node.name}});
			}
		}
		return connectionList;
	
	}
	
	public class Connection:
		var line // Control node that draw line
		public __TYPE fromNode;
		public __TYPE toNode;
		public __TYPE offset = 0 ;// line's y offset to make space for two interconnecting lines
	
		public void _Init(__TYPE pLine, __TYPE pFromNode, __TYPE pToNode)
		{	  
			line = pLine;
			fromNode = pFromNode;
			toNode = pToNode;
	
		// Update line position
		}
	
		public void Join()
		{	  
			line.Join(GetFromPos(), GetToPos(), offset, new Array(){fromNode ? fromNode.GetRect() : new Rect2(), toNode ? toNode.GetRect() : new Rect2()});
	
		// Return start position of line
		}
	
		public __TYPE GetFromPos()
		{	  
			return fromNode.rect_position + fromNode.rect_size / 2;
	
		// Return destination position of line
		}
	
		public __TYPE GetToPos()
		{	  
			return toNode ? toNode.rect_position + toNode.rect_size / 2 : line.rect_position
	
	
		}
	
	
	
}