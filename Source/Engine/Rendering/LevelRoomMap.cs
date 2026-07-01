using Game.Core.Level;
using Game.Features.Doors;

namespace Game.Engine.Rendering;

/// <summary>
/// Precomputed room regions (flood fill with all doors closed) and door-to-room links
/// for room-scoped static mesh visibility. Separate from sound propagation (no blocking objects).
/// </summary>
public sealed class LevelRoomMap
{
    private static readonly (int dx, int dy)[] CardinalNeighbors =
    {
        (0, -1), (1, 0), (0, 1), (-1, 0),
    };

    public int Width { get; }
    public int Height { get; }
    public int RoomCount { get; }
    /// <summary>Per-tile room id, or -1 when not part of a room interior.</summary>
    public int[] TileRoomId { get; }
    public IReadOnlyList<RoomDoorLink> DoorLinks { get; }

    private LevelRoomMap(int width, int height, int[] tileRoomId, List<RoomDoorLink> doorLinks, int roomCount)
    {
        Width = width;
        Height = height;
        TileRoomId = tileRoomId;
        DoorLinks = doorLinks;
        RoomCount = roomCount;
    }

    public static LevelRoomMap Build(MapData mapData)
    {
        int width = mapData.Width;
        int height = mapData.Height;
        int tileCount = width * height;
        var tileRoomId = new int[tileCount];
        Array.Fill(tileRoomId, -1);

        var secretTiles = BuildSecretTileSet(mapData);
        int nextRoomId = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = LevelData.GetIndex(x, y, width);
                if (tileRoomId[index] >= 0)
                    continue;

                if (!IsInteriorRoomCell(mapData, x, y, secretTiles))
                    continue;

                FloodFillRoom(mapData, x, y, secretTiles, nextRoomId, tileRoomId);
                nextRoomId++;
            }
        }

        var doorLinks = BuildDoorLinks(mapData, tileRoomId);
        return new LevelRoomMap(width, height, tileRoomId, doorLinks, nextRoomId);
    }

    public int GetRoomAt(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
            return FindAdjacentRoom(tileX, tileY);

        int roomId = TileRoomId[LevelData.GetIndex(tileX, tileY, Width)];
        if (roomId >= 0)
            return roomId;

        return FindAdjacentRoom(tileX, tileY);
    }

    /// <summary>
    /// Room ids owning geometry on or adjacent to a tile (interior tile, or both rooms at a door).
    /// </summary>
    public List<int> GetTileRoomIds(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
            return new List<int> { -1 };

        int index = LevelData.GetIndex(tileX, tileY, Width);
        int selfRoom = TileRoomId[index];
        if (selfRoom >= 0)
            return new List<int> { selfRoom };

        var adjacent = new HashSet<int>();
        foreach (var (dx, dy) in CardinalNeighbors)
        {
            int nx = tileX + dx;
            int ny = tileY + dy;
            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                continue;

            int roomId = TileRoomId[LevelData.GetIndex(nx, ny, Width)];
            if (roomId >= 0)
                adjacent.Add(roomId);
        }

        if (adjacent.Count == 0)
            return new List<int> { -1 };

        return adjacent.OrderBy(id => id).ToList();
    }

    /// <summary>
    /// Rooms visible from the player tile: current room plus rooms reachable through non-closed doors.
    /// </summary>
    public HashSet<int> ComputeVisibleRooms(int playerTileX, int playerTileY, IReadOnlyList<Door> doors)
    {
        var visible = new HashSet<int>();
        int startRoom = GetRoomAt(playerTileX, playerTileY);
        if (startRoom < 0)
            return visible;

        var queue = new Queue<int>();
        visible.Add(startRoom);
        queue.Enqueue(startRoom);

        while (queue.Count > 0)
        {
            int roomId = queue.Dequeue();
            foreach (var link in DoorLinks)
            {
                if (link.RoomA != roomId && link.RoomB != roomId)
                    continue;

                if (!IsDoorPassable(doors, link.DoorTileX, link.DoorTileY))
                    continue;

                int otherRoom = link.RoomA == roomId ? link.RoomB : link.RoomA;
                if (otherRoom < 0 || otherRoom == roomId)
                    continue;

                if (visible.Add(otherRoom))
                    queue.Enqueue(otherRoom);
            }
        }

        return visible;
    }

    public IEnumerable<(int x, int y, int roomId)> EnumerateRoomTiles()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int roomId = TileRoomId[LevelData.GetIndex(x, y, Width)];
                if (roomId >= 0)
                    yield return (x, y, roomId);
            }
        }
    }

    private int FindAdjacentRoom(int tileX, int tileY)
    {
        foreach (var (dx, dy) in CardinalNeighbors)
        {
            int nx = tileX + dx;
            int ny = tileY + dy;
            if (nx < 0 || nx >= Width || ny < 0 || ny >= Height)
                continue;

            int roomId = TileRoomId[LevelData.GetIndex(nx, ny, Width)];
            if (roomId >= 0)
                return roomId;
        }

        return -1;
    }

    private static void FloodFillRoom(
        MapData mapData,
        int startX,
        int startY,
        HashSet<(int x, int y)> secretTiles,
        int roomId,
        int[] tileRoomId)
    {
        int width = mapData.Width;
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        tileRoomId[LevelData.GetIndex(startX, startY, width)] = roomId;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (dx, dy) in CardinalNeighbors)
            {
                int nx = x + dx;
                int ny = y + dy;
                int index = LevelData.GetIndex(nx, ny, width);
                if (index < 0 || index >= tileRoomId.Length || tileRoomId[index] >= 0)
                    continue;

                if (!IsInteriorRoomCell(mapData, nx, ny, secretTiles))
                    continue;

                tileRoomId[index] = roomId;
                queue.Enqueue((nx, ny));
            }
        }
    }

    private static List<RoomDoorLink> BuildDoorLinks(MapData mapData, int[] tileRoomId)
    {
        var links = new List<RoomDoorLink>();
        int width = mapData.Width;

        for (int y = 0; y < mapData.Height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = LevelData.GetIndex(x, y, width);
                uint doorValue = mapData.Doors[index];
                if (!DoorTileEncoding.IsDoorTile(doorValue))
                    continue;

                var neighborRooms = new HashSet<int>();
                foreach (var (dx, dy) in CardinalNeighbors)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= mapData.Height)
                        continue;

                    int roomId = tileRoomId[LevelData.GetIndex(nx, ny, width)];
                    if (roomId >= 0)
                        neighborRooms.Add(roomId);
                }

                if (neighborRooms.Count < 2)
                    continue;

                var rooms = neighborRooms.OrderBy(id => id).ToArray();
                links.Add(new RoomDoorLink(x, y, rooms[0], rooms[^1]));
            }
        }

        return links;
    }

    /// <summary>
    /// Interior walkable cell when all doors are closed. Door tiles and walls are excluded.
    /// </summary>
    internal static bool IsInteriorRoomCell(
        MapData mapData,
        int x,
        int y,
        IReadOnlySet<(int x, int y)> secretTiles)
    {
        if (x < 0 || x >= mapData.Width || y < 0 || y >= mapData.Height)
            return false;

        int index = LevelData.GetIndex(x, y, mapData.Width);
        bool hasDoor = mapData.Doors[index] != 0 && DoorTileEncoding.IsDoorTile(mapData.Doors[index]);

        if (mapData.Walls[index] != 0)
            return false;

        if (hasDoor)
            return false;

        if (mapData.Floor[index] != 0 || mapData.Ceiling[index] != 0)
            return true;

        return false;
    }

    private static HashSet<(int x, int y)> BuildSecretTileSet(MapData mapData)
    {
        var secretTiles = new HashSet<(int x, int y)>();
        foreach (var secret in mapData.SecretWalls)
            secretTiles.Add((secret.TileX, secret.TileY));
        return secretTiles;
    }

    private static bool IsDoorPassable(IReadOnlyList<Door> doors, int doorTileX, int doorTileY)
    {
        foreach (var door in doors)
        {
            int tileX = (int)MathF.Round(door.StartPosition.X);
            int tileY = (int)MathF.Round(door.StartPosition.Y);
            if (tileX != doorTileX || tileY != doorTileY)
                continue;

            return door.DoorState != DoorState.CLOSED;
        }

        return false;
    }
}

public readonly record struct RoomDoorLink(int DoorTileX, int DoorTileY, int RoomA, int RoomB);
