using System.Numerics;
using Raylib_cs;

namespace Game.Entities;

public class Player
{
    public Camera3D Camera { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 OldPosition { get; set; }
    public Vector3 Velocity { get; set; }

    public float CollisionRadius { get; set; } = 0.8f;
    public float MoveSpeed { get; set; } = 5.0f;
}
