using System.Text.Json;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Editor;

public class PatrolWaypointData
{
    public int TileX { get; set; }
    public int TileY { get; set; }
}

public class EnemyPlacementData
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public float Rotation { get; set; }
    public string EnemyType { get; set; } = "Guard";
    public List<PatrolWaypointData> PatrolPath { get; set; } = new();
}

public class LevelFileData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public uint[] Floor { get; set; } = Array.Empty<uint>();
    public uint[] Walls { get; set; } = Array.Empty<uint>();
    public uint[] Ceiling { get; set; } = Array.Empty<uint>();
    public uint[] Doors { get; set; } = Array.Empty<uint>();
    public List<EnemyPlacementData> Enemies { get; set; } = new();
}

public enum BmpTileLayer { Floor, Walls, Ceiling, Doors }

public static class LevelSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    // BMP tile constants
    private const int TilePixelSize = 7;
    private const int BorderSize = 1;

    // All-black tile: 49 pixels * 3 bytes (RGB) = 147 bytes -> 294 hex chars, all zeroes
    private static readonly string BlackTileHash = new('0', TilePixelSize * TilePixelSize * 3 * 2);

    private static readonly Dictionary<string, (uint TileId, BmpTileLayer Layer)> TileHashMap = new()
    {
        { BlackTileHash, (1, BmpTileLayer.Floor) } // greystone on floor
    };

    private static string ComputeTileHash(Image image, int startX, int startY, int tileSize)
    {
        Span<byte> pixels = stackalloc byte[tileSize * tileSize * 3];
        int i = 0;
        for (int py = 0; py < tileSize; py++)
        {
            for (int px = 0; px < tileSize; px++)
            {
                var color = GetImageColor(image, startX + px, startY + py);
                pixels[i++] = color.R;
                pixels[i++] = color.G;
                pixels[i++] = color.B;
            }
        }
        return Convert.ToHexString(pixels);
    }

    public static void SaveToJson(MapData mapData, string path)
    {
        var json = SerializeToJson(mapData);
        File.WriteAllText(path, json);
    }

    public static void LoadFromJson(MapData mapData, string path)
    {
        var json = File.ReadAllText(path);
        DeserializeFromJson(mapData, json);
    }

    /// <summary>
    /// Serialize MapData to a JSON string (no file system needed, suitable for WASM).
    /// </summary>
    public static string SerializeToJson(MapData mapData)
    {
        var fileData = new LevelFileData
        {
            Width = mapData.Width,
            Height = mapData.Height,
            Floor = mapData.Floor,
            Walls = mapData.Walls,
            Ceiling = mapData.Ceiling,
            Doors = mapData.Doors,
            Enemies = mapData.Enemies.Select(e => new EnemyPlacementData
            {
                TileX = e.TileX,
                TileY = e.TileY,
                Rotation = e.Rotation,
                EnemyType = e.EnemyType,
                PatrolPath = e.PatrolPath.Select(w => new PatrolWaypointData
                {
                    TileX = w.TileX,
                    TileY = w.TileY
                }).ToList()
            }).ToList()
        };

        return JsonSerializer.Serialize(fileData, JsonOptions);
    }

    /// <summary>
    /// Deserialize a JSON string into MapData (no file system needed, suitable for WASM).
    /// </summary>
    public static void DeserializeFromJson(MapData mapData, string json)
    {
        var fileData = JsonSerializer.Deserialize<LevelFileData>(json)
            ?? throw new InvalidOperationException("Failed to deserialize level JSON");

        mapData.Width = fileData.Width;
        mapData.Height = fileData.Height;
        mapData.Floor = fileData.Floor;
        mapData.Walls = fileData.Walls;
        mapData.Ceiling = fileData.Ceiling;
        mapData.Doors = fileData.Doors;
        mapData.Enemies = fileData.Enemies.Select(e => new EnemyPlacement
        {
            TileX = e.TileX,
            TileY = e.TileY,
            Rotation = e.Rotation,
            EnemyType = e.EnemyType,
            PatrolPath = e.PatrolPath.Select(w => new PatrolWaypoint
            {
                TileX = w.TileX,
                TileY = w.TileY
            }).ToList()
        }).ToList();
    }

    public static void LoadFromTmx(MapData mapData, string path)
    {
        var loader = DotTiled.Serialization.Loader.Default();
        var map = loader.LoadMap(path);

        var floor = map.Layers[0] as DotTiled.TileLayer;
        var walls = map.Layers[1] as DotTiled.TileLayer;
        var ceiling = map.Layers[2] as DotTiled.TileLayer;
        var doors = map.Layers[3] as DotTiled.TileLayer;

        if (walls == null || floor == null || ceiling == null || doors == null)
            throw new InvalidOperationException("TMX must have floor, walls, ceiling, and doors layers");

        mapData.Width = (int)walls.Width;
        mapData.Height = (int)walls.Height;
        mapData.Floor = (uint[])floor.Data!.Value.GlobalTileIDs!.Value.Clone();
        mapData.Walls = (uint[])walls.Data!.Value.GlobalTileIDs!.Value.Clone();
        mapData.Ceiling = (uint[])ceiling.Data!.Value.GlobalTileIDs!.Value.Clone();
        mapData.Doors = (uint[])doors.Data!.Value.GlobalTileIDs!.Value.Clone();
    }

    public static void LoadFromBmp(MapData mapData, string path)
    {
        var image = LoadImage(path);

        int mapWidth = (image.Width - 2 * BorderSize) / TilePixelSize;
        int mapHeight = (image.Height - 2 * BorderSize) / TilePixelSize;

        if (mapWidth <= 0 || mapHeight <= 0)
        {
            UnloadImage(image);
            throw new InvalidOperationException(
                $"BMP dimensions ({image.Width}x{image.Height}) are too small for {TilePixelSize}px tiles with {BorderSize}px border");
        }

        int tileCount = mapWidth * mapHeight;

        mapData.Width = mapWidth;
        mapData.Height = mapHeight;
        mapData.Floor = new uint[tileCount];
        mapData.Walls = new uint[tileCount];
        mapData.Ceiling = new uint[tileCount];
        mapData.Doors = new uint[tileCount];
        mapData.Enemies = new List<EnemyPlacement>();

        for (int tileY = 0; tileY < mapHeight; tileY++)
        {
            for (int tileX = 0; tileX < mapWidth; tileX++)
            {
                int pixelX = BorderSize + tileX * TilePixelSize;
                int pixelY = BorderSize + tileY * TilePixelSize;
                string hash = ComputeTileHash(image, pixelX, pixelY, TilePixelSize);

                if (TileHashMap.TryGetValue(hash, out var mapping))
                {
                    uint[] targetLayer = mapping.Layer switch
                    {
                        BmpTileLayer.Floor => mapData.Floor,
                        BmpTileLayer.Walls => mapData.Walls,
                        BmpTileLayer.Ceiling => mapData.Ceiling,
                        BmpTileLayer.Doors => mapData.Doors,
                        _ => mapData.Floor
                    };
                    targetLayer[tileY * mapWidth + tileX] = mapping.TileId;
                }
            }
        }

        UnloadImage(image);
    }

    public static Dictionary<string, int> DiscoverBmpTileHashes(string path)
    {
        var image = LoadImage(path);

        int mapWidth = (image.Width - 2 * BorderSize) / TilePixelSize;
        int mapHeight = (image.Height - 2 * BorderSize) / TilePixelSize;

        if (mapWidth <= 0 || mapHeight <= 0)
        {
            UnloadImage(image);
            throw new InvalidOperationException(
                $"BMP dimensions ({image.Width}x{image.Height}) are too small for {TilePixelSize}px tiles with {BorderSize}px border");
        }

        var hashCounts = new Dictionary<string, int>();

        for (int tileY = 0; tileY < mapHeight; tileY++)
        {
            for (int tileX = 0; tileX < mapWidth; tileX++)
            {
                int pixelX = BorderSize + tileX * TilePixelSize;
                int pixelY = BorderSize + tileY * TilePixelSize;
                string hash = ComputeTileHash(image, pixelX, pixelY, TilePixelSize);

                if (hashCounts.ContainsKey(hash))
                    hashCounts[hash]++;
                else
                    hashCounts[hash] = 1;
            }
        }

        UnloadImage(image);
        return hashCounts;
    }
}
