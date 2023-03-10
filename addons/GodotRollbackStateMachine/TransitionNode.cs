using Fractural.GodotCodeGenerator.Attributes;
using Godot;
using Fractural.Utils;

public partial class TransitionNode : GraphNode
{
    [OnReadyGet]
    private StateNode source;
    [OnReadyGet]
    private StateNode destination;
    [OnReadyGet]
    private TransitionLine sourceLine;
    [OnReadyGet]
    private TransitionLine destinationLine;

    private Vector2 sourcePrevPos;
    private Vector2 destPrevPos;

    [OnReady]
    public void RealReady()
    {
        sourceLine.Source = source;
        sourceLine.Destination = this;
        destinationLine.Source = this;
        destinationLine.Destination = destination;

        CallDeferred(nameof(UnparentLines));
        Connect("dragged", this, nameof(OnDragged));
    }

    private void UnparentLines()
    {
        sourceLine.Reparent(GetParent());
        destinationLine.Reparent(GetParent());
        sourceLine.RectPosition = Vector2.Zero;
        destinationLine.RectPosition = Vector2.Zero;
    }

    public void OnDragged(Vector2 from, Vector2 to)
    {
        GD.Print("Dragged " + from + " " + to);
        RectPosition = from;
    }
}
