using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Pickups;
using Game.Features.Players;
using Raylib_cs;

namespace Game.Core.Level;

/// <summary>
/// Shared level data stored as raw tile ID arrays.
/// Both the game and editor scenes reference this, and serialization operates on these arrays directly.
/// Feature placement records live in <see cref="Game.Features.Enemies"/>, <see cref="Game.Features.Pickups"/>, <see cref="Game.Features.LevelProgress"/>, and <see cref="Game.Features.Players"/>.
/// </summary>
public class MapData
{
    public uint[] Floor { get; set; } = Array.Empty<uint>();
    public uint[] Walls { get; set; } = Array.Empty<uint>();
    public uint[] Ceiling { get; set; } = Array.Empty<uint>();
    public uint[] Doors { get; set; } = Array.Empty<uint>();
    /// <summary>Placed blocking objects. 0 = empty; 1..20 = sprite ID (see <see cref="Game.Features.WorldObjects.ObjectSprites"/>).</summary>
    public uint[] Objects { get; set; } = Array.Empty<uint>();
    public List<EnemyPlacement> Enemies { get; set; } = new();
    public List<PickupPlacement> Pickups { get; set; } = new();
    public List<SecretWallPlacement> SecretWalls { get; set; } = new();
    /// <summary>Sparse per-face texture exceptions. Missing faces use <see cref="Walls"/>.</summary>
    public List<WallFaceOverride> WallFaceOverrides { get; set; } = new();
    public PlayerSpawnPlacement Spawn { get; set; } = new();
    /// <summary>
    /// Combined wall/floor/ceiling and door tile textures. Wall entries use
    /// <see cref="Game.Engine.Rendering.WallTilePalette"/> texture indices; doors follow them
    /// using <see cref="Game.Features.Doors.DoorTileEncoding"/> texture indices.
    /// </summary>
    public List<Texture2D> TileTextures { get; set; } = new();

    /// <summary>Enemy, weapons, pickups, and other non-level-tile textures.</summary>
    public List<Texture2D> GameTextures { get; set; } = new();
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>Incremented when wall/floor/ceiling geometry changes at runtime (secrets, exits).</summary>
    public int GeometryVersion { get; private set; }

    public void NotifyGeometryChanged() => GeometryVersion++;

    public void ResetGeometryVersion() => GeometryVersion = 0;

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

    public WallFaceOverride? FindWallFaceOverride(int x, int y) =>
        WallFaceOverrides.Find(o => o.TileX == x && o.TileY == y);

    public void RemoveWallFaceOverrideAt(int x, int y)
    {
        int index = WallFaceOverrides.FindIndex(o => o.TileX == x && o.TileY == y);
        if (index >= 0)
            WallFaceOverrides.RemoveAt(index);
    }

    /// <summary>
    /// Tile ID for one wall face. Falls back to <see cref="Walls"/> when the face is unset.
    /// </summary>
    public uint GetWallFaceTile(int x, int y, WallFace face)
    {
        uint fallback = GetTile(Walls, x, y);
        uint? id = FindWallFaceOverride(x, y)?.Get(face);
        return id is > 0 ? id.Value : fallback;
    }
}
