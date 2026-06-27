using Game.DebugConsole;
using Game.Engine.Input;

namespace Game.Features.Recording;

public sealed class RecordingSystem
{
    private const string DemosFolder = "demos";

    private readonly InputSystem _inputSystem;
    private readonly LiveInputProvider _liveProvider;
    private readonly InputRecorder _recorder = new();
    private readonly ReplayInputProvider _replayProvider = new();
    private readonly RecordingUploadClient _uploadClient = new();

    private Func<string, ConsoleCommandResult>? _loadLevel;
    private Func<ConsoleCommandResult>? _restartLevel;
    private Func<string>? _getCurrentLevelPath;
    private Action<float>? _applyMouseSensitivity;
    private Action? _restoreControlSettings;
    private Func<PlayerSnapshot>? _capturePlayerSnapshot;
    private Action<PlayerSnapshot>? _applyPlayerSnapshot;
    private Action<ConsoleCommandResult>? _onUploadCompleted;

    private Task? _pendingUpload;
    private string? _pendingUploadName;
    private IInputProvider _activeProvider;
    private string? _recordingPath;
    private string? _recordingLevelPath;
    private float _recordingMouseSensitivity;
    private PlayerSnapshot? _recordingPlayerSnapshot;

    public RecordingSystem(InputSystem inputSystem)
    {
        _inputSystem = inputSystem;
        _liveProvider = new LiveInputProvider(inputSystem);
        _activeProvider = _liveProvider;
        _liveProvider.Polled += OnLivePolled;
    }

    public IInputProvider ActiveProvider => _activeProvider;
    public bool IsRecording => _recordingPath != null;
    public bool IsReplaying => _activeProvider == _replayProvider;

    public void Configure(
        Func<string, ConsoleCommandResult> loadLevel,
        Func<ConsoleCommandResult> restartLevel,
        Func<string> getCurrentLevelPath,
        Action<float> applyMouseSensitivity,
        Action restoreControlSettings,
        Func<PlayerSnapshot> capturePlayerSnapshot,
        Action<PlayerSnapshot> applyPlayerSnapshot,
        Action<ConsoleCommandResult>? onUploadCompleted = null)
    {
        _loadLevel = loadLevel;
        _restartLevel = restartLevel;
        _getCurrentLevelPath = getCurrentLevelPath;
        _applyMouseSensitivity = applyMouseSensitivity;
        _restoreControlSettings = restoreControlSettings;
        _capturePlayerSnapshot = capturePlayerSnapshot;
        _applyPlayerSnapshot = applyPlayerSnapshot;
        _onUploadCompleted = onUploadCompleted;
    }

    public ConsoleCommandResult StartRecording(string filename, float mouseSensitivity)
    {
        if (_getCurrentLevelPath == null || _restartLevel == null || _capturePlayerSnapshot == null)
            return ConsoleCommandResult.Fail("Recording system is not configured.");

        if (IsReplaying)
            return ConsoleCommandResult.Fail("Stop replay before recording.");

        if (IsRecording)
            return ConsoleCommandResult.Fail("Already recording. Use 'stoprecord' first.");

        if (string.IsNullOrWhiteSpace(filename))
            return ConsoleCommandResult.Fail("Usage: record <filename>");

        var restartResult = _restartLevel();
        if (!restartResult.Success)
            return ConsoleCommandResult.Fail($"record: {restartResult.Message}");

        _recorder.Reset();
        _recordingPlayerSnapshot = _capturePlayerSnapshot();
        _recordingPath = ResolveDemoPath(filename);
        _recordingLevelPath = _getCurrentLevelPath();
        _recordingMouseSensitivity = mouseSensitivity;

        return ConsoleCommandResult.Ok(
            $"Recording to '{_recordingPath}' (level restarted, snapshot captured).");
    }

    public ConsoleCommandResult StopRecording()
    {
        if (!IsRecording || _recordingPath == null || _recordingLevelPath == null)
            return ConsoleCommandResult.Fail("Not currently recording.");

        try
        {
            float duration = _recorder.Duration;
            int eventCount = _recorder.Events.Count;
            var file = new RecFile
            {
                Version = RecFile.CurrentVersion,
                LevelPath = _recordingLevelPath,
                MouseSensitivity = _recordingMouseSensitivity,
                PlayerSnapshot = _recordingPlayerSnapshot,
                Events = _recorder.Events.ToList()
            };

            string path = _recordingPath;
            RecFileSerializer.Write(path, file);
            ClearRecordingState();
            return ConsoleCommandResult.Ok(
                $"Saved recording '{path}' ({eventCount} events, {duration:F2}s).");
        }
        catch (Exception ex)
        {
            ClearRecordingState();
            return ConsoleCommandResult.Fail($"stoprecord: {ex.Message}");
        }
    }

    public ConsoleCommandResult StartReplay(string filename)
    {
        if (_loadLevel == null || _restartLevel == null || _getCurrentLevelPath == null
            || _applyMouseSensitivity == null || _restoreControlSettings == null
            || _applyPlayerSnapshot == null)
        {
            return ConsoleCommandResult.Fail("Recording system is not configured.");
        }

        if (IsRecording)
            return ConsoleCommandResult.Fail("Stop recording before replay.");

        if (IsReplaying)
            return ConsoleCommandResult.Fail("Already replaying. Use 'stopreplay' first.");

        if (string.IsNullOrWhiteSpace(filename))
            return ConsoleCommandResult.Fail("Usage: replay <filename>");

        string path = ResolveDemoPath(filename);

        try
        {
            var rec = RecFileSerializer.Read(path);

            if (!string.Equals(rec.LevelPath, _getCurrentLevelPath(), StringComparison.OrdinalIgnoreCase))
            {
                var loadResult = _loadLevel(rec.LevelPath);
                if (!loadResult.Success)
                    return ConsoleCommandResult.Fail($"Replay level load failed: {loadResult.Message}");
            }

            var restartResult = _restartLevel();
            if (!restartResult.Success)
                return ConsoleCommandResult.Fail($"Replay restart failed: {restartResult.Message}");

            if (rec.PlayerSnapshot != null)
                _applyPlayerSnapshot(rec.PlayerSnapshot);

            _applyMouseSensitivity(rec.MouseSensitivity);
            _inputSystem.DisableMouse();
            _replayProvider.Reset(rec.Events);
            _activeProvider = _replayProvider;

            string snapshotNote = rec.PlayerSnapshot != null
                ? "player snapshot restored"
                : "no player snapshot (legacy recording)";

            return ConsoleCommandResult.Ok(
                $"Replaying '{path}' ({rec.Events.Count} events, level '{rec.LevelPath}', {snapshotNote}).");
        }
        catch (Exception ex)
        {
            StopReplayInternal(restoreControls: true);
            return ConsoleCommandResult.Fail($"replay: {ex.Message}");
        }
    }

    public ConsoleCommandResult StopReplay()
    {
        if (!IsReplaying)
            return ConsoleCommandResult.Fail("Not currently replaying.");

        StopReplayInternal(restoreControls: true);
        return ConsoleCommandResult.Ok("Replay stopped.");
    }

    public ConsoleCommandResult SendRecording(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return ConsoleCommandResult.Fail("Usage: sendrecording <filename>");

        if (!RecordingNameSanitizer.TrySanitize(filename, out var sanitizedName, out var sanitizeError))
            return ConsoleCommandResult.Fail(sanitizeError);

        string path = ResolveDemoPath(filename);
        if (!File.Exists(path))
            return ConsoleCommandResult.Fail($"Recording not found: '{path}'.");

        if (_pendingUpload is { IsCompleted: false })
            return ConsoleCommandResult.Fail("Recording upload already in progress.");

        _pendingUploadName = sanitizedName;
        _pendingUpload = _uploadClient.SendAsync(sanitizedName, path);
        return ConsoleCommandResult.Ok($"Uploading recording '{sanitizedName}'...");
    }

    public void Update(float deltaTime)
    {
        if (IsReplaying && _replayProvider.IsFinished)
            StopReplayInternal(restoreControls: true);

        CompletePendingUpload();
    }

    private void CompletePendingUpload()
    {
        if (_pendingUpload is not { IsCompleted: true } task)
            return;

        string name = _pendingUploadName ?? "recording";
        _pendingUpload = null;
        _pendingUploadName = null;

        ConsoleCommandResult result;
        if (task.IsCompletedSuccessfully)
            result = ConsoleCommandResult.Ok($"Uploaded recording '{name}' to server.");
        else if (task.IsFaulted)
            result = ConsoleCommandResult.Fail($"sendrecording: {task.Exception?.GetBaseException().Message ?? "Upload failed."}");
        else
            result = ConsoleCommandResult.Fail("sendrecording: Upload was cancelled.");

        _onUploadCompleted?.Invoke(result);
    }

    private void OnLivePolled(InputPollResult poll, float deltaTime)
    {
        if (!IsRecording)
            return;

        _recorder.CaptureFrame(poll, deltaTime);
    }

    private void StopReplayInternal(bool restoreControls)
    {
        _activeProvider = _liveProvider;
        if (restoreControls)
            _restoreControlSettings?.Invoke();
    }

    private void ClearRecordingState()
    {
        _recordingPath = null;
        _recordingLevelPath = null;
        _recordingMouseSensitivity = 0f;
        _recordingPlayerSnapshot = null;
        _recorder.Reset();
    }

    private static string ResolveDemoPath(string filename)
    {
        filename = filename.Trim();
        if (Path.IsPathRooted(filename) || filename.Contains(Path.DirectorySeparatorChar) || filename.Contains('/'))
            return filename;

        if (!filename.EndsWith(".rec", StringComparison.OrdinalIgnoreCase))
            filename += ".rec";

        return Path.Combine(DemosFolder, filename);
    }
}
