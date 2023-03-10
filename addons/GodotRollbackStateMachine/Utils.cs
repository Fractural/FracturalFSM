using Godot;

public static class Utils
{
    // NOTE: A scaled rect is a rect whose size is scaled by the Control item's RectScale
    //       Note that the position remains unscaled.

    public static Rect2 GetGlobalScaledRect(this Control item)
    {
        Rect2 rect = item.GetGlobalRect();
        rect.Size *= item.GetGlobalTransform().Scale;
        return rect;
    }

    public static Rect2 GetScaledRect(this Control item)
    {
        Rect2 rect = item.GetRect();
        rect.Size *= item.RectScale;
        return rect;
    }

    public static void SetScaledPosition(this Control item, Vector2 position)
    {
        var scaledRect = item.GetScaledRect();
        scaledRect.Position = position;
        item.SetScaledRect(scaledRect);
    }

    public static void SetScaledCenter(this Control item, Vector2 center)
    {
        var scaledRect = item.GetScaledRect();
        scaledRect.Position = center - (scaledRect.Size / 2f);
        item.SetScaledRect(scaledRect);
    }

    public static void SetScaledRect(this Control item, Rect2 rect)
    {
        item.RectSize = rect.Size / item.RectScale;
        item.RectPosition = rect.Position;
    }

    public static void SetCenter(this Control item, Vector2 center)
    {
        GD.Print("Setting center, rect size " + item.RectSize);
        item.RectPosition = center - (item.RectSize / 2f);
    }
}
