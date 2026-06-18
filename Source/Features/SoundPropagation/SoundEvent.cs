namespace Game.Features.SoundPropagation;

/// <summary>
/// A single propagated sound burst from a tile origin. Consumed by hearing AI (phase 3).
/// </summary>
public sealed class SoundEvent
{
    public required int OriginX { get; init; }
    public required int OriginY { get; init; }
    public required HashSet<(int X, int Y)> ReachedTiles { get; init; }
}
