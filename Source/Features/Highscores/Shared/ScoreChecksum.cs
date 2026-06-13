using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Game.Features.Highscores.Shared;

/// <summary>
/// Deterministic score checksum shared by client and server.
/// A secret embedded in WASM deters casual tampering but is not real security.
/// </summary>
public static class ScoreChecksum
{
    private const string SharedSecret = "Wolfrender.Highscores.v1";

    public static string Compute(ScoreSubmission submission)
    {
        var canonical = string.Join('|',
            submission.LevelId,
            submission.PlayerName,
            submission.FinalScore.ToString(CultureInfo.InvariantCulture),
            submission.LevelScore.ToString(CultureInfo.InvariantCulture),
            submission.Kills.ToString(CultureInfo.InvariantCulture),
            submission.TreasuresCollected.ToString(CultureInfo.InvariantCulture),
            submission.SecretsFound.ToString(CultureInfo.InvariantCulture),
            submission.ElapsedSeconds.ToString("F3", CultureInfo.InvariantCulture),
            SharedSecret);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(ScoreSubmission submission)
    {
        if (string.IsNullOrWhiteSpace(submission.Checksum))
            return false;

        var expected = Compute(submission);
        return string.Equals(expected, submission.Checksum.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
