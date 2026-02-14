using System.Numerics;
using Game.Entities;

namespace Game.Utilities;

/// <summary>
/// A* pathfinding on a tile-based map. Operates on a rectangular slice
/// of the map to avoid solving the entire grid every time.
/// </summary>
public static class Pathfinding
{
    private static readonly (int dx, int dy, float cost)[] Neighbors =
    {
        ( 0, -1, 1.0f),    // N
        ( 1,  0, 1.0f),    // E
        ( 0,  1, 1.0f),    // S
        (-1,  0, 1.0f),    // W
        ( 1, -1, 1.414f),  // NE
        ( 1,  1, 1.414f),  // SE
        (-1,  1, 1.414f),  // SW
        (-1, -1, 1.414f),  // NW
    };

    /// <summary>
    /// Compute a padded bounding box (in tile coords) around start and end,
    /// clamped to the map boundaries.
    /// </summary>
    public static (int x, int y, int w, int h) ComputeSliceBounds(
        Vector2 startTile, Vector2 endTile,
        int mapWidth, int mapHeight,
        int padding = 10)
    {
        int minX = (int)MathF.Floor(MathF.Min(startTile.X, endTile.X)) - padding;
        int minY = (int)MathF.Floor(MathF.Min(startTile.Y, endTile.Y)) - padding;
        int maxX = (int)MathF.Ceiling(MathF.Max(startTile.X, endTile.X)) + padding;
        int maxY = (int)MathF.Ceiling(MathF.Max(startTile.Y, endTile.Y)) + padding;

        minX = Math.Max(0, minX);
        minY = Math.Max(0, minY);
        maxX = Math.Min(mapWidth - 1, maxX);
        maxY = Math.Min(mapHeight - 1, maxY);

        return (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>
    /// Find a path from startTile to endTile using A* on a rectangular slice of the map.
    /// All coordinates are in absolute tile space.
    /// Returns a list of tile positions forming the path (including start and end), or null if no path exists.
    /// </summary>
    public static List<Vector2>? FindPath(
        MapData mapData,
        List<Door> doors,
        int sliceX, int sliceY,
        int sliceWidth, int sliceHeight,
        Vector2 startTile, Vector2 endTile)
    {
        int startX = (int)MathF.Floor(startTile.X);
        int startY = (int)MathF.Floor(startTile.Y);
        int endX = (int)MathF.Floor(endTile.X);
        int endY = (int)MathF.Floor(endTile.Y);

        // Clamp start/end into slice
        startX = Math.Clamp(startX, sliceX, sliceX + sliceWidth - 1);
        startY = Math.Clamp(startY, sliceY, sliceY + sliceHeight - 1);
        endX = Math.Clamp(endX, sliceX, sliceX + sliceWidth - 1);
        endY = Math.Clamp(endY, sliceY, sliceY + sliceHeight - 1);

        // If start or end is inside a wall, bail out
        if (IsTileBlocked(mapData, doors, startX, startY) ||
            IsTileBlocked(mapData, doors, endX, endY))
            return null;

        // Already at the destination
        if (startX == endX && startY == endY)
            return new List<Vector2> { new(startX, startY) };

        // Node index within the slice: local coords
        int LocalIndex(int absX, int absY) => (absY - sliceY) * sliceWidth + (absX - sliceX);
        int totalNodes = sliceWidth * sliceHeight;

        var gScore = new float[totalNodes];
        var cameFrom = new int[totalNodes];
        Array.Fill(gScore, float.MaxValue);
        Array.Fill(cameFrom, -1);

        int startIdx = LocalIndex(startX, startY);
        int endIdx = LocalIndex(endX, endY);
        gScore[startIdx] = 0;

        var openSet = new PriorityQueue<int, float>();
        openSet.Enqueue(startIdx, Heuristic(startX, startY, endX, endY));

        while (openSet.Count > 0)
        {
            int currentIdx = openSet.Dequeue();
            if (currentIdx == endIdx)
                return ReconstructPath(cameFrom, currentIdx, sliceX, sliceY, sliceWidth);

            int cx = sliceX + currentIdx % sliceWidth;
            int cy = sliceY + currentIdx / sliceWidth;

            for (int i = 0; i < Neighbors.Length; i++)
            {
                int nx = cx + Neighbors[i].dx;
                int ny = cy + Neighbors[i].dy;

                // Must be within the slice
                if (nx < sliceX || nx >= sliceX + sliceWidth ||
                    ny < sliceY || ny >= sliceY + sliceHeight)
                    continue;

                // Wall / door check
                if (IsTileBlocked(mapData, doors, nx, ny))
                    continue;

                // Diagonal: prevent corner cutting
                if (Neighbors[i].dx != 0 && Neighbors[i].dy != 0)
                {
                    if (IsTileBlocked(mapData, doors, cx + Neighbors[i].dx, cy) ||
                        IsTileBlocked(mapData, doors, cx, cy + Neighbors[i].dy))
                        continue;
                }

                int neighborIdx = LocalIndex(nx, ny);
                float tentativeG = gScore[currentIdx] + Neighbors[i].cost;

                if (tentativeG < gScore[neighborIdx])
                {
                    cameFrom[neighborIdx] = currentIdx;
                    gScore[neighborIdx] = tentativeG;
                    float f = tentativeG + Heuristic(nx, ny, endX, endY);
                    openSet.Enqueue(neighborIdx, f);
                }
            }
        }

        // No path found
        return null;
    }

    /// <summary>
    /// Check if a tile is impassable (wall or closed/closing door).
    /// </summary>
    private static bool IsTileBlocked(MapData mapData, List<Door> doors, int tileX, int tileY)
    {
        if (mapData.GetTile(mapData.Walls, tileX, tileY) > 0)
            return true;

        foreach (var door in doors)
        {
            int doorTileX = (int)MathF.Round(door.StartPosition.X);
            int doorTileY = (int)MathF.Round(door.StartPosition.Y);
            if (doorTileX == tileX && doorTileY == tileY &&
                door.DoorState != DoorState.OPEN)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Chebyshev distance — admissible heuristic for 8-directional movement.
    /// </summary>
    private static float Heuristic(int ax, int ay, int bx, int by)
    {
        int dx = Math.Abs(ax - bx);
        int dy = Math.Abs(ay - by);
        return Math.Max(dx, dy) + (1.414f - 1.0f) * Math.Min(dx, dy);
    }

    /// <summary>
    /// Walk the cameFrom chain backwards and return the path in forward order.
    /// </summary>
    private static List<Vector2> ReconstructPath(int[] cameFrom, int currentIdx, int sliceX, int sliceY, int sliceWidth)
    {
        var path = new List<Vector2>();
        while (currentIdx != -1)
        {
            int x = sliceX + currentIdx % sliceWidth;
            int y = sliceY + currentIdx / sliceWidth;
            path.Add(new Vector2(x, y));
            currentIdx = cameFrom[currentIdx];
        }
        path.Reverse();
        return path;
    }
}
