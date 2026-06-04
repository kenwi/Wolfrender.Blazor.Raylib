using Raylib_cs;

namespace Game.Core.Level;

/// <summary>
/// Shared level data stored as raw tile ID arrays.
/// Both the game and editor scenes reference this, and serialization operates on these arrays directly.
/// Feature placement records live in <see cref="Game.Features.Enemies"/> and <see cref="Game.Features.Pickups"/>.
/// </summary>
public class MapData
{
    public uint[] Floor { get; set; } = Array.Empty<uint>();
    public uint[] Walls { get; set; } = Array.Empty<uint>();
    public uint[] Ceiling { get; set; } = Array.Empty<uint>();
    public uint[] Doors { get; set; } = Array.Empty<uint>();
    /// <summary>Placed blocking objects. 0 = empty; 1..20 = sprite ID (see <see cref="Rendering.ObjectSprites"/>).</summary>
    public uint[] Objects { get; set; } = Array.Empty<uint>();
    public List<EnemyPlacement> Enemies { get; set; } = new();
    public List<PickupPlacement> Pickups { get; set; } = new();
    public int PlayerSpawnTileX { get; set; } = 30;
    public int PlayerSpawnTileY { get; set; } = 28;
    public float PlayerSpawnWorldY { get; set; } = 2f;
    /// <summary>Spawn facing in radians, snapped to 45° steps (same convention as <see cref="EnemyPlacement.Rotation"/>).</summary>
    public float PlayerSpawnRotation { get; set; } = -MathF.PI / 2f;
    /// <summary>64 baked tiles from <see cref="Rendering.TileSpriteSheet"/> (index 0 = tile ID 1).</summary>
    public List<Texture2D> TileTextures { get; set; } = new();

    /// <summary>Enemy, weapons, pickups, and other non-level-tile textures.</summary>
    public List<Texture2D> GameTextures { get; set; } = new();
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
