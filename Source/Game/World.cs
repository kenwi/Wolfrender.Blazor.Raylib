using System.Numerics;
using Game.Entities;
using Game.Systems;
using Game.Utilities;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game;

public class World : IScene
{
    private readonly Player _player;
    private readonly SoundSystem _soundSystem;
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

    private readonly RenderTexture2D _sceneRenderTexture;
    private InputState _inputState = new();
    private readonly EnemySystem _enemySystem;

    public Player Player => _player;
    public EnemySystem EnemySystem => _enemySystem;
    public DoorSystem DoorSystem => _doorSystem;

    public World(MapData mapData)
    {
        int screenWidth = (int)RenderData.Resolution.X / RenderData.ResolutionDownScaleMultiplier;
        int screenHeight = (int)RenderData.Resolution.Y / RenderData.ResolutionDownScaleMultiplier;

        _mapData = mapData;
        _level = new LevelData(mapData);
        _textures = mapData.Textures;
        _player = new Player
        {
            Position = new Vector3(30.0f * 4, 2.0f, 28f * 4),
            Camera = new Camera3D
            {
                Position = new Vector3(30.0f * 4, 2.0f, 30f * 4),
                Target = new Vector3(120.0f, 2.0f, 119.0f),
                Up = new Vector3(0.0f, 1.0f, 0.0f),
                FovY = 60.0f,
                Projection = CameraProjection.Perspective
            }
        };

        _soundSystem = new SoundSystem("resources/03.mp3");
        _inputSystem = new InputSystem();
        _movementSystem = new MovementSystem();
        _doorSystem = new DoorSystem(mapData.Doors, mapData.Width, _textures);
        _collisionSystem = new CollisionSystem(_level, _doorSystem);
        _cameraSystem = new CameraSystem(_collisionSystem);
        _renderSystem = new RenderSystem(_level, _textures);
        _hudSystem = new HudSystem(screenWidth, screenHeight);
        _minimapSystem = new MinimapSystem(_level, _renderSystem);
        _enemySystem = new EnemySystem(_player, _inputSystem, _collisionSystem, _doorSystem);
        _animationSystem = new AnimationSystem(_textures[7], _player, _enemySystem);
        
        _sceneRenderTexture = LoadRenderTexture(screenWidth, screenHeight);
        Debug.Setup(_doorSystem.Doors, _player, _animationSystem, _enemySystem);
    }

    public void SetVolume(float volume)
    {
        _soundSystem.SetVolume(volume);
    }

    public void OnEnter()
    {
        // Rebuild doors and enemies from current MapData (may have changed in the editor)
        _doorSystem.Rebuild(_mapData.Doors, _mapData.Width);
        _enemySystem.Rebuild(_mapData.Enemies, _mapData);

        // Browser pointer lock requires a user gesture (click) before it can activate,
        // so start with mouse free and let InputSystem lock on first click.
        // On desktop we can grab the cursor immediately.
        if (OperatingSystem.IsBrowser())
            _inputSystem.EnableMouse();
        else
            _inputSystem.DisableMouse();
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
        _inputState = _inputSystem.GetInputState();
        var mouseDelta = _inputState.MouseDelta;
        
        if (_inputState.IsMouseFree)
        {
            mouseDelta = new Vector2(0, 0);
        }

        _inputSystem.Update();

        if (_inputState.IsGamePaused)
        {
            _player.Velocity = _inputSystem.GetMoveDirection(_player) * _player.MoveSpeed;

            _movementSystem.Update(_player, deltaTime);
            _collisionSystem.Update(_player, deltaTime);
            _cameraSystem.Update(_player, _inputState.IsMouseFree, mouseDelta);
            _doorSystem.Update(deltaTime, _inputState, _player.Position, _enemySystem.Enemies);
            _animationSystem.Update(deltaTime);
            _enemySystem.Update(deltaTime);
        }
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
        
        EndDrawing();
    }

}

