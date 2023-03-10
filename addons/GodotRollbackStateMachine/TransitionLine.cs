using Fractural.GodotCodeGenerator.Attributes;
using Godot;
using Fractural.Utils;

public partial class TransitionLine : Control
{
    [OnReadyGet(OrNull = true)]
    public Control Source { get; set; }
    [OnReadyGet(OrNull = true)]
    public Control Destination { get; set; }

    private float Scale => Source.RectScale.x;

    private Label transitionLabel;
    private Control detailsContainer;
    private Control arrowDetailsContainer;
    private TextureRect arrow;

    private Vector2 sourcePrevPos;
    private Vector2 destPrevPos;

    [OnReady]
    public void RealReady()
    {
        RectPosition = Vector2.Zero;

        arrow = new TextureRect();
        transitionLabel = new Label();
        detailsContainer = new Control();

        detailsContainer.AddChild(transitionLabel);
        arrowDetailsContainer = new Control();
        arrowDetailsContainer.AddChild(arrow);
        arrowDetailsContainer.AddChild(detailsContainer);

        arrow.Texture = this.GetThemeFromAncestor().GetIcon("GuiScrollArrowRightHl", "EditorIcons");
        arrow.RectSize = arrow.Texture.GetSize();
        arrow.RectScale = Vector2.One * 2f;
        arrow.SetScaledCenter(Vector2.Zero);

        detailsContainer.RectPosition = Vector2.Zero;

        transitionLabel.Text = "transition text";
        transitionLabel.Align = Label.AlignEnum.Center;
        transitionLabel.Valign = Label.VAlign.Bottom;
        transitionLabel.RectSize = Vector2.One;
        transitionLabel.SetAnchorsAndMarginsPreset(LayoutPreset.CenterBottom);
        transitionLabel.SetScaledCenter(new Vector2(0, -40));
        transitionLabel.GrowHorizontal = GrowDirection.Both;
        transitionLabel.GrowVertical = GrowDirection.Begin;

        AddChild(arrowDetailsContainer);
    }

    public override void _Process(float delta)
    {
        if (Source.RectPosition != sourcePrevPos || Destination.RectPosition != destPrevPos)
        {
            sourcePrevPos = Source.RectPosition;
            destPrevPos = Destination.RectPosition;
            Update();
        }
    }

    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == (int)ButtonList.Left)
        {
            GD.Print(mouseEvent.GlobalPosition);
            var distSquared = mouseEvent.GlobalPosition.DistanceToLineSegmentSquared(Source.RectGlobalPosition, Destination.RectGlobalPosition);
            if (distSquared < 20f * Scale)
            {
                GD.Print("ClICKKED! at dist " + Mathf.Sqrt(distSquared));
                AcceptEvent();
            }
        }
    }

    public override void _Draw()
    {
        base._Draw();
        var color = Colors.White;
        color.a = 0.5f;

        Vector2 start = Source.GetScaledRect().GetCenter();
        Vector2 end = Destination.GetScaledRect().GetCenter();

        float padding = 10f;
        var rect = RectUtils.FromStartEnd(start, end).AddPadding(padding);
        RectPosition = rect.Position;
        RectSize = rect.Size;

        var parentToLocal = GetTransform().AffineInverse();
        var relativeStart = parentToLocal * start;
        var relativeEnd = parentToLocal * end;
        DrawLine(relativeStart, relativeEnd, color, 5 * Scale, true);

        rect.Position = Vector2.Zero;
        DrawRect(rect, Colors.Red);

        Vector2 direction = relativeEnd - relativeStart;
        float angle = Mathf.Atan2(direction.y, direction.x);

        Vector2 midPoint = (relativeStart + relativeEnd) / 2f;

        arrowDetailsContainer.RectScale = Vector2.One * Scale;
        arrowDetailsContainer.SetRotation(angle);
        arrowDetailsContainer.RectPosition = midPoint;

        if (Mathf.Abs(angle) > Mathf.Deg2Rad(90))
        {
            detailsContainer.RectRotation = 180;
        }
        else
        {
            detailsContainer.RectRotation = 0;
        }
    }
}
