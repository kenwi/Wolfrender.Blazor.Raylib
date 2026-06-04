using System.Numerics;

namespace Game.Core.Level;

public static class Res
{
#if CONSOLE_APP
    public static string Path(string relativePath) => relativePath;
#else
    public static string Path(string relativePath) => relativePath;
#endif
}

public static class RenderData
{
    public static Vector2 Resolution = new Vector2(1025, 411);
    public static int ResolutionDownScaleMultiplier = 4;
}

public class LevelData
{
    private readonly MapData _mapData;
    private const int MapWidth = 64;
    public int Width => _mapData.Width;
    public int Height => _mapData.Height;
    public static int QuadSize => 4;
    public static int DrawedQuads = 0;
    public static int TileCount = MapWidth * MapWidth;

    public LevelData(MapData mapData)
    {
        _mapData = mapData;
    }

    public static int GetIndex(int col, int row, int width = MapWidth) => width * row + col;

    public static (int col, int row) GetColRow(int index, int width = MapWidth) => (index % width, index / width);

    /// <summary>
    /// World X/Z anchor for tile (tileX, tileY) — same as walls, floors, and enemies
    /// (<c>RenderSystem</c> / <c>EnemySystem</c>: cube and billboards centered on this point).
    /// </summary>
    public static Vector3 GetTileAnchorWorld(int tileX, int tileY, float worldY = 0f) =>
        new(tileX * QuadSize, worldY, tileY * QuadSize);

    /// <summary>Map tile whose quad cell contains the given world X/Z.</summary>
    public static (int tileX, int tileY) GetTileFromWorld(float worldX, float worldZ) =>
        ((int)MathF.Floor(worldX / QuadSize), (int)MathF.Floor(worldZ / QuadSize));

    /// <summary>
    /// Gameplay tile for an entity at world X/Z (anchor + half a tile), matching pickup collection
    /// and the 2D editor convention of showing entities at tile center.
    /// </summary>
    public static (int tileX, int tileY) GetEntityTileFromWorld(float worldX, float worldZ)
    {
        float half = QuadSize * 0.5f;
        return GetTileFromWorld(worldX + half, worldZ + half);
    }

    /// <summary>World X/Z center of tile (tileX, tileY).</summary>
    public static (float worldX, float worldZ) GetTileCenterWorldXZ(int tileX, int tileY) =>
        ((tileX + 0.5f) * QuadSize, (tileY + 0.5f) * QuadSize);

    public bool IsWallAt(float worldX, float worldZ)
    {
        int tileX = (int)(worldX / 4 + 0.5f);
        int tileY = (int)(worldZ / 4 + 0.5f);

        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
            return true;

        int index = GetIndex(tileX, tileY, Width);
        return _mapData.Walls[index] != 0;
    }

    public uint GetWallTile(int x, int y) => _mapData.GetTile(_mapData.Walls, x, y);

    public uint GetFloorTile(int x, int y) => _mapData.GetTile(_mapData.Floor, x, y);

    public uint GetCeilingTile(int x, int y) => _mapData.GetTile(_mapData.Ceiling, x, y);

    public uint GetObjectTile(int x, int y) => _mapData.GetTile(_mapData.Objects, x, y);

    public bool HasObjectAt(int x, int y) => GetObjectTile(x, y) > 0;
}
