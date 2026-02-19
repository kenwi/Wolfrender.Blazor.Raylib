using System.Numerics;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using static Raylib_cs.Raylib;
using Color = Raylib_cs.Color;
using Raylib_cs;
using Game;
using Game.Utilities;

namespace Wolfrender.Blazor.Raylib.Pages;

public partial class ThreeDFirstPerson : IDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private int ScreenWidth = 1920;
    private int ScreenHeight = 1080;

    private IScene? _activeScene = null;
    private IScene? _gameScene = null;
    private IScene? _editorScene = null;

    private ElementReference _logTextArea;
    private readonly StringBuilder _logBuilder = new();
    public string BlazorUILog => _logBuilder.ToString();
    public bool ShowDebugLogUI = false;
    public bool ShowOptionsUI = false;

    public float Volume { get; set; } = 0.5f;
    public event Action<float>? VolumeChanged;

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
            "resources/03.mp3",
        };

        await Task.WhenAll(resourceFiles.Select(
            Wolfrender.Blazor.Raylib.Components.Raylib.PreloadFile));

        InitWindow(ScreenWidth, ScreenHeight, "Wolfrender");
        InitAudioDevice();
        // DisableCursor();

        OnResize((ScreenWidth, ScreenHeight));

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
            if (ShowOptionsUI)
            {
                Log("Enabling cursor");
                EnableCursor();
            }
            else
            {
                Log("Disabling cursor");
                DisableCursor();
            }
            await InvokeAsync(StateHasChanged);
            await ScrollLogToBottom();
        }

        if (IsKeyPressed(KeyboardKey.I))
        {
            ShowDebugLogUI = !ShowDebugLogUI;
            await InvokeAsync(StateHasChanged);
        }

        if (IsKeyPressed(KeyboardKey.Enter))
        {
            Log("Enter pressed");
            await InvokeAsync(StateHasChanged);
            await ScrollLogToBottom();
        }

        var deltaTime = GetFrameTime();
        _activeScene.Update(deltaTime);
        _activeScene.Render();
    }

    private void OnResize((int width, int height) Size)
    {
        SetWindowSize(Size.width, Size.height);
    }

    public void Dispose()
    {
        CloseAudioDevice();
        CloseWindow();
    }
}
