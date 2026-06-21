using System.Text.Json;
using Game.Features.Highscores.Shared;
using Microsoft.Extensions.Logging;

namespace Wolfrender.Highscores.Server;

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

    public async Task AddScoreAsync(ScoreSubmission submission, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadAsync(cancellationToken);
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
                SubmittedAt = submittedAt
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
    }
}
