using Raylib_cs;

namespace Game;

/// <summary>
/// A waypoint in an enemy patrol path, stored as tile coordinates.
/// </summary>
public class PatrolWaypoint
{
    public int TileX { get; set; }
    public int TileY { get; set; }
}

/// <summary>
/// Describes a placed enemy in tile coordinates, used for editor and serialization.
/// </summary>
public class EnemyPlacement
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public float Rotation { get; set; }
    public string EnemyType { get; set; } = "Guard";
    public List<PatrolWaypoint> PatrolPath { get; set; } = new();
    public bool ShowPatrolPath { get; set; } = true;
}

/// <summary>
/// Shared level data stored as raw tile ID arrays.
/// Both the game and editor scenes reference this, and serialization operates on these arrays directly.
/// </summary>
public class MapData
{
    public uint[] Floor { get; set; } = Array.Empty<uint>();
    public uint[] Walls { get; set; } = Array.Empty<uint>();
    public uint[] Ceiling { get; set; } = Array.Empty<uint>();
    public uint[] Doors { get; set; } = Array.Empty<uint>();
    public List<EnemyPlacement> Enemies { get; set; } = new();
    public List<Texture2D> Textures { get; set; } = new();
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>
    /// Get the tile ID at a given position for a specific layer array.
    /// Returns 0 if out of bounds.
    /// </summary>
    public uint GetTile(uint[] layer, int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return 0;

        return layer[Width * y + x];
    }
}
