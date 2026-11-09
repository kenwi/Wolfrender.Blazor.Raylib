using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.WorldObjects;

/// <summary>
/// Objects-layer catalog for Wolf static sprites (extractor indices 2..49).
/// <see cref="MapData.Objects"/> stores those indices; 0 is empty.
/// A runtime atlas is packed from <c>resources/sprites/</c> at startup.
/// </summary>
public static class ObjectSprites
{
    public const int FrameSize = 64;
    public const int FirstObjectId = 2;
    public const int LastObjectId = 49;
    public const int ObjectCount = LastObjectId - FirstObjectId + 1;
    public const int PaletteColumns = 8;
    public const int AtlasColumns = 8;
    public const int AtlasRows = (ObjectCount + AtlasColumns - 1) / AtlasColumns;
    public const int PaletteIconSize = 64;

    public const string SpritesDirectory = "resources/sprites";

    /// <summary>Runtime atlas size after <see cref="BuildAtlas"/>.</summary>
    public const int SheetWidth = AtlasColumns * FrameSize;
    public const int SheetHeight = AtlasRows * FrameSize;

    /// <summary>Basename (no extension) per atlas slot, object IDs 2..49 in order.</summary>
    private static readonly string[] SpriteBasenames =
    [
        "002_water_puddle",
        "003_green_barrel",
        "004_table_and_chairs",
        "005_floor_lamp",
        "006_chandelier",
        "007_hanging_skeleton",
        "008_dog_food",
        "009_pillar",
        "010_tree",
        "011_skeleton",
        "012_sink",
        "013_pot_plant",
        "014_pot",
        "015_table",
        "016_light_green_hanging",
        "017_pots_and_pans",
        "018_knight",
        "019_cage_empty",
        "020_cage_with_skeleton",
        "021_bones_pile",
        "022_gold_key",
        "023_silver_key",
        "024_bed",
        "025_basket",
        "026_food",
        "027_first_aid",
        "028_ammo",
        "029_medium_gun",
        "030_big_gun",
        "031_gold_cross",
        "032_gold_cup",
        "033_gold_chest",
        "034_gold_crown",
        "035_extra_life_orb",
        "036_blood_and_bones",
        "037_brown_barrel",
        "038_full_well",
        "039_empty_well",
        "040_blood_pool",
        "041_flag_standing",
        "042_say_aardwolf",
        "043_broken_bones",
        "044_debris",
        "045_debris_2",
        "046_pots_and_pans_2",
        "047_wood_burning_stove",
        "048_spears",
        "049_vines",
    ];

    /// <summary>
    /// Fixtures that emit tile lights. Floor lamp (5) also blocks movement;
    /// chandelier (6) and hanging green (16) do not.
    /// </summary>
    public static readonly uint[] LightObjectIds = [5, 6, 16];

    /// <summary>Wolf <c>statinfo</c> entries without the block flag (walk-through props + non-solid lights).</summary>
    private static readonly HashSet<uint> NonBlockingIds =
    [
        2,  // water puddle
        6,  // chandelier
        11, // skeleton flat
        16, // hanging green light
        17, // pots and pans
        21, // bones pile
        25, // basket
        36, // blood and bones
        40, // blood pool
        43, // broken bones
        44, // debris
        45, // debris 2
        46, // pots and pans 2
        49, // vines
    ];

    public static bool IsLightObject(uint objectId) => LightObjectIds.Contains(objectId);

    public static float CollisionRadius => LevelData.QuadSize * 0.2f;

    public static bool IsValidObjectId(uint objectId) =>
        objectId >= FirstObjectId && objectId <= LastObjectId;

    public static int ObjectIdToAtlasIndex(uint objectId) => (int)objectId - FirstObjectId;

    public static uint AtlasIndexToObjectId(int atlasIndex) =>
        (uint)(atlasIndex + FirstObjectId);

    /// <summary>Atlas cell for 0-based palette/atlas index (0 = object ID 2).</summary>
    public static Rectangle GetFrameRect(int atlasIndex)
    {
        if (atlasIndex < 0 || atlasIndex >= ObjectCount)
            return new Rectangle(0, 0, FrameSize, FrameSize);

        int col = atlasIndex % AtlasColumns;
        int row = atlasIndex / AtlasColumns;
        return new Rectangle(col * FrameSize, row * FrameSize, FrameSize, FrameSize);
    }

    public static Rectangle GetFrameRectForObjectId(uint objectId) =>
        GetFrameRect(ObjectIdToAtlasIndex(objectId));

    public static (Vector2 Uv0, Vector2 Uv1) GetFrameUv(uint objectId)
    {
        var rect = GetFrameRectForObjectId(objectId);
        return (
            new Vector2(rect.X / SheetWidth, rect.Y / SheetHeight),
            new Vector2((rect.X + rect.Width) / SheetWidth, (rect.Y + rect.Height) / SheetHeight));
    }

    public static string GetSpriteResourcePath(uint objectId)
    {
        if (!IsValidObjectId(objectId))
            return $"{SpritesDirectory}/002_water_puddle.png";
        return $"{SpritesDirectory}/{SpriteBasenames[ObjectIdToAtlasIndex(objectId)]}.png";
    }

    public static string GetSpriteResourcePathForAtlasIndex(int atlasIndex) =>
        GetSpriteResourcePath(AtlasIndexToObjectId(atlasIndex));

    /// <summary>
    /// Web palette style: one PNG per cell (not a packed sheet). Magenta key is handled by CSS filter.
    /// </summary>
    public static string GetPaletteSpriteSheetStyle(int atlasIndex)
    {
        string path = GetSpriteResourcePathForAtlasIndex(atlasIndex);
        return $"background-color:#000;background-image:url({path});" +
               "background-size:100% 100%;background-position:0 0;background-repeat:no-repeat;";
    }

    public static string GetPaletteSpriteSheetStyleForObjectId(uint objectId) =>
        GetPaletteSpriteSheetStyle(ObjectIdToAtlasIndex(objectId));

    public static bool BlocksMovement(uint objectId) =>
        IsValidObjectId(objectId) && !NonBlockingIds.Contains(objectId);

    /// <summary>
    /// Pack extractor sprites 002..049 into one atlas, remapping palette magenta to
    /// <see cref="PrimitiveRenderer.SpriteTransparencyKey"/> so existing color-key shaders match.
    /// </summary>
    public static Texture2D BuildAtlas(Func<string, string> resolvePath)
    {
        Image atlas = GenImageColor(SheetWidth, SheetHeight, new Color(0, 0, 0, 0));
        ImageFormat(ref atlas, PixelFormat.UncompressedR8G8B8A8);

        Color key = PrimitiveRenderer.SpriteTransparencyKey;

        for (int i = 0; i < ObjectCount; i++)
        {
            string path = resolvePath(GetSpriteResourcePathForAtlasIndex(i));
            Image frame = LoadImage(path);
            if (frame.Width == 0 || frame.Height == 0)
            {
                UnloadImage(atlas);
                throw new InvalidOperationException($"Failed to load static sprite: {path}");
            }

            ImageFormat(ref frame, PixelFormat.UncompressedR8G8B8A8);
            // Corner pixel is the extractor magenta key; remap to the engine color-key.
            Color sourceKey = GetImageColor(frame, 0, 0);
            if (sourceKey.R != key.R || sourceKey.G != key.G || sourceKey.B != key.B)
                ImageColorReplace(ref frame, sourceKey, key);

            var src = new Rectangle(0, 0, frame.Width, frame.Height);
            var dst = GetFrameRect(i);
            ImageDraw(ref atlas, frame, src, dst, Color.White);
            UnloadImage(frame);
        }

        Texture2D texture = LoadTextureFromImage(atlas);
        UnloadImage(atlas);
        if (texture.Id == 0)
            throw new InvalidOperationException("Failed to create static-object sprite atlas.");
        return texture;
    }
}
