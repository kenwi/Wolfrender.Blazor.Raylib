using System.Text.Json;

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

public static class LevelSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void SaveToJson(MapData mapData, string path)
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

        var json = JsonSerializer.Serialize(fileData, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static void LoadFromJson(MapData mapData, string path)
    {
        var json = File.ReadAllText(path);
        var fileData = JsonSerializer.Deserialize<LevelFileData>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize level file: {path}");

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
}
