using Game.Features.Doors;
using Game.Features.WorldObjects;

namespace Game.Features.SoundPropagation;

/// <summary>
/// Tile-based BFS sound reach through walkable floor cells.
/// Open / opening / closing doors pass at no cost. Closed door tiles can be crossed with a
/// +1 door cost so listeners with a higher hearing threshold can hear into adjacent rooms.
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
    /// Flood-fill sound from the origin. Returns every reachable tile with the minimum
    /// number of closed door tiles crossed along the cheapest path.
    /// </summary>
    /// <param name="treatAllDoorsClosed">
    /// When true, every door tile is treated as closed regardless of runtime <see cref="Door.DoorState"/>.
    /// Use in the editor when not simulating so stale open doors do not leak sound for free.
    /// </param>
    /// <param name="maxClosedDoorCrossings">
    /// Paths that would exceed this many closed-door crossings are not expanded further.
    /// </param>
    public static SoundReach ComputeReach(
        MapData map,
        IReadOnlyList<Door> doors,
        int originX,
        int originY,
        bool treatAllDoorsClosed = false,
        int maxClosedDoorCrossings = 8)
    {
        var doorCrossings = new Dictionary<(int X, int Y), int>();

        // Origin must be a normal floor cell (not wall / blocking object / closed door).
        if (!IsOpenPropagationCell(map, doors, originX, originY, treatAllDoorsClosed))
            return new SoundReach { DoorCrossingsByTile = doorCrossings };

        // 0-1 BFS: stepping onto a closed door costs +1; everything else costs 0.
        var queue = new LinkedList<(int x, int y, int doors)>();
        queue.AddFirst((originX, originY, 0));
        doorCrossings[(originX, originY)] = 0;

        while (queue.Count > 0)
        {
            var (x, y, doorsCrossed) = queue.First!.Value;
            queue.RemoveFirst();

            // Stale queue entry after a cheaper path was found later.
            if (doorCrossings[(x, y)] < doorsCrossed)
                continue;

            foreach (var (dx, dy) in CardinalNeighbors)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (!IsTraversableForSound(map, nx, ny))
                    continue;

                bool neighborIsClosedDoor = IsClosedDoorAtTile(doors, nx, ny, treatAllDoorsClosed);
                int nextDoors = doorsCrossed + (neighborIsClosedDoor ? 1 : 0);
                if (nextDoors > maxClosedDoorCrossings)
                    continue;

                if (doorCrossings.TryGetValue((nx, ny), out int best) && best <= nextDoors)
                    continue;

                doorCrossings[(nx, ny)] = nextDoors;
                if (neighborIsClosedDoor)
                    queue.AddLast((nx, ny, nextDoors));
                else
                    queue.AddFirst((nx, ny, nextDoors));
            }
        }

        return new SoundReach { DoorCrossingsByTile = doorCrossings };
    }

    /// <summary>True when the door fully blocks free (0-cost) sound (only <see cref="DoorState.CLOSED"/>).</summary>
    public static bool IsDoorBlockingSound(Door door, bool treatAllDoorsClosed) =>
        treatAllDoorsClosed || door.DoorState == DoorState.CLOSED;

    /// <summary>Floor cell that sound can occupy without paying a door cost (not a closed door).</summary>
    private static bool IsOpenPropagationCell(
        MapData map, IReadOnlyList<Door> doors, int tileX, int tileY, bool treatAllDoorsClosed)
    {
        if (!IsTraversableForSound(map, tileX, tileY))
            return false;

        return !IsClosedDoorAtTile(doors, tileX, tileY, treatAllDoorsClosed);
    }

    /// <summary>Walls and movement-blocking objects stop sound; door tiles are traversable.</summary>
    private static bool IsTraversableForSound(MapData map, int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= map.Width || tileY < 0 || tileY >= map.Height)
            return false;

        if (map.GetTile(map.Walls, tileX, tileY) > 0)
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
