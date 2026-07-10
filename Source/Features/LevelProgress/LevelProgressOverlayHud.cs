using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.LevelProgress;

/// <summary>Score, exit countdown, and level-complete intermission overlays.</summary>
public static class LevelProgressOverlayHud
{
    public static readonly Color Accent = new(255, 220, 40, 255);
    public static readonly Color Hint = new(200, 200, 200, 255);
    public static readonly Color PanelFill = new(0, 0, 0, 210);

    public const int IntermissionPanelW = 600;
    public const int IntermissionPanelH = 360;
    public const int IntermissionLeaderboardPanelW = 760;
    public const int IntermissionLineSize = 22;

    public readonly record struct IntermissionPanelLayout(
        int PanelX,
        int PanelY,
        int PanelW,
        int PanelH,
        int ContentX,
        int ContentY);

    public static void DrawScore(ScoreSystem score, int screenWidth)
    {
        const int fontSize = 20;
        var label = $"SCORE: {score.LevelScore}";
        int labelWidth = MeasureText(label, fontSize);
        DrawText(label, screenWidth - labelWidth - 10, 40, fontSize, Accent);
    }

    public static void DrawExitCountdown(float secondsRemaining, int screenWidth, int screenHeight) =>
        Hud.HudBanner.DrawCenter(
            "STAND BY",
            secondsRemaining > 0f ? $"EXIT IN {(int)MathF.Ceiling(secondsRemaining)}" : "EXITING",
            new Color(80, 220, 120, 255),
            screenWidth,
            screenHeight);

    public static IntermissionPanelLayout DrawIntermissionFrame(
        int screenWidth,
        int screenHeight,
        string title,
        int panelWidth = IntermissionPanelW,
        int panelHeight = IntermissionPanelH)
    {
        int panelX = (screenWidth - panelWidth) / 2;
        int panelY = (screenHeight - panelHeight) / 2;

        DrawRectangle(panelX, panelY, panelWidth, panelHeight, PanelFill);
        DrawRectangleLines(panelX, panelY, panelWidth, panelHeight, Accent);

        const int titleSize = 40;
        int titleW = MeasureText(title, titleSize);
        DrawText(title, (screenWidth - titleW) / 2, panelY + 20, titleSize, Accent);

        return new IntermissionPanelLayout(
            panelX,
            panelY,
            panelWidth,
            panelHeight,
            panelX + 40,
            panelY + 80);
    }

    public static void DrawIntermissionHint(string text, in IntermissionPanelLayout layout, int screenWidth, int fontSize = IntermissionLineSize)
    {
        int hintW = MeasureText(text, fontSize);
        DrawText(text, (screenWidth - hintW) / 2, layout.PanelY + layout.PanelH - 48, fontSize, Hint);
    }

    public static void DrawLevelComplete(
        ScoreSystem score,
        int screenWidth,
        int screenHeight,
        bool showRestartHint = true,
        string? restartHint = null)
    {
        var layout = DrawIntermissionFrame(screenWidth, screenHeight, "LEVEL COMPLETE");

        int y = layout.ContentY;
        void Line(string text)
        {
            DrawText(text, layout.ContentX, y, IntermissionLineSize, Color.RayWhite);
            y += 28;
        }

        int minutes = (int)score.ElapsedActiveSeconds / 60;
        int seconds = (int)score.ElapsedActiveSeconds % 60;

        Line($"KILLS: {(int)score.KillRatio}% ({score.Kills}/{score.TotalKillableEnemies})");
        Line($"TREASURE: {(int)score.TreasureRatio}% ({score.TreasuresCollected}/{score.TotalTreasures})");
        Line($"SECRETS: {(int)score.SecretRatio}% ({score.SecretsFound}/{score.TotalSecrets})");
        Line($"TIME: {minutes}:{seconds:D2}");
        Line($"BASE SCORE: {score.LevelScore}");
        Line($"BONUS: {score.CompletionBonus}");
        Line($"FINAL SCORE: {score.FinalScore}");

        if (showRestartHint)
            DrawIntermissionHint(restartHint ?? "Press R or click to continue", layout, screenWidth);
    }
}
