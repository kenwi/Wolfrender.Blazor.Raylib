using Game.Features.Doors;

namespace Game.Features.SoundPropagation;

/// <summary>
/// Tile-based BFS sound reach through walkable floor cells. Walls and closed doors
/// bound propagation; open, opening, and closing doors allow sound into adjacent rooms.
/// </summary>
public static class SoundPropagation
{
    private static readonly (int dx, int dy)[] CardinalNeighbors =
    {
        (0, -1),
        (1, 0),
        (0, 1),
        (-1, 0),
    };

    /// <summary>
    /// Returns every tile sound reaches from the origin (including the origin when valid).
    /// </summary>
    /// <param name="treatAllDoorsClosed">
    /// When true, every door tile is treated as closed regardless of runtime <see cref="Door.DoorState"/>.
    /// Use in the editor when not simulating so stale open doors do not leak sound.
    /// </param>
    public static HashSet<(int X, int Y)> ComputeReach(
        MapData map,
        IReadOnlyList<Door> doors,
        int originX,
        int originY,
        bool treatAllDoorsClosed = false)
    {
        var reached = new HashSet<(int X, int Y)>();

        if (!IsPropagationCell(map, doors, originX, originY, treatAllDoorsClosed))
            return reached;

        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((originX, originY));
        reached.Add((originX, originY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in CardinalNeighbors)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (reached.Contains((nx, ny)))
                    continue;

                if (!IsPropagationCell(map, doors, nx, ny, treatAllDoorsClosed))
                    continue;

                reached.Add((nx, ny));
                queue.Enqueue((nx, ny));
            }
        }

        return reached;
    }

    /// <summary>True when the door fully blocks sound (only <see cref="DoorState.CLOSED"/>).</summary>
    public static bool IsDoorBlockingSound(Door door, bool treatAllDoorsClosed) =>
        treatAllDoorsClosed || door.DoorState == DoorState.CLOSED;

    private static bool IsPropagationCell(
        MapData map, IReadOnlyList<Door> doors, int tileX, int tileY, bool treatAllDoorsClosed)
    {
        if (tileX < 0 || tileX >= map.Width || tileY < 0 || tileY >= map.Height)
            return false;

        if (map.GetTile(map.Walls, tileX, tileY) > 0)
            return false;

        if (IsClosedDoorAtTile(doors, tileX, tileY, treatAllDoorsClosed))
            return false;

        uint objectId = map.GetTile(map.Objects, tileX, tileY);
        if (ObjectSprites.BlocksMovement(objectId))
            return false;

        return true;
    }

    private static bool IsClosedDoorAtTile(
        IReadOnlyList<Door> doors, int tileX, int tileY, bool treatAllDoorsClosed)
    {
        foreach (var door in doors)
        {
            int doorTileX = (int)MathF.Round(door.StartPosition.X);
            int doorTileY = (int)MathF.Round(door.StartPosition.Y);
            if (doorTileX != tileX || doorTileY != tileY)
                continue;

            if (IsDoorBlockingSound(door, treatAllDoorsClosed))
                return true;
        }

        return false;
    }
}
