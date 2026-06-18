using Game.Features.Highscores.Shared;
using Game.Features.LevelProgress;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Highscores;

/// <summary>Level-complete name entry, score submission, and leaderboard display.</summary>
public sealed class HighscoreIntermission
{
    private enum Phase
    {
        Hidden,
        LevelScore,
        NameEntry,
        Submitting,
        LoadingLeaderboard,
        Leaderboard
    }

    private readonly HighscoreClient _client;
    private Phase _phase = Phase.Hidden;
    private string _levelId = string.Empty;
    private ScoreSystem? _score;
    private string _playerNameInput = string.Empty;
    private string? _submittedPlayerName;
    private int? _submittedFinalScore;
    private float? _submittedElapsedSeconds;
    private IReadOnlyList<HighscoreEntry> _leaderboard = Array.Empty<HighscoreEntry>();
    private string? _statusMessage;
    private Task? _pendingTask;

    public HighscoreIntermission(HighscoreClient client)
    {
        _client = client;
    }

    public bool IsActive => _phase != Phase.Hidden;
    public bool IsBlockingRestart => _phase is Phase.LevelScore or Phase.NameEntry or Phase.Submitting or Phase.LoadingLeaderboard;

    public void ResetForLevel()
    {
        _phase = Phase.Hidden;
        _levelId = string.Empty;
        _score = null;
        _playerNameInput = string.Empty;
        _submittedPlayerName = null;
        _submittedFinalScore = null;
        _submittedElapsedSeconds = null;
        _leaderboard = Array.Empty<HighscoreEntry>();
        _statusMessage = null;
        _pendingTask = null;
    }

    public void Begin(string levelPath, ScoreSystem score)
    {
        _levelId = ScoreSanitizer.LevelIdFromPath(levelPath);
        _score = score;
        _playerNameInput = string.Empty;
        _submittedPlayerName = null;
        _submittedFinalScore = null;
        _submittedElapsedSeconds = null;
        _leaderboard = Array.Empty<HighscoreEntry>();
        _statusMessage = null;
        _pendingTask = null;
        _phase = Phase.LevelScore;
    }

    public void Update()
    {
        switch (_phase)
        {
            case Phase.LevelScore:
                UpdateLevelScoreAdvance();
                break;
            case Phase.NameEntry:
                UpdateNameEntry();
                break;
            case Phase.Submitting:
            case Phase.LoadingLeaderboard:
                PollPendingTask();
                break;
            case Phase.Leaderboard:
                UpdateLeaderboardDismiss();
                break;
        }
    }

    public void Draw(ScoreSystem score, int screenWidth, int screenHeight)
    {
        switch (_phase)
        {
            case Phase.Hidden:
                return;
            case Phase.LevelScore:
                LevelProgressOverlayHud.DrawLevelComplete(
                    score,
                    screenWidth,
                    screenHeight,
                    showRestartHint: true,
                    restartHint: "Click or press Enter to submit score");
                return;
            case Phase.NameEntry:
            case Phase.Submitting:
                DrawNameEntryPanel(score, screenWidth, screenHeight);
                return;
            case Phase.LoadingLeaderboard:
            case Phase.Leaderboard:
                DrawLeaderboardPanel(screenWidth, screenHeight);
                break;
        }
    }

    private void UpdateLevelScoreAdvance()
    {
        if (IsMouseButtonPressed(MouseButton.Left) || IsKeyPressed(KeyboardKey.Enter))
            _phase = Phase.NameEntry;
    }

    private void UpdateNameEntry()
    {
        int key = GetCharPressed();
        while (key > 0)
        {
            if (key >= 32 && key <= 126 && _playerNameInput.Length < ScoreSanitizer.MaxPlayerNameLength)
                _playerNameInput += (char)key;
            key = GetCharPressed();
        }

        if (IsKeyPressed(KeyboardKey.Backspace) && _playerNameInput.Length > 0)
            _playerNameInput = _playerNameInput[..^1];

        if (IsKeyPressed(KeyboardKey.Escape))
        {
            BeginLoadLeaderboard();
            return;
        }

        if (!IsKeyPressed(KeyboardKey.Enter))
            return;

        if (_score is null)
        {
            BeginLoadLeaderboard();
            return;
        }

        _phase = Phase.Submitting;
        _statusMessage = "Submitting score...";

        var submission = _client.CreateSubmission(_levelId, _playerNameInput, _score);
        _submittedPlayerName = submission.PlayerName;
        _submittedFinalScore = submission.FinalScore;
        _submittedElapsedSeconds = submission.ElapsedSeconds;

        _pendingTask = SubmitAndContinueAsync(submission);
    }

    private async Task SubmitAndContinueAsync(ScoreSubmission submission)
    {
        try
        {
            await _client.SubmitAsync(submission);
            _statusMessage = "Score submitted.";
        }
        catch (Exception ex)
        {
            _statusMessage = $"Submit failed: {ex.Message}";
        }

        await LoadLeaderboardAsync();
        _phase = Phase.Leaderboard;
        _pendingTask = null;
    }

    private void BeginLoadLeaderboard()
    {
        _phase = Phase.LoadingLeaderboard;
        _statusMessage = "Loading leaderboard...";
        _pendingTask = LoadLeaderboardAndContinueAsync();
    }

    private async Task LoadLeaderboardAndContinueAsync()
    {
        await LoadLeaderboardAsync();
        _phase = Phase.Leaderboard;
        _pendingTask = null;
    }

    private async Task LoadLeaderboardAsync()
    {
        try
        {
            _leaderboard = await _client.GetTopAsync(_levelId);
            _statusMessage = null;
        }
        catch (Exception ex)
        {
            _leaderboard = Array.Empty<HighscoreEntry>();
            _statusMessage = $"Leaderboard unavailable: {ex.Message}";
        }
    }

    private void PollPendingTask()
    {
        if (_pendingTask is null || !_pendingTask.IsCompleted)
            return;

        _ = _pendingTask.Exception;
    }

    private void UpdateLeaderboardDismiss()
    {
        if (IsKeyPressed(KeyboardKey.Escape))
            _phase = Phase.Hidden;
    }

    private void DrawNameEntryPanel(ScoreSystem score, int screenWidth, int screenHeight)
    {
        var layout = LevelProgressOverlayHud.DrawIntermissionFrame(screenWidth, screenHeight, "SUBMIT SCORE");
        const int lineSize = LevelProgressOverlayHud.IntermissionLineSize;

        int y = layout.ContentY;
        DrawText($"FINAL SCORE: {score.FinalScore}", layout.ContentX, y, lineSize, Color.RayWhite);
        y += 40;

        DrawText("ENTER YOUR NAME", layout.ContentX, y, lineSize, LevelProgressOverlayHud.Accent);
        y += 28;

        string nameDisplay = string.IsNullOrEmpty(_playerNameInput) ? "_" : _playerNameInput + "_";
        DrawText(nameDisplay, layout.ContentX, y, lineSize + 2, Color.RayWhite);

        string hint = _phase == Phase.Submitting
            ? _statusMessage ?? "Submitting score..."
            : "Enter to submit  |  Esc to skip";
        LevelProgressOverlayHud.DrawIntermissionHint(hint, layout, screenWidth, lineSize - 2);
    }

    private void DrawLeaderboardPanel(int screenWidth, int screenHeight)
    {
        var layout = LevelProgressOverlayHud.DrawIntermissionFrame(screenWidth, screenHeight, "HIGH SCORES");
        const int lineSize = LevelProgressOverlayHud.IntermissionLineSize - 4;
        var highlight = new Color(120, 220, 120, 255);

        int y = layout.ContentY;

        if (_phase == Phase.LoadingLeaderboard)
        {
            DrawText(_statusMessage ?? "Loading leaderboard...", layout.ContentX, y, lineSize, Color.RayWhite);
            return;
        }

        if (!string.IsNullOrEmpty(_statusMessage))
        {
            DrawText(_statusMessage, layout.ContentX, y, lineSize, new Color(255, 140, 140, 255));
            y += 24;
        }

        if (_leaderboard.Count == 0)
        {
            DrawText("No scores yet.", layout.ContentX, y, lineSize, Color.RayWhite);
        }
        else
        {
            foreach (var entry in _leaderboard)
            {
                int minutes = (int)entry.ElapsedSeconds / 60;
                int seconds = (int)entry.ElapsedSeconds % 60;
                string line = $"{entry.Rank,2}. {entry.PlayerName,-16} {entry.FinalScore,8}  {minutes}:{seconds:D2}";

                bool highlightEntry = _submittedFinalScore.HasValue
                    && entry.FinalScore == _submittedFinalScore.Value
                    && string.Equals(entry.PlayerName, _submittedPlayerName, StringComparison.Ordinal)
                    && MathF.Abs(entry.ElapsedSeconds - (_submittedElapsedSeconds ?? 0f)) < 0.05f;

                DrawText(line, layout.ContentX, y, lineSize, highlightEntry ? highlight : Color.RayWhite);
                y += 22;
            }
        }

        LevelProgressOverlayHud.DrawIntermissionHint("Press R or click to continue", layout, screenWidth, lineSize);
    }
}
