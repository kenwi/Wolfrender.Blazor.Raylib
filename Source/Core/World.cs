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
    private readonly ControlsIntroSystem _controlsIntroSystem = new();
    private readonly PlayOptionsFacade _playOptions;
    private readonly PlayOverlayInputController _overlayInput;
    private readonly PlayConsoleCommands _consoleCommands;
    private readonly PlayHudComposer _hudComposer;
    private bool _highscoreIntermissionStarted;
    private bool _suppressLevelCompleteClickRestart;

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
        _collisionSystem = new CollisionSystem(
            _level,
            new CompositeMovementBlocker(_doorSystem, _secretSystem),
            ObjectCollisionRules.Instance);
        _soundPropagationSystem = new SoundPropagationSystem(mapData, _doorSystem);
        _cameraSystem = new CameraSystem(_collisionSystem);
        _playOptions = new PlayOptionsFacade(_optionsMenu, _soundSystem, _cameraSystem);
        _renderSystem = new RenderSystem(_level, _mapData, _tileTextures, DoorTileEncoding.ForEngine);
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
        _consoleCommands = new PlayConsoleCommands(
            _tickDiagnostics,
            _renderSystem,
            _player,
            _mapData,
            _doorSystem,
            _lightOcclusionMap,
            _recordingSystem,
            _consoleOverlay,
            _inputSystem,
            _controlsIntroSystem,
            () => GetRenderPose().Position,
            () => _currentLevelPath);
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
            submission => _recordingSystem.UploadRecordingForScoreAsync(submission),
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
            sensitivity => _playOptions.SetMouseSensitivity(sensitivity),
            () =>
            {
                _playOptions.ApplyControlSettings();
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

        _overlayInput = new PlayOverlayInputController(
            _controlsIntroSystem,
            _consoleOverlay,
            _optionsMenu,
            _highscoreBoardOverlay,
            _highscoreIntermission,
            _recordingSystem,
            _player,
            _exitSystem,
            _inputSystem,
            _playOptions,
            _runtimeConsole,
            ExecuteConsoleLine,
            RestartCurrentLevel);
        _hudComposer = new PlayHudComposer(
            _player,
            _consoleOverlay,
            _optionsMenu,
            _controlsIntroSystem,
            _exitSystem,
            _doorSystem,
            _weaponSystem,
            _effectSystem,
            _scoreSystem,
            _animationSystem,
            _highscoreBoardOverlay,
            _highscoreIntermission,
            _recordingSystem,
            _tickDiagnostics,
            _minimapSystem,
            () => _inputState,
            () => _player.Camera);

        Debug.Setup(_doorSystem.Doors, _player, _animationSystem, _enemySystem);
        _playOptions.InitializeRenderTargets();
#if DEBUG
        ConsoleSelfTests.RunOnce();
#endif
    }

    public void SetVolume(float volume) => _playOptions.SetVolume(volume);

    public float GetVolume() => _playOptions.GetVolume();

    public void SetMouseSensitivity(float sensitivity) =>
        _playOptions.SetMouseSensitivity(sensitivity);

    public void ApplyControlSettings() => _playOptions.ApplyControlSettings();

    public void ApplyAudioSettings() => _playOptions.ApplyAudioSettings();

    public void ApplyWindowDisplay() => _playOptions.ApplyWindowDisplay();

    public void ApplyGameResolution() => _playOptions.ApplyGameResolution();

    public bool GetFullscreenEnabled() => _playOptions.GetFullscreenEnabled();

    public void SetFullscreenEnabled(bool enabled) =>
        _playOptions.SetFullscreenEnabled(enabled);

    public string GetWindowResolutionPresetId() =>
        _playOptions.GetWindowResolutionPresetId();

    public void SetWindowResolutionPresetId(string presetId) =>
        _playOptions.SetWindowResolutionPresetId(presetId);

    public string GetGameResolutionPresetId() =>
        _playOptions.GetGameResolutionPresetId();

    public void SetGameResolutionPresetId(string presetId) =>
        _playOptions.SetGameResolutionPresetId(presetId);

    public bool GetVSyncEnabled() => _playOptions.GetVSyncEnabled();

    public void SetVSyncEnabled(bool enabled) =>
        _playOptions.SetVSyncEnabled(enabled);

    public int GetTargetFps() => _playOptions.GetTargetFps();

    public void SetTargetFps(int fps) => _playOptions.SetTargetFps(fps);

    public void ApplyGraphicsSettings() => _playOptions.ApplyGraphicsSettings();

    public void EnsureStartupDisplay() => _playOptions.EnsureStartupDisplay();

    public void OnWindowResize() => _playOptions.OnWindowResize();

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
        else if (!_controlsIntroSystem.IsVisible)
            _inputSystem.DisableMouse();
        else
            _inputSystem.EnableMouse();

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

    public void OnExit()
    {
        _optionsMenu.Dismiss();
        _inputSystem.EnableMouse();
    }

    public void ToggleMouse()
    {
        _inputSystem.ToggleMouse();
    }

    public void Update(float deltaTime)
    {
        _soundSystem.Update();
        _recordingSystem.Update(deltaTime);

        if (_overlayInput.TryHandleOverlays(deltaTime))
            return;

        if (!_recordingSystem.IsReplaying)
        {
            _overlayInput.PollBrowserPointerLockEvents();
            _overlayInput.ArmBrowserMovementCapture();
            _inputSystem.Update(suppressClickToCapture: _overlayInput.ShouldDeferGameplayMouseCapture());
        }

        // Sample Raylib once per render frame; ticks consume latched edges/deltas
        // so per-frame input maps 1:1 onto simulation ticks (see LiveInputProvider).
        _recordingSystem.BeginInputFrame();

        _tickDiagnostics.BeginFrame(deltaTime);
        int ticksThisFrame = _simulationClock.Advance(deltaTime);

        _suppressLevelCompleteClickRestart = false;
        if (_exitSystem.IsLevelComplete && _highscoreIntermission.IsLeaderboardInteractive)
            _suppressLevelCompleteClickRestart = _highscoreIntermission.TryHandleLeaderboardInput();

        for (int i = 0; i < ticksThisFrame; i++)
        {
            _simulationClock.AdvanceTick();
            SimulateGameplayTick(_simulationClock.FixedDeltaTime);
        }

        _tickDiagnostics.RecordSimulationStep(_simulationClock);

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
        _previousSimulationPose = CapturePlayerPose();
        _currentSimulationPose = _previousSimulationPose;
    }

    private void AdvanceSimulationPose()
    {
        _previousSimulationPose = _currentSimulationPose;
        _currentSimulationPose = CapturePlayerPose();
    }

    private SimulationPose GetRenderPose()
    {
        if (!_player.IsAlive || _exitSystem.IsBlockingGameplay)
            return CapturePlayerPose();

        return SimulationPose.Lerp(
            _previousSimulationPose,
            _currentSimulationPose,
            _simulationClock.InterpolationAlpha);
    }

    private SimulationPose CapturePlayerPose() =>
        SimulationPose.FromPositionAndLook(_player.Position, _player.Camera.Target);

    public ConsoleCommandResult ToggleTickDiagnostics() => _consoleCommands.ToggleTickDiagnostics();

    public ConsoleCommandResult SetTickDiagnostics(bool enabled) =>
        _consoleCommands.SetTickDiagnostics(enabled);

    public ConsoleCommandResult GetTickDiagnosticsStatus() =>
        _consoleCommands.GetTickDiagnosticsStatus();

    public ConsoleCommandResult ToggleStaticMeshes() => _consoleCommands.ToggleStaticMeshes();

    public ConsoleCommandResult SetStaticMeshes(bool enabled) =>
        _consoleCommands.SetStaticMeshes(enabled);

    public ConsoleCommandResult GetStaticMeshesStatus() =>
        _consoleCommands.GetStaticMeshesStatus();

    public ConsoleCommandResult ToggleFlying() => _consoleCommands.ToggleFlying();

    public ConsoleCommandResult SetFlying(bool enabled) =>
        _consoleCommands.SetFlying(enabled);

    public ConsoleCommandResult GetFlyingStatus() =>
        _consoleCommands.GetFlyingStatus();

    public ConsoleCommandResult ToggleFullBright() => _consoleCommands.ToggleFullBright();

    public ConsoleCommandResult SetFullBright(bool enabled) =>
        _consoleCommands.SetFullBright(enabled);

    public ConsoleCommandResult GetFullBrightStatus() =>
        _consoleCommands.GetFullBrightStatus();

    public ConsoleCommandResult DumpLightingCheckForConsole() =>
        _consoleCommands.DumpLightingCheck();

    public ConsoleCommandResult StartRecordingForConsole(string filename, float mouseSensitivity) =>
        _consoleCommands.StartRecording(filename, mouseSensitivity);

    public ConsoleCommandResult StartReplayForConsole(string filename) =>
        _consoleCommands.StartReplay(filename);

    public ConsoleCommandResult StartReplayRemote(int rank) =>
        _consoleCommands.StartReplayRemote(rank);

    public ConsoleCommandResult StartVerifyReplayForConsole(string filename) =>
        _consoleCommands.StartVerifyReplay(filename);

    public ConsoleCommandResult ExecuteConsoleLine(string line) =>
        _runtimeConsole.Execute(line);

    public void Render()
    {
        RenderSceneToTexture();
        _hudComposer.RenderHudToTexture(_playOptions.HudRenderTexture);
        _hudComposer.ComposeToScreen(
            _playOptions.SceneRenderTexture.Texture,
            _playOptions.HudRenderTexture.Texture);
    }

    private void RenderSceneToTexture()
    {
        var renderPose = GetRenderPose();
        var renderCamera = renderPose.ToCamera(_player.Camera);
        var renderPosition = renderPose.Position;
        var renderTarget = renderCamera.Target;

        _lightOcclusionMap.Update(
            _mapData,
            DoorTileEncoding.ForEngine,
            _doorSystem,
            _renderSystem.RoomMap);
        PrimitiveRenderer.SetLightOcclusionMap(_lightOcclusionMap, _mapData.Width, _mapData.Height);
        PrimitiveRenderer.SetSpriteRoomMap(_renderSystem.RoomMap);

        var mapLights = TileLightCollector.Collect(_mapData);
        var visibleRooms = _renderSystem.ComputeVisibleRooms(renderPosition, _doorSystem);
        var activeTileLights = TileLightCollector.SelectForVisibleRooms(
            mapLights,
            _renderSystem.RoomMap,
            visibleRooms,
            renderPosition,
            LightObjectEncoding.MaxShaderLights);
        PrimitiveRenderer.SetLightingParameters(renderPosition, tileLights: activeTileLights);
        var lightingShader = PrimitiveRenderer.GetLightingShader();

        var sceneRt = _playOptions.SceneRenderTexture;
        BeginTextureMode(sceneRt);
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
            sceneRt.Texture.Width,
            sceneRt.Texture.Height,
            _doorSystem);
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
}
