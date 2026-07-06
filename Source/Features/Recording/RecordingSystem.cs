using Game.Core.Level;
using Game.DebugConsole;
using Game.Engine.Input;

namespace Game.Features.Recording;

public sealed class RecordingSystem
{
    private const string RecordingsFolder = "recordings";
    private const int MaxReportedDivergences = 5;

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
    private Func<int>? _getSimulationTickHz;
    private Action<int>? _setSimulationTickHz;
    private Func<long, ChecksumKeyframe>? _captureChecksum;
    private Func<int>? _getRngSeed;
    private Action<int?>? _setRngSeedOverride;
    private Action<ConsoleCommandResult>? _onConsoleFeedback;

    private Task? _pendingUpload;
    private string? _pendingUploadName;
    private IInputProvider _activeProvider;
    private string? _recordingPath;
    private string? _recordingLevelPath;
    private float _recordingMouseSensitivity;
    private int _recordingRngSeed;
    private int _tickHzBeforeReplay;
    private PlayerSnapshot? _recordingPlayerSnapshot;

    // Replay checksum verification state
    private IReadOnlyList<ChecksumKeyframe> _replayChecksums = Array.Empty<ChecksumKeyframe>();
    private int _nextChecksumIndex;
    private int _checksumsCompared;
    private int _checksumsMatched;
    private readonly List<string> _divergences = new();
    private bool _verifyReplayRequested;

    public RecordingSystem(InputSystem inputSystem)
    {
        _inputSystem = inputSystem;
        _liveProvider = new LiveInputProvider(inputSystem);
        _activeProvider = _liveProvider;
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
        Func<int> getSimulationTickHz,
        Action<int> setSimulationTickHz,
        Func<long, ChecksumKeyframe> captureChecksum,
        Func<int> getRngSeed,
        Action<int?> setRngSeedOverride,
        Action<ConsoleCommandResult>? onConsoleFeedback = null)
    {
        _loadLevel = loadLevel;
        _restartLevel = restartLevel;
        _getCurrentLevelPath = getCurrentLevelPath;
        _applyMouseSensitivity = applyMouseSensitivity;
        _restoreControlSettings = restoreControlSettings;
        _capturePlayerSnapshot = capturePlayerSnapshot;
        _applyPlayerSnapshot = applyPlayerSnapshot;
        _getSimulationTickHz = getSimulationTickHz;
        _setSimulationTickHz = setSimulationTickHz;
        _captureChecksum = captureChecksum;
        _getRngSeed = getRngSeed;
        _setRngSeedOverride = setRngSeedOverride;
        _onConsoleFeedback = onConsoleFeedback;
    }

    /// <summary>Sample live input once per render frame, before the simulation tick loop.</summary>
    public void BeginInputFrame()
    {
        if (!IsReplaying)
            _liveProvider.BeginFrame();
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
        _liveProvider.ResetLatches();
        _recordingPlayerSnapshot = _capturePlayerSnapshot();
        _recordingPath = ResolveRecordingPath(filename);
        _recordingLevelPath = _getCurrentLevelPath();
        _recordingMouseSensitivity = mouseSensitivity;
        _recordingRngSeed = _getRngSeed?.Invoke() ?? 0;

        return ConsoleCommandResult.Ok(
            $"Recording to '{_recordingPath}' (level restarted, snapshot captured).");
    }

    public ConsoleCommandResult StopRecording()
    {
        if (!IsRecording || _recordingPath == null || _recordingLevelPath == null)
            return ConsoleCommandResult.Fail("Not currently recording.");

        if (_getSimulationTickHz == null)
            return ConsoleCommandResult.Fail("Recording system is not configured.");

        try
        {
            float duration = _recorder.Duration;
            int eventCount = _recorder.Events.Count;
            int tickHz = _getSimulationTickHz();
            long durationTicks = _recorder.DurationTicks;
            var file = new RecFile
            {
                Version = RecFile.CurrentVersion,
                LevelPath = _recordingLevelPath,
                MouseSensitivity = _recordingMouseSensitivity,
                TickHz = tickHz,
                DurationTicks = durationTicks,
                RngSeed = _recordingRngSeed,
                PlayerSnapshot = _recordingPlayerSnapshot,
                Events = _recorder.Events.ToList(),
                Checksums = _recorder.Checksums.ToList()
            };

            string path = _recordingPath;
            RecFileSerializer.Write(path, file);
            ClearRecordingState();
            return ConsoleCommandResult.Ok(
                $"Saved recording '{path}' ({eventCount} events, {durationTicks} ticks, {duration:F2}s, {tickHz} Hz, {file.Checksums.Count} checksums).");
        }
        catch (Exception ex)
        {
            ClearRecordingState();
            return ConsoleCommandResult.Fail($"stoprecord: {ex.Message}");
        }
    }

    public ConsoleCommandResult StartReplay(string filename) =>
        StartReplay(filename, verify: false);

    public ConsoleCommandResult StartVerifyReplay(string filename) =>
        StartReplay(filename, verify: true);

    private ConsoleCommandResult StartReplay(string filename, bool verify)
    {
        if (_loadLevel == null || _restartLevel == null || _getCurrentLevelPath == null
            || _applyMouseSensitivity == null || _restoreControlSettings == null
            || _applyPlayerSnapshot == null || _setSimulationTickHz == null || _getSimulationTickHz == null)
        {
            return ConsoleCommandResult.Fail("Recording system is not configured.");
        }

        if (IsRecording)
            return ConsoleCommandResult.Fail("Stop recording before replay.");

        if (IsReplaying)
            return ConsoleCommandResult.Fail("Already replaying. Use 'stopreplay' first.");

        if (string.IsNullOrWhiteSpace(filename))
            return ConsoleCommandResult.Fail(verify ? "Usage: verifyreplay <filename>" : "Usage: replay <filename>");

        string path = ResolveRecordingPath(filename);

        try
        {
            var rec = RecFileSerializer.Read(path);

            if (!RecFileValidator.TryValidateForReplay(rec, LevelExists, out string validationError))
                return ConsoleCommandResult.Fail($"replay: {validationError}");

            if (!string.Equals(rec.LevelPath, _getCurrentLevelPath(), StringComparison.OrdinalIgnoreCase))
            {
                var loadResult = _loadLevel(rec.LevelPath);
                if (!loadResult.Success)
                    return ConsoleCommandResult.Fail($"Replay level load failed: {loadResult.Message}");
            }

            // Re-seed the level exactly as it was seeded when the recording started.
            if (rec.RngSeed.HasValue)
                _setRngSeedOverride?.Invoke(rec.RngSeed.Value);

            var restartResult = _restartLevel();
            _setRngSeedOverride?.Invoke(null);
            if (!restartResult.Success)
                return ConsoleCommandResult.Fail($"Replay restart failed: {restartResult.Message}");

            if (rec.PlayerSnapshot != null)
                _applyPlayerSnapshot(rec.PlayerSnapshot);

            _tickHzBeforeReplay = _getSimulationTickHz();
            int replayTickHz = rec.ResolveTickHz();
            _setSimulationTickHz(replayTickHz);

            _applyMouseSensitivity(rec.MouseSensitivity);
            _inputSystem.DisableMouse();
            _replayProvider.Reset(rec.Events, rec.UsesTickIndexedEvents, rec.ResolveDurationTicks());
            ResetReplayChecksumState();
            _replayChecksums = rec.Checksums;
            _verifyReplayRequested = verify;
            _activeProvider = _replayProvider;

            string snapshotNote = rec.PlayerSnapshot != null
                ? "player snapshot restored"
                : "no player snapshot (legacy recording)";

            string tickNote = rec.Version >= 3
                ? $"{replayTickHz} Hz"
                : $"{replayTickHz} Hz (legacy, assumed default)";

            string timingNote = rec.UsesTickIndexedEvents
                ? $"tick-indexed, {rec.ResolveDurationTicks()} ticks"
                : "time-indexed events (legacy, replay may be imprecise)";

            string verifyNote = verify
                ? rec.Checksums.Count > 0
                    ? $", verifying {rec.Checksums.Count} checksums"
                    : ", no checksums to verify (legacy recording)"
                : string.Empty;

            return ConsoleCommandResult.Ok(
                $"Replaying '{path}' ({rec.Events.Count} events, level '{rec.LevelPath}', {tickNote}, {timingNote}, {snapshotNote}{verifyNote}).");
        }
        catch (InvalidDataException ex)
        {
            StopReplayInternal(restoreControls: true);
            return ConsoleCommandResult.Fail($"replay: {ex.Message}");
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

        string path = ResolveRecordingPath(filename);
        if (!File.Exists(path))
            return ConsoleCommandResult.Fail($"Recording not found: '{path}'.");

        if (_pendingUpload is { IsCompleted: false })
            return ConsoleCommandResult.Fail("Recording upload already in progress.");

        _pendingUploadName = sanitizedName;
        _pendingUpload = _uploadClient.SendAsync(sanitizedName, path);
        return ConsoleCommandResult.Ok($"Uploading recording '{sanitizedName}'...");
    }

    public static IReadOnlyList<string> ListRecordings()
    {
        EnsureRecordingsFolderExists();
        return Directory.GetFiles(RecordingsFolder, "*.rec")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .ToList();
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

        _onConsoleFeedback?.Invoke(result);
    }

    public void CaptureTick(InputPollResult poll, long tickIndex)
    {
        if (!IsRecording || _getSimulationTickHz == null)
            return;

        _recorder.CaptureTick(poll, tickIndex, _getSimulationTickHz());
    }

    /// <summary>
    /// Called at the end of every simulation tick. Records checksum keyframes while
    /// recording; compares them against the recording while replaying.
    /// </summary>
    public void OnTickSimulated(long tickIndex)
    {
        if (_captureChecksum == null)
            return;

        if (IsRecording)
        {
            if (SimulationChecksum.IsKeyframeTick(tickIndex))
                _recorder.CaptureChecksum(_captureChecksum(tickIndex));
            return;
        }

        if (!IsReplaying || _nextChecksumIndex >= _replayChecksums.Count)
            return;

        var expected = _replayChecksums[_nextChecksumIndex];
        if (expected.Tick != tickIndex)
            return;

        _nextChecksumIndex++;
        _checksumsCompared++;

        var actual = _captureChecksum(tickIndex);
        if (expected.Matches(actual))
        {
            _checksumsMatched++;
            return;
        }

        if (_divergences.Count < MaxReportedDivergences)
        {
            _divergences.Add(
                $"tick {tickIndex}: diverged in {string.Join(", ", expected.DiffComponents(actual))}");
        }
    }

    /// <summary>
    /// Called when the level state is reset outside of the recording system's own
    /// restarts (death restart, 'restart'/'load' console commands). The sim clock
    /// resets to tick 0, which would corrupt tick indices, so end the session cleanly.
    /// </summary>
    public void OnLevelStateReset()
    {
        if (IsRecording)
        {
            var result = StopRecording();
            _onConsoleFeedback?.Invoke(result.Success
                ? ConsoleCommandResult.Ok($"Level reset while recording - recording stopped. {result.Message}")
                : ConsoleCommandResult.Fail($"Level reset while recording - {result.Message}"));
            return;
        }

        if (IsReplaying)
        {
            StopReplayInternal(restoreControls: true);
            _onConsoleFeedback?.Invoke(
                ConsoleCommandResult.Ok("Level reset while replaying - replay stopped."));
        }
    }

    private void StopReplayInternal(bool restoreControls)
    {
        bool wasReplaying = IsReplaying;
        _activeProvider = _liveProvider;
        _liveProvider.ResetLatches();
        if (restoreControls)
        {
            // 0 means replay failed before the pre-replay tick rate was captured;
            // restoring it would clamp the sim to the minimum tick rate.
            if (_tickHzBeforeReplay > 0)
                _setSimulationTickHz?.Invoke(_tickHzBeforeReplay);
            _restoreControlSettings?.Invoke();
        }

        if (wasReplaying)
            EmitReplayChecksumSummary();
        ResetReplayChecksumState();
    }

    private void EmitReplayChecksumSummary()
    {
        if (_onConsoleFeedback == null)
            return;

        if (_replayChecksums.Count == 0)
        {
            if (_verifyReplayRequested)
                _onConsoleFeedback(ConsoleCommandResult.Ok(
                    "verifyreplay: recording has no checksums (pre-v5), nothing to verify."));
            return;
        }

        if (_divergences.Count > 0)
        {
            _onConsoleFeedback(ConsoleCommandResult.Fail(
                $"Replay DIVERGED ({_checksumsMatched}/{_checksumsCompared} keyframes matched). {_divergences[0]}"));
            for (int i = 1; i < _divergences.Count; i++)
                _onConsoleFeedback(ConsoleCommandResult.Fail($"  {_divergences[i]}"));
            return;
        }

        if (_checksumsCompared > 0 || _verifyReplayRequested)
        {
            _onConsoleFeedback(ConsoleCommandResult.Ok(
                $"Replay verified: {_checksumsMatched}/{_checksumsCompared} checksum keyframes matched " +
                $"({_replayChecksums.Count} in recording)."));
        }
    }

    private void ResetReplayChecksumState()
    {
        _replayChecksums = Array.Empty<ChecksumKeyframe>();
        _nextChecksumIndex = 0;
        _checksumsCompared = 0;
        _checksumsMatched = 0;
        _divergences.Clear();
        _verifyReplayRequested = false;
    }

    private void ClearRecordingState()
    {
        _recordingPath = null;
        _recordingLevelPath = null;
        _recordingMouseSensitivity = 0f;
        _recordingRngSeed = 0;
        _recordingPlayerSnapshot = null;
        _recorder.Reset();
    }

    private static void EnsureRecordingsFolderExists()
    {
        Directory.CreateDirectory(RecordingsFolder);
    }

    private static bool LevelExists(string levelPath) =>
        LevelCatalog.TryResolve(levelPath, out _, out _);

    private static string ResolveRecordingPath(string filename)
    {
        filename = filename.Trim();
        if (Path.IsPathRooted(filename) || filename.Contains(Path.DirectorySeparatorChar) || filename.Contains('/'))
            return filename;

        if (!filename.EndsWith(".rec", StringComparison.OrdinalIgnoreCase))
            filename += ".rec";

        return Path.Combine(RecordingsFolder, filename);
    }
}
