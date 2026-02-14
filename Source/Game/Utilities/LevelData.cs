using System.Numerics;

namespace Game.Utilities;

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
}
