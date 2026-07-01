using System.Numerics;
using Game.Features.Combat;
using Raylib_cs;

namespace Game.Features.Players;

public class Player
{
    public Camera3D Camera { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 OldPosition { get; set; }
    public Vector3 Velocity { get; set; }

    public float CollisionRadius { get; set; } = 0.8f;
    public float MoveSpeed { get; set; } = 10.0f;
    public float MoveAcceleration { get; set; } = 60f;
    public float MoveDeceleration { get; set; } = 60f;

    /// <summary>Free-flight debug mode: no collision, Shift/Ctrl for vertical movement.</summary>
    public bool IsFlying { get; set; }

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

    /// <summary>Console / legacy pickup compatibility.</summary>
    public bool HasChainGun
    {
        get => Weapons.IsOwned(WeaponId.ChainGun);
        set
        {
            if (value)
                Weapons.Grant(WeaponId.ChainGun);
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
