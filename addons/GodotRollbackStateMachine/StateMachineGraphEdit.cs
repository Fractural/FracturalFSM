using Godot;

[Tool]
public class StateMachineGraphEdit : GraphEdit
{
    public override void _Ready()
    {
        Connect("connection_request", this, nameof(OnConnectionRequest));
    }

    private void OnConnectionRequest(string from, int fromSlot, string to, int toSlot)
    {
        ConnectNode(from, fromSlot, to, toSlot);
    }
}
