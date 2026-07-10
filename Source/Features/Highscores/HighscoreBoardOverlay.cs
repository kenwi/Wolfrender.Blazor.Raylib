using Game.DebugConsole;
using Game.Features.Highscores.Shared;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Highscores;

/// <summary>In-game highscore board toggled with H.</summary>
public sealed class HighscoreBoardOverlay
{
    private readonly HighscoreClient _client;
    private readonly Func<string> _getLevelPath;
    private readonly Func<int, ConsoleCommandResult> _startReplayRemote;
    private readonly Action<ConsoleCommandResult>? _onFeedback;

    private bool _isOpen;
    private bool _loading;
    private IReadOnlyList<HighscoreEntry> _leaderboard = Array.Empty<HighscoreEntry>();
    private string? _statusMessage;
    private Task? _pendingTask;

    public HighscoreBoardOverlay(
        HighscoreClient client,
        Func<string> getLevelPath,
        Func<int, ConsoleCommandResult> startReplayRemote,
        Action<ConsoleCommandResult>? onFeedback = null)
    {
        _client = client;
        _getLevelPath = getLevelPath;
        _startReplayRemote = startReplayRemote;
        _onFeedback = onFeedback;
    }

    public bool IsOpen => _isOpen;

    public void Toggle()
    {
        if (_isOpen)
            Close();
        else
            Open();
    }

    public void Open()
    {
        _isOpen = true;
        BeginLoad();
    }

    public void Close()
    {
        _isOpen = false;
        _loading = false;
        _leaderboard = Array.Empty<HighscoreEntry>();
        _statusMessage = null;
        _pendingTask = null;
    }

    public void Update()
    {
        if (!_isOpen)
            return;

        if (IsKeyPressed(KeyboardKey.Escape) || IsKeyPressed(KeyboardKey.H))
        {
            Close();
            return;
        }

        PollPendingTask();

        if (HighscoreLeaderboardHud.TryHandleViewReplayClick(out int rank))
        {
            var result = _startReplayRemote(rank);
            _onFeedback?.Invoke(result);
            Close();
        }
    }

    public void Draw(int screenWidth, int screenHeight)
    {
        if (!_isOpen)
            return;

        DrawRectangle(0, 0, screenWidth, screenHeight, new Color(0, 0, 0, 120));

        HighscoreLeaderboardHud.Draw(
            _leaderboard,
            screenWidth,
            screenHeight,
            _loading,
            _statusMessage,
            highlight: null,
            footerHint: "H or Esc to close");
    }

    private void BeginLoad()
    {
        _loading = true;
        _leaderboard = Array.Empty<HighscoreEntry>();
        _statusMessage = "Loading leaderboard...";
        _pendingTask = LoadLeaderboardAsync();
    }

    private async Task LoadLeaderboardAsync()
    {
        var levelId = ScoreSanitizer.LevelIdFromPath(_getLevelPath());
        try
        {
            _leaderboard = string.IsNullOrEmpty(levelId)
                ? Array.Empty<HighscoreEntry>()
                : await _client.GetTopWithSyncedRecordingsAsync(levelId);
            _statusMessage = null;
        }
        catch (Exception ex)
        {
            _leaderboard = Array.Empty<HighscoreEntry>();
            _statusMessage = $"Leaderboard unavailable: {ex.Message}";
        }
        finally
        {
            _loading = false;
            _pendingTask = null;
        }
    }

    private void PollPendingTask()
    {
        if (_pendingTask is null || !_pendingTask.IsCompleted)
            return;

        _ = _pendingTask.Exception;
    }
}
