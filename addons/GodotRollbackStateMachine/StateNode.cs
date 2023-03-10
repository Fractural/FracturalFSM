using Fractural.GodotCodeGenerator.Attributes;
using Godot;

[Tool]
public class StateNode : GraphNode
{
    public override void _Ready()
    {
        GD.Print("Readied state node");
        SetSlot(0, true, 1, Colors.White, true, 1, Colors.White);
    }
}