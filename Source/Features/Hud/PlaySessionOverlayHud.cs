using System.Numerics;
using Game.Features.Combat;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Hud;

/// <summary>Cross-feature play session overlays (death, aim reticle).</summary>
public static class PlaySessionOverlayHud
{
    public static void DrawGameOver(int screenWidth, int screenHeight)
    {
        const int panelW = 520;
        const int panelH = 220;
        int panelX = (screenWidth - panelW) / 2;
        int panelY = (screenHeight - panelH) / 2;

        DrawRectangle(panelX, panelY, panelW, panelH, new Color(0, 0, 0, 190));
        DrawRectangleLines(panelX, panelY, panelW, panelH, new Color(220, 40, 40, 255));

        const string title = "YOU DIED";
        const int titleSize = 48;
        int titleW = MeasureText(title, titleSize);
        DrawText(title, (screenWidth - titleW) / 2, panelY + 28, titleSize, new Color(255, 60, 60, 255));

        const string restartLine = "Press R or click to restart";
        const int lineSize = 22;
        int restartW = MeasureText(restartLine, lineSize);
        DrawText(restartLine, (screenWidth - restartW) / 2, panelY + 100, lineSize, Color.RayWhite);

        const string consoleLine = "Console: type  restart  (~ or . to open)";
        int consoleW = MeasureText(consoleLine, 18);
        DrawText(consoleLine, (screenWidth - consoleW) / 2, panelY + 150, 18, new Color(200, 200, 200, 255));
    }

    public static void DrawReticle(EffectSystem effectSystem, int screenWidth, int screenHeight)
    {
        int cx = screenWidth / 2;
        int cy = screenHeight / 2;
        const float arm = 10f;
        const float gap = 5f;
        const float thick = 2f;
        var outline = new Color(0, 0, 0, 220);
        var fill = effectSystem.GetReticleColor();

        void Stroke(float x1, float y1, float x2, float y2)
        {
            DrawLineEx(new Vector2(x1, y1), new Vector2(x2, y2), thick + 1f, outline);
            DrawLineEx(new Vector2(x1, y1), new Vector2(x2, y2), thick, fill);
        }

        Stroke(cx - gap - arm, cy, cx - gap, cy);
        Stroke(cx + gap, cy, cx + gap + arm, cy);
        Stroke(cx, cy - gap - arm, cx, cy - gap);
        Stroke(cx, cy + gap, cx, cy + gap + arm);
    }
}
