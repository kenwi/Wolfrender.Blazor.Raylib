using System.Numerics;
using Game.DebugConsole;
using Raylib_cs;
using static Raylib_cs.Raylib;
using static Game.Core.Level.Res;
using Game.Features.Options;

namespace Game.Core;

public class Application
{
    private IScene _activeScene;
    private readonly IScene _gameScene;
    private readonly IScene _editorScene;
    private bool _ensuredStartupDisplay;

    public Application()
    {
        NativeConsole.EnsureAttached();

        InitWindow(0, 0, "");
        WindowDisplayMode.CaptureNativeResolution();
        SetWindowState(ConfigFlags.FullscreenMode);
        SetExitKey(KeyboardKey.Null);
        InitAudioDevice();

        WindowDisplayMode.SyncRenderDataFromWindow();

        var mapData = LoadMapData();

        _gameScene = new World(mapData);
        var world = (World)_gameScene;
        _editorScene = new Editor.LevelEditorScene(mapData, world.EnemySystem, world.DoorSystem, world.SecretSystem, world.Player);

        _activeScene = _gameScene;
#if EDITOR
        _activeScene = _editorScene;
#endif
        _activeScene.OnEnter();
    }

    public void Run()
    {
        while (!WindowShouldClose())
        {
            if (!_ensuredStartupDisplay)
            {
                _ensuredStartupDisplay = true;
                if (_gameScene is World world)
                    world.EnsureStartupDisplay();
            }

            // F1 toggles between game and level editor
            if (IsKeyPressed(KeyboardKey.F1))
            {
                ToggleScene();
            }

            var deltaTime = GetFrameTime();
            _activeScene.Update(deltaTime);
            _activeScene.Render();
        }

        Cleanup();
    }

    private void ToggleScene()
    {
        _activeScene.OnExit();

        if (_activeScene == _gameScene)
        {
            _activeScene = _editorScene;
        }
        else
        {
            _activeScene = _gameScene;
        }

        _activeScene.OnEnter();
    }

    public static MapData LoadMapData()
    {
        var mapData = new MapData
        {
            TileTextures = TileTextureAtlas.LoadFromSheet(Path(TileSpriteSheet.SheetPath)),
            GameTextures = new List<Texture2D>
            {
                LoadTexture(Path("resources/enemy_guard.png")),
                LoadTexture(Path("resources/weapons2.png")),
                LoadTexture(Path("resources/Objects.png")),
                // Placeholder: same sheet layout as guard until a dedicated dog spritesheet exists.
                LoadTexture(Path("resources/enemy_guard.png")),
            }
        };
        LevelSerializer.LoadFromJson(mapData, Path(LevelCatalog.DefaultLevelPath));

        return mapData;
    }

    private void Cleanup()
    {
        WindowDisplayMode.RestoreNativeResolutionIfNeeded();
        CloseAudioDevice();
        CloseWindow();
    }
}
