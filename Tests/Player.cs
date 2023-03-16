using Godot;
using System;
using Fractural.StateMachine;
using Fractural.GodotCodeGenerator.Attributes;

namespace Tests.Manual
{
    public partial class Player : KinematicBody2D
    {
        [OnReadyGet]
        private StateMachinePlayer stateMachinePlayer;
        private Vector2 velocity;
        public Vector2 Velocity
        {
            get => velocity;
            set => velocity = value;
        }
        public Vector2 WalkDirection { get; set; }
        [Export]
        public float Speed { get; set; } = 500;
        [Export]
        public float Gravity { get; set; } = 100;
        [Export]
        public float Damping { get; set; } = 0.1f;

        private float lastJumpTime = 0;
        private int jumpCount = 0;

        [OnReady]
        private void RealReady()
        {
            stateMachinePlayer.Connect(nameof(StateMachinePlayer.Updated), this, nameof(OnStateUpdated));
            stateMachinePlayer.Connect(nameof(StateMachinePlayer.Transited), this, nameof(OnTransited));
        }

        private void OnStateUpdated(string state, float delta)
        {
            Velocity += Vector2.Down * Gravity * delta;
            switch (state)
            {
                case "Idle":
                    break;
                case "Walk":
                    Velocity += WalkDirection * Speed * delta;
                    break;
                case "Jump(n)":
                    Jump();
                    stateMachinePlayer.SetParam("jump_count", jumpCount);
                    break;
                case "Jump":
                    jumpCount = 0;
                    Jump();
                    stateMachinePlayer.SetParam("jump_count", jumpCount);
                    break;
                case "Fall":
                    stateMachinePlayer.SetParam("jump_elapsed", OS.GetSystemTimeMsecs() - lastJumpTime);
                    break;
            }
            velocity = MoveAndSlide(Velocity, Vector2.Up);
            velocity.x *= Mathf.Pow(1 - Damping, delta);
        }

        private void OnTransited(string from, string to)
        {
            GD.Print($"Transition({from}->{to})");
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (Input.IsActionJustPressed("ui_accept"))
                stateMachinePlayer.SetTrigger("space");
        }

        public override void _PhysicsProcess(float delta)
        {
            WalkDirection = Vector2.Zero;
            if (Input.IsActionPressed("ui_left"))
                WalkDirection += Vector2.Left;
            if (Input.IsActionPressed("ui_right"))
                WalkDirection += Vector2.Right;
            WalkDirection = WalkDirection.Normalized();

            stateMachinePlayer.SetParam("on_floor", IsOnFloor());
            stateMachinePlayer.SetParam("walk", WalkDirection.Length());
        }

        private void Jump()
        {
            Velocity += Vector2.Up * 10f;
            lastJumpTime = OS.GetSystemTimeMsecs();
            jumpCount++;
        }
    }
}