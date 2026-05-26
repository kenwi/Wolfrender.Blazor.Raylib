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
            FireSoundPath = "resources/PistolFire.ogg",
            Sprite = new WeaponSpriteSpec
            {
                OriginX = PlayerWeaponSpriteLayout.PistolOriginX,
                OriginY = PlayerWeaponSpriteLayout.PistolRowY,
                FrameCount = PlayerWeaponSpriteLayout.PistolFrameCount,
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
                FrameCount = PlayerWeaponSpriteLayout.PistolFrameCount,
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
            FireSoundPath = "resources/PistolFire.ogg",
            Sprite = new WeaponSpriteSpec
            {
                OriginX = PlayerWeaponSpriteLayout.PistolOriginX,
                OriginY = PlayerWeaponSpriteLayout.PistolRowY,
                FrameCount = PlayerWeaponSpriteLayout.PistolFrameCount,
            },
        },
    ];

    /// <summary>Weapon bound to number keys 1–4 (slot 4 reserved).</summary>
    public static readonly WeaponId?[] SlotWeapons =
    [
        WeaponId.Knife,
        WeaponId.Pistol,
        WeaponId.MachineGun,
        null,
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

/// <summary>Shared sheet constants (placeholders until <c>weapons.png</c> per-weapon coords).</summary>
public static class PlayerWeaponSpriteLayout
{
    public const int FrameSize = 64;
    public const int PistolRowY = 96;
    public const int PistolOriginX = 1;
    public const int PistolFrameCount = 5;
    public const float ScreenOverlayScale = 1f;
}
