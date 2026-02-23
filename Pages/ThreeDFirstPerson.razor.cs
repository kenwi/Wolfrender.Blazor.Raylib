using System.Numerics;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using static Raylib_cs.Raylib;
using Color = Raylib_cs.Color;
using Raylib_cs;
using Game;
using Game.Utilities;
using System.Threading.Tasks;

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
    public bool ShowOptionsUI = false;

    public float Volume { get; set; } = 0.5f;
    public float MouseSensitivityX { get; set; } = 1f;
    public float MouseSensitivityY { get; set; } = 1f;
    public int ResolutionDownsampling { get; set; } = 1;

    public event Action<float>? VolumeChanged;
    public event Action<float>? MouseSensitivityXChanged;
    public event Action<float>? MouseSensitivityYChanged;
    public event Action<int>? ResolutionDownsamplingChanged;

    public void Log(string message)
    {
        _logBuilder.AppendLine(message);
    }

    private void OnVolumeChanged(ChangeEventArgs e)
    {
        if (float.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            Volume = value;
            VolumeChanged?.Invoke(Volume);
            Log($"Volume changed to {Volume}");
        }
    }

    private void OnMouseSensitivityXChanged(ChangeEventArgs e)
    {
        if (float.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            MouseSensitivityX = value;
            MouseSensitivityXChanged?.Invoke(MouseSensitivityX);
            Log($"Mouse sensitivity X changed to {MouseSensitivityX}");
        }
    }

    private void OnMouseSensitivityYChanged(ChangeEventArgs e)
    {
        if (float.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            MouseSensitivityY = value;
            MouseSensitivityYChanged?.Invoke(MouseSensitivityY);
            Log($"Mouse sensitivity Y changed to {MouseSensitivityY}");
        }
    }

    private void OnResolutionDownsamplingChanged(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var value))
        {
            ResolutionDownsampling = Math.Clamp(value, 1, 6);
            ResolutionDownsamplingChanged?.Invoke(ResolutionDownsampling);
            Log($"Resolution downsampling changed to {ResolutionDownsampling}");
        }
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
        await ScrollLogToBottom();
        await InvokeAsync(StateHasChanged);
    }

    private async Task Init()
    {
        await DetectBrowserResolution();

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
            "resources/03.mp3",
        };

        await Task.WhenAll(resourceFiles.Select(
            Wolfrender.Blazor.Raylib.Components.Raylib.PreloadFile));

        InitWindow(_screenWidth, _screenHeight, "Wolfrender");
        InitAudioDevice();
        await OnResize((_screenWidth, _screenHeight));

        RenderData.Resolution = new Vector2(GetScreenWidth(), GetScreenHeight());
        var mapData = Application.LoadMapData();

        // Create scenes with shared data
        _gameScene = new World(mapData);    
        var world = (World)_gameScene;
        _editorScene = new Game.Editor.LevelEditorScene(mapData, world.EnemySystem, world.DoorSystem, world.Player);

        VolumeChanged += world.SetVolume;
        VolumeChanged?.Invoke(Volume);

        // Start with the game scene
        _activeScene = _gameScene;
        _activeScene.OnEnter();
    }

    // Main game loop
    private async void Render(float delta)
    {
        if (IsKeyPressed(KeyboardKey.Backspace))
        {
            ShowOptionsUI = !ShowOptionsUI;
            var world = _gameScene as World;
            var logString = ShowOptionsUI ? "Enabling cursor" : "Disabling cursor";
            Log(logString);
            world?.ToggleMouse();
            await ScrollLogToBottom();
            await InvokeAsync(StateHasChanged);
        }

        if (IsKeyPressed(KeyboardKey.I))
        {
            ShowDebugLogUI = !ShowDebugLogUI;
            await ScrollLogToBottom();
            await InvokeAsync(StateHasChanged);
        }

        var deltaTime = GetFrameTime();
        _activeScene.Update(deltaTime);
        _activeScene.Render();
    }

    private async Task OnResize((int width, int height) Size)
    {
        await DetectBrowserResolution();
        SetWindowSize(_screenWidth, _screenHeight);
        RenderData.Resolution = new Vector2(_screenWidth, _screenHeight);
    }

    public void Dispose()
    {
        CloseAudioDevice();
        CloseWindow();
    }
}
