namespace Game.Weapons;

public sealed class WeaponDefinition
{
    public required WeaponId Id { get; init; }
    public required string DisplayName { get; init; }
    public required WeaponKind Kind { get; init; }
    public required float Damage { get; init; }
    public required float CooldownSeconds { get; init; }
    public required float MaxRangeTiles { get; init; }
    public required int AmmoPerShot { get; init; }
    public required string FireSoundPath { get; init; }
    public required WeaponSpriteSpec Sprite { get; init; }

    public bool UsesAmmo => AmmoPerShot > 0;
}
