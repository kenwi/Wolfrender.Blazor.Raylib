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

    /// <summary>Feet / collision origin at level start and after <c>restart</c>.</summary>
    private static readonly Vector3 InitialPlayerPosition =
        new(30.0f * LevelData.QuadSize, 2.0f, 28f * LevelData.QuadSize);

    /// <summary>Legacy eye position used only to derive heading; <see cref="CameraSystem"/> always sets camera at the player.</summary>
    private static readonly Vector3 LegacyCameraEyeForHeading =
        new(30.0f * LevelData.QuadSize, 2.0f, 30f * LevelData.QuadSize);

    private static readonly Vector3 LegacyCameraTargetForHeading = new(120.0f, 2.0f, 119.0f);

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

    private  RenderTexture2D _sceneRenderTexture;
    private InputState _inputState = new();
    private readonly EnemySystem _enemySystem;
    private readonly PickupSystem _pickupSystem;
    private bool _deathHandled;

    public Player Player => _player;
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
        ResetPlayerToInitialSpawn();

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
        _enemySystem = new EnemySystem(_player, _inputSystem, _collisionSystem, _doorSystem, _combatFeedback);
        _pickupSystem = new PickupSystem();
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);
        _animationSystem = new AnimationSystem(
            _textures[GameTextureIndex.EnemyGuard],
            _textures[GameTextureIndex.Weapons],
            _player,
            _enemySystem);
        _consoleOverlay = new ConsoleOverlay();
        _runtimeConsole = WorldConsoleBindings.CreateConsole(this, _player, _enemySystem, _consoleOverlay);
        
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
        // Rebuild doors and enemies from current MapData (may have changed in the editor)
        _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        _enemySystem.Rebuild(_mapData.Enemies, _mapData);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);

        // Browser pointer lock requires a user gesture (click) before it can activate,
        // so start with mouse free and let InputSystem lock on first click.
        // On desktop we can grab the cursor immediately.
        if (OperatingSystem.IsBrowser())
            _inputSystem.EnableMouse();
        else
            _inputSystem.DisableMouse();
    }

    /// <summary>
    /// Loads a level JSON from <see cref="LevelCatalog"/> and resets gameplay state.
    /// </summary>
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

    /// <summary>
    /// Reload <see cref="CurrentLevelPath"/> into the shared <see cref="MapData"/> and reset gameplay state.
    /// </summary>
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

    private void ResetLevelState()
    {
        ResetPlayerToInitialSpawn();
        _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        _enemySystem.Rebuild(_mapData.Enemies, _mapData);
        _pickupSystem.Rebuild(_mapData.Pickups, _mapData);
        _effectSystem.Clear();
        _cameraSystem.ResetDeathFall();
        _deathHandled = false;
    }

    private void ResetPlayerToInitialSpawn()
    {
        _player.Position = InitialPlayerPosition;
        _player.OldPosition = _player.Position;
        _player.Velocity = Vector3.Zero;
        _player.Health = _player.MaxHealth;
        _player.WeaponCooldownRemaining = 0f;
        _player.ResetInventory();

        // CameraSystem forces camera.Position = player.Position every frame. If we leave
        // camera at a different point (old ctor did), the first frame after the console
        // closes snaps the view - looks like restart "moved" the player when toggling UI.
        Vector3 heading = LegacyCameraTargetForHeading - LegacyCameraEyeForHeading;
        float lookDistance = heading.Length();
        Vector3 forward = lookDistance > 0.0001f
            ? Vector3.Normalize(heading)
            : new Vector3(0f, 0f, -1f);

        var cam = _player.Camera;
        cam.Position = _player.Position;
        cam.Target = _player.Position + forward * lookDistance;
        cam.Up = new Vector3(0f, 1f, 0f);
        cam.FovY = 60f;
        cam.Projection = CameraProjection.Perspective;
        _player.Camera = cam;
    }

    public void OnExit()
    {
        // Restore cursor when leaving the game scene
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
            // Consume char events on the same frame as the toggle key press,
            // so keyboard layouts that emit symbols from Grave do not insert junk.
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
        {
            mouseDelta = new Vector2(0, 0);
        }

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
            _player.WeaponCooldownRemaining = MathF.Max(0f, _player.WeaponCooldownRemaining - deltaTime);

            if (_player.IsAlive)
            {
                _player.Velocity = _inputSystem.GetMoveDirection(_player) * _player.MoveSpeed;
                _movementSystem.Update(_player, deltaTime);
                _collisionSystem.Update(_player, deltaTime);
                _pickupSystem.Update(_player);
                _cameraSystem.Update(_player, _inputState.IsMouseFree, mouseDelta);
                _doorSystem.Update(deltaTime, _inputState, _player.Position, _enemySystem.Enemies);
                _animationSystem.Update(deltaTime);
                _enemySystem.Update(deltaTime);

                if (_inputState.IsPrimaryFire
                    && _player.WeaponCooldownRemaining <= 0f
                    && !_inputState.IsMouseFree)
                {
                    TryPlayerFire();
                }
            }
            else
            {
                HandlePlayerDeath();
                _player.Velocity = Vector3.Zero;
                _cameraSystem.UpdateDeathFall(_player, deltaTime);
                _doorSystem.Update(deltaTime, _inputState, _player.Position, _enemySystem.Enemies);
                _animationSystem.Update(deltaTime);
                _enemySystem.Update(deltaTime);
                TryRestartFromGameOver();
            }
        }
    }

    private void HandlePlayerDeath()
    {
        if (_deathHandled)
            return;

        _deathHandled = true;
        _effectSystem.EnableDeathOverlay();
        _inputSystem.EnableMouse();
    }

    private void TryRestartFromGameOver()
    {
        if (_consoleOverlay.IsOpen)
            return;

        bool restartPressed = IsKeyPressed(KeyboardKey.R)
            || IsMouseButtonPressed(MouseButton.Left);

        if (!restartPressed)
            return;

        RestartCurrentLevel();

        if (OperatingSystem.IsBrowser())
            _inputSystem.EnableMouse();
        else
            _inputSystem.DisableMouse();
    }

    public ConsoleCommandResult ExecuteConsoleLine(string line)
    {
        return _runtimeConsole.Execute(line);
    }

    private void TryPlayerFire()
    {
        if (Hitscan.TryHitEnemyScreenRay(
                _mapData,
                _doorSystem.Doors,
                _player.Camera,
                GetScreenWidth(),
                GetScreenHeight(),
                _enemySystem.Enemies,
                _textures[GameTextureIndex.EnemyGuard],
                4f,
                4f,
                Hitscan.DefaultMaxRangeTiles,
                out var hit) && hit is not null)
        {
            hit.ApplyDamage(_player.PistolDamage);
        }

        _player.WeaponCooldownRemaining = _player.PistolCooldownSeconds;
        _effectSystem.TriggerReticleFireFlash();
        _animationSystem.PlayPistolFire();
        _soundSystem.PlayPistolFire();
    }

    public void Render()
    {
        // Update lighting shader with current player position
        PrimitiveRenderer.SetLightingParameters(_player.Position, maxDistance: 50.0f, minBrightness: 0.1f);
        var lightingShader = PrimitiveRenderer.GetLightingShader();
        
        // Render 3D scene
        BeginTextureMode(_sceneRenderTexture);
        BeginMode3D(_player.Camera);
        ClearBackground(Color.Black);
        
        // Enable lighting shader
        if (lightingShader.HasValue)
        {
            BeginShaderMode(lightingShader.Value);
        }

        _renderSystem.Render(_player);
        _doorSystem.Render();
        _animationSystem.Render();
        
        // Disable lighting shader
        if (lightingShader.HasValue)
        {
            EndShaderMode();
        }

        _pickupSystem.Render(_player.Camera.Position);

        // Draw 3D debug overlays (unlit, after shader ends)
        Debug.Draw3DOverlays(_inputState.IsDebugEnabled);
        
        EndMode3D();
        EndTextureMode();

        // Render HUD
        // _hudSystem.Begin();
        // _hudSystem.Render(_player, _level);
        // _hudSystem.End();

        // Composite to screen
        BeginDrawing();
        ClearBackground(Color.Black);

        DrawTexturePro(
            _sceneRenderTexture.Texture,
            new Rectangle(0, 0, (float)_sceneRenderTexture.Texture.Width, (float)-_sceneRenderTexture.Texture.Height),
            new Rectangle(0, 0, GetScreenWidth(), GetScreenHeight()),
            new Vector2(0, 0),
            0,
            Color.White);

        // _hudSystem.DrawToScreen(screenWidth, screenHeight);
        DrawFPS(10, GetScreenHeight() - 120);
        var mouseLabel = _inputState.IsMouseFree ? "MOUSE: FREE" : "MOUSE: LOCKED";
        var mouseColor = _inputState.IsMouseFree ? Color.Green : Color.Red;
        int mouseLabelWidth = MeasureText(mouseLabel, 20);
        DrawText(mouseLabel, GetScreenWidth() - mouseLabelWidth - 10, 10, 20, mouseColor);

        var healthLabel = $"HEALTH: {(int)_player.Health} / {(int)_player.MaxHealth}";
        DrawText(healthLabel, 10, 40, 20, _player.IsAlive ? Color.RayWhite : Color.Red);

        if (_player.IsAlive)
            DrawInventoryHud();

        if (_player.IsAlive && !_consoleOverlay.IsOpen)
            _animationSystem.RenderWeaponOverlay(GetScreenWidth(), GetScreenHeight());

        _effectSystem.RenderScreenOverlay(GetScreenWidth(), GetScreenHeight());

        if (!_player.IsAlive && !_consoleOverlay.IsOpen)
            DrawGameOverOverlay();

        if (!_consoleOverlay.IsOpen && !_inputState.IsMouseFree && _player.IsAlive)
            DrawReticle();
        
        if (_inputState.IsMinimapEnabled)
        {
            // Render minimap
            _minimapSystem.Render(_player);
        }
        
        // Draw debug overlays
        int renderW = (int)RenderData.Resolution.X / RenderData.ResolutionDownScaleMultiplier;
        int renderH = (int)RenderData.Resolution.Y / RenderData.ResolutionDownScaleMultiplier;
        Debug.DrawWorldOverlays(_inputState.IsDebugEnabled, _player.Camera, renderW, renderH);
        Debug.Draw(_inputState.IsDebugEnabled);
        _consoleOverlay.Render();
        
        EndDrawing();
    }

    private void DrawInventoryHud()
    {
        const int fontSize = 18;
        int y = 68;

        string weaponLabel = _player.HasMachineGun ? "WEAPON: MACHINE GUN" : "WEAPON: PISTOL";
        DrawText(weaponLabel, 10, y, fontSize, Color.RayWhite);
        y += 24;

        if (_player.HasMachineGun || _player.Ammo > 0)
        {
            DrawText($"AMMO: {_player.Ammo}", 10, y, fontSize, new Color(255, 220, 40, 255));
            y += 24;
        }

        var goldColor = _player.HasGoldKey ? new Color(255, 210, 40, 255) : new Color(100, 90, 50, 255);
        var silverColor = _player.HasSilverKey ? new Color(200, 220, 255, 255) : new Color(90, 95, 110, 255);
        DrawText("KEYS:", 10, y, fontSize, Color.RayWhite);
        DrawText(" GOLD", 58, y, fontSize, goldColor);
        DrawText(" SILVER", 118, y, fontSize, silverColor);
    }

    private void DrawGameOverOverlay()
    {
        int screenW = GetScreenWidth();
        int screenH = GetScreenHeight();

        const int panelW = 520;
        const int panelH = 220;
        int panelX = (screenW - panelW) / 2;
        int panelY = (screenH - panelH) / 2;

        DrawRectangle(panelX, panelY, panelW, panelH, new Color(0, 0, 0, 190));
        DrawRectangleLines(panelX, panelY, panelW, panelH, new Color(220, 40, 40, 255));

        const string title = "YOU DIED";
        const int titleSize = 48;
        int titleW = MeasureText(title, titleSize);
        DrawText(title, (screenW - titleW) / 2, panelY + 28, titleSize, new Color(255, 60, 60, 255));

        const string restartLine = "Press R or click to restart";
        const int lineSize = 22;
        int restartW = MeasureText(restartLine, lineSize);
        DrawText(restartLine, (screenW - restartW) / 2, panelY + 100, lineSize, Color.RayWhite);

        const string consoleLine = "Console: type  restart  (~ or . to open)";
        int consoleW = MeasureText(consoleLine, 18);
        DrawText(consoleLine, (screenW - consoleW) / 2, panelY + 150, 18, new Color(200, 200, 200, 255));
    }

    /// <summary>Screen-center crosshair aligned with the camera forward axis (hitscan origin).</summary>
    private void DrawReticle()
    {
        int cx = GetScreenWidth() / 2;
        int cy = GetScreenHeight() / 2;
        const float arm = 10f;
        const float gap = 5f;
        const float thick = 2f;
        var outline = new Color(0, 0, 0, 220);
        var fill = _effectSystem.GetReticleColor();

        void Stroke(float x1, float y1, float x2, float y2)
        {
            DrawLineEx(new Vector2(x1, y1), new Vector2(x2, y2), thick + 1f, outline);
            DrawLineEx(new Vector2(x1, y1), new Vector2(x2, y2), thick, fill);
        }

        Stroke(cx - gap - arm, cy, cx - gap, cy);
        Stroke(cx + gap, cy, cx + gap + arm, cy);
        Stroke(cx, cy - gap - arm, cx, cy - gap);
        Stroke(cx, cy + gap, cx, cy + gap + arm);
    }

}

