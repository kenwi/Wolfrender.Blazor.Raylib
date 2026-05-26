namespace Game.Weapons;

/// <summary>Viewmodel sprite row on <c>weapons2.png</c>; stride from <see cref="PlayerWeaponSpriteLayout"/>.</summary>
public sealed class WeaponSpriteSpec
{
    public required int OriginX { get; init; }
    public required int OriginY { get; init; }
    public required int FrameCount { get; init; }
    public float FrameDurationSeconds { get; init; } = 0.07f;
}
