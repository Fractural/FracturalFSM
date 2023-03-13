
using System;
using Fractural.GodotCodeGenerator.Attributes;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace GodotRollbackNetcode.StateMachine
{

    [Tool]
    public partial class ParametersPanel : MarginContainer
    {
        [OnReadyGet("PanelContainer/MarginContainer/VBoxContainer/GridContainer")]
        public GridContainer grid;
        [OnReadyGet("PanelContainer/MarginContainer/VBoxContainer/MarginContainer/Button")]
        public Button button;

        [OnReady]
        public void RealReady()
        {
            button.Connect("pressed", this, nameof(_OnButtonPressed));
        }

        public void UpdateParams(GDC.Dictionary globalParams, GDC.Dictionary localParams)
        {
            // Remove erased parameters from param panel
            foreach (Node paramNode in grid.GetChildren())
            {
                if (!globalParams.Contains(paramNode.Name))
                    RemoveParam(paramNode.Name);
            }
            foreach (string param in globalParams.Keys)
            {
                var value = globalParams[param];
                if (value == null) // Ignore trigger
                    continue;

                SetParam(param, GD.Str(value));
            }

            // Remove erased local parameters from param panel
            foreach (Label param in grid.GetChildren())
                if (!localParams.Contains(param.Name) && !globalParams.Contains(param.Name))
                    RemoveParam(param.Name);

            foreach (string param in localParams.Keys)
            {
                var nestedParams = localParams.Get<GDC.Dictionary>(param);
                foreach (string nestedParam in nestedParams.Keys)
                {
                    var value = nestedParams[nestedParam];
                    if (value == null) // Ignore trigger
                        continue;

                    SetParam(GD.Str(param, "/", nestedParam), GD.Str(value));
                }
            }
        }

        public void SetParam(string param, string value)
        {
            var label = grid.GetNodeOrNull<Label>(param);
            if (label == null)
            {
                label = new Label();
                label.Name = param;
                grid.AddChild(label);
            }
            label.Text = $"{param} = {value}";
        }

        public void RemoveParam(string param)
        {
            var label = grid.GetNodeOrNull<Label>(param);
            if (label != null)
            {
                grid.RemoveChild(label);
                label.QueueFree();
                SetAnchorsPreset(LayoutPreset.BottomRight);
            }
        }

        public void ClearParams()
        {
            foreach (Control child in grid.GetChildren())
            {
                grid.RemoveChild(child);
                child.QueueFree();
            }
        }

        public void _OnButtonPressed()
        {
            grid.Visible = !grid.Visible;

            SetAnchorsPreset(LayoutPreset.BottomRight);
        }
    }
}