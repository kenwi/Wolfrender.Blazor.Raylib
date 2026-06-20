using System.Net.Http.Json;
using System.Text.Json;
using Game.Features.Highscores.Shared;
using Game.Features.LevelProgress;

namespace Game.Features.Highscores;

/// <summary>HTTP client for submitting scores and fetching leaderboards.</summary>
public sealed class HighscoreClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HighscoreClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(HighscoreConfig.ApiBaseUrl.TrimEnd('/') + "/")
            };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    public ScoreSubmission CreateSubmission(string levelId, string playerName, ScoreSystem score) => new()
    {
        LevelId = ScoreSanitizer.SanitizeLevelId(levelId),
        PlayerName = ScoreSanitizer.SanitizePlayerName(playerName),
        FinalScore = score.FinalScore,
        LevelScore = score.LevelScore,
        Kills = score.Kills,
        TreasuresCollected = score.TreasuresCollected,
        SecretsFound = score.SecretsFound,
        ElapsedSeconds = score.ElapsedActiveSeconds
    };

    public ScoreSubmission WithChecksum(ScoreSubmission submission)
    {
        submission.Checksum = ScoreChecksum.Compute(submission);
        return submission;
    }

    public async Task SubmitAsync(ScoreSubmission submission, CancellationToken cancellationToken = default)
    {
        var payload = WithChecksum(submission);
        using var response = await _httpClient.PostAsJsonAsync("api/scores", payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<HighscoreEntry>> GetTopAsync(
        string levelId,
        int top = HighscoreConfig.DefaultTopCount,
        CancellationToken cancellationToken = default)
    {
        var sanitizedLevelId = ScoreSanitizer.SanitizeLevelId(levelId);
        using var response = await _httpClient.GetAsync(
            $"api/scores/{Uri.EscapeDataString(sanitizedLevelId)}?top={top}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var entries = await response.Content.ReadFromJsonAsync<List<HighscoreEntry>>(JsonOptions, cancellationToken);
        return entries ?? [];
    }

    /// <summary>
    /// Fire-and-forget leaderboard fetch at level start so the browser can prompt
    /// for cross-origin access before level-complete score submission.
    /// </summary>
    public void PrefetchLeaderboardAccess(string levelPath)
    {
        var levelId = ScoreSanitizer.LevelIdFromPath(levelPath);
        if (string.IsNullOrEmpty(levelId))
            return;

        _ = PrefetchLeaderboardAccessAsync(levelId);
    }

    private async Task PrefetchLeaderboardAccessAsync(string levelId)
    {
        try
        {
            await GetTopAsync(levelId);
        }
        catch
        {
            // Browser may deny cross-origin access or the server may be offline.
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }
}
