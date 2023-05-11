using GDC = Godot.Collections;

namespace Fractural.StateMachine
{
    /// <summary>
    /// Interface for states.
    /// States should also declare signals whicah can then be used as transitions.
    /// </summary>
    public interface IState
    {
        void StateEntered(GDC.Dictionary args);
        void StateExited();
    }
}