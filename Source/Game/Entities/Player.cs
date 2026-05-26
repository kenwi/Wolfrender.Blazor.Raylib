using System.Numerics;
using Game.Weapons;
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
    public float WeaponCooldownRemaining { get; set; }

    public int Ammo { get; set; }
    public bool HasGoldKey { get; set; }
    public bool HasSilverKey { get; set; }

    public PlayerWeaponInventory Weapons { get; } = new();

    /// <summary>Console / legacy pickup compatibility.</summary>
    public bool HasMachineGun
    {
        get => Weapons.IsOwned(WeaponId.MachineGun);
        set
        {
            if (value)
                Weapons.Grant(WeaponId.MachineGun);
        }
    }

    public void ResetInventory()
    {
        Ammo = 0;
        HasGoldKey = false;
        HasSilverKey = false;
        Weapons.Reset();
    }

    public void TakeDamage(float amount)
    {
        if (!IsAlive || amount <= 0f)
            return;
        Health = MathF.Max(0f, Health - amount);
        System.Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} Player health: {Health}, Took damage: {amount}");
    }
}
