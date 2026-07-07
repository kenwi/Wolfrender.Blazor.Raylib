using System.Text.Json;
using Game.Features.Highscores.Shared;
using Microsoft.Extensions.Logging;

namespace Wolfrender.Highscores.Server;

/// <summary>Outcome of a score submission. Rank is 1-based within the level's leaderboard.</summary>
public sealed record ScoreAddResult(bool Accepted, int Rank, int TotalEntriesForLevel)
{
    public static readonly ScoreAddResult Duplicate = new(false, 0, 0);
}

public sealed class JsonFileScoreStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly ILogger<JsonFileScoreStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonFileScoreStore(IConfiguration configuration, ILogger<JsonFileScoreStore> logger)
    {
        _filePath = configuration["Highscores:FilePath"] ?? "highscores.json";
        _logger = logger;
        _logger.LogInformation("JsonFileScoreStore initialized. FilePath={FilePath}", Path.GetFullPath(_filePath));
    }

    public async Task<ScoreAddResult> TryAddScoreAsync(ScoreSubmission submission, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var checksum = ScoreChecksum.Compute(submission);
            var data = await ReadAsync(cancellationToken);

            if (ContainsChecksum(data, checksum))
            {
                _logger.LogWarning(
                    "Score rejected (duplicate checksum): LevelId={LevelId}, PlayerName={PlayerName}, " +
                    "FinalScore={FinalScore}, Checksum={Checksum}, FilePath={FilePath}",
                    submission.LevelId,
                    submission.PlayerName,
                    submission.FinalScore,
                    checksum,
                    _filePath);
                return ScoreAddResult.Duplicate;
            }

            if (!data.Levels.TryGetValue(submission.LevelId, out var entries))
            {
                entries = [];
                data.Levels[submission.LevelId] = entries;
            }

            var submittedAt = DateTimeOffset.Now;
            var newEntry = new StoredScoreEntry
            {
                PlayerName = submission.PlayerName,
                FinalScore = submission.FinalScore,
                LevelScore = submission.LevelScore,
                Kills = submission.Kills,
                TreasuresCollected = submission.TreasuresCollected,
                SecretsFound = submission.SecretsFound,
                ElapsedSeconds = submission.ElapsedSeconds,
                SubmittedAt = submittedAt,
                Checksum = checksum
            };
            entries.Add(newEntry);

            var rank = entries
                .OrderByDescending(e => e.FinalScore)
                .ThenBy(e => e.ElapsedSeconds)
                .ThenBy(e => e.SubmittedAt)
                .ToList()
                .FindIndex(e => ReferenceEquals(e, newEntry)) + 1;

            _logger.LogInformation(
                "Score persisted: LevelId={LevelId}, PlayerName={PlayerName}, FinalScore={FinalScore}, " +
                "Rank={Rank}, LevelEntryCount={LevelEntryCount}, FilePath={FilePath}",
                submission.LevelId,
                submission.PlayerName,
                submission.FinalScore,
                rank,
                entries.Count,
                _filePath);

            await WriteAsync(data, cancellationToken);
            return new ScoreAddResult(true, rank, entries.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetChecksumByRankAsync(
        string levelId,
        int rank,
        CancellationToken cancellationToken = default)
    {
        if (rank < 1)
            return null;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadAsync(cancellationToken);
            if (!data.Levels.TryGetValue(levelId, out var entries))
                return null;

            var checksum = entries
                .OrderByDescending(e => e.FinalScore)
                .ThenBy(e => e.ElapsedSeconds)
                .ThenBy(e => e.SubmittedAt)
                .Skip(rank - 1)
                .Select(e => e.Checksum)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(checksum))
                return null;

            _logger.LogInformation(
                "Leaderboard checksum resolved: LevelId={LevelId}, Rank={Rank}, Checksum={Checksum}",
                levelId,
                rank,
                checksum);

            return checksum;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<HighscoreEntry>> GetTopAsync(
        string levelId,
        int top,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadAsync(cancellationToken);
            if (!data.Levels.TryGetValue(levelId, out var entries))
                return Array.Empty<HighscoreEntry>();

            var results = entries
                .OrderByDescending(e => e.FinalScore)
                .ThenBy(e => e.ElapsedSeconds)
                .ThenBy(e => e.SubmittedAt)
                .Take(top)
                .Select((entry, index) => new HighscoreEntry
                {
                    Rank = index + 1,
                    PlayerName = entry.PlayerName,
                    FinalScore = entry.FinalScore,
                    ElapsedSeconds = entry.ElapsedSeconds,
                    SubmittedAt = entry.SubmittedAt
                })
                .ToArray();

            _logger.LogInformation(
                "Leaderboard queried: LevelId={LevelId}, RequestedTop={RequestedTop}, ReturnedCount={ReturnedCount}, " +
                "TotalStoredForLevel={TotalStoredForLevel}",
                levelId,
                top,
                results.Length,
                entries.Count);

            return results;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static bool ContainsChecksum(HighscoreStoreData data, string checksum) =>
        data.Levels.Values
            .SelectMany(entries => entries)
            .Any(entry => !string.IsNullOrEmpty(entry.Checksum) &&
                          string.Equals(entry.Checksum, checksum, StringComparison.OrdinalIgnoreCase));

    private async Task<HighscoreStoreData> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
            return new HighscoreStoreData();

        await using var stream = File.OpenRead(_filePath);
        var data = await JsonSerializer.DeserializeAsync<HighscoreStoreData>(stream, JsonOptions, cancellationToken);
        return data ?? new HighscoreStoreData();
    }

    private async Task WriteAsync(HighscoreStoreData data, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken);
    }

    private sealed class HighscoreStoreData
    {
        public Dictionary<string, List<StoredScoreEntry>> Levels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class StoredScoreEntry
    {
        public string PlayerName { get; set; } = string.Empty;
        public int FinalScore { get; set; }
        public int LevelScore { get; set; }
        public int Kills { get; set; }
        public int TreasuresCollected { get; set; }
        public int SecretsFound { get; set; }
        public float ElapsedSeconds { get; set; }
        public DateTimeOffset SubmittedAt { get; set; }
        public string Checksum { get; set; } = string.Empty;
    }
}
