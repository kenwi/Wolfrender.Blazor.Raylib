using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Options;

/// <summary>Immediate-mode Raylib pause / options overlay.</summary>
public static class OptionsMenuHud
{
    public static void Draw(int screenWidth, int screenHeight)
    {
        DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 160));

        const int panelW = 480;
        const int panelH = 280;
        int panelX = (screenWidth - panelW) / 2;
        int panelY = (screenHeight - panelH) / 2;

        DrawRectangle(panelX, panelY, panelW, panelH, new Color(20, 20, 20, 240));
        DrawRectangleLines(panelX, panelY, panelW, panelH, new Color(180, 180, 180, 255));

        const string title = "OPTIONS";
        const int titleSize = 36;
        int titleW = MeasureText(title, titleSize);
        DrawText(title, (screenWidth - titleW) / 2, panelY + 24, titleSize, Color.RayWhite);

        const int lineSize = 20;
        int hintY = panelY + 120;
        DrawCenteredLine("Game paused", screenWidth, hintY, lineSize, new Color(200, 200, 200, 255));
        DrawCenteredLine("ESC - Resume", screenWidth, hintY + 36, lineSize, Color.RayWhite);
        DrawCenteredLine("Q - Quit", screenWidth, hintY + 68, lineSize, new Color(255, 120, 120, 255));
    }

    private static void DrawCenteredLine(string text, int screenWidth, int y, int size, Color color)
    {
        int w = MeasureText(text, size);
        DrawText(text, (screenWidth - w) / 2, y, size, color);
    }
}
