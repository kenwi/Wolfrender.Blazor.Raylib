using Game.Features.Recording;
using Microsoft.Extensions.Logging;

namespace Wolfrender.Highscores.Server;

public static class RecordingSubmissionHandler
{
    private const int MaxEventCount = 500_000;

    public static bool TryPrepare(
        RecordingUploadRequest request,
        ILogger logger,
        out RecordingUploadRequest normalized,
        out string error)
    {
        normalized = request;
        error = string.Empty;

        if (!RecordingNameSanitizer.TrySanitize(request.Name, out var sanitizedName, out error))
            return false;

        if (request.Recording is null)
        {
            error = "Recording payload is required.";
            return false;
        }

        var recording = request.Recording;
        if (recording.Version < 1 || recording.Version > RecFile.CurrentVersion)
        {
            error = $"Unsupported recording version {recording.Version}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(recording.LevelPath))
        {
            error = "Recording levelPath is required.";
            return false;
        }

        if (recording.Events.Count > MaxEventCount)
        {
            error = $"Recording exceeds maximum event count ({MaxEventCount}).";
            return false;
        }

        normalized = new RecordingUploadRequest
        {
            Name = sanitizedName,
            Recording = recording
        };

        logger.LogInformation(
            "Recording upload accepted: Name={Name}, Version={Version}, LevelPath={LevelPath}, " +
            "EventCount={EventCount}, MouseSensitivity={MouseSensitivity:F2}, TickHz={TickHz}, HasPlayerSnapshot={HasPlayerSnapshot}",
            normalized.Name,
            normalized.Recording.Version,
            normalized.Recording.LevelPath,
            normalized.Recording.Events.Count,
            normalized.Recording.MouseSensitivity,
            normalized.Recording.ResolveTickHz(),
            normalized.Recording.PlayerSnapshot != null);

        return true;
    }
}
