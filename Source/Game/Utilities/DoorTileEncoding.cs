using Game.Entities;

namespace Game.Utilities;

public enum DoorLockKind
{
    None,
    Gold,
    Silver
}

/// <summary>
/// Door layer tile IDs: 7/8 = normal H/V doors; 9–12 = locked variants (see <see cref="DoorLockKind"/>).
/// </summary>
public static class DoorTileEncoding
{
    public const uint Horizontal = 7;
    public const uint Vertical = 8;
    public const uint HorizontalGold = 9;
    public const uint VerticalGold = 10;
    public const uint HorizontalSilver = 11;
    public const uint VerticalSilver = 12;

    public const string DoorTexturePath = "resources/door.png";

    /// <summary>Index of <see cref="DoorTexturePath"/> in <see cref="MapData.Textures"/>.</summary>
    public const int DoorTextureIndex = 6;

    /// <summary>Column count for the door palette grid.</summary>
    public const int PaletteColumns = 2;

    public static readonly PaletteEntry[] PaletteEntries =
    {
        new(Horizontal, "Door H", "Normal door", false, DoorLockKind.None),
        new(Vertical, "Door V", "Normal door rotated 90°", true, DoorLockKind.None),
        new(HorizontalGold, "Gold H", "Door with G circle on top", false, DoorLockKind.Gold),
        new(VerticalGold, "Gold V", "Door with G circle on top rotated 90°", true, DoorLockKind.Gold),
        new(HorizontalSilver, "Silver H", "Door with S circle on top", false, DoorLockKind.Silver),
        new(VerticalSilver, "Silver V", "Door with S circle on top rotated 90°", true, DoorLockKind.Silver),
    };

    public readonly record struct PaletteEntry(
        uint Id, string ShortLabel, string Description, bool Vertical, DoorLockKind LockKind);

    public static bool IsDoorTile(uint value) => value > 0 && TryParse(value, out _, out _);

    public static bool TryParse(uint tileValue, out DoorRotation rotation, out DoorLockKind lockKind)
    {
        lockKind = DoorLockKind.None;
        switch (tileValue)
        {
            case Horizontal:
                rotation = DoorRotation.HORIZONTAL;
                return true;
            case Vertical:
                rotation = DoorRotation.VERTICAL;
                return true;
            case HorizontalGold:
                rotation = DoorRotation.HORIZONTAL;
                lockKind = DoorLockKind.Gold;
                return true;
            case VerticalGold:
                rotation = DoorRotation.VERTICAL;
                lockKind = DoorLockKind.Gold;
                return true;
            case HorizontalSilver:
                rotation = DoorRotation.HORIZONTAL;
                lockKind = DoorLockKind.Silver;
                return true;
            case VerticalSilver:
                rotation = DoorRotation.VERTICAL;
                lockKind = DoorLockKind.Silver;
                return true;
            default:
                if (Enum.IsDefined(typeof(DoorRotation), (int)tileValue))
                {
                    rotation = (DoorRotation)tileValue;
                    return true;
                }

                rotation = default;
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
