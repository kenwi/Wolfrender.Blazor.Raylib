using Game.Features.Highscores.Shared;
using Wolfrender.Highscores.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<JsonFileScoreStore>();
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

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Wolfrender.Highscores.Server");
var scoreFilePath = app.Configuration["Highscores:FilePath"] ?? "highscores.json";
startupLogger.LogInformation(
    "Highscores server starting. ScoreFile={ScoreFile}, CorsOrigins={CorsOrigins}",
    Path.GetFullPath(scoreFilePath),
    string.Join(", ", app.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? []));

app.UseHttpsRedirection();
app.UseCors();

app.MapPost("/api/scores", async (
    ScoreSubmission submission,
    JsonFileScoreStore store,
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
        logger.LogWarning(
            "POST /api/scores rejected: ClientIp={ClientIp}, LevelId={LevelId}, PlayerName={PlayerName}, Error={Error}",
            clientIp,
            submission.LevelId,
            submission.PlayerName,
            error);
        return Results.BadRequest(new { error });
    }

    await store.AddScoreAsync(normalized, cancellationToken);
    logger.LogInformation(
        "POST /api/scores succeeded: ClientIp={ClientIp}, LevelId={LevelId}, PlayerName={PlayerName}, FinalScore={FinalScore}",
        clientIp,
        normalized.LevelId,
        normalized.PlayerName,
        normalized.FinalScore);
    return Results.Ok(new { message = "Score accepted." });
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

app.Run();
