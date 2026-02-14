using System.Numerics;
using Game.Entities;
using Game.Utilities;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Systems;

public class MinimapSystem
{
    private readonly LevelData _level;
    private readonly RenderSystem _renderSystem;
    private const float TileSize = 4.0f;
    private const int MinimapSize = 400; // Size of minimap in pixels
    private const int MinimapMargin = 10; // Margin from screen edge

    public MinimapSystem(LevelData level, RenderSystem renderSystem)
    {
        _level = level;
        _renderSystem = renderSystem;
    }

    public void Render(Player player)
    {
        int screenWidth = GetScreenWidth();
        int screenHeight = GetScreenHeight();
        
        // Position minimap in top-right corner
        int minimapX = screenWidth - MinimapSize - MinimapMargin;
        int minimapY = MinimapMargin;
        
        // Draw minimap background
        DrawRectangle(minimapX, minimapY, MinimapSize, MinimapSize, new Color(40, 40, 40, 255));
        DrawRectangleLines(minimapX, minimapY, MinimapSize, MinimapSize, Color.White);
        
        // Calculate scale to fit level in minimap
        float scaleX = (float)MinimapSize / _level.Width;
        float scaleY = (float)MinimapSize / _level.Height;
        float scale = Math.Min(scaleX, scaleY);
        
        // Calculate offset to center minimap
        float offsetX = minimapX + (MinimapSize - _level.Width * scale) / 2;
        float offsetY = minimapY + (MinimapSize - _level.Height * scale) / 2;
        
        // Draw all tiles (walls and floors)
        for (int x = 0; x < _level.Width; x++)
        {
            for (int y = 0; y < _level.Height; y++)
            {
                float tileX = offsetX + x * scale;
                float tileY = offsetY + y * scale;
                
                // Check if tile has wall or floor
                bool hasWall = _level.GetWallTile(x, y) > 0;
                bool hasFloor = _level.GetFloorTile(x, y) > 0;
                
                if (hasWall || hasFloor)
                {
                    // Check if this tile was rendered
                    bool isRendered = _renderSystem.RenderedTiles.Contains((x, y));
                    
                    // White if rendered, dark gray if not
                    Color tileColor = isRendered ? Color.White : new Color(60, 60, 60, 255);
                    
                    DrawRectangle(
                        (int)tileX,
                        (int)tileY,
                        (int)Math.Ceiling(scale),
                        (int)Math.Ceiling(scale),
                        tileColor
                    );
                }
            }
        }
        
        // Draw player position
        int playerTileX = (int)(player.Position.X / TileSize);
        int playerTileY = (int)(player.Position.Z / TileSize);
        
        float playerX = offsetX + playerTileX * scale;
        float playerY = offsetY + playerTileY * scale;
        
        DrawCircle((int)(playerX + scale / 2), (int)(playerY + scale / 2), scale / 3, Color.Red);
    }
}

