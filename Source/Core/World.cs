using System.Numerics;
using Game.DebugConsole;
using Game.Engine.Simulation;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.Highscores;
using Game.Features.LevelProgress;
using Game.Features.Players;
using Game.Features.Recording;
using Game.Features.SoundPropagation;
using Game.Features.WorldObjects;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Core;

public class World : IScene
{
    private readonly WorldComposition _c;
    private RuntimeConsoleService _runtimeConsole = null!;
    private HighscoreIntermission _highscoreIntermission = null!;
    private HighscoreBoardOverlay _highscoreBoardOverlay = null!;
    private PlayOverlayInputController _overlayInput = null!;
    private PlayConsoleCommands _consoleCommands = null!;
    private PlayHudComposer _hudComposer = null!;
    private PlayLevelSession _levelSession = null!;
    private bool _suppressLevelCompleteClickRestart;

    private InputState _inputState = new();
    private SimulationPose _previousSimulationPose;
    private SimulationPose _currentSimulationPose;

    public Player Player => _c.Player;
    public PlayerSystem PlayerSystem => _c.PlayerSystem;
    public EnemySystem EnemySystem => _c.EnemySystem;
    public DoorSystem DoorSystem => _c.DoorSystem;
    public SoundPropagationSystem SoundPropagationSystem => _c.SoundPropagationSystem;
    public ScoreSystem ScoreSystem => _c.ScoreSystem;
    public ExitSystem ExitSystem => _c.ExitSystem;
    public SecretSystem SecretSystem => _c.SecretSystem;
    public string CurrentLevelPath => _levelSession.CurrentLevelPath;

    public World(MapData mapData)
    {
        _c = WorldCompositionRoot.Create(mapData);
        WireSessionComposers();
        ConfigureRecordingAndLifecycle();
        InitializeLevelSystems();
        Debug.Setup(_c.DoorSystem.Doors, _c.Player, _c.AnimationSystem, _c.EnemySystem);
        _c.PlayOptions.InitializeRenderTargets();
#if DEBUG
        ConsoleSelfTests.RunOnce();
#endif
    }

    private void WireSessionComposers()
    {
        _consoleCommands = new PlayConsoleCommands(
            _c.TickDiagnostics,
            _c.RenderSystem,
            _c.Player,
            _c.MapData,
            _c.DoorSystem,
            _c.LightOcclusionMap,
            _c.RecordingSystem,
            _c.ConsoleOverlay,
            _c.InputSystem,
            _c.ControlsIntro,
            () => GetRenderPose().Position,
            () => CurrentLevelPath);
        _runtimeConsole = WorldConsoleBindings.CreateConsole(
            this,
            _c.Player,
            _c.EnemySystem,
            _c.ScoreSystem,
            _c.ConsoleOverlay,
            _c.RecordingSystem,
            () => _c.OptionsMenu.Settings.MouseSensitivity);
        _highscoreIntermission = new HighscoreIntermission(
            _c.HighscoreClient,
            submission => _c.RecordingSystem.PrepareRecordingForScoreSubmission(submission),
            submission => _c.RecordingSystem.UploadRecordingForScoreAsync(submission),
            () => _c.RecordingSystem.DiscardCurrentRecording(),
            StartReplayRemote,
            result => _runtimeConsole.WriteFeedback(result));
        _highscoreBoardOverlay = new HighscoreBoardOverlay(
            _c.HighscoreClient,
            () => CurrentLevelPath,
            StartReplayRemote,
            result => _runtimeConsole.WriteFeedback(result));
        _levelSession = new PlayLevelSession(
            _c.MapData,
            _c.PlayerSystem,
            _c.DoorSystem,
            _c.EnemySystem,
            _c.PickupSystem,
            _c.PlacedObjectSystem,
            _c.ScoreSystem,
            _c.ExitSystem,
            _c.SecretSystem,
            _c.RenderSystem,
            _c.SoundPropagationSystem,
            _highscoreIntermission,
            _c.HighscoreClient,
            _c.EffectSystem,
            _c.RecordingSystem,
            _c.SimulationClock,
            _c.TickDiagnostics,
            _c.InputSystem,
            _c.ControlsIntro,
            _c.OptionsMenu,
            ResetSimulationPoses);
        _overlayInput = new PlayOverlayInputController(
            _c.ControlsIntro,
            _c.ConsoleOverlay,
            _c.OptionsMenu,
            _highscoreBoardOverlay,
            _highscoreIntermission,
            _c.RecordingSystem,
            _c.Player,
            _c.ExitSystem,
            _c.InputSystem,
            _c.PlayOptions,
            _runtimeConsole,
            ExecuteConsoleLine,
            _levelSession.RestartCurrentLevel);
        _hudComposer = new PlayHudComposer(
            _c.Player,
            _c.ConsoleOverlay,
            _c.OptionsMenu,
            _c.ControlsIntro,
            _c.ExitSystem,
            _c.DoorSystem,
            _c.WeaponSystem,
            _c.EffectSystem,
            _c.ScoreSystem,
            _c.AnimationSystem,
            _highscoreBoardOverlay,
            _highscoreIntermission,
            _c.RecordingSystem,
            _c.TickDiagnostics,
            _c.MinimapSystem,
            () => _inputState,
            () => _c.Player.Camera);
    }

    private void ConfigureRecordingAndLifecycle()
    {
        _c.RecordingSystem.Configure(
            _levelSession.LoadLevel,
            _levelSession.RestartCurrentLevel,
            () => _levelSession.CurrentLevelPath,
            sensitivity => _c.PlayOptions.SetMouseSensitivity(sensitivity),
            () =>
            {
                _c.PlayOptions.ApplyControlSettings();
                _c.InputSystem.RestoreGameplayMouse();
            },
            () => PlayerSnapshotApplication.From(_c.Player),
            snapshot =>
            {
                snapshot.ApplyTo(_c.Player);
                ResetSimulationPoses();
            },
            () => _c.SimulationClock.TickHz,
            tickHz => _c.SimulationClock.SetTickHz(tickHz),
            tick => SimulationChecksum.Capture(
                tick, _c.Player, _c.EnemySystem.Enemies, _c.DoorSystem.Doors, _c.ScoreSystem),
            () => _levelSession.CurrentRngSeed,
            seed => _levelSession.SetRngSeedOverride(seed),
            result => _runtimeConsole.WriteFeedback(result),
            () =>
            {
                _c.ConsoleOverlay.Close();
                _c.InputSystem.DisableMouse();
            });
        _c.PlayerSystem.ConfigureLifecycle(
            () => _c.ConsoleOverlay.IsOpen,
            () => _ = _levelSession.RestartCurrentLevel(),
            () => _highscoreIntermission.IsBlockingRestart,
            () => _c.RecordingSystem.IsReplaying,
            () => _suppressLevelCompleteClickRestart);
    }

    private void InitializeLevelSystems()
    {
        _c.PlayerSystem.ResetForLevelLoad(_c.MapData);
        ResetSimulationPoses();
        _c.ExitSystem.Rebuild(_c.MapData);
        _c.SecretSystem.Rebuild(_c.MapData);
        _c.RenderSystem.RebuildMeshes();
    }

    public void SetVolume(float volume) => _c.PlayOptions.SetVolume(volume);

    public float GetVolume() => _c.PlayOptions.GetVolume();

    public void SetMouseSensitivity(float sensitivity) =>
        _c.PlayOptions.SetMouseSensitivity(sensitivity);

    public void ApplyControlSettings() => _c.PlayOptions.ApplyControlSettings();

    public void ApplyAudioSettings() => _c.PlayOptions.ApplyAudioSettings();

    public void ApplyWindowDisplay() => _c.PlayOptions.ApplyWindowDisplay();

    public void ApplyGameResolution() => _c.PlayOptions.ApplyGameResolution();

    public bool GetFullscreenEnabled() => _c.PlayOptions.GetFullscreenEnabled();

    public void SetFullscreenEnabled(bool enabled) =>
        _c.PlayOptions.SetFullscreenEnabled(enabled);

    public string GetWindowResolutionPresetId() =>
        _c.PlayOptions.GetWindowResolutionPresetId();

    public void SetWindowResolutionPresetId(string presetId) =>
        _c.PlayOptions.SetWindowResolutionPresetId(presetId);

    public string GetGameResolutionPresetId() =>
        _c.PlayOptions.GetGameResolutionPresetId();

    public void SetGameResolutionPresetId(string presetId) =>
        _c.PlayOptions.SetGameResolutionPresetId(presetId);

    public bool GetVSyncEnabled() => _c.PlayOptions.GetVSyncEnabled();

    public void SetVSyncEnabled(bool enabled) =>
        _c.PlayOptions.SetVSyncEnabled(enabled);

    public int GetTargetFps() => _c.PlayOptions.GetTargetFps();

    public void SetTargetFps(int fps) => _c.PlayOptions.SetTargetFps(fps);

    public void ApplyGraphicsSettings() => _c.PlayOptions.ApplyGraphicsSettings();

    public void EnsureStartupDisplay() => _c.PlayOptions.EnsureStartupDisplay();

    public void OnWindowResize() => _c.PlayOptions.OnWindowResize();

    public void OnEnter() => _levelSession.OnEnter();

    public ConsoleCommandResult LoadLevel(string pathOrName) =>
        _levelSession.LoadLevel(pathOrName);

    public ConsoleCommandResult RestartCurrentLevel() =>
        _levelSession.RestartCurrentLevel();

    public ConsoleCommandResult ListPickupsForConsole() =>
        _levelSession.ListPickupsForConsole();

    public void OnExit()
    {
        _c.OptionsMenu.Dismiss();
        _c.InputSystem.EnableMouse();
    }

    public void ToggleMouse()
    {
        _c.InputSystem.ToggleMouse();
    }

    public void Update(float deltaTime)
    {
        _c.SoundSystem.Update();
        _c.RecordingSystem.Update(deltaTime);

        if (_overlayInput.TryHandleOverlays(deltaTime))
            return;

        if (!_c.RecordingSystem.IsReplaying)
        {
            _overlayInput.PollBrowserPointerLockEvents();
            _overlayInput.ArmBrowserMovementCapture();
            _c.InputSystem.Update(suppressClickToCapture: _overlayInput.ShouldDeferGameplayMouseCapture());
        }

        // Sample Raylib once per render frame; ticks consume latched edges/deltas
        // so per-frame input maps 1:1 onto simulation ticks (see LiveInputProvider).
        _c.RecordingSystem.BeginInputFrame();

        _c.TickDiagnostics.BeginFrame(deltaTime);
        int ticksThisFrame = _c.SimulationClock.Advance(deltaTime);

        _suppressLevelCompleteClickRestart = false;
        if (_c.ExitSystem.IsLevelComplete && _highscoreIntermission.IsLeaderboardInteractive)
            _suppressLevelCompleteClickRestart = _highscoreIntermission.TryHandleLeaderboardInput();

        for (int i = 0; i < ticksThisFrame; i++)
        {
            _c.SimulationClock.AdvanceTick();
            SimulateGameplayTick(_c.SimulationClock.FixedDeltaTime);
        }

        _c.TickDiagnostics.RecordSimulationStep(_c.SimulationClock);

        if (_c.ExitSystem.IsLevelComplete && !_levelSession.HighscoreIntermissionStarted)
        {
            _levelSession.HighscoreIntermissionStarted = true;
            if (!_c.RecordingSystem.IsReplaying)
                _highscoreIntermission.Begin(_levelSession.CurrentLevelPath, _c.ScoreSystem);
        }

        if (_c.ExitSystem.IsLevelComplete)
            _highscoreIntermission.Update();
    }

    private void SimulateGameplayTick(float fixedDeltaTime)
    {
        var poll = _c.RecordingSystem.ActiveProvider.Poll(fixedDeltaTime);
        _c.RecordingSystem.CaptureTick(poll, _c.SimulationClock.TickIndex);
        _inputState = poll.InputState;
        var mouseDelta = poll.MouseDelta;

        if (!_c.ExitSystem.IsBlockingGameplay)
            _c.ScoreSystem.Tick(fixedDeltaTime);

        _c.EffectSystem.Update(fixedDeltaTime);

        int screenWidth = RenderData.InternalWidth;
        int screenHeight = RenderData.InternalHeight;

        if (_c.Player.IsAlive)
        {
            _c.PlayerSystem.UpdateAlive(fixedDeltaTime, _inputState, mouseDelta, screenWidth, screenHeight);
            if (!_c.ExitSystem.IsBlockingGameplay)
                _c.EnemySystem.Update(fixedDeltaTime, _inputState);
        }
        else
        {
            _c.PlayerSystem.UpdateDead(fixedDeltaTime, _inputState, mouseDelta);
            _c.EnemySystem.Update(fixedDeltaTime, _inputState);
        }

        _c.RecordingSystem.OnTickSimulated(_c.SimulationClock.TickIndex);
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
        if (!_c.Player.IsAlive || _c.ExitSystem.IsBlockingGameplay)
            return CapturePlayerPose();

        return SimulationPose.Lerp(
            _previousSimulationPose,
            _currentSimulationPose,
            _c.SimulationClock.InterpolationAlpha);
    }

    private SimulationPose CapturePlayerPose() =>
        SimulationPose.FromPositionAndLook(_c.Player.Position, _c.Player.Camera.Target);

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
        _hudComposer.RenderHudToTexture(_c.PlayOptions.HudRenderTexture);
        _hudComposer.ComposeToScreen(
            _c.PlayOptions.SceneRenderTexture.Texture,
            _c.PlayOptions.HudRenderTexture.Texture);
    }

    private void RenderSceneToTexture()
    {
        var renderPose = GetRenderPose();
        var renderCamera = renderPose.ToCamera(_c.Player.Camera);
        var renderPosition = renderPose.Position;
        var renderTarget = renderCamera.Target;

        SceneLightingSetup.ApplyForView(
            _c.MapData,
            _c.LightOcclusionMap,
            _c.RenderSystem.RoomMap,
            DoorTileEncoding.ForEngine,
            _c.DoorSystem,
            renderPosition,
            _c.RenderSystem.ComputeVisibleRooms);
        var lightingShader = PrimitiveRenderer.GetLightingShader();

        var sceneRt = _c.PlayOptions.SceneRenderTexture;
        BeginTextureMode(sceneRt);
        BeginMode3D(renderCamera);
        ClearBackground(Color.Black);

        if (lightingShader.HasValue)
        {
            PrimitiveRenderer.SetMeshRoomId(-1);
            BeginShaderMode(lightingShader.Value);
            PrimitiveRenderer.ApplyWallLightingUniforms();
        }

        _c.RenderSystem.Render(
            renderCamera,
            sceneRt.Texture.Width,
            sceneRt.Texture.Height,
            _c.DoorSystem);
        _c.SecretSystem.Render(renderPosition);
        _c.DoorSystem.Render();
        _c.AnimationSystem.Render();

        if (lightingShader.HasValue)
            EndShaderMode();

        _c.PlacedObjectSystem.Render(renderPosition, renderTarget);
        _c.PickupSystem.Render(renderPosition, renderTarget);
        Debug.Draw3DOverlays(_inputState.IsDebugEnabled);

        EndMode3D();
        EndTextureMode();
    }
}
