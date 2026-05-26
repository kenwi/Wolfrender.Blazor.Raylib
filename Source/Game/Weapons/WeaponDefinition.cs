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

    /// <summary>When true, holding primary fire repeats shots at <see cref="CooldownSeconds"/> intervals.</summary>
    public bool HoldToFire { get; init; }

    /// <summary>While holding fire, cycle frames 1..N-1 continuously instead of a one-shot burst.</summary>
    public bool LoopFireAnimation { get; init; }

    /// <summary>Multiplier for fire viewmodel frame advance (2 = twice as fast).</summary>
    public float FireAnimationSpeed { get; init; } = 1f;

    public bool UsesAmmo => AmmoPerShot > 0;

    public float GetFireFrameDurationSeconds() =>
        Sprite.FrameDurationSeconds / FireAnimationSpeed;
}
