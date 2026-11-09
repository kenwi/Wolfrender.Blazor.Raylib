using Game.Core.Level;
using Game.Engine.Rendering;

namespace Game.Features.Doors;

public enum DoorLockKind
{
    None,
    Gold,
    Silver
}

/// <summary>
/// Door layer tile IDs 1001–1010: one entry per extracted texture whose name
/// contains "door". The tile ID no longer encodes a horizontal/vertical
/// variant; <see cref="DetectRotation"/> derives the orientation from the
/// walls next to the door tile.
/// </summary>
public static class DoorTileEncoding
{
    /// <summary>Engine-facing encoding probe (avoids rendering importing this feature type graph).</summary>
    public static readonly IDoorTileEncoding ForEngine = new EngineEncoding();

    public const uint FirstId = 1001;
    public const uint DefaultId = 1003; // 098_door.png
    public const uint ElevatorId = 1007; // 102_grey_door_red_light_arrows.png
    public const uint LockedId = 1009; // 104_grey_locked_door.png
    public const int PaletteColumns = 2;

    public static readonly PaletteEntry[] PaletteEntries =
    {
        new(1001, "Red Arrow Door", "Grey door with red arrow",
            DoorLockKind.None, "024_grey_door_red_arrows.png", WallTilePalette.Count + 0),
        new(1002, "Red Arrow Door Dark", "Dark grey door with red arrow",
            DoorLockKind.None, "025_grey_door_red_arrows_dark.png", WallTilePalette.Count + 1),
        new(1003, "Door", "Blue steel door",
            DoorLockKind.None, "098_door.png", WallTilePalette.Count + 2),
        new(1004, "Door Dark", "Dark blue steel door",
            DoorLockKind.None, "099_door_dark.png", WallTilePalette.Count + 3),
        new(1005, "Door Side", "Blue steel door side panel",
            DoorLockKind.None, "100_door_side.png", WallTilePalette.Count + 4),
        new(1006, "Door Side Dark", "Dark blue steel door side panel",
            DoorLockKind.None, "101_door_side_dark.png", WallTilePalette.Count + 5),
        new(1007, "Red Light Door", "Grey door with red light arrows",
            DoorLockKind.None, "102_grey_door_red_light_arrows.png", WallTilePalette.Count + 6),
        new(1008, "Red Light Door Dark", "Dark grey door with red light arrows",
            DoorLockKind.None, "103_grey_door_red_light_arrows_dark.png", WallTilePalette.Count + 7),
        new(1009, "Locked Door", "Grey locked door; needs the gold key",
            DoorLockKind.Gold, "104_grey_locked_door.png", WallTilePalette.Count + 8),
        new(1010, "Locked Door Dark", "Dark grey locked door; needs the gold key",
            DoorLockKind.Gold, "105_grey_locked_door_dark.png", WallTilePalette.Count + 9),
    };

    public readonly record struct PaletteEntry(
        uint Id,
        string ShortLabel,
        string Description,
        DoorLockKind LockKind,
        string FileName,
        int TextureIndex)
    {
        public string ResourcePath => $"{WallTilePalette.TextureDirectory}/{FileName}";
        public string PaletteStyle =>
            $"background-color:#000;background-image:url({ResourcePath});" +
            "background-size:100% 100%;background-repeat:no-repeat;";
    }

    public readonly record struct DoorTileInfo(
        DoorLockKind LockKind,
        int TextureIndex);

    public static bool IsDoorTile(uint value) =>
        value >= FirstId && value < FirstId + PaletteEntries.Length;

    public static bool TryParse(uint tileValue, out DoorTileInfo info)
    {
        foreach (var entry in PaletteEntries)
        {
            if (entry.Id != tileValue)
                continue;

            info = new DoorTileInfo(entry.LockKind, entry.TextureIndex);
            return true;
        }

        info = default;
        return false;
    }

    /// <summary>
    /// Horizontal when the door is framed by walls on its left and right (X-1/X+1);
    /// vertical when it is framed above and below (Y-1/Y+1).
    /// </summary>
    public static DoorRotation DetectRotation(MapData mapData, int tileX, int tileY)
    {
        bool wallLeft = mapData.GetTile(mapData.Walls, tileX - 1, tileY) != 0;
        bool wallRight = mapData.GetTile(mapData.Walls, tileX + 1, tileY) != 0;
        bool wallUp = mapData.GetTile(mapData.Walls, tileX, tileY - 1) != 0;
        bool wallDown = mapData.GetTile(mapData.Walls, tileX, tileY + 1) != 0;

        if (wallLeft && wallRight)
            return DoorRotation.HORIZONTAL;

        if (wallUp && wallDown)
            return DoorRotation.VERTICAL;

        // While a door is being painted there may not be walls on both sides
        // yet. Use whichever axis has a wall, then fall back to horizontal.
        if (!(wallLeft || wallRight) && (wallUp || wallDown))
            return DoorRotation.VERTICAL;

        return DoorRotation.HORIZONTAL;
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

    public static string GetPaletteStyle(uint tileId)
    {
        foreach (var entry in PaletteEntries)
        {
            if (entry.Id == tileId)
                return entry.PaletteStyle;
        }
        return "background-color:#000;";
    }

    private sealed class EngineEncoding : IDoorTileEncoding
    {
        public bool IsDoorTile(uint doorLayerValue) => DoorTileEncoding.IsDoorTile(doorLayerValue);
    }
}
