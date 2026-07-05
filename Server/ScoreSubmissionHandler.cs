using Game.Features.Highscores.Shared;
using Microsoft.Extensions.Logging;

namespace Wolfrender.Highscores.Server;

public static class ScoreSubmissionHandler
{
    public static bool TryPrepare(
        ScoreSubmission submission,
        ILogger logger,
        out ScoreSubmission normalized,
        out string error)
    {
        logger.LogInformation(
            "Score submission received: LevelId={LevelId}, PlayerName={PlayerName}, FinalScore={FinalScore}, " +
            "LevelScore={LevelScore}, Kills={Kills}, TreasuresCollected={TreasuresCollected}, " +
            "SecretsFound={SecretsFound}, ElapsedSeconds={ElapsedSeconds:F3}, Checksum={Checksum}",
            submission.LevelId,
            submission.PlayerName,
            submission.FinalScore,
            submission.LevelScore,
            submission.Kills,
            submission.TreasuresCollected,
            submission.SecretsFound,
            submission.ElapsedSeconds,
            submission.Checksum);

        normalized = ScoreSanitizer.NormalizeSubmission(submission);
        LogNormalizationChanges(logger, submission, normalized);

        if (!ScoreSanitizer.TryValidateSubmission(normalized, out error))
        {
            logger.LogWarning(
                "Score submission rejected (validation): LevelId={LevelId}, PlayerName={PlayerName}, Error={Error}",
                normalized.LevelId,
                normalized.PlayerName,
                error);
            return false;
        }

        if (!ScoreChecksum.Verify(normalized))
        {
            error = "Checksum verification failed.";
            var expected = ScoreChecksum.Compute(normalized);
            logger.LogWarning(
                "Suspected fake score submission (checksum mismatch): LevelId={LevelId}, PlayerName={PlayerName}, " +
                "FinalScore={FinalScore}, LevelScore={LevelScore}, Kills={Kills}, TreasuresCollected={TreasuresCollected}, " +
                "SecretsFound={SecretsFound}, ElapsedSeconds={ElapsedSeconds:F3}, " +
                "ProvidedChecksum={ProvidedChecksum}, ExpectedChecksum={ExpectedChecksum}",
                normalized.LevelId,
                normalized.PlayerName,
                normalized.FinalScore,
                normalized.LevelScore,
                normalized.Kills,
                normalized.TreasuresCollected,
                normalized.SecretsFound,
                normalized.ElapsedSeconds,
                submission.Checksum,
                expected);
            return false;
        }

        var suspiciousReasons = ScorePlausibilityChecker.GetSuspiciousReasons(normalized);
        if (suspiciousReasons.Count > 0)
        {
            logger.LogWarning(
                "Suspicious but accepted score submission: LevelId={LevelId}, PlayerName={PlayerName}, " +
                "FinalScore={FinalScore}, LevelScore={LevelScore}, Kills={Kills}, TreasuresCollected={TreasuresCollected}, " +
                "SecretsFound={SecretsFound}, ElapsedSeconds={ElapsedSeconds:F3}, Reasons={Reasons}",
                normalized.LevelId,
                normalized.PlayerName,
                normalized.FinalScore,
                normalized.LevelScore,
                normalized.Kills,
                normalized.TreasuresCollected,
                normalized.SecretsFound,
                normalized.ElapsedSeconds,
                string.Join("; ", suspiciousReasons));
        }

        logger.LogInformation(
            "Score submission accepted for storage: LevelId={LevelId}, PlayerName={PlayerName}, " +
            "FinalScore={FinalScore}, LevelScore={LevelScore}, Kills={Kills}, TreasuresCollected={TreasuresCollected}, " +
            "SecretsFound={SecretsFound}, ElapsedSeconds={ElapsedSeconds:F3}",
            normalized.LevelId,
            normalized.PlayerName,
            normalized.FinalScore,
            normalized.LevelScore,
            normalized.Kills,
            normalized.TreasuresCollected,
            normalized.SecretsFound,
            normalized.ElapsedSeconds);

        return true;
    }

    private static void LogNormalizationChanges(
        ILogger logger,
        ScoreSubmission raw,
        ScoreSubmission normalized)
    {
        if (raw.LevelId != normalized.LevelId)
        {
            logger.LogInformation(
                "Normalized LevelId: raw='{RawLevelId}' -> '{NormalizedLevelId}'",
                raw.LevelId,
                normalized.LevelId);
        }

        if (raw.PlayerName != normalized.PlayerName)
        {
            logger.LogInformation(
                "Normalized PlayerName: raw='{RawPlayerName}' -> '{NormalizedPlayerName}'",
                raw.PlayerName,
                normalized.PlayerName);
        }
    }
}
