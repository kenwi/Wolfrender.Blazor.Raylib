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

    public float MaxHealth { get; set; } = 100f;
    public float Health { get; set; } = 100f;
    public bool IsAlive => Health > 0f;
    public float PistolDamage { get; set; } = 15f;
    public float PistolCooldownSeconds { get; set; } = 0.35f;
    public float WeaponCooldownRemaining { get; set; }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
            return;
        Health = MathF.Max(0f, Health - amount);
    }
}
