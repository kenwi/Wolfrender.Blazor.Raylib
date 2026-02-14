using System.Numerics;
using static Raylib_cs.Raylib;
using Color = Raylib_cs.Color;
using Raylib_cs;
using Game;
using Game.Utilities;

namespace Wolfrender.Blazor.Raylib.Pages;

public partial class ThreeDFirstPerson : IDisposable
{
    private const int ScreenWidth = 800;
    private const int ScreenHeight = 450;

    private IScene? _activeScene = null;
    private IScene? _gameScene = null;
    private IScene? _editorScene = null;

    private async Task Init()
    {
        // Preload all resources into the Emscripten VFS before Raylib/File APIs can use them
        var resourceFiles = new[]
        {
            "resources/greystone.png",
            "resources/bluestone.png",
            "resources/colorstone.png",
            "resources/mossy.png",
            "resources/redbrick.png",
            "resources/wood.png",
            "resources/door.png",
            "resources/enemy_guard.png",
            "resources/level.json",
            "resources/shaders/transparency.fs",
            "resources/shaders/lighting.vs",
            "resources/shaders/lighting.fs",
        };

        await Task.WhenAll(resourceFiles.Select(
            Wolfrender.Blazor.Raylib.Components.Raylib.PreloadFile));

        InitWindow(ScreenWidth, ScreenHeight, "Wolfrender");
        DisableCursor();

        RenderData.Resolution = new Vector2(GetScreenWidth(), GetScreenHeight());
        var mapData = Application.LoadMapData();

        // Create scenes with shared data
        _gameScene = new World(mapData);
        var world = (World)_gameScene;
        _editorScene = new Game.Editor.LevelEditorScene(mapData, world.EnemySystem, world.DoorSystem, world.Player);

        // Start with the game scene
        _activeScene = _gameScene;
        _activeScene.OnEnter();

        OnResize((ScreenWidth, ScreenHeight));
    }

    // Main game loop
    private async void Render(float delta)
    {
        await Task.CompletedTask;
    }

    private void OnResize((int width, int height) Size)
    {
        SetWindowSize(Size.width, Size.height);
    }

    public void Dispose()
    {
        CloseWindow();
    }
}
