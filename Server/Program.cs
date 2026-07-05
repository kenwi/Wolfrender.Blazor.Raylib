using Game.Features.Highscores.Shared;
using Game.Features.Recording;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Primitives;
using System.Text.Json;
using Wolfrender.Highscores.Server;
using Wolfrender.Highscores.Server.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFileLogger(builder.Configuration);

const string dataConfigPath = "/data/appsettings.json";
if (File.Exists(dataConfigPath))
    builder.Configuration.AddJsonFile(dataConfigPath, optional: false, reloadOnChange: true);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddSingleton<JsonFileScoreStore>();
builder.Services.AddSingleton<FileRecordingStore>();
builder.Services.AddSingleton<SubmissionRejectionTracker>();
builder.Services.AddHttpClient<DiscordScoreAnnouncer>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ??
        [
            "http://localhost:5000",
            "https://localhost:5001",
            "http://localhost:5217",
            "https://localhost:7217"
        ];

        policy.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<RequestLoggingMiddleware>();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Wolfrender.Highscores.Server");
var scoreFilePath = app.Configuration["Highscores:FilePath"] ?? "highscores.json";
var recordingsDirectory = app.Configuration["Recordings:Directory"] ?? "recordings";
var logFilePath = app.Configuration["Logging:File:Path"];
startupLogger.LogInformation(
    "Highscores server starting. ScoreFile={ScoreFile}, RecordingsDirectory={RecordingsDirectory}, LogFile={LogFile}, CorsOrigins={CorsOrigins}",
    Path.GetFullPath(scoreFilePath),
    Path.GetFullPath(recordingsDirectory),
    string.IsNullOrWhiteSpace(logFilePath) ? "(disabled)" : Path.GetFullPath(logFilePath),
    string.Join(", ", app.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? []));

if (File.Exists(dataConfigPath))
{
    ChangeToken.OnChange(
        () => app.Configuration.GetReloadToken(),
        () => startupLogger.LogInformation("Configuration reloaded from {ConfigPath}", dataConfigPath));
}

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors();

app.MapPost("/api/scores", async (
    ScoreSubmission submission,
    JsonFileScoreStore store,
    DiscordScoreAnnouncer announcer,
    SubmissionRejectionTracker rejectionTracker,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var origin = httpContext.Request.Headers.Origin.ToString();
    var referer = httpContext.Request.Headers.Referer.ToString();
    var userAgent = httpContext.Request.Headers.UserAgent.ToString();

    logger.LogInformation(
        "POST /api/scores request: ClientIp={ClientIp}, Origin={Origin}, Referer={Referer}, UserAgent={UserAgent}",
        clientIp,
        string.IsNullOrEmpty(origin) ? "(none)" : origin,
        string.IsNullOrEmpty(referer) ? "(none)" : referer,
        string.IsNullOrEmpty(userAgent) ? "(none)" : userAgent);

    if (!ScoreSubmissionHandler.TryPrepare(submission, logger, out var normalized, out var error))
    {
        var isSuspectedFake = error.Contains("Checksum", StringComparison.OrdinalIgnoreCase);
        var rejectionCount = rejectionTracker.RecordRejection(clientIp);
        if (rejectionTracker.IsRepeatOffender(clientIp, rejectionCount))
        {
            logger.LogWarning(
                "Repeat score rejection offender: ClientIp={ClientIp}, RejectionCount={RejectionCount}, " +
                "WindowMinutes={WindowMinutes}, Threshold={Threshold}",
                clientIp,
                rejectionCount,
                rejectionTracker.Window.TotalMinutes,
                rejectionTracker.Threshold);
        }

        logger.LogWarning(
            "POST /api/scores rejected: ClientIp={ClientIp}, Origin={Origin}, UserAgent={UserAgent}, " +
            "LevelId={LevelId}, PlayerName={PlayerName}, FinalScore={FinalScore}, RejectionCount={RejectionCount}, " +
            "SuspectedFake={SuspectedFake}, Error={Error}",
            clientIp,
            string.IsNullOrEmpty(origin) ? "(none)" : origin,
            string.IsNullOrEmpty(userAgent) ? "(none)" : userAgent,
            submission.LevelId,
            submission.PlayerName,
            submission.FinalScore,
            rejectionCount,
            isSuspectedFake,
            error);
        return Results.BadRequest(new { error });
    }

    var addResult = await store.TryAddScoreAsync(normalized, cancellationToken);
    if (!addResult.Accepted)
    {
        logger.LogWarning(
            "POST /api/scores rejected (duplicate): ClientIp={ClientIp}, LevelId={LevelId}, PlayerName={PlayerName}, FinalScore={FinalScore}",
            clientIp,
            normalized.LevelId,
            normalized.PlayerName,
            normalized.FinalScore);
        return Results.Conflict(new { error = "This score has already been submitted." });
    }

    announcer.AnnounceInBackground(normalized, addResult);

    logger.LogInformation(
        "POST /api/scores succeeded: ClientIp={ClientIp}, LevelId={LevelId}, PlayerName={PlayerName}, FinalScore={FinalScore}, Rank={Rank}",
        clientIp,
        normalized.LevelId,
        normalized.PlayerName,
        normalized.FinalScore,
        addResult.Rank);
    return Results.Ok(new { message = "Score accepted.", rank = addResult.Rank });
});

app.MapGet("/api/scores/{levelId}", async (
    string levelId,
    int? top,
    JsonFileScoreStore store,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var origin = httpContext.Request.Headers.Origin.ToString();
    var count = Math.Clamp(top ?? 10, 1, 100);

    logger.LogInformation(
        "GET /api/scores/{LevelId} request: ClientIp={ClientIp}, Origin={Origin}, RawLevelId={RawLevelId}, Top={Top}",
        levelId,
        clientIp,
        string.IsNullOrEmpty(origin) ? "(none)" : origin,
        levelId,
        count);

    var sanitizedLevelId = ScoreSanitizer.SanitizeLevelId(levelId);
    if (string.IsNullOrEmpty(sanitizedLevelId))
    {
        logger.LogWarning(
            "GET /api/scores rejected: ClientIp={ClientIp}, RawLevelId={RawLevelId}, Error=LevelId is required",
            clientIp,
            levelId);
        return Results.BadRequest(new { error = "LevelId is required." });
    }

    if (sanitizedLevelId != levelId)
    {
        logger.LogInformation(
            "GET /api/scores normalized LevelId: raw='{RawLevelId}' -> '{SanitizedLevelId}'",
            levelId,
            sanitizedLevelId);
    }

    var entries = await store.GetTopAsync(sanitizedLevelId, count, cancellationToken);
    logger.LogInformation(
        "GET /api/scores/{LevelId} succeeded: ClientIp={ClientIp}, ReturnedCount={ReturnedCount}",
        sanitizedLevelId,
        clientIp,
        entries.Count);
    return Results.Ok(entries);
});

app.MapPost("/api/recordings", async (
    HttpRequest httpRequest,
    FileRecordingStore store,
    HttpContext httpContext,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var origin = httpContext.Request.Headers.Origin.ToString();
    var contentLength = httpRequest.ContentLength;

    RecordingUploadWireRequest request;
    try
    {
        request = await httpRequest.ReadFromJsonAsync<RecordingUploadWireRequest>(
            RecFileSerializer.JsonOptions,
            cancellationToken) ?? throw new JsonException("Recording upload body is empty.");
    }
    catch (JsonException ex)
    {
        logger.LogWarning(
            ex,
            "POST /api/recordings rejected: invalid JSON. ClientIp={ClientIp}, ContentLength={ContentLength}",
            clientIp,
            contentLength);
        return Results.BadRequest(new { error = $"Invalid recording JSON: {ex.Message}" });
    }

    logger.LogInformation(
        "POST /api/recordings request: ClientIp={ClientIp}, Origin={Origin}, Name={Name}, ContentLength={ContentLength}",
        clientIp,
        string.IsNullOrEmpty(origin) ? "(none)" : origin,
        request.Name,
        contentLength);

    RecFile recording;
    try
    {
        recording = request.Recording.ToRecFile();
    }
    catch (Exception ex)
    {
        logger.LogWarning(
            ex,
            "POST /api/recordings rejected: invalid recording payload. ClientIp={ClientIp}, Name={Name}, ContentLength={ContentLength}",
            clientIp,
            request.Name,
            contentLength);
        return Results.BadRequest(new { error = ex.Message });
    }

    var upload = new RecordingUploadRequest
    {
        Name = request.Name,
        Recording = recording
    };

    if (!RecordingSubmissionHandler.TryPrepare(upload, logger, out var normalized, out var error))
    {
        logger.LogWarning(
            "POST /api/recordings rejected: ClientIp={ClientIp}, Name={Name}, ContentLength={ContentLength}, " +
            "Version={Version}, LevelPath={LevelPath}, EventCount={EventCount}, Error={Error}",
            clientIp,
            request.Name,
            contentLength,
            recording.Version,
            recording.LevelPath,
            recording.Events.Count,
            error);
        return Results.BadRequest(new { error });
    }

    await store.SaveAsync(normalized, cancellationToken);

    logger.LogInformation(
        "POST /api/recordings succeeded: ClientIp={ClientIp}, Name={Name}, LevelPath={LevelPath}, EventCount={EventCount}",
        clientIp,
        normalized.Name,
        normalized.Recording.LevelPath,
        normalized.Recording.Events.Count);

    return Results.Ok(new { message = "Recording accepted.", name = normalized.Name });
});

app.Run();
