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

    public static string GetPaletteLabel(uint tileId) => tileId switch
    {
        Horizontal => "Door H",
        Vertical => "Door V",
        HorizontalGold => "Gold H",
        VerticalGold => "Gold V",
        HorizontalSilver => "Silver H",
        VerticalSilver => "Silver V",
        _ => $"ID {tileId}"
    };
}
