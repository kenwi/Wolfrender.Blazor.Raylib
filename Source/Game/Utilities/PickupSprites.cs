using System.Numerics;
using Game.Entities;
using Raylib_cs;

namespace Game.Utilities;

/// <summary>Sprite sheet layout for <c>Objects.png</c> (64×64 pickup frames).</summary>
public static class PickupSprites
{
    public const int FrameSize = 64;

    /// <summary>Display size for palette icons; matches tile palette buttons in the editor.</summary>
    public const int PaletteIconSize = 64;

    /// <summary>Column count for tile and pickup palette grids.</summary>
    public const int PaletteColumns = 3;
    public const int SheetWidth = 261;
    public const int SheetHeight = 1053;
    public const string SheetPath = "resources/Objects.png";

    /// <summary>Index of <see cref="SheetPath"/> in <see cref="MapData.GameTextures"/> (<see cref="Game.GameTextureIndex.Objects"/>).</summary>
    public const int ObjectsTextureIndex = Game.GameTextureIndex.Objects;

    /// <summary>Matches <see cref="PrimitiveRenderer.SpriteTransparencyKey"/> (#980088).</summary>
    public const string TransparencyKeyHex = "#980088";

    public const int PlaceholderX = 2;
    public const int PlaceholderY = 35;

    public const int HealthX = 66;
    public const int HealthY = 748;

    public const int AmmoX = 1;
    public const int AmmoY = 828;

    public const int MachineGunX = 66;
    public const int MachineGunY = 828;

    /// <summary>Floor pickup icon on <see cref="SheetPath"/> (placeholder column beside MG).</summary>
    public const int ChainGunX = 131;
    public const int ChainGunY = 828;

    public const int GoldKeyX = 1;
    public const int GoldKeyY = 988;

    public const int SilverKeyX = 66;
    public const int SilverKeyY = 988;

    public static Rectangle GetFrameRect(PickupType type) => type switch
    {
        PickupType.Health => new Rectangle(HealthX, HealthY, FrameSize, FrameSize),
        PickupType.Ammo => new Rectangle(AmmoX, AmmoY, FrameSize, FrameSize),
        PickupType.MachineGun => new Rectangle(MachineGunX, MachineGunY, FrameSize, FrameSize),
        PickupType.ChainGun => new Rectangle(ChainGunX, ChainGunY, FrameSize, FrameSize),
        PickupType.GoldKey => new Rectangle(GoldKeyX, GoldKeyY, FrameSize, FrameSize),
        PickupType.SilverKey => new Rectangle(SilverKeyX, SilverKeyY, FrameSize, FrameSize),
        _ => new Rectangle(PlaceholderX, PlaceholderY, FrameSize, FrameSize)
    };

    /// <summary>UV rectangle (0–1) for drawing a pickup frame from <see cref="SheetPath"/>.</summary>
    public static (Vector2 Uv0, Vector2 Uv1) GetFrameUv(PickupType type)
    {
        var rect = GetFrameRect(type);
        return (
            new Vector2(rect.X / SheetWidth, rect.Y / SheetHeight),
            new Vector2((rect.X + rect.Width) / SheetWidth, (rect.Y + rect.Height) / SheetHeight));
    }

    /// <summary>
    /// Inline CSS for the sprite sheet layer inside a tile-sized palette cell.
    /// Uses percentages so the sprite scales with the parent (same size as tile palette buttons).
    /// </summary>
    public static string GetPaletteSpriteSheetStyle(PickupType type)
    {
        var rect = GetFrameRect(type);
        float bgWidthPct = SheetWidth / (float)FrameSize * 100f;
        float bgHeightPct = SheetHeight / (float)FrameSize * 100f;
        float posXPct = rect.X / FrameSize * 100f;
        float posYPct = rect.Y / FrameSize * 100f;
        return $"background-color:#000;background-image:url({SheetPath});" +
               $"background-size:{bgWidthPct:F4}% {bgHeightPct:F4}%;" +
               $"background-position:-{posXPct:F4}% -{posYPct:F4}%;";
    }
}
