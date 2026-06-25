using System.Numerics;
using Game.Core.Level;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Hud;

/// <summary>Maps window-space coordinates to the internal game render resolution.</summary>
public static class GameRenderSpace
{
    public static int InternalWidth => RenderData.InternalWidth;
    public static int InternalHeight => RenderData.InternalHeight;

    public static Vector2 WindowToInternal(Vector2 windowPoint, int windowWidth, int windowHeight)
    {
        if (windowWidth <= 0 || windowHeight <= 0)
            return windowPoint;

        return new Vector2(
            windowPoint.X * RenderData.InternalWidth / windowWidth,
            windowPoint.Y * RenderData.InternalHeight / windowHeight);
    }

    public static void DrawTextureToWindow(Texture2D texture, int windowWidth, int windowHeight)
    {
        DrawTexturePro(
            texture,
            new Rectangle(0, 0, texture.Width, -texture.Height),
            new Rectangle(0, 0, windowWidth, windowHeight),
            Vector2.Zero,
            0,
            Color.White);
    }
}
