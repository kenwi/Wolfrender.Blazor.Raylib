using System.Numerics;
using Game.Console;
using Game.Editor;
using Game.Entities;
using Game.Systems;
using Game.Utilities;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game;

public class World : IScene
{
    private string _currentLevelPath = LevelCatalog.DefaultLevelPath;

    private readonly Player _player;
    private readonly SoundSystem _soundSystem;
    private readonly EffectSystem _effectSystem;
    private readonly CombatFeedback _combatFeedback;
    private readonly MapData _mapData;
    private readonly LevelData _level;
    private readonly List<Texture2D> _textures;
    private readonly InputSystem _inputSystem;
    private readonly MovementSystem _movementSystem;
    private readonly CollisionSystem _collisionSystem;
    private readonly CameraSystem _cameraSystem;
    private readonly DoorSystem _doorSystem;
    private readonly RenderSystem _renderSystem;
    private readonly HudSystem _hudSystem;
    private readonly AnimationSystem _animationSystem;
    private readonly MinimapSystem _minimapSystem;
    private readonly ConsoleOverlay _consoleOverlay;
    private readonly RuntimeConsoleService _runtimeConsole;
    private readonly EnemySystem _enemySystem;
    private readonly PickupSystem _pickupSystem;
    private readonly WeaponSystem _weaponSystem;
    private readonly PlayerSystem _playerSystem;

    private RenderTexture2D _sceneRenderTexture;
    private InputState _inputState = new();

    public Player Player => _player;
    public PlayerSystem PlayerSystem => _playerSystem;
    public EnemySystem EnemySystem => _enemySystem;
    public DoorSystem DoorSystem => _doorSystem;
    public string CurrentLevelPath => _currentLevelPath;

    public World(MapData mapData)
    {
        int screenWidth = (int)RenderData.Resolution.X / RenderData.ResolutionDownScaleMultiplier;
        int screenHeight = (int)RenderData.Resolution.Y / RenderData.ResolutionDownScaleMultiplier;

        _mapData = mapData;
        _level = new LevelData(mapData);
        _textures = mapData.Textures;
        _player = new Player();

        _soundSystem = new SoundSystem(Utilities.Res.Path("resources/03.mp3"));
        _effectSystem = new EffectSystem();
        _combatFeedback = new CombatFeedback(_soundSystem, _effectSystem);
        _inputSystem = new InputSystem();
        _movementSystem = new MovementSystem();
        _doorSystem = new DoorSystem(mapData.Doors, mapData.Width, _textures);
        _collisionSystem = new CollisionSystem(_level, _doorSystem);
        _cameraSystem = new CameraSystem(_collisionSystem);
        _renderSystem = new RenderSystem(_level, _textures);
        _hudSystem = new HudSystem(screenWidth, screenHeight);
        _minimapSystem = new MinimapSystem(_level, _renderSystem);
        _pickupSystem = new PickupSystem();
        _enemySystem = new EnemySystem(_player, _inputSystem, _collisionSystem, _doorSystem, _combatFeedback, _pickupSystem);
        _pickupSystem.SetObjectsTexture(_textures[GameTextureIndex.Objects]);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);
        _animationSystem = new AnimationSystem(
            _textures[GameTextureIndex.EnemyGuard],
            _textures[GameTextureIndex.Weapons],
            _player,
            _enemySystem);
        _weaponSystem = new WeaponSystem(
            _mapData,
            _doorSystem,
            _enemySystem,
            _textures[GameTextureIndex.EnemyGuard],
            _effectSystem,
            _soundSystem,
            _animationSystem);
        _playerSystem = new PlayerSystem(
            _player,
            _inputSystem,
            _movementSystem,
            _collisionSystem,
            _cameraSystem,
            _pickupSystem,
            _doorSystem,
            _animationSystem,
            _enemySystem,
            _weaponSystem,
            _effectSystem);

        _consoleOverlay = new ConsoleOverlay();
        _runtimeConsole = WorldConsoleBindings.CreateConsole(this, _player, _enemySystem, _consoleOverlay);
        _playerSystem.ConfigureLifecycle(
            () => _consoleOverlay.IsOpen,
            () => _ = RestartCurrentLevel());
        _playerSystem.ResetForLevelLoad(_mapData);

        _sceneRenderTexture = LoadRenderTexture(screenWidth, screenHeight);
        Debug.Setup(_doorSystem.Doors, _player, _animationSystem, _enemySystem);
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

    public void SetResolutionDownScaleMultiplier(int multiplier)
    {
        RenderData.ResolutionDownScaleMultiplier = multiplier;
        int newScreenWidth = (int)RenderData.Resolution.X / RenderData.ResolutionDownScaleMultiplier;
        int newScreenHeight = (int)RenderData.Resolution.Y / RenderData.ResolutionDownScaleMultiplier;

        UnloadRenderTexture(_sceneRenderTexture);
        _sceneRenderTexture = LoadRenderTexture(newScreenWidth, newScreenHeight);
    }

    public void OnEnter()
    {
        _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        _enemySystem.Rebuild(_mapData.Enemies, _mapData);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);

        if (OperatingSystem.IsBrowser())
            _inputSystem.EnableMouse();
        else
            _inputSystem.DisableMouse();
    }

    public ConsoleCommandResult LoadLevel(string pathOrName)
    {
        if (!LevelCatalog.TryResolve(pathOrName, out var resolvedPath, out var error))
            return ConsoleCommandResult.Fail(error);

        try
        {
            LevelSerializer.LoadFromJson(_mapData, Utilities.Res.Path(resolvedPath));
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
            LevelSerializer.LoadFromJson(_mapData, Utilities.Res.Path(_currentLevelPath));
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
        _effectSystem.Clear();
    }

    public void OnExit()
    {
        _inputSystem.EnableMouse();
    }

    public void ToggleMouse()
    {
        _inputSystem.ToggleMouse();
    }

    public void Update(float deltaTime)
    {
        _soundSystem.Update();
        bool toggledConsoleThisFrame = false;

        if (IsKeyPressed(KeyboardKey.Grave) ||
            (OperatingSystem.IsBrowser() && IsKeyPressed(KeyboardKey.Period)))
        {
            _consoleOverlay.Toggle();
            toggledConsoleThisFrame = true;
            if (_consoleOverlay.IsOpen)
                _inputSystem.EnableMouse();
            else
                _inputSystem.DisableMouse();
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

        if (IsKeyPressed(KeyboardKey.PageDown) && RenderData.ResolutionDownScaleMultiplier > 1)
        {
            RenderData.ResolutionDownScaleMultiplier /= 2;
            SetResolutionDownScaleMultiplier(RenderData.ResolutionDownScaleMultiplier);
        }

        if (IsKeyPressed(KeyboardKey.PageUp))
        {
            RenderData.ResolutionDownScaleMultiplier *= 2;
            SetResolutionDownScaleMultiplier(RenderData.ResolutionDownScaleMultiplier);
        }

        if (!_inputState.IsPaused)
        {
            _effectSystem.Update(deltaTime);

            int screenWidth = GetScreenWidth();
            int screenHeight = GetScreenHeight();

            if (_player.IsAlive)
            {
                _playerSystem.UpdateAlive(deltaTime, _inputState, mouseDelta, screenWidth, screenHeight);
                _enemySystem.Update(deltaTime);
            }
            else
            {
                _playerSystem.UpdateDead(deltaTime, _inputState, mouseDelta);
                _enemySystem.Update(deltaTime);
            }
        }
    }

    public ConsoleCommandResult ExecuteConsoleLine(string line)
    {
        return _runtimeConsole.Execute(line);
    }

    public void Render()
    {
        PrimitiveRenderer.SetLightingParameters(_player.Position, maxDistance: 50.0f, minBrightness: 0.1f);
        var lightingShader = PrimitiveRenderer.GetLightingShader();

        BeginTextureMode(_sceneRenderTexture);
        BeginMode3D(_player.Camera);
        ClearBackground(Color.Black);

        if (lightingShader.HasValue)
            BeginShaderMode(lightingShader.Value);

        _renderSystem.Render(_player);
        _doorSystem.Render();
        _animationSystem.Render();

        if (lightingShader.HasValue)
            EndShaderMode();

        _pickupSystem.Render(_player.Camera.Position);
        Debug.Draw3DOverlays(_inputState.IsDebugEnabled);

        EndMode3D();
        EndTextureMode();

        BeginDrawing();
        ClearBackground(Color.Black);

        DrawTexturePro(
            _sceneRenderTexture.Texture,
            new Rectangle(0, 0, (float)_sceneRenderTexture.Texture.Width, (float)-_sceneRenderTexture.Texture.Height),
            new Rectangle(0, 0, GetScreenWidth(), GetScreenHeight()),
            new Vector2(0, 0),
            0,
            Color.White);

        DrawFPS(10, GetScreenHeight() - 120);
        var mouseLabel = _inputState.IsMouseFree ? "MOUSE: FREE" : "MOUSE: LOCKED";
        var mouseColor = _inputState.IsMouseFree ? Color.Green : Color.Red;
        int mouseLabelWidth = MeasureText(mouseLabel, 20);
        DrawText(mouseLabel, GetScreenWidth() - mouseLabelWidth - 10, 10, 20, mouseColor);

        var healthLabel = $"HEALTH: {(int)_player.Health} / {(int)_player.MaxHealth}";
        DrawText(healthLabel, 10, 40, 20, _player.IsAlive ? Color.RayWhite : Color.Red);

        int screenWidth = GetScreenWidth();
        int screenHeight = GetScreenHeight();

        if (_player.IsAlive)
            GameOverlayHud.DrawInventory(_player);

        if (_player.IsAlive && !_consoleOverlay.IsOpen)
            _animationSystem.RenderWeaponOverlay(screenWidth, screenHeight);

        _effectSystem.RenderScreenOverlay(screenWidth, screenHeight);

        if (_player.IsAlive && !_consoleOverlay.IsOpen && _doorSystem.HasLockedHint)
            GameOverlayHud.DrawDoorLockedHint(_doorSystem, screenWidth, screenHeight);
        else if (_player.IsAlive && !_consoleOverlay.IsOpen && _weaponSystem.HasNoAmmoHint)
            GameOverlayHud.DrawNoAmmoHint(_weaponSystem, screenWidth, screenHeight);

        if (!_player.IsAlive && !_consoleOverlay.IsOpen)
            GameOverlayHud.DrawGameOver(screenWidth, screenHeight);

        if (!_consoleOverlay.IsOpen && !_inputState.IsMouseFree && _player.IsAlive)
            GameOverlayHud.DrawReticle(_effectSystem, screenWidth, screenHeight);

        if (_inputState.IsMinimapEnabled)
            _minimapSystem.Render(_player);

        int renderW = (int)RenderData.Resolution.X / RenderData.ResolutionDownScaleMultiplier;
        int renderH = (int)RenderData.Resolution.Y / RenderData.ResolutionDownScaleMultiplier;
        Debug.DrawWorldOverlays(_inputState.IsDebugEnabled, _player.Camera, renderW, renderH);
        Debug.Draw(_inputState.IsDebugEnabled);
        _consoleOverlay.Render();

        EndDrawing();
    }
}
