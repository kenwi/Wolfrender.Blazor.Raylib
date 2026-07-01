using Game.Features.Doors;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Rendering;

/// <summary>
/// GPU texture of tiles that block placed light (walls and closed doors). Updated each frame.
/// </summary>
public sealed class LightOcclusionMap : IDisposable
{
    private Texture2D _texture;
    private byte[] _pixels = Array.Empty<byte>();
    private int _width;
    private int _height;
    private bool _hasTexture;

    public Texture2D Texture => _texture;

    public void Update(MapData mapData, IReadOnlyList<Door> doors)
    {
        int width = mapData.Width;
        int height = mapData.Height;
        int tileCount = width * height;

        if (_width != width || _height != height || _pixels.Length != tileCount)
        {
            if (_hasTexture)
            {
                UnloadTexture(_texture);
                _hasTexture = false;
            }

            _width = width;
            _height = height;
            _pixels = new byte[tileCount];
        }

        FillPixels(mapData, doors);

        Image image = BuildImage();
        if (_hasTexture)
            UnloadTexture(_texture);

        _texture = LoadTextureFromImage(image);
        UnloadImage(image);
        _hasTexture = true;
    }

    private Image BuildImage()
    {
        Image image = GenImageColor(_width, _height, Color.Black);
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int index = LevelData.GetIndex(x, y, _width);
                if (_pixels[index] != 0)
                    ImageDrawPixel(ref image, x, y, Color.White);
            }
        }

        return image;
    }

    private void FillPixels(MapData mapData, IReadOnlyList<Door> doors)
    {
        int width = mapData.Width;

        for (int y = 0; y < mapData.Height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = LevelData.GetIndex(x, y, width);
                _pixels[index] = TileBlocksLight(mapData, doors, x, y) ? (byte)255 : (byte)0;
            }
        }
    }

    private static bool TileBlocksLight(MapData mapData, IReadOnlyList<Door> doors, int x, int y)
    {
        if (x < 0 || x >= mapData.Width || y < 0 || y >= mapData.Height)
            return true;

        int index = LevelData.GetIndex(x, y, mapData.Width);
        bool hasDoor = mapData.Doors[index] != 0 && DoorTileEncoding.IsDoorTile(mapData.Doors[index]);

        if (mapData.Walls[index] != 0 && !hasDoor)
            return true;

        if (hasDoor && IsClosedDoor(doors, x, y))
            return true;

        if (mapData.Floor[index] != 0 || mapData.Ceiling[index] != 0)
            return false;

        if (hasDoor)
            return false;

        return true;
    }

    private static bool IsClosedDoor(IReadOnlyList<Door> doors, int tileX, int tileY)
    {
        foreach (var door in doors)
        {
            int doorTileX = (int)MathF.Round(door.StartPosition.X);
            int doorTileY = (int)MathF.Round(door.StartPosition.Y);
            if (doorTileX != tileX || doorTileY != tileY)
                continue;

            return door.DoorState == DoorState.CLOSED;
        }

        return true;
    }

    public void Dispose()
    {
        if (_hasTexture)
        {
            UnloadTexture(_texture);
            _hasTexture = false;
        }
    }
}
