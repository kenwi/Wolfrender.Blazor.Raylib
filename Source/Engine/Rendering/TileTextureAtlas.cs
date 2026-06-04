using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Rendering;

/// <summary>Bakes per-tile <see cref="Texture2D"/> instances from <see cref="TileSpriteSheet"/>.</summary>
public static class TileTextureAtlas
{
    public static List<Texture2D> LoadFromSheet(string path)
    {
        var sheet = LoadImage(path);
        if (sheet.Width == 0 || sheet.Height == 0)
        {
            UnloadImage(sheet);
            throw new InvalidOperationException($"Failed to load tile spritesheet: {path}");
        }

        var textures = new List<Texture2D>(TileSpriteSheet.TileCount);
        for (int i = 0; i < TileSpriteSheet.TileCount; i++)
        {
            var frame = TileSpriteSheet.GetFrameRect(i);
            var crop = ImageFromImage(sheet, frame);
            var texture = LoadTextureFromImage(crop);
            UnloadImage(crop);
            textures.Add(texture);
        }

        UnloadImage(sheet);
        return textures;
    }
}
