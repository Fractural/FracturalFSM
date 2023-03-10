using Fractural.GodotCodeGenerator.Attributes;
using Godot;

[Tool]
public class StateNode : GraphNode
{
    public override void _Ready()
    {
        GD.Print("Readied state node");
    }
}