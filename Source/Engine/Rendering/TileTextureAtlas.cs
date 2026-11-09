using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Rendering;

/// <summary>Loads the individual texture files that make up the tile palette.</summary>
public static class TileTextureAtlas
{
    /// <summary>
    /// Load every texture in <paramref name="resourcePaths"/> in order. The list
    /// order must match the palette texture indices.
    /// </summary>
    public static List<Texture2D> LoadAll(IEnumerable<string> resourcePaths)
    {
        var textures = new List<Texture2D>();
        foreach (string path in resourcePaths)
        {
            var texture = LoadTexture(path);
            if (texture.Id == 0)
            {
                foreach (var loaded in textures)
                    UnloadTexture(loaded);
                throw new InvalidOperationException($"Failed to load tile texture: {path}");
            }

            textures.Add(texture);
        }

        return textures;
    }
}
