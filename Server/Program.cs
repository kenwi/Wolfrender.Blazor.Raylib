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

app.UseHttpsRedirection();
app.UseCors();

app.MapPost("/api/scores", async (ScoreSubmission submission, JsonFileScoreStore store, CancellationToken cancellationToken) =>
{
    if (!ScoreSubmissionHandler.TryPrepare(submission, out var normalized, out var error))
        return Results.BadRequest(new { error });

    await store.AddScoreAsync(normalized, cancellationToken);
    return Results.Ok(new { message = "Score accepted." });
});

app.MapGet("/api/scores/{levelId}", async (
    string levelId,
    int? top,
    JsonFileScoreStore store,
    CancellationToken cancellationToken) =>
{
    var sanitizedLevelId = ScoreSanitizer.SanitizeLevelId(levelId);
    if (string.IsNullOrEmpty(sanitizedLevelId))
        return Results.BadRequest(new { error = "LevelId is required." });

    var count = Math.Clamp(top ?? 10, 1, 100);
    var entries = await store.GetTopAsync(sanitizedLevelId, count, cancellationToken);
    return Results.Ok(entries);
});

app.Run();
