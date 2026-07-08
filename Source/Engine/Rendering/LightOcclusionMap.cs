using Game.Features.Doors;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Rendering;

/// <summary>
/// GPU tile maps for placed-light occlusion (walls/closed doors) and per-room influence.
/// </summary>
public sealed class LightOcclusionMap : IDisposable
{
    private Texture2D _occlusionTexture;
    private Texture2D _roomTexture;
    private byte[] _occlusionPixels = Array.Empty<byte>();
    private byte[] _roomPixels = Array.Empty<byte>();
    private byte[] _lastUploadedOcclusion = Array.Empty<byte>();
    private byte[] _lastUploadedRoom = Array.Empty<byte>();
    private Color[] _occlusionColors = Array.Empty<Color>();
    private Color[] _roomColors = Array.Empty<Color>();
    private int _width;
    private int _height;
    private bool _hasOcclusionTexture;
    private bool _hasRoomTexture;
    private bool _forceOcclusionUpload = true;
    private bool _forceRoomUpload = true;

    public Texture2D OcclusionTexture => _occlusionTexture;
    public Texture2D RoomTexture => _roomTexture;
    public int MapWidth => _width;
    public int MapHeight => _height;
    public bool HasOcclusionTexture => _hasOcclusionTexture;
    public bool HasRoomTexture => _hasRoomTexture;

    public string DescribeTextures() =>
        $"size={_width}x{_height} occId={(_hasOcclusionTexture ? _occlusionTexture.Id : 0)} roomId={(_hasRoomTexture ? _roomTexture.Id : 0)}";

    public byte GetRoomPixel(int tileX, int tileY)
    {
        if (!TryGetIndex(tileX, tileY, out int index))
            return 0;

        return _roomPixels[index];
    }

    public int DecodeRoomId(int tileX, int tileY)
    {
        byte pixel = GetRoomPixel(tileX, tileY);
        return pixel == 0 ? -1 : 254 - pixel;
    }

    public bool TileBlocksAt(int tileX, int tileY)
    {
        if (!TryGetIndex(tileX, tileY, out int index))
            return true;

        return _occlusionPixels[index] > 127;
    }

    public int CountBlockingTiles()
    {
        int count = 0;
        foreach (byte pixel in _occlusionPixels)
        {
            if (pixel > 127)
                count++;
        }

        return count;
    }

    public int CountEncodedRoomTiles()
    {
        int count = 0;
        foreach (byte pixel in _roomPixels)
        {
            if (pixel != 0)
                count++;
        }

        return count;
    }

    /// <summary>Read back a room id from the uploaded GPU texture (for diagnostics).</summary>
    public int ReadBackRoomIdFromGpu(int tileX, int tileY)
    {
        if (!_hasRoomTexture || tileX < 0 || tileX >= _width || tileY < 0 || tileY >= _height)
            return -1;

        Image image = LoadImageFromTexture(_roomTexture);
        int decodedDirect = DecodeRoomIdFromColor(GetImageColor(image, tileX, tileY));
        int decodedFlipped = DecodeRoomIdFromColor(GetImageColor(image, tileX, _height - 1 - tileY));
        UnloadImage(image);

        int cpu = DecodeRoomId(tileX, tileY);
        if (decodedDirect == cpu)
            return decodedDirect;

        if (decodedFlipped == cpu)
            return decodedFlipped;

        return decodedDirect;
    }

    private static int DecodeRoomIdFromColor(Color color)
    {
        byte pixel = color.R;
        return pixel == 0 ? -1 : 254 - pixel;
    }

    private bool TryGetIndex(int tileX, int tileY, out int index)
    {
        if (tileX < 0 || tileX >= _width || tileY < 0 || tileY >= _height)
        {
            index = -1;
            return false;
        }

        index = LevelData.GetIndex(tileX, tileY, _width);
        return true;
    }

    public void Update(MapData mapData, IReadOnlyList<Door> doors, LevelRoomMap roomMap)
    {
        int width = mapData.Width;
        int height = mapData.Height;
        int tileCount = width * height;

        if (_width != width || _height != height || _occlusionPixels.Length != tileCount)
        {
            DisposeTextures();
            _width = width;
            _height = height;
            _occlusionPixels = new byte[tileCount];
            _roomPixels = new byte[tileCount];
            _lastUploadedOcclusion = new byte[tileCount];
            _lastUploadedRoom = new byte[tileCount];
            _occlusionColors = new Color[tileCount];
            _roomColors = new Color[tileCount];
            _forceOcclusionUpload = true;
            _forceRoomUpload = true;
        }

        FillOcclusionPixels(mapData, doors);
        FillRoomPixels(roomMap);

        UploadTextureIfChanged(
            ref _occlusionTexture,
            ref _hasOcclusionTexture,
            _occlusionPixels,
            _occlusionColors,
            _lastUploadedOcclusion,
            ref _forceOcclusionUpload);

        UploadTextureIfChanged(
            ref _roomTexture,
            ref _hasRoomTexture,
            _roomPixels,
            _roomColors,
            _lastUploadedRoom,
            ref _forceRoomUpload,
            pointFilter: true);
    }

    private void UploadTextureIfChanged(
        ref Texture2D texture,
        ref bool hasTexture,
        byte[] pixels,
        Color[] colors,
        byte[] lastUploaded,
        ref bool forceUpload,
        bool pointFilter = false)
    {
        if (!forceUpload && pixels.AsSpan().SequenceEqual(lastUploaded))
            return;

        FillColorBuffer(pixels, colors);

        if (!hasTexture)
        {
            Image image = GenImageColor(_width, _height, Color.Black);
            texture = LoadTextureFromImage(image);
            UnloadImage(image);
            if (pointFilter)
                SetTextureFilter(texture, TextureFilter.Point);

            hasTexture = true;
        }

        UpdateTexture(texture, colors);
        pixels.CopyTo(lastUploaded, 0);
        forceUpload = false;
    }

    private static void FillColorBuffer(byte[] pixels, Color[] colors)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            byte value = pixels[i];
            colors[i] = value == 0 ? Color.Black : new Color(value, value, value, (byte)255);
        }
    }

    private void FillOcclusionPixels(MapData mapData, IReadOnlyList<Door> doors)
    {
        int width = mapData.Width;

        for (int y = 0; y < mapData.Height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = LevelData.GetIndex(x, y, width);
                _occlusionPixels[index] = TileBlocksLight(mapData, doors, x, y) ? (byte)255 : (byte)0;
            }
        }
    }

    private void FillRoomPixels(LevelRoomMap roomMap)
    {
        for (int i = 0; i < _roomPixels.Length; i++)
        {
            int roomId = roomMap.TileRoomId[i];
            // High values survive mediump texture sampling (roomId+1 would be near-black).
            _roomPixels[i] = roomId >= 0 ? (byte)(254 - roomId) : (byte)0;
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

    private void DisposeTextures()
    {
        if (_hasOcclusionTexture)
        {
            UnloadTexture(_occlusionTexture);
            _hasOcclusionTexture = false;
        }

        if (_hasRoomTexture)
        {
            UnloadTexture(_roomTexture);
            _hasRoomTexture = false;
        }

        _forceOcclusionUpload = true;
        _forceRoomUpload = true;
    }

    public void Dispose() => DisposeTextures();
}
