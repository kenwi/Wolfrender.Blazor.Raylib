using System.Text;
using System.Text.RegularExpressions;

namespace Game.Features.Highscores.Shared;

/// <summary>Shared input normalization used by client and server before checksum and storage.</summary>
public static partial class ScoreSanitizer
{
    public const int MaxPlayerNameLength = 16;
    public const int MaxLevelIdLength = 64;
    public const int MaxScore = 10_000_000;
    public const int MaxCounter = 10_000;
    public const float MaxElapsedSeconds = 86_400f;

    [GeneratedRegex(@"[^A-Za-z0-9 _-]")]
    private static partial Regex InvalidNameChars();

    [GeneratedRegex(@"[^A-Za-z0-9_-]")]
    private static partial Regex InvalidLevelIdChars();

    public static string SanitizePlayerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Anonymous";

        var trimmed = name.Trim();
        if (trimmed.Length > MaxPlayerNameLength)
            trimmed = trimmed[..MaxPlayerNameLength];

        var cleaned = InvalidNameChars().Replace(trimmed, string.Empty);
        return string.IsNullOrEmpty(cleaned) ? "Anonymous" : cleaned;
    }

    public static string SanitizeLevelId(string? levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            return string.Empty;

        var trimmed = levelId.Trim();
        if (trimmed.Length > MaxLevelIdLength)
            trimmed = trimmed[..MaxLevelIdLength];

        return InvalidLevelIdChars().Replace(trimmed, string.Empty);
    }

    public static string LevelIdFromPath(string levelPath)
    {
        var normalized = levelPath.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(normalized);
        return SanitizeLevelId(fileName);
    }

    public static bool TryValidateSubmission(ScoreSubmission submission, out string error)
    {
        var sb = new StringBuilder();

        if (string.IsNullOrEmpty(SanitizeLevelId(submission.LevelId)))
            sb.AppendLine("LevelId is required.");

        if (submission.FinalScore < 0 || submission.FinalScore > MaxScore)
            sb.AppendLine("FinalScore is out of range.");

        if (submission.LevelScore < 0 || submission.LevelScore > MaxScore)
            sb.AppendLine("LevelScore is out of range.");

        if (submission.Kills < 0 || submission.Kills > MaxCounter)
            sb.AppendLine("Kills is out of range.");

        if (submission.TreasuresCollected < 0 || submission.TreasuresCollected > MaxCounter)
            sb.AppendLine("TreasuresCollected is out of range.");

        if (submission.SecretsFound < 0 || submission.SecretsFound > MaxCounter)
            sb.AppendLine("SecretsFound is out of range.");

        if (submission.ElapsedSeconds <= 0f || submission.ElapsedSeconds > MaxElapsedSeconds)
            sb.AppendLine("ElapsedSeconds is out of range.");

        error = sb.ToString().Trim();
        return error.Length == 0;
    }

    public static ScoreSubmission NormalizeSubmission(ScoreSubmission submission) => new()
    {
        LevelId = SanitizeLevelId(submission.LevelId),
        PlayerName = SanitizePlayerName(submission.PlayerName),
        FinalScore = submission.FinalScore,
        LevelScore = submission.LevelScore,
        Kills = submission.Kills,
        TreasuresCollected = submission.TreasuresCollected,
        SecretsFound = submission.SecretsFound,
        ElapsedSeconds = submission.ElapsedSeconds,
        Checksum = submission.Checksum
    };
}
