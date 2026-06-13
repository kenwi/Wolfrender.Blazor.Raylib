using Game.Features.Highscores.Shared;

namespace Wolfrender.Highscores.Server;

public static class ScoreSubmissionHandler
{
    public static bool TryPrepare(ScoreSubmission submission, out ScoreSubmission normalized, out string error)
    {
        normalized = ScoreSanitizer.NormalizeSubmission(submission);

        if (!ScoreSanitizer.TryValidateSubmission(normalized, out error))
            return false;

        if (!ScoreChecksum.Verify(normalized))
        {
            error = "Checksum verification failed.";
            return false;
        }

        return true;
    }
}
