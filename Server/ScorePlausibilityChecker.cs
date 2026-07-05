using Game.Features.Highscores.Shared;

namespace Wolfrender.Highscores.Server;

/// <summary>
/// Log-only heuristics for scores that pass checksum validation but look implausible.
/// </summary>
public static class ScorePlausibilityChecker
{
    private const int MinEnemyPoints = 100;
    private const int MinTreasurePoints = 100;
    private const int MaxCompletionBonus = 200_000;
    private const float MaxScorePerSecond = 50_000f;
    private const float FastRunSeconds = 10f;
    private const int FastRunScoreThreshold = 100_000;

    public static IReadOnlyList<string> GetSuspiciousReasons(ScoreSubmission submission)
    {
        var reasons = new List<string>();

        if (submission.FinalScore < submission.LevelScore)
            reasons.Add("FinalScore is less than LevelScore");

        var completionBonus = submission.FinalScore - submission.LevelScore;
        if (completionBonus > MaxCompletionBonus)
            reasons.Add($"Completion bonus unusually large ({completionBonus})");

        var minimumLevelScore = submission.Kills * MinEnemyPoints +
                                submission.TreasuresCollected * MinTreasurePoints;
        if (submission.LevelScore < minimumLevelScore)
            reasons.Add($"LevelScore below minimum from kills and treasures ({minimumLevelScore})");

        var scorePerSecond = submission.FinalScore / submission.ElapsedSeconds;
        if (scorePerSecond > MaxScorePerSecond)
            reasons.Add($"Score rate unusually high ({scorePerSecond:F0} pts/s)");

        if (submission.ElapsedSeconds < FastRunSeconds && submission.FinalScore > FastRunScoreThreshold)
            reasons.Add($"High score completed in under {FastRunSeconds:F0} seconds");

        return reasons;
    }
}
