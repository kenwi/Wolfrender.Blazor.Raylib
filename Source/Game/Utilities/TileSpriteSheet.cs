using System.Numerics;
using Raylib_cs;

namespace Game.Utilities;

/// <summary>Sprite sheet layout for <c>spritesheet_tiles.png</c> (64×64 cells, 1px gap, 8×8 grid).</summary>
public static class TileSpriteSheet
{
    public const int FrameSize = 64;
    public const int ColumnGap = 1;
    public const int RowGap = 1;
    public const int FrameStride = FrameSize + ColumnGap;
    public const int OriginX = 1;
    public const int OriginY = 16;
    public const int Columns = 8;
    public const int Rows = 8;
    public const int TileCount = Columns * Rows;

    public const int SheetWidth = 521;
    public const int SheetHeight = 1021;
    public const string SheetPath = "resources/spritesheet_tiles.png";

    /// <summary>0-based sheet index for legacy greystone … wood (IDs 1–6).</summary>
    public const int GreystoneIndex = 0;
    public const int BluestoneIndex = 1;
    public const int ColorstoneIndex = 2;
    public const int MossyIndex = 3;
    public const int RedbrickIndex = 4;
    public const int WoodIndex = 5;

    /// <summary>0-based sheet index for light door art (sheet tile ID 57).</summary>
    public const int DoorIndex = 56;

    /// <summary>0-based sheet index for dark door art (sheet tile ID 58).</summary>
    public const int DoorDarkIndex = 57;

    public const int PaletteIconSize = 64;
    public const int PaletteColumns = 8;

    public static Rectangle GetFrameRect(int tileIndex)
    {
        int col = tileIndex % Columns;
        int row = tileIndex / Columns;
        return new Rectangle(
            OriginX + col * FrameStride,
            OriginY + row * FrameStride,
            FrameSize,
            FrameSize);
    }

    public static Rectangle GetFrameRectForTileId(uint tileId)
    {
        if (tileId == 0 || tileId > TileCount)
            return GetFrameRect(0);
        return GetFrameRect((int)tileId - 1);
    }

    public static (Vector2 Uv0, Vector2 Uv1) GetFrameUv(int tileIndex)
    {
        var rect = GetFrameRect(tileIndex);
        return (
            new Vector2(rect.X / SheetWidth, rect.Y / SheetHeight),
            new Vector2((rect.X + rect.Width) / SheetWidth, (rect.Y + rect.Height) / SheetHeight));
    }

    /// <summary>Inline CSS for a tile-sized palette cell (same math as <see cref="PickupSprites.GetPaletteSpriteSheetStyle"/>).</summary>
    public static string GetPaletteSpriteSheetStyle(int tileIndex)
    {
        var rect = GetFrameRect(tileIndex);
        float bgWidthPct = SheetWidth / (float)FrameSize * 100f;
        float bgHeightPct = SheetHeight / (float)FrameSize * 100f;
        float posXPct = rect.X / FrameSize * 100f;
        float posYPct = rect.Y / FrameSize * 100f;
        return $"background-color:#000;background-image:url({SheetPath});" +
               $"background-size:{bgWidthPct:F4}% {bgHeightPct:F4}%;" +
               $"background-position:-{posXPct:F4}% -{posYPct:F4}%;";
    }
}
