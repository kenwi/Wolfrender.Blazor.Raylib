using System.Numerics;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using static Raylib_cs.Raylib;
using Raylib_cs;
using Game.Core;
using Game.Features.Options;
using System.Threading.Tasks;
using Wolfrender.Blazor.Raylib.Components;

namespace Wolfrender.Blazor.Raylib.Pages;

public partial class ThreeDFirstPerson : IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private int _screenWidth;
    private int _screenHeight;

    private IScene? _activeScene = null;
    private IScene? _gameScene = null;
    private IScene? _editorScene = null;

    private ElementReference _logTextArea;
    private readonly StringBuilder _logBuilder = new();
    public string BlazorUILog => _logBuilder.ToString();
    public bool ShowDebugLogUI = false;

    public void Log(string message)
    {
        _logBuilder.AppendLine(message);
        InvokeAsync(ScrollLogToBottom);
    }

    private async Task ScrollLogToBottom()
    {
        try
        {
            await JS.InvokeVoidAsync("eval", "document.querySelector('textarea[readonly]').scrollTop = document.querySelector('textarea[readonly]').scrollHeight");
        }
        catch { }
    }

    private async Task DetectBrowserResolution()
    {
        _screenWidth = await JS.InvokeAsync<int>("eval", "window.innerWidth");
        _screenHeight = await JS.InvokeAsync<int>("eval", "window.innerHeight");
        Log($"Browser resolution: {_screenWidth}x{_screenHeight}");
        await InvokeAsync(StateHasChanged);
    }

    private async Task Init()
    {
        await DetectBrowserResolution();

        var resourceFiles = new[]
        {
            "resources/spritesheet_tiles.png",
            "resources/enemy_guard.png",
            "resources/weapons2.png",
            "resources/Objects.png",
            "resources/level.json",
            "resources/test.json",
            "resources/shaders/transparency.fs",
            "resources/shaders/screen_sprite.vs",
            "resources/shaders/lighting.vs",
            "resources/shaders/lighting.fs",
            "resources/shaders/lighting_common.glsl",
            "resources/shaders/sprite_lit.fs",
            "resources/03.mp3",
            "resources/PistolFire.ogg",
            "resources/SmgFire.ogg",
            "resources/EnemyPistolFire.ogg",
            "resources/ChaingunFire.ogg"
        };

        await Task.WhenAll(resourceFiles.Select(
            Wolfrender.Blazor.Raylib.Components.Raylib.PreloadFile));

        InitWindow(_screenWidth, _screenHeight, "Wolfrender");
        SetExitKey(KeyboardKey.Null);
        InitAudioDevice();

        GraphicsFramePacing.BrowserApply = Components.Raylib.SetFramePacing;

        await OnResize((_screenWidth, _screenHeight));

        RenderData.Resolution = new Vector2(GetScreenWidth(), GetScreenHeight());
        var mapData = Application.LoadMapData();

        _gameScene = new World(mapData);
        _editorScene = new Game.Editor.LevelEditorScene(
            mapData,
            ((World)_gameScene).EnemySystem,
            ((World)_gameScene).DoorSystem,
            ((World)_gameScene).Player);

        _activeScene = _gameScene;
        _activeScene.OnEnter();
    }

    private async void Render(float delta)
    {
        if (IsKeyPressed(KeyboardKey.I))
        {
            ShowDebugLogUI = !ShowDebugLogUI;
            await InvokeAsync(ScrollLogToBottom);
            await InvokeAsync(StateHasChanged);
        }

        var deltaTime = GetFrameTime();
        _activeScene?.Update(deltaTime);
        _activeScene?.Render();
    }

    private async Task OnResize((int width, int height) Size)
    {
        await DetectBrowserResolution();
        SetWindowSize(_screenWidth, _screenHeight);
        RenderData.Resolution = new Vector2(_screenWidth, _screenHeight);
        (_gameScene as World)?.OnWindowResize();
    }

    public void Dispose()
    {
        CloseAudioDevice();
        CloseWindow();
    }
}
