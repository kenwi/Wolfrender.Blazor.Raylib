namespace Game.Weapons;

/// <summary>Viewmodel sprite row on <c>weapons2.png</c> (64×64 cells, 1px column gap).</summary>
public sealed class WeaponSpriteSpec
{
    public required int OriginX { get; init; }
    public required int OriginY { get; init; }
    public required int FrameCount { get; init; }
    public float FrameDurationSeconds { get; init; } = 0.07f;
}
