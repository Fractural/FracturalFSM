using System.Linq;
using Fractural.Utils;
using GDC = Godot.Collections;

namespace Fractural.StateMachine
{
    public class InspectorRemoteStateMachinePlayer : InspectorRemoteStackPlayer
    {
        public override string Previous => base.Previous ?? "";
        public override string Current => base.Current ?? "";
        public GDC.Dictionary LocalParameters => Source.GetRemote<GDC.Dictionary>("localParameters");
        public GDC.Dictionary Parameters => Source.GetRemote<GDC.Dictionary>("parameters");
        public StateMachine StateMachine => Source.GetRemote<StateMachine>(nameof(StateMachinePlayer.StateMachineResource));

        public InspectorRemoteStateMachinePlayer() { }
        public InspectorRemoteStateMachinePlayer(Godot.Object source) : base(source) { }
    }
}