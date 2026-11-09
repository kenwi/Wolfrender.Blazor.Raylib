using System.Buffers.Binary;
using System.Text.Json;
using Game.Engine.Rendering;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Pickups;
using Game.Features.Players;
using Game.Features.WorldObjects;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Core.Level;

public class LevelFileData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public uint[] Floor { get; set; } = Array.Empty<uint>();
    public uint[] Walls { get; set; } = Array.Empty<uint>();
    public uint[] Ceiling { get; set; } = Array.Empty<uint>();
    public uint[] Doors { get; set; } = Array.Empty<uint>();
    public uint[] Objects { get; set; } = Array.Empty<uint>();
    public List<EnemyPlacementData> Enemies { get; set; } = new();
    public List<PickupPlacementData> Pickups { get; set; } = new();
    public List<SecretWallPlacementData> SecretWalls { get; set; } = new();
    public List<WallFaceOverrideData> WallFaceOverrides { get; set; } = new();
    public PlayerSpawnData? PlayerSpawn { get; set; }
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
    /// Feature placements map through their slice-owned DTOs.
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
            Objects = mapData.Objects,
            Enemies = mapData.Enemies.Select(EnemyPlacementData.FromPlacement).ToList(),
            Pickups = mapData.Pickups.Select(PickupPlacementData.FromPlacement).ToList(),
            SecretWalls = mapData.SecretWalls.Select(SecretWallPlacementData.FromPlacement).ToList(),
            WallFaceOverrides = mapData.WallFaceOverrides.Select(WallFaceOverrideData.FromPlacement).ToList(),
            PlayerSpawn = PlayerSpawnData.FromPlacement(mapData.Spawn)
        };

        return JsonSerializer.Serialize(fileData, JsonOptions);
    }

    /// <summary>
    /// Deserialize a JSON string into MapData (no file system needed, suitable for WASM).
    /// Feature placements map through their slice-owned DTOs.
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
        mapData.Objects = NormalizeTileLayer(fileData.Objects, fileData.Width, fileData.Height);
        mapData.Enemies = fileData.Enemies.Select(e => e.ToPlacement()).ToList();
        mapData.Pickups = (fileData.Pickups ?? new List<PickupPlacementData>())
            .Select(p => p.ToPlacement())
            .ToList();
        mapData.SecretWalls = (fileData.SecretWalls ?? new List<SecretWallPlacementData>())
            .Select(s => s.ToPlacement())
            .ToList();
        mapData.WallFaceOverrides = (fileData.WallFaceOverrides ?? new List<WallFaceOverrideData>())
            .Select(o => o.ToPlacement())
            .Where(o => o.HasAnyFace)
            .ToList();
        fileData.PlayerSpawn?.ApplyTo(mapData.Spawn);
        mapData.ResetGeometryVersion();
    }

    /// <summary>Returns the layer array when length matches the map, otherwise a zero-filled array.</summary>
    private static uint[] NormalizeTileLayer(uint[]? layer, int width, int height)
    {
        int tileCount = width * height;
        if (layer != null && layer.Length == tileCount)
            return layer;

        return new uint[tileCount];
    }

    /// <summary>
    /// Load a Wolfenstein 3D map dump. Pass either plane file (<c>_m0</c>/<c>_m1</c>)
    /// or a shared prefix; plane 0 supplies walls/doors and a sibling <c>_m1</c>
    /// supplies enemies, player start, and patrol markers when present.
    /// </summary>
    public static void LoadFromW3dmap(MapData mapData, string path)
    {
        var (plane0Path, plane1Path) = ResolveW3dmapPlanePaths(path);
        if (plane0Path == null || !File.Exists(plane0Path))
            throw new FileNotFoundException(
                $"W3DMAP plane 0 not found next to '{path}'. Expected a *_m0.w3dmap file.",
                plane0Path ?? path);

        byte[] plane0 = File.ReadAllBytes(plane0Path);
        byte[]? plane1 = plane1Path != null && File.Exists(plane1Path)
            ? File.ReadAllBytes(plane1Path)
            : null;
        DeserializeFromW3dmap(mapData, plane0, plane1);
    }

    /// <summary>Load a W3DMAP plane 0 from raw bytes (suitable for browser file input).</summary>
    public static void DeserializeFromW3dmap(MapData mapData, ReadOnlySpan<byte> plane0) =>
        DeserializeFromW3dmap(mapData, plane0, plane1: default);

    /// <summary>
    /// Load plane 0 walls/doors and optional plane 1 actors from raw bytes.
    /// </summary>
    public static void DeserializeFromW3dmap(
        MapData mapData, ReadOnlySpan<byte> plane0, ReadOnlySpan<byte> plane1)
    {
        if (plane0.Length == 0 || plane0.Length % 2 != 0)
            throw new InvalidOperationException(
                $"W3DMAP must contain an even number of bytes, got {plane0.Length}.");

        int tileCount = plane0.Length / 2;
        int side = (int)Math.Sqrt(tileCount);
        if (side <= 0 || side * side != tileCount)
            throw new InvalidOperationException(
                $"W3DMAP contains {tileCount} tiles, which is not a square map.");

        uint defaultFloorTile = WallTilePalette.GetIdByFileName("068_grey_brick.png");
        if (defaultFloorTile == 0)
            throw new InvalidOperationException(
                "Default W3DMAP floor texture '068_grey_brick.png' is missing from the wall palette.");
        uint defaultCeilingTile = WallTilePalette.GetIdByFileName("069_grey_brick_dark.png");
        if (defaultCeilingTile == 0)
            throw new InvalidOperationException(
                "Default W3DMAP ceiling texture '069_grey_brick_dark.png' is missing from the wall palette.");

        mapData.Width = side;
        mapData.Height = side;
        mapData.Floor = new uint[tileCount];
        mapData.Walls = new uint[tileCount];
        mapData.Ceiling = new uint[tileCount];
        mapData.Doors = new uint[tileCount];
        mapData.Objects = new uint[tileCount];
        mapData.Enemies = new List<EnemyPlacement>();
        mapData.Pickups = new List<PickupPlacement>();
        mapData.SecretWalls = new List<SecretWallPlacement>();
        mapData.WallFaceOverrides = new List<WallFaceOverride>();

        var values = new ushort[tileCount];
        for (int i = 0; i < tileCount; i++)
        {
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(plane0.Slice(i * 2, 2));
            values[i] = value;

            if (value is >= 1 and <= 63)
            {
                int chunk = (value - 1) * 2;
                uint wallTile = WallTilePalette.GetIdForChunk(chunk);
                if (wallTile == 0)
                    throw new InvalidOperationException(
                        $"W3DMAP wall value {value} references missing texture chunk {chunk}.");

                mapData.Walls[i] = wallTile;
                continue;
            }

            // Plane 0 has no floor/ceiling textures. Area codes (106..143), empty
            // space and doors are walkable: default floor + matching ceiling.
            mapData.Floor[i] = defaultFloorTile;
            mapData.Ceiling[i] = defaultCeilingTile;

            if (value is >= 90 and <= 101)
                mapData.Doors[i] = MapW3dmapDoor(value);
        }

        // Wolf fills the void with solid wall cells. Keep only walls that
        // touch a floor tile (orthogonal distance 1) so the editor shows the
        // playable outline instead of a solid brick field.
        CullW3dmapWallsNotAdjacentToFloor(mapData);

        var (spawnX, spawnY) = FindW3dmapSpawn(values, side);
        mapData.Spawn = new PlayerSpawnPlacement
        {
            TileX = spawnX,
            TileY = spawnY,
            WorldY = 2f,
            Rotation = -MathF.PI / 2f
        };

        if (plane1.Length > 0)
        {
            if (plane1.Length != plane0.Length)
                throw new InvalidOperationException(
                    $"W3DMAP plane 1 length {plane1.Length} does not match plane 0 ({plane0.Length}).");

            var things = new ushort[tileCount];
            for (int i = 0; i < tileCount; i++)
                things[i] = BinaryPrimitives.ReadUInt16LittleEndian(plane1.Slice(i * 2, 2));

            W3dmapEnemyImport.ApplyPlane1(mapData, things, values);
            W3dmapPickupImport.ApplyPlane1(mapData, things);
            W3dmapObjectImport.ApplyPlane1(mapData, things);
            W3dmapSecretImport.ApplyPlane1(mapData, things, values);
        }

        DoorSideFaceAutoDetect.ApplyAllDoors(mapData);

        mapData.ResetGeometryVersion();
    }

    /// <summary>
    /// Resolve <c>*_m0.w3dmap</c> / <c>*_m1.w3dmap</c> siblings from a plane
    /// path or shared level prefix.
    /// </summary>
    public static (string? Plane0Path, string? Plane1Path) ResolveW3dmapPlanePaths(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        string stem = Path.GetFileNameWithoutExtension(path);
        if (stem.EndsWith("_m0", StringComparison.OrdinalIgnoreCase)
            || stem.EndsWith("_m1", StringComparison.OrdinalIgnoreCase)
            || stem.EndsWith("_m2", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^3];
        }

        string prefix = string.IsNullOrEmpty(directory)
            ? stem
            : Path.Combine(directory, stem);
        return (prefix + "_m0.w3dmap", prefix + "_m1.w3dmap");
    }

    /// <summary>Strip plane suffixes so <c>e1_l1_m0.w3dmap</c> becomes <c>e1_l1.json</c>.</summary>
    public static string W3dmapLevelFileName(string path)
    {
        string stem = Path.GetFileNameWithoutExtension(path);
        if (stem.EndsWith("_m0", StringComparison.OrdinalIgnoreCase)
            || stem.EndsWith("_m1", StringComparison.OrdinalIgnoreCase)
            || stem.EndsWith("_m2", StringComparison.OrdinalIgnoreCase))
        {
            stem = stem[..^3];
        }

        return stem + ".json";
    }

    private static uint MapW3dmapDoor(ushort value) => value switch
    {
        // 90/91 and the unused lock slots 98/99 use the ordinary door texture.
        90 or 91 or 98 or 99 => DoorTileEncoding.DefaultId,
        >= 92 and <= 97 => DoorTileEncoding.LockedId,
        100 or 101 => DoorTileEncoding.ElevatorId,
        _ => DoorTileEncoding.DefaultId
    };

    /// <summary>
    /// Clear wall cells that are not orthogonally adjacent to a floor tile
    /// (exactly one step in X or Y). Diagonal-only contact does not keep a wall.
    /// </summary>
    private static void CullW3dmapWallsNotAdjacentToFloor(MapData mapData)
    {
        int width = mapData.Width;
        int height = mapData.Height;
        int tileCount = width * height;
        var keep = new bool[tileCount];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = width * y + x;
                if (mapData.Walls[index] == 0)
                    continue;

                if (HasOrthogonallyAdjacentFloor(mapData, x, y))
                    keep[index] = true;
            }
        }

        for (int i = 0; i < tileCount; i++)
        {
            if (mapData.Walls[i] != 0 && !keep[i])
                mapData.Walls[i] = 0;
        }
    }

    private static bool HasOrthogonallyAdjacentFloor(MapData mapData, int x, int y) =>
        mapData.GetTile(mapData.Floor, x - 1, y) != 0
        || mapData.GetTile(mapData.Floor, x + 1, y) != 0
        || mapData.GetTile(mapData.Floor, x, y - 1) != 0
        || mapData.GetTile(mapData.Floor, x, y + 1) != 0;

    private static (int X, int Y) FindW3dmapSpawn(ReadOnlySpan<ushort> values, int side)
    {
        int center = side / 2;
        int bestIndex = -1;
        long bestDistance = long.MaxValue;

        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                ushort value = values[y * side + x];
                bool walkable = value == 0 || value is >= 106 and < 144;
                if (!walkable)
                    continue;

                long dx = x - center;
                long dy = y - center;
                long distance = dx * dx + dy * dy;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = y * side + x;
            }
        }

        // If a malformed map has no explicit open tile, keep the spawn in bounds.
        return bestIndex >= 0
            ? (bestIndex % side, bestIndex / side)
            : (0, 0);
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
        mapData.Objects = new uint[mapData.Width * mapData.Height];
        mapData.Pickups = new List<PickupPlacement>();
        mapData.SecretWalls = new List<SecretWallPlacement>();
        mapData.WallFaceOverrides = new List<WallFaceOverride>();
        mapData.ResetGeometryVersion();
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
        mapData.Objects = new uint[tileCount];
        mapData.Enemies = new List<EnemyPlacement>();
        mapData.Pickups = new List<PickupPlacement>();
        mapData.SecretWalls = new List<SecretWallPlacement>();
        mapData.WallFaceOverrides = new List<WallFaceOverride>();

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
        mapData.ResetGeometryVersion();
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
