namespace Game.Features.SoundPropagation;

/// <summary>
/// Result of a gunshot (or editor test) flood-fill. Each reached tile stores the
/// minimum number of <see cref="Doors.DoorState.CLOSED"/> door tiles crossed from the origin.
/// </summary>
public sealed class SoundReach
{
    /// <summary>Minimum closed doors crossed to reach each tile (including the origin at 0).</summary>
    public required Dictionary<(int X, int Y), int> DoorCrossingsByTile { get; init; }

    public int TileCount => DoorCrossingsByTile.Count;

    public bool TryGetDoorCrossings((int X, int Y) tile, out int closedDoorsCrossed) =>
        DoorCrossingsByTile.TryGetValue(tile, out closedDoorsCrossed);

    /// <summary>True when the tile is reached and door cost is within the listener's threshold.</summary>
    public bool CanHearAt((int X, int Y) tile, int maxClosedDoorsCrossed) =>
        DoorCrossingsByTile.TryGetValue(tile, out int doors) && doors <= maxClosedDoorsCrossed;
}
