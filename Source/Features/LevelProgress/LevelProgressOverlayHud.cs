using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.LevelProgress;

/// <summary>Score, exit countdown, and level-complete intermission overlays.</summary>
public static class LevelProgressOverlayHud
{
    public static void DrawScore(ScoreSystem score, int screenWidth)
    {
        const int fontSize = 20;
        var label = $"SCORE: {score.LevelScore}";
        int labelWidth = MeasureText(label, fontSize);
        DrawText(label, screenWidth - labelWidth - 10, 40, fontSize, new Color(255, 220, 40, 255));
    }

    public static void DrawExitCountdown(float secondsRemaining, int screenWidth, int screenHeight) =>
        Hud.HudBanner.DrawCenter(
            "STAND BY",
            secondsRemaining > 0f ? $"EXIT IN {(int)MathF.Ceiling(secondsRemaining)}" : "EXITING",
            new Color(80, 220, 120, 255),
            screenWidth,
            screenHeight);

    public static void DrawLevelComplete(ScoreSystem score, int screenWidth, int screenHeight, bool showRestartHint = true)
    {
        const int panelW = 560;
        const int panelH = 360;
        int panelX = (screenWidth - panelW) / 2;
        int panelY = (screenHeight - panelH) / 2;

        DrawRectangle(panelX, panelY, panelW, panelH, new Color(0, 0, 0, 210));
        DrawRectangleLines(panelX, panelY, panelW, panelH, new Color(255, 220, 40, 255));

        const string title = "LEVEL COMPLETE";
        const int titleSize = 40;
        int titleW = MeasureText(title, titleSize);
        DrawText(title, (screenWidth - titleW) / 2, panelY + 20, titleSize, new Color(255, 220, 40, 255));

        int y = panelY + 80;
        const int lineSize = 22;
        void Line(string text)
        {
            DrawText(text, panelX + 40, y, lineSize, Color.RayWhite);
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

        if (!showRestartHint)
            return;

        const string restartLine = "Press R or click to continue";
        int restartW = MeasureText(restartLine, lineSize);
        DrawText(restartLine, (screenWidth - restartW) / 2, panelY + panelH - 48, lineSize, new Color(200, 200, 200, 255));
    }
}
