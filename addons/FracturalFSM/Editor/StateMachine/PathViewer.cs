
using System;
using Godot;
using GDC = Godot.Collections;
using Fractural.Utils;

[Tool]
public class PathViewer : HBoxContainer
{
    [Signal] public delegate void DirPressed(string dir, int index);

    public PathViewer()
    {
        AddDir("root");
    }

    /// <summary>
    /// Select parent dir & return its path
    /// </summary>
    /// <returns></returns>
    public string Back()
    {
        return SelectDir(GetChild(Mathf.Max(GetChildCount() - 1 - 1, 0)).Name);
    }

    /// <summary>
    /// Select dir and return its path
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    public string SelectDir(string dir)
    {
        for (int i = 0; i < GetChildCount(); i++)
        {
            var child = GetChild(i);
            if (child.Name == dir)
            {
                RemoveDirUntil(i);
                return GetDirUntil(i);
            }
        }
        return null;
    }

    /// <summary>
    /// Add directory button
    /// </summary>
    /// <param name="dir"></param>
    /// <returns></returns>
    public Button AddDir(string dir)
    {
        var button = new Button();

        button.Name = dir;
        button.Flat = true;
        button.Text = dir;
        AddChild(button);
        button.Connect("pressed", this, nameof(OnButtonPressed), GDUtils.GDParams(button));
        return button;
    }

    /// <summary>
    /// Remove directory until Index(exclusive)
    /// </summary>
    /// <param name="index"></param>
    public void RemoveDirUntil(int index)
    {
        GDC.Array<Node> toRemove = new GDC.Array<Node>();
        for (int i = 0; i < GetChildCount(); i++)
        {
            if (index == GetChildCount() - 1 - i)
            {
                break;
            }
            var child = GetChild(GetChildCount() - 1 - i);
            toRemove.Add(child);
        }
        foreach (var n in toRemove)
        {
            RemoveChild(n);
            n.QueueFree();
        }
    }

    /// <summary>
    /// Return current working directory
    /// </summary>
    /// <returns></returns>
    public string GetCwd()
    {
        return GetDirUntil(GetChildCount() - 1);
    }

    /// <summary>
    /// Return path until Index(inclusive) of directory
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public string GetDirUntil(int index)
    {
        string path = "";
        for (int i = 0; i < GetChildCount(); i++)
        {
            if (i > index)
                break;

            var child = GetChild<Button>(i);
            if (i == 0)
                path = "root";
            else
                path = GD.Str(path, "/", child.Text);
        }
        return path;
    }

    private void OnButtonPressed(Button button)
    {
        var index = button.GetIndex();
        var dir = button.Name;
        EmitSignal(nameof(DirPressed), dir, index);
    }
}