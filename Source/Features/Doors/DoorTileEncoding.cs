
namespace Game.Features.Doors;

public enum DoorLockKind
{
    None,
    Gold,
    Silver
}

/// <summary>
/// Door layer tile IDs 57–68: light door art (sheet tile 57) and dark door art (sheet tile 58),
/// each with H/V and gold/silver lock variants. Legacy IDs 7–12 map to the light set.
/// </summary>
public static class DoorTileEncoding
{
    public const int LightDoorTextureIndex = TileSpriteSheet.DoorIndex;
    public const int DarkDoorTextureIndex = TileSpriteSheet.DoorDarkIndex;

    public const uint LightHorizontal = 57;
    public const uint LightVertical = 58;
    public const uint LightHorizontalGold = 59;
    public const uint LightVerticalGold = 60;
    public const uint LightHorizontalSilver = 61;
    public const uint LightVerticalSilver = 62;

    public const uint DarkHorizontal = 63;
    public const uint DarkVertical = 64;
    public const uint DarkHorizontalGold = 65;
    public const uint DarkVerticalGold = 66;
    public const uint DarkHorizontalSilver = 67;
    public const uint DarkVerticalSilver = 68;

    public const string DoorTexturePath = TileSpriteSheet.SheetPath;

    public const int PaletteColumns = 2;

    public static readonly PaletteEntry[] PaletteEntries =
    {
        new(LightHorizontal, "Light H", "Light door", false, DoorLockKind.None, LightDoorTextureIndex),
        new(LightVertical, "Light V", "Light door rotated 90°", true, DoorLockKind.None, LightDoorTextureIndex),
        new(LightHorizontalGold, "Light Gold H", "Light door, gold lock", false, DoorLockKind.Gold, LightDoorTextureIndex),
        new(LightVerticalGold, "Light Gold V", "Light door, gold lock rotated 90°", true, DoorLockKind.Gold, LightDoorTextureIndex),
        new(LightHorizontalSilver, "Light Silver H", "Light door, silver lock", false, DoorLockKind.Silver, LightDoorTextureIndex),
        new(LightVerticalSilver, "Light Silver V", "Light door, silver lock rotated 90°", true, DoorLockKind.Silver, LightDoorTextureIndex),
        new(DarkHorizontal, "Dark H", "Dark door", false, DoorLockKind.None, DarkDoorTextureIndex),
        new(DarkVertical, "Dark V", "Dark door rotated 90°", true, DoorLockKind.None, DarkDoorTextureIndex),
        new(DarkHorizontalGold, "Dark Gold H", "Dark door, gold lock", false, DoorLockKind.Gold, DarkDoorTextureIndex),
        new(DarkVerticalGold, "Dark Gold V", "Dark door, gold lock rotated 90°", true, DoorLockKind.Gold, DarkDoorTextureIndex),
        new(DarkHorizontalSilver, "Dark Silver H", "Dark door, silver lock", false, DoorLockKind.Silver, DarkDoorTextureIndex),
        new(DarkVerticalSilver, "Dark Silver V", "Dark door, silver lock rotated 90°", true, DoorLockKind.Silver, DarkDoorTextureIndex),
    };

    public readonly record struct PaletteEntry(
        uint Id,
        string ShortLabel,
        string Description,
        bool Vertical,
        DoorLockKind LockKind,
        int TextureIndex);

    public readonly record struct DoorTileInfo(
        DoorRotation Rotation,
        DoorLockKind LockKind,
        int TextureIndex);

    public static bool IsDoorTile(uint value) => value > 0 && TryParse(value, out _);

    public static bool TryParse(uint tileValue, out DoorTileInfo info)
    {
        foreach (var entry in PaletteEntries)
        {
            if (entry.Id != tileValue)
                continue;
            info = new DoorTileInfo(
                entry.Vertical ? DoorRotation.VERTICAL : DoorRotation.HORIZONTAL,
                entry.LockKind,
                entry.TextureIndex);
            return true;
        }

        if (TryParseLegacy(tileValue, out info))
            return true;

        info = default;
        return false;
    }

    public static bool TryParse(uint tileValue, out DoorRotation rotation, out DoorLockKind lockKind)
    {
        if (!TryParse(tileValue, out var info))
        {
            rotation = default;
            lockKind = default;
            return false;
        }

        rotation = info.Rotation;
        lockKind = info.LockKind;
        return true;
    }

    private static bool TryParseLegacy(uint tileValue, out DoorTileInfo info)
    {
        info = default;
        switch (tileValue)
        {
            case 7:
                info = new DoorTileInfo(DoorRotation.HORIZONTAL, DoorLockKind.None, LightDoorTextureIndex);
                return true;
            case 8:
                info = new DoorTileInfo(DoorRotation.VERTICAL, DoorLockKind.None, LightDoorTextureIndex);
                return true;
            case 9:
                info = new DoorTileInfo(DoorRotation.HORIZONTAL, DoorLockKind.Gold, LightDoorTextureIndex);
                return true;
            case 10:
                info = new DoorTileInfo(DoorRotation.VERTICAL, DoorLockKind.Gold, LightDoorTextureIndex);
                return true;
            case 11:
                info = new DoorTileInfo(DoorRotation.HORIZONTAL, DoorLockKind.Silver, LightDoorTextureIndex);
                return true;
            case 12:
                info = new DoorTileInfo(DoorRotation.VERTICAL, DoorLockKind.Silver, LightDoorTextureIndex);
                return true;
            default:
                return false;
        }
    }

    public static string GetPaletteLabel(uint tileId)
    {
        foreach (var entry in PaletteEntries)
        {
            if (entry.Id == tileId)
                return entry.ShortLabel;
        }
        return $"ID {tileId}";
    }

    public static string GetPaletteDescription(uint tileId)
    {
        foreach (var entry in PaletteEntries)
        {
            if (entry.Id == tileId)
                return entry.Description;
        }
        return $"Tile ID {tileId}";
    }
}
