using Fractural.GodotCodeGenerator.Attributes;
using Godot;

public partial class TransitionNode : Control
{
    [OnReadyGet]
    private StateNode source;
    [OnReadyGet]
    private StateNode destination;

    private Vector2 sourcePrevPos;

    private Vector2 destPrevPos;

    [OnReady]
    public void RealReady()
    {
        GD.Print("Ready");
    }

    public override void _Process(float delta)
    {
        if (source.RectPosition != sourcePrevPos || destination.RectPosition != destPrevPos)
        {
            sourcePrevPos = source.RectPosition;
            destPrevPos = destination.RectPosition;
            Update();
        }
    }
    public override void _Draw()
    {
        base._Draw();
        var color = Colors.White;
        color.a = 0.5f;
        DrawLine(source.GetScaledRect().GetCenter(), destination.GetScaledRect().GetCenter(), color, 5 * source.RectScale.x, true);
    }
}

public static class Utils
{
    public static Rect2 GetScaledRect(this Control item)
    {
        Rect2 rect = item.GetRect();
        rect.Size *= item.RectScale;
        return rect;
    }
}
