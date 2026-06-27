using System.Text.Json;
using Game.Features.Recording;
using Microsoft.Extensions.Logging;

namespace Wolfrender.Highscores.Server;

public sealed class FileRecordingStore
{
    private readonly string _directory;
    private readonly ILogger<FileRecordingStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileRecordingStore(IConfiguration configuration, ILogger<FileRecordingStore> logger)
    {
        _directory = configuration["Recordings:Directory"] ?? "recordings";
        _logger = logger;
        _logger.LogInformation(
            "FileRecordingStore initialized. Directory={Directory}",
            Path.GetFullPath(_directory));
    }

    public async Task SaveAsync(RecordingUploadRequest upload, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_directory);
            string path = GetRecordingPath(upload.Name);
            RecFileSerializer.Write(path, upload.Recording);

            _logger.LogInformation(
                "Recording persisted: Name={Name}, Path={Path}, EventCount={EventCount}, LevelPath={LevelPath}",
                upload.Name,
                path,
                upload.Recording.Events.Count,
                upload.Recording.LevelPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    public string GetRecordingPath(string sanitizedName) =>
        Path.Combine(_directory, $"{sanitizedName}.rec");
}
