namespace Game.Engine.Rendering;

/// <summary>
/// Wall, floor and ceiling tile palette: one entry per extracted Wolfenstein 3D texture.
///
/// Tile IDs are 1-based and stable. <see cref="Entry.TextureIndex"/> indexes the
/// combined tile texture list in <c>MapData.TileTextures</c>; wall entries come
/// first, followed by the door entries from <c>DoorTileEncoding</c>.
/// </summary>
public static class WallTilePalette
{
    public const string TextureDirectory = "resources/textures";
    public const int PaletteColumns = 8;
    public const int PaletteIconSize = 64;

    // Every extracted texture whose name does not contain "door".
    private static readonly string[] FileNames =
    {
        "000_grey_stone.png",
        "001_grey_stone_dark.png",
        "002_grey_stone_2.png",
        "003_grey_stone_2_dark.png",
        "004_grey_stone_banner.png",
        "005_grey_stone_banner_dark.png",
        "006_grey_stone_hitler_portrait.png",
        "007_grey_stone_hitler_portrait_dark.png",
        "008_blue_stone_bars.png",
        "009_blue_stone_bars_dark.png",
        "010_stone_eagle.png",
        "011_stone_eagle_dark.png",
        "012_blue_stone_bars_bones.png",
        "013_blue_stone_bars_bones_dark.png",
        "014_blue_stone.png",
        "015_blue_stone_dark.png",
        "016_blue_stone_2.png",
        "017_blue_stone_2_dark.png",
        "018_wood_eagle_portrait.png",
        "019_wood_eagle_portrait_dark.png",
        "020_wood_hitler_portrait.png",
        "021_wood_hitler_portrait_dark.png",
        "022_wood.png",
        "023_wood_dark.png",
        "026_steel_wall_verboten.png",
        "027_steel_wall_achtung_dark.png",
        "028_steel_wall.png",
        "029_steel_wall_dark.png",
        "030_outside_daylight.png",
        "031_outside_night.png",
        "032_red_brick.png",
        "033_red_brick_dark.png",
        "034_red_brick_gold_wreath.png",
        "035_red_brick_gold_wreath_dark.png",
        "036_purple_mud.png",
        "037_purple_mud_dark.png",
        "038_phoenix_red_brick.png",
        "039_phoenix_red_brick_dark.png",
        "040_elevator_side.png",
        "041_elevator_switch_down.png",
        "042_blank_elevator.png",
        "043_elevator_switch_up.png",
        "044_wood_cross_gold.png",
        "045_wood_cross_gold_dark.png",
        "046_grey_stone_yellow.png",
        "047_grey_stone_yellow_dark.png",
        "048_purple_mud_blood.png",
        "049_purple_mud_blood_dark.png",
        "050_grey_stone_yellow_2.png",
        "051_grey_stone_yellow_2_dark.png",
        "052_grey_stone_3.png",
        "053_grey_stone_3_dark.png",
        "054_grey_stone_verboten.png",
        "055_grey_stone_achtung_dark.png",
        "056_sandstone.png",
        "057_sandstone_dark.png",
        "058_sandstone_blood.png",
        "059_sandstone_blood_metal_dark.png",
        "060_sandstone_blood_2.png",
        "061_sandstone_blood_2_dark.png",
        "062_sandstone_blood_3.png",
        "063_sandstone_blood_3_dark.png",
        "064_grey_hitler_mosaic.png",
        "065_grey_hitler_mosaic_dark.png",
        "066_blue_skull.png",
        "067_blue_skull_dark.png",
        "068_grey_brick.png",
        "069_grey_brick_dark.png",
        "070_blue_swastika.png",
        "071_blue_swastika_dark.png",
        "072_grey_brick_hole.png",
        "073_grey_brick_hole_dark.png",
        "074_red_brown_grey_brick.png",
        "075_red_brown_grey_brick_dark.png",
        "076_grey_brick_crack.png",
        "077_grey_brick_crack_dark.png",
        "078_blue_blank.png",
        "079_blue_blank_dark.png",
        "080_blue_stone_verboten.png",
        "081_blue_stone_achtung.png",
        "082_sand_squares.png",
        "083_sand_squares_dark.png",
        "084_grey_brick_map.png",
        "085_grey_brick_map_dark.png",
        "086_sand_bricklike.png",
        "087_sand_bricklike_dark.png",
        "088_sand_bricklike_2.png",
        "089_sand_bricklike_2_dark.png",
        "090_sand_squares_2.png",
        "091_sand_squares_2_dark.png",
        "092_sand_squares_banner.png",
        "093_sand_squares_banner_dark.png",
        "094_sand_blank.png",
        "095_sand_blank_dark.png",
        "096_grey_brick_hitler_portrait.png",
        "097_grey_brick_hitler_portrait_dark.png",
    };

    // Wolf3D wall tile 13 selects VSWAP chunks 24/25, whose filenames contain
    // "door". They stay out of the visible wall palette, but maps still need
    // to reference them, so they get map-only wall IDs just past the palette.
    private static readonly Entry[] MapOnlyEntries =
    {
        new((uint)FileNames.Length + 1, "grey door red arrows", "024_grey_door_red_arrows.png"),
        new((uint)FileNames.Length + 2, "grey door red arrows dark", "025_grey_door_red_arrows_dark.png"),
    };

    public readonly record struct Entry(uint Id, string Name, string FileName)
    {
        public int TextureIndex => (int)Id - 1;
        public string ResourcePath => $"{TextureDirectory}/{FileName}";
    }

    public static readonly Entry[] Entries = BuildEntries();

    private static readonly Dictionary<int, uint> IdBySourceChunk = BuildIdBySourceChunk();

    public static int Count => Entries.Length;

    /// <summary>Largest valid wall tile ID, including map-only aliases.</summary>
    public static int MaxTileId => Entries.Length + MapOnlyEntries.Length;

    private static Entry[] BuildEntries()
    {
        var entries = new Entry[FileNames.Length];
        for (int i = 0; i < FileNames.Length; i++)
            entries[i] = new Entry((uint)(i + 1), NameFromFile(FileNames[i]), FileNames[i]);
        return entries;
    }

    private static Dictionary<int, uint> BuildIdBySourceChunk()
    {
        var ids = new Dictionary<int, uint>();
        foreach (var entry in EnumerateAllEntries())
        {
            int underscore = entry.FileName.IndexOf('_');
            if (underscore == 3
                && int.TryParse(entry.FileName.AsSpan(0, 3), out int chunk))
            {
                ids[chunk] = entry.Id;
            }
        }

        return ids;
    }

    private static IEnumerable<Entry> EnumerateAllEntries()
    {
        foreach (var entry in Entries)
            yield return entry;
        foreach (var entry in MapOnlyEntries)
            yield return entry;
    }

    private static string NameFromFile(string fileName)
    {
        string stem = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^4]
            : fileName;
        int underscore = stem.IndexOf('_');
        if (underscore >= 0 && underscore + 1 < stem.Length)
            stem = stem[(underscore + 1)..];
        return stem.Replace('_', ' ');
    }

    public static bool IsWallTile(uint tileId) => tileId >= 1 && tileId <= MaxTileId;

    public static int GetTextureIndex(uint tileId) =>
        TryGetEntry(tileId, out var entry) ? entry.TextureIndex : -1;

    public static bool TryGetEntry(uint tileId, out Entry entry)
    {
        if (tileId >= 1 && tileId <= Entries.Length)
        {
            entry = Entries[(int)tileId - 1];
            return true;
        }

        int aliasIndex = (int)tileId - Entries.Length - 1;
        if (aliasIndex >= 0 && aliasIndex < MapOnlyEntries.Length)
        {
            entry = MapOnlyEntries[aliasIndex];
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>Resolve a VSWAP wall texture chunk (0..97) to its wall tile ID.</summary>
    public static uint GetIdForChunk(int chunk) =>
        IdBySourceChunk.TryGetValue(chunk, out uint id) ? id : 0;

    /// <summary>Resolve an extracted texture filename (e.g. <c>068_grey_brick.png</c>) to a tile ID.</summary>
    public static uint GetIdByFileName(string fileName)
    {
        foreach (var entry in EnumerateAllEntries())
        {
            if (string.Equals(entry.FileName, fileName, StringComparison.OrdinalIgnoreCase))
                return entry.Id;
        }

        return 0;
    }

    public static string GetName(uint tileId) =>
        TryGetEntry(tileId, out var entry) ? entry.Name : $"ID {tileId}";

    public static string GetPaletteStyle(uint tileId) =>
        TryGetEntry(tileId, out var entry)
            ? $"background-color:#000;background-image:url({entry.ResourcePath});" +
              "background-size:100% 100%;background-repeat:no-repeat;"
            : "background-color:#000;";

    /// <summary>Find a wall tile by its human-readable name (e.g. <c>elevator_switch_down</c>).</summary>
    public static uint GetIdByName(string name)
    {
        string normalized = name.Replace('_', ' ');
        foreach (var entry in EnumerateAllEntries())
        {
            if (string.Equals(entry.Name, normalized, StringComparison.OrdinalIgnoreCase))
                return entry.Id;
        }

        return 0;
    }
}
