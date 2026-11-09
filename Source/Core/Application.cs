using System.Numerics;
using Game.DebugConsole;
using Game.Features.Doors;
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

        EnsureGraphicsEnvironment();
        InitWindow(0, 0, "");
        if (!IsWindowReady())
            throw GraphicsInitFailed("see the GLFW error printed above");

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

    // raylib 5.5 InitWindow() still calls rlglInit() after GLFW failure, which SIGSEGVs.
    private static void EnsureGraphicsEnvironment()
    {
        if (OperatingSystem.IsBrowser() || !OperatingSystem.IsLinux())
            return;

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            return;

        throw GraphicsInitFailed("no DISPLAY or WAYLAND_DISPLAY");
    }

    private static InvalidOperationException GraphicsInitFailed(string detail) =>
        new("Failed to initialize the graphics window / OpenGL context " +
            $"({detail}).");

    public void Run()
    {
        while (IsWindowReady() && !WindowShouldClose())
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
        var tileTexturePaths = new List<string>(
            WallTilePalette.Count + DoorTileEncoding.PaletteEntries.Length);
        foreach (var entry in WallTilePalette.Entries)
            tileTexturePaths.Add(Path(entry.ResourcePath));
        foreach (var entry in DoorTileEncoding.PaletteEntries)
            tileTexturePaths.Add(Path(entry.ResourcePath));

        var mapData = new MapData
        {
            TileTextures = TileTextureAtlas.LoadAll(tileTexturePaths),
            GameTextures = new List<Texture2D>
            {
                LoadTexture(Path("resources/enemy_guard.png")),
                LoadTexture(Path("resources/weapons2.png")),
                LoadTexture(Path("resources/Objects.png")),
                // Placeholder: same sheet layout as guard until a dedicated dog spritesheet exists.
                LoadTexture(Path("resources/enemy_dog.png")),
                // Placeholder: same sheet layout as guard until a dedicated officer spritesheet exists.
                LoadTexture(Path("resources/enemy_officer.png")),
            }
        };
#if EDITOR
        string editorW3dmap = Path(LevelCatalog.DefaultEditorW3dmapPath);
        if (File.Exists(editorW3dmap))
            LevelSerializer.LoadFromW3dmap(mapData, editorW3dmap);
        else
            LevelSerializer.LoadFromJson(mapData, Path(LevelCatalog.DefaultLevelPath));
#else
        LevelSerializer.LoadFromJson(mapData, Path(LevelCatalog.DefaultLevelPath));
#endif

        return mapData;
    }

    private void Cleanup()
    {
        WindowDisplayMode.RestoreNativeResolutionIfNeeded();
        CloseAudioDevice();
        CloseWindow();
    }
}
