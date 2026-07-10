using System.Numerics;
using Game.DebugConsole;
using Game.Editor;
using Game.Engine.Movement;
using Game.Engine.Simulation;
using Game.Features.Animation;
using Game.Features.Combat;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.Highscores;
using Game.Features.Hud;
using Game.Features.LevelProgress;
using Game.Features.Options;
using Game.Features.Pickups;
using Game.Features.Players;
using Game.Features.Recording;
using Game.Features.WorldObjects;
using Game.Features.SoundPropagation;
using Game.Engine.Rendering;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Core;

public class World : IScene
{
    private string _currentLevelPath = LevelCatalog.DefaultLevelPath;

    private readonly Player _player;
    private readonly SoundSystem _soundSystem;
    private readonly EffectSystem _effectSystem;
    private readonly CombatFeedback _combatFeedback;
    private readonly MapData _mapData;
    private readonly LevelData _level;
    private readonly List<Texture2D> _tileTextures;
    private readonly List<Texture2D> _gameTextures;
    private readonly InputSystem _inputSystem;
    private readonly RecordingSystem _recordingSystem;
    private readonly FixedSimulationClock _simulationClock = new();
    private readonly TickDiagnostics _tickDiagnostics = new();
    private readonly CollisionSystem _collisionSystem;
    private readonly CameraSystem _cameraSystem;
    private readonly DoorSystem _doorSystem;
    private readonly RenderSystem _renderSystem;
    private readonly LightOcclusionMap _lightOcclusionMap = new();
    private readonly AnimationSystem _animationSystem;
    private readonly MinimapSystem _minimapSystem;
    private readonly ConsoleOverlay _consoleOverlay;
    private readonly RuntimeConsoleService _runtimeConsole;
    private readonly EnemySystem _enemySystem;
    private readonly PickupSystem _pickupSystem;
    private readonly PlacedObjectSystem _placedObjectSystem;
    private readonly WeaponSystem _weaponSystem;
    private readonly SoundPropagationSystem _soundPropagationSystem;
    private readonly PlayerSystem _playerSystem;
    private readonly ScoreSystem _scoreSystem;
    private readonly ExitSystem _exitSystem;
    private readonly SecretSystem _secretSystem;
    private readonly HighscoreClient _highscoreClient;
    private readonly HighscoreIntermission _highscoreIntermission;
    private readonly HighscoreBoardOverlay _highscoreBoardOverlay;
    private readonly OptionsMenuSystem _optionsMenu = new();
    private bool _highscoreIntermissionStarted;
    private bool _suppressLevelCompleteClickRestart;

    private RenderTexture2D _sceneRenderTexture;
    private RenderTexture2D _hudRenderTexture;
    private bool _hasSceneRenderTexture;
    private bool _hasHudRenderTexture;
    private InputState _inputState = new();
    private SimulationPose _previousSimulationPose;
    private SimulationPose _currentSimulationPose;
    private int _currentRngSeed;
    private int? _rngSeedOverride;

    public Player Player => _player;
    public PlayerSystem PlayerSystem => _playerSystem;
    public EnemySystem EnemySystem => _enemySystem;
    public DoorSystem DoorSystem => _doorSystem;
    public SoundPropagationSystem SoundPropagationSystem => _soundPropagationSystem;
    public ScoreSystem ScoreSystem => _scoreSystem;
    public ExitSystem ExitSystem => _exitSystem;
    public SecretSystem SecretSystem => _secretSystem;
    public string CurrentLevelPath => _currentLevelPath;

    public World(MapData mapData)
    {
        _mapData = mapData;
        _level = new LevelData(mapData);
        _tileTextures = mapData.TileTextures;
        _gameTextures = mapData.GameTextures;
        _player = new Player();

        _soundSystem = new SoundSystem(Res.Path("resources/03.mp3"));
        _effectSystem = new EffectSystem();
        _combatFeedback = new CombatFeedback(_soundSystem, _effectSystem);
        _inputSystem = new InputSystem();
        _recordingSystem = new RecordingSystem(_inputSystem);
        _doorSystem = new DoorSystem(mapData.Doors, mapData.Width, _tileTextures);
        _scoreSystem = new ScoreSystem();
        _secretSystem = new SecretSystem(_scoreSystem, _tileTextures);
        _collisionSystem = new CollisionSystem(_level, new CompositeMovementBlocker(_doorSystem, _secretSystem));
        _soundPropagationSystem = new SoundPropagationSystem(mapData, _doorSystem);
        _cameraSystem = new CameraSystem(_collisionSystem);
        _renderSystem = new RenderSystem(_level, _mapData, _tileTextures);
        _minimapSystem = new MinimapSystem(_level, _renderSystem);
        _exitSystem = new ExitSystem(_scoreSystem);
        _highscoreClient = new HighscoreClient();
        _pickupSystem = new PickupSystem(_scoreSystem);
        _placedObjectSystem = new PlacedObjectSystem();
        _enemySystem = new EnemySystem(
            _player, _inputSystem, _collisionSystem, _doorSystem, _combatFeedback,
            _pickupSystem, _scoreSystem, _soundPropagationSystem);
        _pickupSystem.SetObjectsTexture(_gameTextures[GameTextureIndex.Objects]);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);
        _placedObjectSystem.SetObjectsTexture(_gameTextures[GameTextureIndex.Objects]);
        _placedObjectSystem.Rebuild(_mapData);
        _animationSystem = new AnimationSystem(
            _gameTextures[GameTextureIndex.EnemyGuard],
            _gameTextures[GameTextureIndex.Weapons],
            _player,
            _enemySystem);
        _weaponSystem = new WeaponSystem(
            _mapData,
            _doorSystem,
            _enemySystem,
            _gameTextures[GameTextureIndex.EnemyGuard],
            _effectSystem,
            _soundSystem,
            _animationSystem,
            _soundPropagationSystem);
        _playerSystem = new PlayerSystem(
            _player,
            _inputSystem,
            _collisionSystem,
            _cameraSystem,
            _pickupSystem,
            _doorSystem,
            _animationSystem,
            _enemySystem,
            _weaponSystem,
            _effectSystem,
            _exitSystem,
            _secretSystem);

        _consoleOverlay = new ConsoleOverlay();
        _runtimeConsole = WorldConsoleBindings.CreateConsole(
            this,
            _player,
            _enemySystem,
            _scoreSystem,
            _consoleOverlay,
            _recordingSystem,
            () => _optionsMenu.Settings.MouseSensitivity);
        _highscoreIntermission = new HighscoreIntermission(
            _highscoreClient,
            submission => _recordingSystem.PrepareRecordingForScoreSubmission(submission),
            submission => _recordingSystem.QueueRecordingUploadForScore(submission),
            () => _recordingSystem.DiscardCurrentRecording(),
            StartReplayRemote,
            result => _runtimeConsole.WriteFeedback(result));
        _highscoreBoardOverlay = new HighscoreBoardOverlay(
            _highscoreClient,
            () => _currentLevelPath,
            StartReplayRemote,
            result => _runtimeConsole.WriteFeedback(result));
        _recordingSystem.Configure(
            LoadLevel,
            RestartCurrentLevel,
            () => _currentLevelPath,
            SetMouseSensitivity,
            () =>
            {
                ApplyControlSettings();
                _inputSystem.RestoreGameplayMouse();
            },
            () => PlayerSnapshotApplication.From(_player),
            snapshot =>
            {
                snapshot.ApplyTo(_player);
                ResetSimulationPoses();
            },
            () => _simulationClock.TickHz,
            tickHz => _simulationClock.SetTickHz(tickHz),
            tick => SimulationChecksum.Capture(
                tick, _player, _enemySystem.Enemies, _doorSystem.Doors, _scoreSystem),
            () => _currentRngSeed,
            seed => _rngSeedOverride = seed,
            result => _runtimeConsole.WriteFeedback(result),
            () =>
            {
                _consoleOverlay.Close();
                _inputSystem.DisableMouse();
            });
        _playerSystem.ConfigureLifecycle(
            () => _consoleOverlay.IsOpen,
            () => _ = RestartCurrentLevel(),
            () => _highscoreIntermission.IsBlockingRestart,
            () => _recordingSystem.IsReplaying,
            () => _suppressLevelCompleteClickRestart);
        _playerSystem.ResetForLevelLoad(_mapData);
        ResetSimulationPoses();
        _exitSystem.Rebuild(_mapData);
        _secretSystem.Rebuild(_mapData);
        _renderSystem.RebuildMeshes();

        WindowDisplayMode.SyncRenderDataFromWindow();
        GameRenderResolution.Apply(
            _optionsMenu.Settings,
            ResolveWindowWidth(),
            ResolveWindowHeight());
        _sceneRenderTexture = LoadRenderTexture(RenderData.InternalWidth, RenderData.InternalHeight);
        _hudRenderTexture = LoadRenderTexture(RenderData.InternalWidth, RenderData.InternalHeight);
        _hasSceneRenderTexture = true;
        _hasHudRenderTexture = true;
        Debug.Setup(_doorSystem.Doors, _player, _animationSystem, _enemySystem);
        ApplyGraphicsSettings();
        ApplyControlSettings();
        ApplyAudioSettings();
        EnsureStartupDisplay();
#if DEBUG
        ConsoleSelfTests.RunOnce();
#endif
    }

    public void SetVolume(float volume)
    {
        _soundSystem.SetVolume(volume);
    }

    public float GetVolume()
    {
        return _soundSystem.GetVolume();
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        _cameraSystem.SetMouseSensitivity(sensitivity);
    }

    public void ApplyControlSettings()
    {
        SetMouseSensitivity(_optionsMenu.Settings.MouseSensitivity);
    }

    public void ApplyAudioSettings()
    {
        var settings = _optionsMenu.Settings;
        _soundSystem.SetSfxLevel(settings.AudioLevel);
        _soundSystem.SetMusicLevel(settings.MusicLevel);
    }

    public void ApplyWindowDisplay()
    {
        WindowDisplayMode.Apply(_optionsMenu.Settings);
    }

    public void ApplyGameResolution()
    {
        GameRenderResolution.Apply(
            _optionsMenu.Settings,
            ResolveWindowWidth(),
            ResolveWindowHeight());
        RecreateRenderTextures();
    }

    public bool GetFullscreenEnabled() => _optionsMenu.Settings.FullscreenEnabled;

    public void SetFullscreenEnabled(bool enabled)
    {
        _optionsMenu.Settings.FullscreenEnabled = enabled;
        ApplyWindowDisplay();
        ApplyGameResolution();
    }

    public string GetWindowResolutionPresetId() => _optionsMenu.Settings.WindowResolutionPresetId;

    public void SetWindowResolutionPresetId(string presetId)
    {
        _optionsMenu.Settings.WindowResolutionPresetId = presetId;
        ApplyWindowDisplay();
        ApplyGameResolution();
    }

    public string GetGameResolutionPresetId() => _optionsMenu.Settings.GameResolutionPresetId;

    public void SetGameResolutionPresetId(string presetId)
    {
        _optionsMenu.Settings.GameResolutionPresetId = presetId;
        ApplyGameResolution();
    }

    public bool GetVSyncEnabled() => _optionsMenu.Settings.VSyncEnabled;

    public void SetVSyncEnabled(bool enabled)
    {
        _optionsMenu.Settings.VSyncEnabled = enabled;
        ApplyGraphicsSettings();
    }

    public int GetTargetFps() => _optionsMenu.Settings.TargetFps;

    public void SetTargetFps(int fps)
    {
        _optionsMenu.Settings.TargetFps = GraphicsFramePacing.ClampTargetFps(fps);
        ApplyGraphicsSettings();
    }

    public void ApplyGraphicsSettings() => GraphicsFramePacing.Apply(_optionsMenu.Settings);

    public void EnsureStartupDisplay()
    {
        WindowDisplayMode.ReapplyFullscreenIfNeeded(_optionsMenu.Settings);
        WindowDisplayMode.SyncRenderDataFromWindow();

        if (KnownResolutions.FindById(_optionsMenu.Settings.GameResolutionPresetId).IsNative)
            ApplyGameResolution();
    }

    public void OnWindowResize()
    {
        WindowDisplayMode.SyncRenderDataFromWindow();
        if (KnownResolutions.FindById(_optionsMenu.Settings.GameResolutionPresetId).IsNative)
            ApplyGameResolution();
    }

    private static int ResolveWindowWidth()
    {
        int width = (int)RenderData.Resolution.X;
        return width > 0 ? width : GetScreenWidth();
    }

    private static int ResolveWindowHeight()
    {
        int height = (int)RenderData.Resolution.Y;
        return height > 0 ? height : GetScreenHeight();
    }

    private void RecreateRenderTextures()
    {
        int newWidth = RenderData.InternalWidth;
        int newHeight = RenderData.InternalHeight;

        if (_hasSceneRenderTexture &&
            _sceneRenderTexture.Texture.Width == newWidth &&
            _sceneRenderTexture.Texture.Height == newHeight &&
            _hasHudRenderTexture &&
            _hudRenderTexture.Texture.Width == newWidth &&
            _hudRenderTexture.Texture.Height == newHeight)
            return;

        if (_hasSceneRenderTexture)
            UnloadRenderTexture(_sceneRenderTexture);
        if (_hasHudRenderTexture)
            UnloadRenderTexture(_hudRenderTexture);

        _sceneRenderTexture = LoadRenderTexture(newWidth, newHeight);
        _hudRenderTexture = LoadRenderTexture(newWidth, newHeight);
        _hasSceneRenderTexture = true;
        _hasHudRenderTexture = true;
    }

    public void OnEnter()
    {
        _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        _enemySystem.Rebuild(_mapData.Enemies, _mapData);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);
        _placedObjectSystem.Rebuild(_mapData);
        _scoreSystem.ResetForLevel(_mapData);
        _exitSystem.Rebuild(_mapData);
        _secretSystem.Rebuild(_mapData);
        _renderSystem.RebuildMeshes();

        if (OperatingSystem.IsBrowser())
        {
            _highscoreClient.PrefetchLeaderboardAccess(_currentLevelPath);
            _inputSystem.EnableMouse();
        }
        else
            _inputSystem.DisableMouse();

        _recordingSystem.ResetInputLatches();
        TryStartAutoRecording();
    }

    public ConsoleCommandResult LoadLevel(string pathOrName)
    {
        if (!LevelCatalog.TryResolve(pathOrName, out var resolvedPath, out var error))
            return ConsoleCommandResult.Fail(error);

        try
        {
            LevelSerializer.LoadFromJson(_mapData, Res.Path(resolvedPath));
            _currentLevelPath = resolvedPath;
            ResetLevelState();
            return ConsoleCommandResult.Ok($"Loaded '{resolvedPath}'.");
        }
        catch (Exception ex)
        {
            return ConsoleCommandResult.Fail($"load: {ex.Message}");
        }
    }

    public ConsoleCommandResult RestartCurrentLevel()
    {
        try
        {
            LevelSerializer.LoadFromJson(_mapData, Res.Path(_currentLevelPath));
            ResetLevelState();
            return ConsoleCommandResult.Ok($"Restarted from '{_currentLevelPath}'.");
        }
        catch (Exception ex)
        {
            return ConsoleCommandResult.Fail($"restart: {ex.Message}");
        }
    }

    public ConsoleCommandResult ListPickupsForConsole()
    {
        var placements = _mapData.Pickups;
        if (placements.Count == 0)
            return ConsoleCommandResult.Ok($"No pickups in '{_currentLevelPath}'.");

        var activeByTile = new HashSet<(int X, int Y)>(
            _pickupSystem.ActivePickups.Select(p => (p.TileX, p.TileY)));

        var rows = new List<string>(placements.Count);
        for (int i = 0; i < placements.Count; i++)
        {
            var placement = placements[i];
            int amount = PickupDefaults.GetAmount(placement.Type, placement.Amount);
            string amountText = placement.Amount == 0
                ? $"amount={amount} (default)"
                : $"amount={amount}";

            var world = LevelData.GetTileAnchorWorld(placement.TileX, placement.TileY, 1.5f);
            string active = activeByTile.Contains((placement.TileX, placement.TileY)) ? "yes" : "no";

            rows.Add(
                $"#{i} {placement.Type} tile=({placement.TileX},{placement.TileY}) {amountText} " +
                $"world=({world.X:F1},{world.Y:F1},{world.Z:F1}) active={active}");
        }

        int activeCount = _pickupSystem.ActivePickups.Count;
        string summary = placements.Count == 1
            ? $"1 pickup in '{_currentLevelPath}' ({activeCount} active):"
            : $"{placements.Count} pickups in '{_currentLevelPath}' ({activeCount} active):";

        return ConsoleCommandResult.Ok(summary, rows);
    }

    private void ResetLevelState()
    {
        // A level reset invalidates recording/replay tick indexing (sim clock restarts
        // at tick 0). The recording system's own restarts happen before it flags
        // recording/replay active, so this only fires for external resets.
        _recordingSystem.OnLevelStateReset();

        _currentRngSeed = _rngSeedOverride ?? Random.Shared.Next();
        _enemySystem.SetRandomSeed(_currentRngSeed);

        _playerSystem.ResetForLevelLoad(_mapData);
        _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        _enemySystem.Rebuild(_mapData.Enemies, _mapData);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);
        _placedObjectSystem.Rebuild(_mapData);
        _scoreSystem.ResetForLevel(_mapData);
        _exitSystem.Rebuild(_mapData);
        _secretSystem.Rebuild(_mapData);
        _renderSystem.RebuildMeshes();
        _soundPropagationSystem.ClearPendingEvents();
        _highscoreIntermission.ResetForLevel();
        _highscoreIntermissionStarted = false;
        _effectSystem.Clear();
        _simulationClock.Reset();
        _tickDiagnostics.Reset();
        ResetSimulationPoses();

        if (OperatingSystem.IsBrowser())
            _highscoreClient.PrefetchLeaderboardAccess(_currentLevelPath);

        TryStartAutoRecording();
    }

    private void TryStartAutoRecording()
    {
        if (_recordingSystem.ShouldAutoRecordOnLevelReset)
            _recordingSystem.StartAutoRecording(_optionsMenu.Settings.MouseSensitivity);
    }

    private bool CanToggleHighscoreBoard() =>
        !_consoleOverlay.IsOpen
        && !_optionsMenu.IsOpen
        && !_highscoreIntermission.BlocksHighscoreBoardToggle;

    public void OnExit()
    {
        _optionsMenu.Dismiss();
        _inputSystem.EnableMouse();
    }

    public void ToggleMouse()
    {
        _inputSystem.ToggleMouse();
    }

    private void HandleBrowserPointerLockLost(bool escapeHeld)
    {
        _inputSystem.SyncPointerLockReleased();

        if (!escapeHeld || _consoleOverlay.IsOpen || _optionsMenu.IsOpen || _highscoreIntermission.CapturesEscapeKey)
            return;

        _optionsMenu.Open(_inputSystem);
    }

    private void PollBrowserPointerLockEvents()
    {
        if (!OperatingSystem.IsBrowser())
            return;

        switch (BrowserPointerLockBridge.PollPointerLockEvent())
        {
            case "acquired":
                _inputSystem.OnBrowserPointerLockAcquired();
                break;
            case "failed":
                _inputSystem.OnBrowserPointerLockFailed();
                break;
            case "lost":
                HandleBrowserPointerLockLost(IsKeyDown(KeyboardKey.Escape));
                break;
        }
    }

    public void Update(float deltaTime)
    {
        _soundSystem.Update();
        _recordingSystem.Update(deltaTime);
        bool toggledConsoleThisFrame = false;

        if (_consoleOverlay.IsOpen && IsKeyPressed(KeyboardKey.Escape))
        {
            _consoleOverlay.Close();
            if (!_optionsMenu.IsOpen)
                _inputSystem.RestoreGameplayMouse();
            return;
        }

        if (_optionsMenu.IsOpen)
        {
            var inputResult = _optionsMenu.HandleInput(
                RenderData.InternalWidth,
                RenderData.InternalHeight,
                GetScreenWidth(),
                GetScreenHeight());

            if (inputResult.WindowDisplayChanged)
            {
                ApplyWindowDisplay();
                ApplyGameResolution();
            }

            if (inputResult.GameResolutionChanged)
                ApplyGameResolution();
            if (inputResult.GraphicsChanged)
                ApplyGraphicsSettings();
            if (inputResult.ControlsChanged)
                ApplyControlSettings();
            if (inputResult.AudioChanged)
                ApplyAudioSettings();

            if (IsKeyPressed(KeyboardKey.Escape))
                _optionsMenu.Close(_inputSystem);
            else if (IsKeyPressed(KeyboardKey.Q))
                CloseWindow();

            return;
        }

        if (IsKeyPressed(KeyboardKey.Escape) && !_highscoreIntermission.CapturesEscapeKey)
        {
            if (_consoleOverlay.IsOpen)
                _consoleOverlay.Close();

            // Locked pointer: browser consumes ESC to exit pointer lock; menu opens via BrowserPointerLockBridge.
            if (OperatingSystem.IsBrowser() && !_inputSystem.IsMouseFree)
                return;

            _optionsMenu.Open(_inputSystem);
            return;
        }

        if (!_highscoreIntermission.BlocksConsoleToggle &&
            (IsKeyPressed(KeyboardKey.Grave) ||
            (OperatingSystem.IsBrowser() && IsKeyPressed(KeyboardKey.Period))))
        {
            _consoleOverlay.Toggle();
            toggledConsoleThisFrame = true;
            if (_consoleOverlay.IsOpen)
                _inputSystem.EnableMouse();
            else
                _inputSystem.RestoreGameplayMouse();
        }

        if (_consoleOverlay.IsOpen)
        {
            _consoleOverlay.UpdateInput(
                deltaTime,
                line => ExecuteConsoleLine(line),
                (line, cursor) => _runtimeConsole.GetCompletions(line, cursor),
                toggledConsoleThisFrame);
            return;
        }

        if (_highscoreBoardOverlay.IsOpen)
        {
            if (!_inputSystem.IsMouseFree)
                _inputSystem.EnableMouse();
            _highscoreBoardOverlay.Update();
            return;
        }

        if (CanToggleHighscoreBoard() && IsKeyPressed(KeyboardKey.H))
        {
            _highscoreBoardOverlay.Toggle();
            if (_highscoreBoardOverlay.IsOpen)
                _inputSystem.EnableMouse();
            else
                _inputSystem.RestoreGameplayMouse();
            return;
        }

        if (!_recordingSystem.IsReplaying)
        {
            PollBrowserPointerLockEvents();
            _inputSystem.Update();
        }

        // Sample Raylib once per render frame; ticks consume latched edges/deltas
        // so per-frame input maps 1:1 onto simulation ticks (see LiveInputProvider).
        _recordingSystem.BeginInputFrame();

        _tickDiagnostics.BeginFrame(deltaTime);
        int ticksThisFrame = _simulationClock.Advance(deltaTime);
        _tickDiagnostics.RecordSimulationStep(_simulationClock);

        _suppressLevelCompleteClickRestart = false;
        if (_exitSystem.IsLevelComplete && _highscoreIntermission.IsLeaderboardInteractive)
            _suppressLevelCompleteClickRestart = _highscoreIntermission.TryHandleLeaderboardInput();

        for (int i = 0; i < ticksThisFrame; i++)
            SimulateGameplayTick(_simulationClock.FixedDeltaTime);

        if (_exitSystem.IsLevelComplete && !_highscoreIntermissionStarted)
        {
            _highscoreIntermissionStarted = true;
            if (!_recordingSystem.IsReplaying)
                _highscoreIntermission.Begin(_currentLevelPath, _scoreSystem);
        }

        if (_exitSystem.IsLevelComplete)
            _highscoreIntermission.Update();
    }

    private void SimulateGameplayTick(float fixedDeltaTime)
    {
        var poll = _recordingSystem.ActiveProvider.Poll(fixedDeltaTime);
        _recordingSystem.CaptureTick(poll, _simulationClock.TickIndex);
        _inputState = poll.InputState;
        var mouseDelta = poll.MouseDelta;

        if (!_exitSystem.IsBlockingGameplay)
            _scoreSystem.Tick(fixedDeltaTime);

        _effectSystem.Update(fixedDeltaTime);

        int screenWidth = RenderData.InternalWidth;
        int screenHeight = RenderData.InternalHeight;

        if (_player.IsAlive)
        {
            _playerSystem.UpdateAlive(fixedDeltaTime, _inputState, mouseDelta, screenWidth, screenHeight);
            if (!_exitSystem.IsBlockingGameplay)
                _enemySystem.Update(fixedDeltaTime, _inputState);
        }
        else
        {
            _playerSystem.UpdateDead(fixedDeltaTime, _inputState, mouseDelta);
            _enemySystem.Update(fixedDeltaTime, _inputState);
        }

        _recordingSystem.OnTickSimulated(_simulationClock.TickIndex);
        AdvanceSimulationPose();
    }

    private void ResetSimulationPoses()
    {
        _previousSimulationPose = SimulationPose.FromPlayer(_player);
        _currentSimulationPose = _previousSimulationPose;
    }

    private void AdvanceSimulationPose()
    {
        _previousSimulationPose = _currentSimulationPose;
        _currentSimulationPose = SimulationPose.FromPlayer(_player);
    }

    private SimulationPose GetRenderPose()
    {
        if (!_player.IsAlive || _exitSystem.IsBlockingGameplay)
            return SimulationPose.FromPlayer(_player);

        return SimulationPose.Lerp(
            _previousSimulationPose,
            _currentSimulationPose,
            _simulationClock.InterpolationAlpha);
    }

    public ConsoleCommandResult ToggleTickDiagnostics()
    {
        _tickDiagnostics.OverlayEnabled = !_tickDiagnostics.OverlayEnabled;
        return ConsoleCommandResult.Ok(
            _tickDiagnostics.OverlayEnabled
                ? "Tick diagnostics overlay enabled."
                : "Tick diagnostics overlay disabled.");
    }

    public ConsoleCommandResult SetTickDiagnostics(bool enabled)
    {
        _tickDiagnostics.OverlayEnabled = enabled;
        return ConsoleCommandResult.Ok(
            enabled
                ? "Tick diagnostics overlay enabled."
                : "Tick diagnostics overlay disabled.");
    }

    public ConsoleCommandResult GetTickDiagnosticsStatus() =>
        ConsoleCommandResult.Ok(_tickDiagnostics.BuildStatusLine());

    public ConsoleCommandResult ToggleStaticMeshes()
    {
        _renderSystem.UseStaticMeshes = !_renderSystem.UseStaticMeshes;
        return ConsoleCommandResult.Ok(BuildStaticMeshesStatusMessage());
    }

    public ConsoleCommandResult SetStaticMeshes(bool enabled)
    {
        _renderSystem.UseStaticMeshes = enabled;
        return ConsoleCommandResult.Ok(BuildStaticMeshesStatusMessage());
    }

    public ConsoleCommandResult GetStaticMeshesStatus() =>
        ConsoleCommandResult.Ok(BuildStaticMeshesStatusMessage());

    private string BuildStaticMeshesStatusMessage()
    {
        string mode = _renderSystem.UseStaticMeshes ? "on (room-scoped baked meshes)" : "off (legacy quads)";
        return $"Static meshes: {mode}. Baked wall quads: {_renderSystem.BakedQuadCount}.";
    }

    public ConsoleCommandResult ToggleFlying()
    {
        _player.IsFlying = !_player.IsFlying;
        if (_player.IsFlying)
            _player.Velocity = Vector3.Zero;
        return ConsoleCommandResult.Ok(BuildFlyingStatusMessage());
    }

    public ConsoleCommandResult SetFlying(bool enabled)
    {
        _player.IsFlying = enabled;
        if (!_player.IsFlying)
            _player.Velocity = Vector3.Zero;
        return ConsoleCommandResult.Ok(BuildFlyingStatusMessage());
    }

    public ConsoleCommandResult GetFlyingStatus() =>
        ConsoleCommandResult.Ok(BuildFlyingStatusMessage());

    public ConsoleCommandResult ToggleFullBright()
    {
        PrimitiveRenderer.SetFullBright(!PrimitiveRenderer.FullBright);
        return ConsoleCommandResult.Ok(BuildFullBrightStatusMessage());
    }

    public ConsoleCommandResult SetFullBright(bool enabled)
    {
        PrimitiveRenderer.SetFullBright(enabled);
        return ConsoleCommandResult.Ok(BuildFullBrightStatusMessage());
    }

    public ConsoleCommandResult GetFullBrightStatus() =>
        ConsoleCommandResult.Ok(BuildFullBrightStatusMessage());

    private static string BuildFullBrightStatusMessage() =>
        PrimitiveRenderer.FullBright
            ? "Fullbright: on (scene drawn at 100% brightness, torch and placed lights disabled)."
            : "Fullbright: off (normal distance and fixture lighting).";

    public ConsoleCommandResult DumpLightingCheckForConsole()
    {
        var renderPose = GetRenderPose();
        var renderPosition = renderPose.Position;

        _lightOcclusionMap.Update(_mapData, _doorSystem.Doors, _renderSystem.RoomMap);
        PrimitiveRenderer.SetLightOcclusionMap(_lightOcclusionMap, _mapData.Width, _mapData.Height);
        PrimitiveRenderer.SetSpriteRoomMap(_renderSystem.RoomMap);

        var mapLights = TileLightCollector.Collect(_mapData);
        var visibleRooms = _renderSystem.ComputeVisibleRooms(renderPosition, _doorSystem.Doors);
        var activeTileLights = TileLightCollector.SelectForVisibleRooms(
            mapLights,
            _renderSystem.RoomMap,
            visibleRooms,
            renderPosition,
            LightObjectEncoding.MaxShaderLights);
        PrimitiveRenderer.SetLightingParameters(renderPosition, tileLights: activeTileLights);

        var shaderState = PrimitiveRenderer.GetLightingDebugSnapshot();
        var rows = LightingDiagnostics.BuildReport(
            _mapData,
            _renderSystem.RoomMap,
            _doorSystem.Doors,
            renderPosition,
            _lightOcclusionMap,
            shaderState);

        string summary = $"Lighting check for '{_currentLevelPath}':";
        string logPath = LightingReportWriter.Publish(summary, rows);

        var displayRows = new List<string>(rows.Count + 1)
        {
            $"Saved to: {logPath}"
        };
        displayRows.AddRange(rows);

        return ConsoleCommandResult.Ok($"{summary} (see terminal stderr or {logPath})", displayRows);
    }

    private string BuildFlyingStatusMessage()
    {
        if (!_player.IsFlying)
            return "Flying: off. Use Shift/Ctrl for vertical movement when enabled.";

        return $"Flying: on. Position Y={_player.Position.Y:F1}. Shift=up, Ctrl=down.";
    }

    public ConsoleCommandResult StartRecordingForConsole(string filename, float mouseSensitivity)
    {
        var result = _recordingSystem.StartRecording(filename, mouseSensitivity);
        if (result.Success)
        {
            _consoleOverlay.Close();
            _inputSystem.DisableMouse();
        }

        return result;
    }

    public ConsoleCommandResult StartReplayForConsole(string filename)
    {
        var result = _recordingSystem.StartReplay(filename);
        if (result.Success)
        {
            _consoleOverlay.Close();
            _inputSystem.DisableMouse();
        }

        return result;
    }

    public ConsoleCommandResult StartReplayRemote(int rank) =>
        _recordingSystem.ReplayRemote(rank);

    public ConsoleCommandResult StartVerifyReplayForConsole(string filename)
    {
        var result = _recordingSystem.StartVerifyReplay(filename);
        if (result.Success)
        {
            _consoleOverlay.Close();
            _inputSystem.DisableMouse();
        }

        return result;
    }

    public ConsoleCommandResult ExecuteConsoleLine(string line)
    {
        return _runtimeConsole.Execute(line);
    }

    public void Render()
    {
        RenderSceneToTexture();
        RenderHudToTexture();
        RenderToScreen();
    }

    private void RenderSceneToTexture()
    {
        var renderPose = GetRenderPose();
        var renderCamera = renderPose.ToCamera(_player.Camera);
        var renderPosition = renderPose.Position;
        var renderTarget = renderCamera.Target;

        _lightOcclusionMap.Update(_mapData, _doorSystem.Doors, _renderSystem.RoomMap);
        PrimitiveRenderer.SetLightOcclusionMap(_lightOcclusionMap, _mapData.Width, _mapData.Height);
        PrimitiveRenderer.SetSpriteRoomMap(_renderSystem.RoomMap);

        var mapLights = TileLightCollector.Collect(_mapData);
        var visibleRooms = _renderSystem.ComputeVisibleRooms(renderPosition, _doorSystem.Doors);
        var activeTileLights = TileLightCollector.SelectForVisibleRooms(
            mapLights,
            _renderSystem.RoomMap,
            visibleRooms,
            renderPosition,
            LightObjectEncoding.MaxShaderLights);
        PrimitiveRenderer.SetLightingParameters(renderPosition, tileLights: activeTileLights);
        var lightingShader = PrimitiveRenderer.GetLightingShader();

        BeginTextureMode(_sceneRenderTexture);
        BeginMode3D(renderCamera);
        ClearBackground(Color.Black);

        if (lightingShader.HasValue)
        {
            PrimitiveRenderer.SetMeshRoomId(-1);
            BeginShaderMode(lightingShader.Value);
            PrimitiveRenderer.ApplyWallLightingUniforms();
        }

        _renderSystem.Render(
            renderCamera,
            _sceneRenderTexture.Texture.Width,
            _sceneRenderTexture.Texture.Height,
            _doorSystem.Doors);
        _secretSystem.Render(renderPosition);
        _doorSystem.Render();
        _animationSystem.Render();

        if (lightingShader.HasValue)
            EndShaderMode();

        _placedObjectSystem.Render(renderPosition, renderTarget);
        _pickupSystem.Render(renderPosition, renderTarget);
        Debug.Draw3DOverlays(_inputState.IsDebugEnabled);

        EndMode3D();
        EndTextureMode();
    }

    private void RenderHudToTexture()
    {
        int renderWidth = RenderData.InternalWidth;
        int renderHeight = RenderData.InternalHeight;
        bool consoleOpen = _consoleOverlay.IsOpen;
        bool optionsOpen = _optionsMenu.IsOpen;
        bool showWeaponView = _player.IsAlive && !consoleOpen && !optionsOpen && !_exitSystem.IsBlockingGameplay;

        BeginTextureMode(_hudRenderTexture);
        ClearBackground(new Color(0, 0, 0, 0));

        RenderNotificationOverlays(renderWidth, renderHeight, consoleOpen);

        if (!consoleOpen && !_inputState.IsMouseFree && showWeaponView)
            PlaySessionOverlayHud.DrawReticle(_effectSystem, renderWidth, renderHeight);

        if (optionsOpen)
            OptionsMenuHud.Draw(_optionsMenu.Settings, renderWidth, renderHeight);

        EndTextureMode();
    }

    private void RenderToScreen()
    {
        int screenWidth = GetScreenWidth();
        int screenHeight = GetScreenHeight();

        BeginDrawing();
        ClearBackground(Color.Black);

        GameRenderSpace.DrawTextureToWindow(_sceneRenderTexture.Texture, screenWidth, screenHeight);
        GameRenderSpace.DrawTextureToWindow(_hudRenderTexture.Texture, screenWidth, screenHeight);

        RenderDebugLabels(screenWidth, screenHeight);
        RenderPlayHud(screenWidth, screenHeight);
        RenderMinimapAndDebugOverlays();

        _consoleOverlay.Render();

        EndDrawing();
    }

    private void RenderDebugLabels(int screenWidth, int screenHeight)
    {
        DrawFPS(10, screenHeight - 120);

        var mouseLabel = _optionsMenu.IsOpen || _inputState.IsMouseFree ? "MOUSE: FREE" : "MOUSE: LOCKED";
        var mouseColor = _optionsMenu.IsOpen || _inputState.IsMouseFree ? Color.Green : Color.Red;
        int mouseLabelWidth = MeasureText(mouseLabel, 20);
        DrawText(mouseLabel, screenWidth - mouseLabelWidth - 10, 10, 20, mouseColor);

        var healthLabel = $"HEALTH: {(int)_player.Health} / {(int)_player.MaxHealth}";
        DrawText(healthLabel, 10, 40, 20, _player.IsAlive ? Color.RayWhite : Color.Red);

        if (_tickDiagnostics.OverlayEnabled)
            TickDiagnosticsHud.Draw(_tickDiagnostics);

        RecordingStatusHud.Draw(
            _recordingSystem.IsRecording,
            _recordingSystem.IsReplaying,
            screenWidth);
    }

    private void RenderPlayHud(int screenWidth, int screenHeight)
    {
        bool consoleOpen = _consoleOverlay.IsOpen;
        bool optionsOpen = _optionsMenu.IsOpen;
        bool inActiveLevel = _player.IsAlive && !_exitSystem.IsLevelComplete;
        bool showWeaponView = _player.IsAlive && !consoleOpen && !optionsOpen && !_exitSystem.IsBlockingGameplay;

        if (inActiveLevel)
        {
            LevelProgressOverlayHud.DrawScore(_scoreSystem, screenWidth);
            CombatOverlayHud.DrawInventory(_player);
        }

        if (showWeaponView)
            _animationSystem.RenderWeaponOverlay(screenWidth, screenHeight);

        _effectSystem.RenderScreenOverlay(screenWidth, screenHeight);
        RenderIntermissionOverlay(screenWidth, screenHeight, consoleOpen);

        if (!_player.IsAlive && !consoleOpen)
            PlaySessionOverlayHud.DrawGameOver(screenWidth, screenHeight);

        if (_highscoreBoardOverlay.IsOpen)
            _highscoreBoardOverlay.Draw(screenWidth, screenHeight);
    }

    /// <summary>Centered banner notifications drawn at internal resolution.</summary>
    private void RenderNotificationOverlays(int renderWidth, int renderHeight, bool consoleOpen)
    {
        if (consoleOpen || _exitSystem.IsLevelComplete || !_player.IsAlive)
            return;

        if (_exitSystem.IsExitPending)
        {
            LevelProgressOverlayHud.DrawExitCountdown(_exitSystem.ExitCountdownRemaining, renderWidth, renderHeight);
            return;
        }

        if (_doorSystem.HasLockedHint)
            DoorOverlayHud.DrawLockedHint(_doorSystem, renderWidth, renderHeight);
        else if (_weaponSystem.HasNoAmmoHint)
            CombatOverlayHud.DrawNoAmmoHint(_weaponSystem, renderWidth, renderHeight);
    }

    /// <summary>Full-screen intermission panels remain in window space.</summary>
    private void RenderIntermissionOverlay(int screenWidth, int screenHeight, bool consoleOpen)
    {
        if (consoleOpen || !_exitSystem.IsLevelComplete)
            return;

        if (_highscoreIntermission.IsActive)
            _highscoreIntermission.Draw(_scoreSystem, screenWidth, screenHeight);
        else
            LevelProgressOverlayHud.DrawLevelComplete(
                _scoreSystem,
                screenWidth,
                screenHeight,
                showRestartHint: !_recordingSystem.IsReplaying);
    }

    private void RenderMinimapAndDebugOverlays()
    {
        if (_inputState.IsMinimapEnabled)
            _minimapSystem.Render(_player);

        int renderW = RenderData.InternalWidth;
        int renderH = RenderData.InternalHeight;
        Debug.DrawWorldOverlays(_inputState.IsDebugEnabled, _player.Camera, renderW, renderH);
        Debug.Draw(_inputState.IsDebugEnabled);
    }
}
