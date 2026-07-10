using System.Numerics;
using Game.Features.Highscores.Shared;
using Game.Features.LevelProgress;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Highscores;

public static class HighscoreLeaderboardHud
{
    private const string ViewReplayLabel = "View replay";
    private static readonly List<(int Rank, Rectangle Button)> ReplayButtons = new();

    public readonly record struct HighlightMatch(
        string? PlayerName,
        int? FinalScore,
        float? ElapsedSeconds);

    public static void Draw(
        IReadOnlyList<HighscoreEntry> entries,
        int screenWidth,
        int screenHeight,
        bool loading,
        string? statusMessage,
        HighlightMatch? highlight,
        string footerHint)
    {
        ReplayButtons.Clear();

        var layout = LevelProgressOverlayHud.DrawIntermissionFrame(
            screenWidth,
            screenHeight,
            "HIGH SCORES",
            LevelProgressOverlayHud.IntermissionLeaderboardPanelW,
            LevelProgressOverlayHud.IntermissionPanelH);

        const int lineSize = LevelProgressOverlayHud.IntermissionLineSize - 4;
        var highlightColor = new Color(120, 220, 120, 255);

        int y = layout.ContentY;

        if (loading)
        {
            DrawText(statusMessage ?? "Loading leaderboard...", layout.ContentX, y, lineSize, Color.RayWhite);
            LevelProgressOverlayHud.DrawIntermissionHint(footerHint, layout, screenWidth, lineSize);
            return;
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            DrawText(statusMessage, layout.ContentX, y, lineSize, new Color(255, 140, 140, 255));
            y += 24;
        }

        if (entries.Count == 0)
        {
            DrawText("No scores yet.", layout.ContentX, y, lineSize, Color.RayWhite);
        }
        else
        {
            int contentLeft = layout.ContentX;
            int contentRight = layout.PanelX + layout.PanelW - 40;
            const int columnGap = 16;
            const int buttonHeight = 22;

            int rankColWidth = MeasureText($"{entries.Max(e => e.Rank)}.", lineSize);
            int maxTimeWidth = MeasureText(HighscoreTimeFormat.Format(5999.99f), lineSize);
            int buttonWidth = MeasureText(ViewReplayLabel, lineSize - 2) + 16;
            int replayColRight = contentRight;
            int timeColRight = replayColRight - buttonWidth - columnGap;
            int scoreColRight = timeColRight - maxTimeWidth - columnGap;
            int nameColLeft = contentLeft + rankColWidth + columnGap;

            foreach (var entry in entries)
            {
                bool highlightEntry = highlight is { } match
                    && match.FinalScore.HasValue
                    && entry.FinalScore == match.FinalScore.Value
                    && string.Equals(entry.PlayerName, match.PlayerName, StringComparison.Ordinal)
                    && MathF.Abs(entry.ElapsedSeconds - (match.ElapsedSeconds ?? 0f)) < 0.005f;

                var color = highlightEntry ? highlightColor : Color.RayWhite;

                DrawText($"{entry.Rank}.", contentLeft, y, lineSize, color);
                DrawText(entry.PlayerName, nameColLeft, y, lineSize, color);

                string scoreText = entry.FinalScore.ToString();
                DrawText(scoreText, scoreColRight - MeasureText(scoreText, lineSize), y, lineSize, color);

                string timeText = HighscoreTimeFormat.Format(entry.ElapsedSeconds);
                DrawText(timeText, timeColRight - MeasureText(timeText, lineSize), y, lineSize, color);

                if (entry.HasRecording)
                {
                    var button = new Rectangle(
                        replayColRight - buttonWidth,
                        y - 2,
                        buttonWidth,
                        buttonHeight);
                    DrawReplayButton(button);
                    ReplayButtons.Add((entry.Rank, button));
                }

                y += 22;
            }
        }

        LevelProgressOverlayHud.DrawIntermissionHint(footerHint, layout, screenWidth, lineSize);
    }

    public static bool TryHandleViewReplayClick(Vector2 mousePosition, out int rank)
    {
        rank = 0;
        if (!IsMouseButtonPressed(MouseButton.Left))
            return false;

        foreach (var (entryRank, button) in ReplayButtons)
        {
            if (!CheckCollisionPointRec(mousePosition, button))
                continue;

            rank = entryRank;
            return true;
        }

        return false;
    }

    public static bool IsMouseOverReplayButton(Vector2 mousePosition)
    {
        foreach (var (_, button) in ReplayButtons)
        {
            if (CheckCollisionPointRec(mousePosition, button))
                return true;
        }

        return false;
    }

    private static void DrawReplayButton(Rectangle rect)
    {
        bool hovered = CheckCollisionPointRec(GetMousePosition(), rect);
        var fill = hovered ? new Color(70, 110, 70, 255) : new Color(50, 50, 50, 255);
        DrawRectangleRec(rect, fill);
        DrawRectangleLinesEx(rect, 1f, new Color(140, 180, 140, 255));

        const int fontSize = LevelProgressOverlayHud.IntermissionLineSize - 6;
        int labelWidth = MeasureText(ViewReplayLabel, fontSize);
        DrawText(
            ViewReplayLabel,
            (int)(rect.X + (rect.Width - labelWidth) / 2),
            (int)(rect.Y + 3),
            fontSize,
            Color.RayWhite);
    }
}
