using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Hud;

/// <summary>Shared centered banner panel for transient HUD messages.</summary>
public static class HudBanner
{
    public static void DrawCenter(
        string subtitle,
        string title,
        Color accentColor,
        int screenWidth,
        int screenHeight)
    {
        const int subtitleSize = 28;
        const int titleSize = 52;

        int titleW = MeasureText(title, titleSize);
        int subtitleW = MeasureText(subtitle, subtitleSize);
        int panelW = Math.Max(titleW, subtitleW) + 80;
        int panelH = 140;
        int panelX = (screenWidth - panelW) / 2;
        int panelY = (screenHeight - panelH) / 2;

        DrawRectangle(panelX, panelY, panelW, panelH, new Color(0, 0, 0, 200));
        DrawRectangleLines(panelX, panelY, panelW, panelH, accentColor);

        DrawText(subtitle, (screenWidth - subtitleW) / 2, panelY + 16, subtitleSize, new Color(220, 220, 220, 255));
        DrawText(title, (screenWidth - titleW) / 2, panelY + 56, titleSize, accentColor);
    }
}
