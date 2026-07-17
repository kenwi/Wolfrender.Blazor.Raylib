using System.Text.Json;
using Game.Features.Highscores.Shared;
using Game.Features.Recording;
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
    private readonly FileRecordingStore _recordingStore;
    private readonly ILogger<JsonFileScoreStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public JsonFileScoreStore(
        IConfiguration configuration,
        FileRecordingStore recordingStore,
        ILogger<JsonFileScoreStore> logger)
    {
        _filePath = configuration["Highscores:FilePath"] ?? "highscores.json";
        _recordingStore = recordingStore;
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
                Checksum = checksum,
                HasRecording = HasRecordingForChecksum(checksum)
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
                    SubmittedAt = entry.SubmittedAt,
                    HasRecording = entry.HasRecording
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

    public async Task<int> RemoveEntriesWithoutRecordingsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadAsync(cancellationToken);
            int removed = 0;

            foreach (var (levelId, entries) in data.Levels.ToList())
            {
                int before = entries.Count;
                entries.RemoveAll(entry => !HasRecordingForChecksum(entry.Checksum));
                removed += before - entries.Count;

                if (entries.Count == 0)
                    data.Levels.Remove(levelId);
            }

            if (removed > 0)
            {
                await WriteAsync(data, cancellationToken);
                _logger.LogInformation(
                    "Removed highscore entries without recordings: RemovedEntries={RemovedEntries}, FilePath={FilePath}",
                    removed,
                    _filePath);
            }
            else
            {
                _logger.LogInformation(
                    "Highscore recording cleanup complete: no entries to remove. FilePath={FilePath}",
                    _filePath);
            }

            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> SyncRecordingAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadAsync(cancellationToken);
            int updated = 0;

            foreach (var entry in data.Levels.Values.SelectMany(entries => entries))
            {
                bool hasRecording = HasRecordingForChecksum(entry.Checksum);
                if (entry.HasRecording == hasRecording)
                    continue;

                entry.HasRecording = hasRecording;
                updated++;
            }

            if (updated > 0)
            {
                await WriteAsync(data, cancellationToken);
                _logger.LogInformation(
                    "Recording availability synced: UpdatedEntries={UpdatedEntries}, FilePath={FilePath}",
                    updated,
                    _filePath);
            }
            else
            {
                _logger.LogInformation(
                    "Recording availability sync complete: no changes needed. FilePath={FilePath}",
                    _filePath);
            }

            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> TrySetHasRecordingByChecksumAsync(
        string checksum,
        bool hasRecording,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checksum))
            return false;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            var data = await ReadAsync(cancellationToken);
            bool changed = false;

            foreach (var entry in data.Levels.Values.SelectMany(entries => entries))
            {
                if (!string.Equals(entry.Checksum, checksum, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.HasRecording == hasRecording)
                    return false;

                entry.HasRecording = hasRecording;
                changed = true;
            }

            if (!changed)
                return false;

            await WriteAsync(data, cancellationToken);
            _logger.LogInformation(
                "Recording availability updated: Checksum={Checksum}, HasRecording={HasRecording}, FilePath={FilePath}",
                checksum,
                hasRecording,
                _filePath);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool HasRecordingForChecksum(string checksum)
    {
        if (string.IsNullOrWhiteSpace(checksum))
            return false;

        if (!RecordingNameSanitizer.TrySanitize(checksum, out var sanitizedName, out _))
            return false;

        return _recordingStore.RecordingExists(sanitizedName);
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
        public bool HasRecording { get; set; }
    }
}
