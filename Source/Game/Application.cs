using System.Numerics;
using Game.Utilities;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game;

public class Application
{
    private IScene _activeScene;
    private readonly IScene _gameScene;
    private readonly IScene _editorScene;

    public Application()
    {
        SetTargetFPS(120);
        InitWindow(0, 0, "");
        SetWindowState(ConfigFlags.FullscreenMode);
        InitAudioDevice();

        RenderData.Resolution = new Vector2(GetScreenWidth(), GetScreenHeight());

        // Load shared map data
        var mapData = LoadMapData();

        // Create scenes with shared data
        _gameScene = new World(mapData);
        var world = (World)_gameScene;
        _editorScene = new Editor.LevelEditorScene(mapData, world.EnemySystem, world.DoorSystem, world.Player);

        // // Start with the game scene
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
        var textures = new List<Texture2D>
        {
            LoadTexture("resources/greystone.png"),
            LoadTexture("resources/bluestone.png"),
            LoadTexture("resources/colorstone.png"),
            LoadTexture("resources/mossy.png"),
            LoadTexture("resources/redbrick.png"),
            LoadTexture("resources/wood.png"),
            LoadTexture("resources/door.png"),
            LoadTexture("resources/enemy_guard.png")
        };

        var mapData = new MapData { Textures = textures };
        Editor.LevelSerializer.LoadFromJson(mapData, "resources/level.json");

        return mapData;
    }

    private void Cleanup()
    {
        CloseAudioDevice();
        CloseWindow();
    }
}
