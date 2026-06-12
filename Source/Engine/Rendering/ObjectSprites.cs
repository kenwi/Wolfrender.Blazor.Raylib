using System.Numerics;
using Game.Features.Pickups;
using Game.Features.WorldObjects;
using Raylib_cs;

namespace Game.Engine.Rendering;

/// <summary>
/// Sprite sheet layout for blocking placed objects on <c>Objects.png</c>
/// (4×5 grid of 64×64 frames starting at 1, 309).
/// Pickup frames on the same sheet are defined in <see cref="PickupSprites"/>.
/// </summary>
public static class ObjectSprites
{
    public const int FrameSize = 64;
    public const int ColumnGap = 1;
    public const int RowGap = 1;
    public const int FrameStride = FrameSize + ColumnGap;
    public const int OriginX = 1;
    public const int OriginY = 309;
    public const int Columns = 4;
    public const int Rows = 5;
    public const int ObjectCount = Columns * Rows;
    public const int PaletteColumns = 4;

    /// <summary>Display size for palette icons; matches tile palette buttons in the editor.</summary>
    public const int PaletteIconSize = 64;

    public const string SheetPath = PickupSprites.SheetPath;
    public const int SheetWidth = PickupSprites.SheetWidth;
    public const int SheetHeight = PickupSprites.SheetHeight;

    /// <summary>World-space blocking radius from tile anchor (half of 4×4 sprite width).</summary>
    public static float CollisionRadius => LevelData.QuadSize * 0.2f;

    /// <summary>0-based spritesheet cell index (column-major).</summary>
    public static Rectangle GetFrameRect(int objectIndex)
    {
        if (objectIndex < 0 || objectIndex >= ObjectCount)
            return new Rectangle(OriginX, OriginY, FrameSize, FrameSize);

        int col = objectIndex % Columns;
        int row = objectIndex / Columns;
        return new Rectangle(
            OriginX + col * FrameStride,
            OriginY + row * FrameStride,
            FrameSize,
            FrameSize);
    }

    /// <summary>1-based object ID stored in <see cref="MapData.Objects"/> (1..20).</summary>
    public static Rectangle GetFrameRectForObjectId(uint objectId) =>
        GetFrameRect((int)objectId - 1);

    /// <summary>UV rectangle (0–1) for drawing an object frame from <see cref="SheetPath"/>.</summary>
    public static (Vector2 Uv0, Vector2 Uv1) GetFrameUv(uint objectId)
    {
        var rect = GetFrameRectForObjectId(objectId);
        return (
            new Vector2(rect.X / SheetWidth, rect.Y / SheetHeight),
            new Vector2((rect.X + rect.Width) / SheetWidth, (rect.Y + rect.Height) / SheetHeight));
    }

    /// <summary>
    /// Inline CSS for the sprite sheet layer inside a tile-sized palette cell.
    /// Uses percentages so the sprite scales with the parent (same technique as <see cref="PickupSprites"/>).
    /// </summary>
    public static string GetPaletteSpriteSheetStyle(int objectIndex)
    {
        var rect = GetFrameRect(objectIndex);
        float bgWidthPct = SheetWidth / (float)FrameSize * 100f;
        float bgHeightPct = SheetHeight / (float)FrameSize * 100f;
        float posXPct = rect.X / FrameSize * 100f;
        float posYPct = rect.Y / FrameSize * 100f;
        return $"background-color:#000;background-image:url({SheetPath});" +
               $"background-size:{bgWidthPct:F4}% {bgHeightPct:F4}%;" +
               $"background-position:-{posXPct:F4}% -{posYPct:F4}%;";
    }

    public static string GetPaletteSpriteSheetStyleForObjectId(uint objectId) =>
        GetPaletteSpriteSheetStyle((int)objectId - 1);

    public static bool IsValidObjectId(uint objectId) =>
        objectId >= 1 && objectId <= ObjectCount;

    /// <summary>Objects that occupy the grid for rendering but do not block movement or pathfinding.</summary>
    public static bool BlocksMovement(uint objectId) =>
        IsValidObjectId(objectId) && !LightObjectEncoding.IsLightObject(objectId);
}
