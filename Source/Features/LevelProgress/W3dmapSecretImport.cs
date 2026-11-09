using Game.Core.Level;

namespace Game.Features.LevelProgress;

/// <summary>
/// Decodes Wolfenstein 3D plane-1 pushwall markers (thing 98) into
/// <see cref="SecretWallPlacement"/> records. Slide direction is inferred from
/// map clearance: Wolf secrets always travel two tiles, so a direction needs at
/// least two free cells. When both sides look open, the sealed two-tile pocket
/// is the slide direction (the three-plus side is the approach room). Things and
/// pickups count as blocked.
/// </summary>
public static class W3dmapSecretImport
{
    public const ushort PushwallThing = 98;

    /// <summary>Wolf pushwalls always travel two tiles when geometry allows.</summary>
    public const int TravelTiles = 2;

    public const int ProbeDepth = 3;

    /// <summary>
    /// Append plane-1 pushwalls onto <paramref name="mapData"/>.SecretWalls.
    /// Skips markers with no two-tile clearance. Does not clear existing entries;
    /// call after a fresh map reset.
    /// </summary>
    public static void ApplyPlane1(
        MapData mapData,
        ReadOnlySpan<ushort> things,
        ReadOnlySpan<ushort> walls)
    {
        int side = mapData.Width;
        if (side <= 0 || mapData.Height != side)
            throw new InvalidOperationException("W3DMAP plane 1 requires a square map matching plane 0.");
        if (things.Length != side * side || walls.Length != side * side)
            throw new InvalidOperationException("W3DMAP plane 1 size must match plane 0.");

        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                if (things[y * side + x] != PushwallThing)
                    continue;
                if (!TryInferDirection(x, y, walls, things, side, out var direction))
                    continue;

                mapData.SecretWalls.Add(new SecretWallPlacement
                {
                    TileX = x,
                    TileY = y,
                    Direction = direction,
                    TravelTiles = TravelTiles
                });
            }
        }
    }

    public static bool IsPushwallThing(ushort thing) => thing == PushwallThing;

    /// <summary>
    /// Infer slide direction from clearance. Returns false when no two-tile path exists
    /// or when multiple equally valid directions remain after the 3-tile tie-break.
    /// </summary>
    public static bool TryInferDirection(
        int tileX,
        int tileY,
        ReadOnlySpan<ushort> walls,
        ReadOnlySpan<ushort> things,
        int side,
        out SecretWallDirection direction)
    {
        direction = SecretWallDirection.North;

        var clearances = new (SecretWallDirection Direction, int Clearance)[]
        {
            (SecretWallDirection.East, CountClearance(tileX, tileY, 1, 0, walls, things, side)),
            (SecretWallDirection.West, CountClearance(tileX, tileY, -1, 0, walls, things, side)),
            (SecretWallDirection.South, CountClearance(tileX, tileY, 0, 1, walls, things, side)),
            (SecretWallDirection.North, CountClearance(tileX, tileY, 0, -1, walls, things, side)),
        };

        var withTwo = clearances.Where(c => c.Clearance >= TravelTiles).ToArray();
        if (withTwo.Length == 0)
            return false;
        if (withTwo.Length == 1)
        {
            direction = withTwo[0].Direction;
            return true;
        }

        // Ambiguous openings: the long (3+) side is the room you push from;
        // the sealed exactly-2 pocket behind the wall is the slide direction.
        var exactTwo = withTwo.Where(c => c.Clearance == TravelTiles).ToArray();
        if (exactTwo.Length == 1)
        {
            direction = exactTwo[0].Direction;
            return true;
        }

        return false;
    }

    private static int CountClearance(
        int tileX,
        int tileY,
        int dx,
        int dy,
        ReadOnlySpan<ushort> walls,
        ReadOnlySpan<ushort> things,
        int side)
    {
        int clearance = 0;
        for (int step = 1; step <= ProbeDepth; step++)
        {
            int x = tileX + dx * step;
            int y = tileY + dy * step;
            if (IsBlocked(x, y, walls, things, side))
                break;
            clearance++;
        }

        return clearance;
    }

    /// <summary>
    /// Solid walls, door tiles, and any plane-1 thing (enemies, pickups, markers)
    /// block pushwall travel. The pushwall marker itself is only on the secret cell.
    /// </summary>
    private static bool IsBlocked(
        int x,
        int y,
        ReadOnlySpan<ushort> walls,
        ReadOnlySpan<ushort> things,
        int side)
    {
        if (x < 0 || y < 0 || x >= side || y >= side)
            return true;

        int index = y * side + x;
        ushort wall = walls[index];
        if (wall is >= 1 and <= 63)
            return true;
        // Door / elevator codes on plane 0 are solid openings, not slide space.
        if (wall is >= 90 and <= 101)
            return true;
        return things[index] != 0;
    }
}
