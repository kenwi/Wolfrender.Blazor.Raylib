using System.Numerics;
using Game.Entities;
using Game.Utilities;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Systems;

public class HudSystem
{
    private readonly RenderTexture2D _hudRenderTexture;
    private const float TileSize = 4.0f;

    public HudSystem(int screenWidth, int screenHeight)
    {
        _hudRenderTexture = LoadRenderTexture(screenWidth, screenHeight);
    }

    public void Begin()
    {
        BeginTextureMode(_hudRenderTexture);
    }

    public void Render(Player player, LevelData level)
    {
        var position = player.Position;
        var velocity = player.Velocity;

        int tileX = (int)(position.X / TileSize + 0.5f);
        int tileY = (int)(position.Z / TileSize + 0.5f);

        DrawRectangle(5, 5, 400, 100, ColorAlpha(Color.SkyBlue, 0.5f));
        DrawRectangleLines(5, 5, 400, 100, Color.Blue);
        DrawText($"Camera X: {position.X / TileSize + 0.5:F2} Y: {position.Z / TileSize + 0.5:F2}",
            15, 15, 10 * 2, Color.Black);

        DrawText($"Player velocity X: {velocity.Length():F2} Y: {velocity.Z * TileSize:F2}",
            15, 35, 10 * 2, Color.Black);

        var wallTile = level.GetWallTile(tileX, tileY);
        DrawText($"Colliding {wallTile}", 15, 55, 10 * 2, Color.Black);
        
    }

    public void End()
    {
        EndTextureMode();
    }

    public void DrawToScreen(int screenWidth, int screenHeight)
    {
        DrawTexturePro(
            _hudRenderTexture.Texture,
            new Rectangle(0, 0, (float)_hudRenderTexture.Texture.Width, (float)-_hudRenderTexture.Texture.Height),
            new Rectangle(0, 0, (float)_hudRenderTexture.Texture.Width, (float)_hudRenderTexture.Texture.Height),
            new Vector2(0, 0),
            0,
            Color.White);
    }

    public void Dispose()
    {
        UnloadRenderTexture(_hudRenderTexture);
    }
}

