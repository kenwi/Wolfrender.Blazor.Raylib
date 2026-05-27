using System.Numerics;
using Game.Entities;
using Game.Weapons;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Systems;

/// <summary>Screen-space HUD overlays drawn after the 3D scene composite.</summary>
public static class GameOverlayHud
{
    public static void DrawScore(ScoreSystem score, int screenWidth)
    {
        const int fontSize = 20;
        var label = $"SCORE: {score.LevelScore}";
        int labelWidth = MeasureText(label, fontSize);
        DrawText(label, screenWidth - labelWidth - 10, 40, fontSize, new Color(255, 220, 40, 255));
    }

    public static void DrawInventory(Player player)
    {
        const int fontSize = 18;
        int y = 68;

        var active = WeaponCatalog.Get(player.Weapons.ActiveWeapon);
        DrawText($"WEAPON: {active.DisplayName}", 10, y, fontSize, Color.RayWhite);
        y += 24;

        DrawText($"AMMO: {player.Ammo}", 10, y, fontSize, new Color(255, 220, 40, 255));
        y += 24;

        var goldColor = player.HasGoldKey ? new Color(255, 210, 40, 255) : new Color(100, 90, 50, 255);
        var silverColor = player.HasSilverKey ? new Color(200, 220, 255, 255) : new Color(90, 95, 110, 255);
        DrawText("KEYS:", 10, y, fontSize, Color.RayWhite);
        DrawText(" GOLD", 58, y, fontSize, goldColor);
        DrawText(" SILVER", 118, y, fontSize, silverColor);

        y += 24;
        DrawText("1 KNIFE  2 PISTOL  3 MG  4 CG", 10, y, 14, new Color(180, 180, 180, 255));
    }

    public static void DrawDoorLockedHint(DoorSystem doorSystem, int screenWidth, int screenHeight) =>
        DrawCenterBanner(
            "DOOR LOCKED",
            doorSystem.LockedHintOverlayText,
            doorSystem.LockedHintColor,
            screenWidth,
            screenHeight);

    public static void DrawNoAmmoHint(WeaponSystem weaponSystem, int screenWidth, int screenHeight) =>
        DrawCenterBanner(
            weaponSystem.NoAmmoHintSubtitle,
            weaponSystem.NoAmmoHintTitle,
            weaponSystem.NoAmmoHintColor,
            screenWidth,
            screenHeight);

    public static void DrawCenterBanner(
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
