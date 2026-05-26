namespace Game.Weapons;

public static class WeaponCatalog
{
    private static readonly WeaponDefinition[] Definitions =
    [
        new()
        {
            Id = WeaponId.Knife,
            DisplayName = "KNIFE",
            Kind = WeaponKind.Melee,
            Damage = 20f,
            CooldownSeconds = 0.4f,
            MaxRangeTiles = 1f,
            AmmoPerShot = 0,
            FireSoundPath = "",
            Sprite = new WeaponSpriteSpec
            {
                OriginX = PlayerWeaponSpriteLayout.KnifeOriginX,
                OriginY = PlayerWeaponSpriteLayout.KnifeRowY,
                FrameCount = PlayerWeaponSpriteLayout.FireFrameCount,
            },
        },
        new()
        {
            Id = WeaponId.Pistol,
            DisplayName = "PISTOL",
            Kind = WeaponKind.Hitscan,
            Damage = 15f,
            CooldownSeconds = 0.35f,
            MaxRangeTiles = 48f,
            AmmoPerShot = 1,
            FireSoundPath = "resources/PistolFire.ogg",
            Sprite = new WeaponSpriteSpec
            {
                OriginX = PlayerWeaponSpriteLayout.PistolOriginX,
                OriginY = PlayerWeaponSpriteLayout.PistolRowY,
                FrameCount = PlayerWeaponSpriteLayout.FireFrameCount,
            },
        },
        new()
        {
            Id = WeaponId.MachineGun,
            DisplayName = "MACHINE GUN",
            Kind = WeaponKind.Hitscan,
            Damage = 8f,
            CooldownSeconds = 0.12f,
            MaxRangeTiles = 48f,
            AmmoPerShot = 1,
            HoldToFire = true,
            FireSoundPath = "resources/SmgFire.ogg",
            Sprite = new WeaponSpriteSpec
            {
                OriginX = PlayerWeaponSpriteLayout.MachineGunOriginX,
                OriginY = PlayerWeaponSpriteLayout.MachineGunRowY,
                FrameCount = PlayerWeaponSpriteLayout.FireFrameCount,
            },
        },
        new()
        {
            Id = WeaponId.ChainGun,
            DisplayName = "CHAIN GUN",
            Kind = WeaponKind.Hitscan,
            Damage = 10f,
            CooldownSeconds = 0.075f,
            MaxRangeTiles = 48f,
            AmmoPerShot = 1,
            HoldToFire = true,
            LoopFireAnimation = true,
            FireAnimationSpeed = 2f,
            FireSoundPath = "resources/ChaingunFire.ogg",
            Sprite = new WeaponSpriteSpec
            {
                OriginX = PlayerWeaponSpriteLayout.ChainGunOriginX,
                OriginY = PlayerWeaponSpriteLayout.ChainGunRowY,
                FrameCount = PlayerWeaponSpriteLayout.FireFrameCount,
            },
        },
    ];

    /// <summary>Weapon bound to number keys 1–4.</summary>
    public static readonly WeaponId?[] SlotWeapons =
    [
        WeaponId.Knife,
        WeaponId.Pistol,
        WeaponId.MachineGun,
        WeaponId.ChainGun,
    ];

    public static WeaponDefinition Get(WeaponId id) =>
        Definitions.First(d => d.Id == id);

    public static WeaponId? GetWeaponForSlot(int slot)
    {
        if (slot < 1 || slot > SlotWeapons.Length)
            return null;
        return SlotWeapons[slot - 1];
    }
}

/// <summary>Sprite sheet layout for <c>weapons2.png</c> (64×64 cells, 1px column gap).</summary>
public static class PlayerWeaponSpriteLayout
{
    /// <summary>Size of each frame in the sprite sheet.</summary>
    public const int FrameSize = 64;

    /// <summary>Horizontal gap between frame columns.</summary>
    public const int ColumnGap = 1;

    /// <summary>Horizontal stride between frame columns.</summary>
    public const int FrameStride = FrameSize + ColumnGap;

    /// <summary>Origin X coordinate for the knife viewmodel.</summary>
    public const int KnifeOriginX = 1;
    
    /// <summary>Origin Y coordinate for the knife viewmodel.</summary>
    public const int KnifeRowY = 16;

    /// <summary>Origin X coordinate for the pistol viewmodel.</summary>
    public const int PistolOriginX = 1;

    /// <summary>Origin Y coordinate for the pistol viewmodel.</summary>
    public const int PistolRowY = 96;

    /// <summary>Origin X coordinate for the machine gun viewmodel.</summary>   
    public const int MachineGunOriginX = 1;

    /// <summary>Origin Y coordinate for the machine gun viewmodel.</summary>
    public const int MachineGunRowY = 176;

    /// <summary>Origin X coordinate for the chain gun viewmodel.</summary>
    public const int ChainGunOriginX = 1;

    /// <summary>Origin Y coordinate for the chain gun viewmodel.</summary>
    public const int ChainGunRowY = 256;

    /// <summary>Fire animation length (idle + fire frames) for all viewmodels on the sheet.</summary>
    public const int FireFrameCount = 5;

    /// <summary>Alias for <see cref="FireFrameCount"/>.</summary>
    public const int PistolFrameCount = FireFrameCount;

    /// <summary>Scale factor for the screen overlay.</summary>
    public const float ScreenOverlayScale = 1f;
}
