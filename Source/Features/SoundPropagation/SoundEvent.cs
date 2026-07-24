namespace Game.Features.SoundPropagation;

/// <summary>
/// A single propagated sound burst from a tile origin. Consumed by hearing AI.
/// </summary>
public sealed class SoundEvent
{
    public required int OriginX { get; init; }
    public required int OriginY { get; init; }

    /// <summary>
    /// Minimum closed doors crossed to reach each tile from the origin.
    /// Listeners compare this cost against their <c>MaxHearThroughClosedDoors</c> threshold.
    /// </summary>
    public required IReadOnlyDictionary<(int X, int Y), int> DoorCrossingsByTile { get; init; }

    public bool CanHearAt((int X, int Y) tile, int maxClosedDoorsCrossed) =>
        DoorCrossingsByTile.TryGetValue(tile, out int doors) && doors <= maxClosedDoorsCrossed;
}
