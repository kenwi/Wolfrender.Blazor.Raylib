using System.Numerics;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using static Raylib_cs.Raylib;
using Raylib_cs;
using Game;
using Game.Editor;
using Game.Utilities;

namespace Wolfrender.Blazor.Raylib.Pages;

public partial class WebEditor : IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private int ScreenWidth = 1920;
    private int ScreenHeight = 1080;

    private WebEditorScene? _editorScene;
    private IScene? _gameScene;
    private IScene? _activeScene;

    private readonly StringBuilder _logBuilder = new();

    // Panel visibility
    private bool _showLayers = true;
    private bool _showTilePalette = true;
    private bool _showCursorInfo = true;
    private bool _showEnemyProperties = true;
    private bool _showDebugLog = false;

    // File dialogs
    private bool _showSaveDialog;
    private bool _showLoadDialog;

    // Cursor info (updated each frame from Raylib, displayed by Blazor)
    private int _cursorTileX;
    private int _cursorTileY;
    private float _cursorWorldX;
    private float _cursorWorldY;
    private bool _cursorInBounds;

    // Throttle StateHasChanged to avoid excessive re-renders
    private int _frameCount;
    private bool _loggedFirstFrame;

    private async Task Init()
    {
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
            "resources/03.mp3",
        };

        await Task.WhenAll(resourceFiles.Select(
            Wolfrender.Blazor.Raylib.Components.Raylib.PreloadFile));

        InitWindow(ScreenWidth, ScreenHeight, "Wolfrender - Level Editor");
        InitAudioDevice();
        OnResize((ScreenWidth, ScreenHeight));

        RenderData.Resolution = new Vector2(GetScreenWidth(), GetScreenHeight());
        var mapData = Application.LoadMapData();

        _gameScene = new World(mapData);
        var world = (World)_gameScene;
        _editorScene = new WebEditorScene(mapData, world.EnemySystem, world.DoorSystem, world.Player);

        _activeScene = _editorScene;
        _activeScene.OnEnter();

        _editorScene.State.StateChanged += OnEditorStateChanged;

        await InvokeAsync(StateHasChanged);
    }

    private async void Render(float delta)
    {
        if (!_loggedFirstFrame)
        {
            Console.WriteLine($"[WebEditor] Render loop started. Active scene: {_activeScene?.GetType().Name}");
            _loggedFirstFrame = true;
        }

        // Detect any key press for debugging
        int keyPressed = GetKeyPressed();
        if (keyPressed != 0)
        {
            Console.WriteLine($"[WebEditor] Key pressed: {keyPressed} (Q={((int)KeyboardKey.Q)})");
        }

        if (IsKeyPressed(KeyboardKey.Q))
        {
            Console.WriteLine("[WebEditor] Q detected via IsKeyPressed - toggling scene");
            ToggleScene();
            await InvokeAsync(StateHasChanged);
        }

        var deltaTime = GetFrameTime();
        _activeScene?.Update(deltaTime);
        _activeScene?.Render();

        // Update cursor info from Raylib each frame for the Blazor UI
        if (_editorScene != null && _activeScene == _editorScene)
        {
            var mouseScreen = GetMousePosition();
            var worldPos = _editorScene.State.Camera.ScreenToWorld(mouseScreen);
            _cursorTileX = (int)MathF.Floor(worldPos.X);
            _cursorTileY = (int)MathF.Floor(worldPos.Y);
            _cursorWorldX = worldPos.X;
            _cursorWorldY = worldPos.Y;
            _cursorInBounds = _cursorTileX >= 0 && _cursorTileX < _editorScene.State.MapData.Width
                           && _cursorTileY >= 0 && _cursorTileY < _editorScene.State.MapData.Height;
        }

        _frameCount++;
        if (_frameCount % 10 == 0)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ToggleScene()
    {
        _activeScene?.OnExit();

        if (_activeScene == _gameScene)
        {
            _activeScene = _editorScene;
        }
        else
        {
            _activeScene = _gameScene;
        }

        _activeScene?.OnEnter();
    }

    private void OnResize((int width, int height) Size)
    {
        SetWindowSize(Size.width, Size.height);
    }

    private async void OnEditorStateChanged()
    {
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnNewLevel()
    {
        _editorScene?.State.ClearLevel();
        await InvokeAsync(StateHasChanged);
    }

    private async Task Refresh()
    {
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        if (_editorScene != null)
            _editorScene.State.StateChanged -= OnEditorStateChanged;
        CloseAudioDevice();
        CloseWindow();
    }
}
