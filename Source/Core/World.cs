using System.Numerics;
using Game.DebugConsole;
using Game.Editor;
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
using Game.Features.WorldObjects;
using Game.Features.SoundPropagation;
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
    private readonly CollisionSystem _collisionSystem;
    private readonly CameraSystem _cameraSystem;
    private readonly DoorSystem _doorSystem;
    private readonly RenderSystem _renderSystem;
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
    private readonly HighscoreClient _highscoreClient;
    private readonly HighscoreIntermission _highscoreIntermission;
    private readonly OptionsMenuSystem _optionsMenu = new();
    private bool _highscoreIntermissionStarted;

    private RenderTexture2D _sceneRenderTexture;
    private bool _hasSceneRenderTexture;
    private InputState _inputState = new();

    public Player Player => _player;
    public PlayerSystem PlayerSystem => _playerSystem;
    public EnemySystem EnemySystem => _enemySystem;
    public DoorSystem DoorSystem => _doorSystem;
    public SoundPropagationSystem SoundPropagationSystem => _soundPropagationSystem;
    public ScoreSystem ScoreSystem => _scoreSystem;
    public ExitSystem ExitSystem => _exitSystem;
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
        _doorSystem = new DoorSystem(mapData.Doors, mapData.Width, _tileTextures);
        _collisionSystem = new CollisionSystem(_level, _doorSystem);
        _soundPropagationSystem = new SoundPropagationSystem(mapData, _doorSystem);
        _cameraSystem = new CameraSystem(_collisionSystem);
        _renderSystem = new RenderSystem(_level, _tileTextures);
        _minimapSystem = new MinimapSystem(_level, _renderSystem);
        _scoreSystem = new ScoreSystem();
        _exitSystem = new ExitSystem(_scoreSystem);
        _highscoreClient = new HighscoreClient();
        _highscoreIntermission = new HighscoreIntermission(_highscoreClient);
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
            _exitSystem);

        _consoleOverlay = new ConsoleOverlay();
        _runtimeConsole = WorldConsoleBindings.CreateConsole(this, _player, _enemySystem, _scoreSystem, _consoleOverlay);
        _playerSystem.ConfigureLifecycle(
            () => _consoleOverlay.IsOpen,
            () => _ = RestartCurrentLevel(),
            () => _highscoreIntermission.IsBlockingRestart);
        _playerSystem.ResetForLevelLoad(_mapData);
        _exitSystem.Rebuild(_mapData);

        WindowDisplayMode.SyncRenderDataFromWindow();
        GameRenderResolution.Apply(
            _optionsMenu.Settings,
            ResolveWindowWidth(),
            ResolveWindowHeight());
        _sceneRenderTexture = LoadRenderTexture(RenderData.InternalWidth, RenderData.InternalHeight);
        _hasSceneRenderTexture = true;
        Debug.Setup(_doorSystem.Doors, _player, _animationSystem, _enemySystem);
        ApplyGraphicsSettings();
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
        RecreateSceneRenderTexture();
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

    private void RecreateSceneRenderTexture()
    {
        int newScreenWidth = RenderData.InternalWidth;
        int newScreenHeight = RenderData.InternalHeight;

        if (_hasSceneRenderTexture &&
            _sceneRenderTexture.Texture.Width == newScreenWidth &&
            _sceneRenderTexture.Texture.Height == newScreenHeight)
            return;

        if (_hasSceneRenderTexture)
            UnloadRenderTexture(_sceneRenderTexture);

        _sceneRenderTexture = LoadRenderTexture(newScreenWidth, newScreenHeight);
        _hasSceneRenderTexture = true;
    }

    public void OnEnter()
    {
        _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        _enemySystem.Rebuild(_mapData.Enemies, _mapData);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);
        _placedObjectSystem.Rebuild(_mapData);
        _scoreSystem.ResetForLevel(_mapData);
        _exitSystem.Rebuild(_mapData);

        if (OperatingSystem.IsBrowser())
        {
            BrowserPointerLockBridge.PointerLockLost = HandleBrowserPointerLockLost;
            _inputSystem.EnableMouse();
        }
        else
            _inputSystem.DisableMouse();
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
        _playerSystem.ResetForLevelLoad(_mapData);
        _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        _enemySystem.Rebuild(_mapData.Enemies, _mapData);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);
        _placedObjectSystem.Rebuild(_mapData);
        _scoreSystem.ResetForLevel(_mapData);
        _exitSystem.Rebuild(_mapData);
        _soundPropagationSystem.ClearPendingEvents();
        _highscoreIntermission.ResetForLevel();
        _highscoreIntermissionStarted = false;
        _effectSystem.Clear();
    }

    public void OnExit()
    {
        BrowserPointerLockBridge.PointerLockLost = null;
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

    public void Update(float deltaTime)
    {
        _soundSystem.Update();
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
            var inputResult = _optionsMenu.HandleInput(GetScreenWidth(), GetScreenHeight());

            if (inputResult.WindowDisplayChanged)
            {
                ApplyWindowDisplay();
                ApplyGameResolution();
            }

            if (inputResult.GameResolutionChanged)
                ApplyGameResolution();
            if (inputResult.GraphicsChanged)
                ApplyGraphicsSettings();

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

        if (IsKeyPressed(KeyboardKey.Grave) ||
            (OperatingSystem.IsBrowser() && IsKeyPressed(KeyboardKey.Period)))
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

        _inputState = _inputSystem.GetInputState();
        var mouseDelta = _inputState.MouseDelta;

        if (_inputState.IsMouseFree)
            mouseDelta = new Vector2(0, 0);

        _inputSystem.Update();

        if (!_exitSystem.IsBlockingGameplay)
            _scoreSystem.Tick(deltaTime);

        _effectSystem.Update(deltaTime);

        int screenWidth = GetScreenWidth();
        int screenHeight = GetScreenHeight();

        if (_player.IsAlive)
        {
            _playerSystem.UpdateAlive(deltaTime, _inputState, mouseDelta, screenWidth, screenHeight);
            if (!_exitSystem.IsBlockingGameplay)
                _enemySystem.Update(deltaTime);
        }
        else
        {
            _playerSystem.UpdateDead(deltaTime, _inputState, mouseDelta);
            _enemySystem.Update(deltaTime);
        }

        if (_exitSystem.IsLevelComplete && !_highscoreIntermissionStarted)
        {
            _highscoreIntermissionStarted = true;
            _highscoreIntermission.Begin(_currentLevelPath, _scoreSystem);
        }

        if (_exitSystem.IsLevelComplete)
            _highscoreIntermission.Update();
    }

    public ConsoleCommandResult ExecuteConsoleLine(string line)
    {
        return _runtimeConsole.Execute(line);
    }

    public void Render()
    {
        RenderSceneToTexture();
        RenderToScreen();
    }

    private void RenderSceneToTexture()
    {
        var mapLights = TileLightCollector.Collect(_mapData);
        var activeTileLights = TileLightCollector.SelectNearest(
            mapLights,
            _player.Position,
            LightObjectEncoding.MaxShaderLights);
        PrimitiveRenderer.SetLightingParameters(_player.Position, tileLights: activeTileLights);
        var lightingShader = PrimitiveRenderer.GetLightingShader();

        BeginTextureMode(_sceneRenderTexture);
        BeginMode3D(_player.Camera);
        ClearBackground(Color.Black);

        if (lightingShader.HasValue)
        {
            BeginShaderMode(lightingShader.Value);
            PrimitiveRenderer.ApplyWallLightingUniforms();
        }

        _renderSystem.Render(
            _player.Camera,
            _sceneRenderTexture.Texture.Width,
            _sceneRenderTexture.Texture.Height);
        _doorSystem.Render();
        _animationSystem.Render();

        if (lightingShader.HasValue)
            EndShaderMode();

        _placedObjectSystem.Render(_player.Camera.Position, _player.Camera.Target);
        _pickupSystem.Render(_player.Camera.Position, _player.Camera.Target);
        Debug.Draw3DOverlays(_inputState.IsDebugEnabled);

        EndMode3D();
        EndTextureMode();
    }

    private void RenderToScreen()
    {
        int screenWidth = GetScreenWidth();
        int screenHeight = GetScreenHeight();

        BeginDrawing();
        ClearBackground(Color.Black);

        DrawTexturePro(
            _sceneRenderTexture.Texture,
            new Rectangle(0, 0, (float)_sceneRenderTexture.Texture.Width, (float)-_sceneRenderTexture.Texture.Height),
            new Rectangle(0, 0, screenWidth, screenHeight),
            new Vector2(0, 0),
            0,
            Color.White);

        RenderDebugLabels(screenWidth, screenHeight);
        RenderPlayHud(screenWidth, screenHeight);
        RenderMinimapAndDebugOverlays();

        _consoleOverlay.Render();

        if (_optionsMenu.IsOpen)
            OptionsMenuHud.Draw(_optionsMenu.Settings, screenWidth, screenHeight);

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
        RenderModalOverlay(screenWidth, screenHeight, consoleOpen);

        if (!_player.IsAlive && !consoleOpen)
            PlaySessionOverlayHud.DrawGameOver(screenWidth, screenHeight);

        if (!consoleOpen && !_inputState.IsMouseFree && showWeaponView)
            PlaySessionOverlayHud.DrawReticle(_effectSystem, screenWidth, screenHeight);
    }

    /// <summary>Mutually exclusive banners: level complete, exit countdown, door hint, no-ammo hint.</summary>
    private void RenderModalOverlay(int screenWidth, int screenHeight, bool consoleOpen)
    {
        if (consoleOpen)
            return;

        if (_exitSystem.IsLevelComplete)
        {
            if (_highscoreIntermission.IsActive)
                _highscoreIntermission.Draw(_scoreSystem, screenWidth, screenHeight);
            else
                LevelProgressOverlayHud.DrawLevelComplete(_scoreSystem, screenWidth, screenHeight);
            return;
        }

        if (_exitSystem.IsExitPending)
        {
            LevelProgressOverlayHud.DrawExitCountdown(_exitSystem.ExitCountdownRemaining, screenWidth, screenHeight);
            return;
        }

        if (!_player.IsAlive)
            return;

        if (_doorSystem.HasLockedHint)
            DoorOverlayHud.DrawLockedHint(_doorSystem, screenWidth, screenHeight);
        else if (_weaponSystem.HasNoAmmoHint)
            CombatOverlayHud.DrawNoAmmoHint(_weaponSystem, screenWidth, screenHeight);
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
