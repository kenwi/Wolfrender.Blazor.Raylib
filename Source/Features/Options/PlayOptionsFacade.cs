using Game.Features.Hud;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Options;

/// <summary>
/// Applies options settings to audio, camera, display, and play render targets.
/// Keeps World free of options policy while owning scene/HUD texture lifecycle.
/// </summary>
public sealed class PlayOptionsFacade
{
    private readonly OptionsMenuSystem _optionsMenu;
    private readonly SoundSystem _soundSystem;
    private readonly CameraSystem _cameraSystem;

    private RenderTexture2D _sceneRenderTexture;
    private RenderTexture2D _hudRenderTexture;
    private bool _hasSceneRenderTexture;
    private bool _hasHudRenderTexture;

    public RenderTexture2D SceneRenderTexture => _sceneRenderTexture;
    public RenderTexture2D HudRenderTexture => _hudRenderTexture;

    public PlayOptionsFacade(
        OptionsMenuSystem optionsMenu,
        SoundSystem soundSystem,
        CameraSystem cameraSystem)
    {
        _optionsMenu = optionsMenu;
        _soundSystem = soundSystem;
        _cameraSystem = cameraSystem;
    }

    public void InitializeRenderTargets()
    {
        WindowDisplayMode.SyncRenderDataFromWindow();
        GameRenderResolution.Apply(
            _optionsMenu.Settings,
            ResolveWindowWidth(),
            ResolveWindowHeight());
        RecreateRenderTextures();
        ApplyGraphicsSettings();
        ApplyControlSettings();
        ApplyAudioSettings();
        EnsureStartupDisplay();
    }

    public void SetVolume(float volume)
    {
        float level = AudioVolumeLevel.FromSliderPosition(Math.Clamp(volume, 0f, 1f));
        float raylibVolume = AudioVolumeLevel.ToRaylibVolume(level);
        _soundSystem.ApplySfxVolume(raylibVolume);
        _soundSystem.ApplyMusicVolume(raylibVolume);
    }

    public float GetVolume() => _soundSystem.GetSfxVolume();

    public void SetMouseSensitivity(float sensitivity) =>
        _cameraSystem.SetMouseSensitivity(sensitivity);

    public void ApplyControlSettings() =>
        SetMouseSensitivity(_optionsMenu.Settings.MouseSensitivity);

    public void ApplyAudioSettings()
    {
        var settings = _optionsMenu.Settings;
        _soundSystem.ApplySfxVolume(AudioVolumeLevel.ToRaylibVolume(settings.AudioLevel));
        _soundSystem.ApplyMusicVolume(AudioVolumeLevel.ToRaylibVolume(settings.MusicLevel));
    }

    public void ApplyWindowDisplay() =>
        WindowDisplayMode.Apply(_optionsMenu.Settings);

    public void ApplyGameResolution()
    {
        GameRenderResolution.Apply(
            _optionsMenu.Settings,
            ResolveWindowWidth(),
            ResolveWindowHeight());
        RecreateRenderTextures();
    }

    public bool GetFullscreenEnabled() => _optionsMenu.Settings.FullscreenEnabled;

    public void SetFullscreenEnabled(bool enabled)
    {
        _optionsMenu.Settings.FullscreenEnabled = enabled;
        ApplyWindowDisplay();
        ApplyGameResolution();
    }

    public string GetWindowResolutionPresetId() => _optionsMenu.Settings.WindowResolutionPresetId;

    public void SetWindowResolutionPresetId(string presetId)
    {
        _optionsMenu.Settings.WindowResolutionPresetId = presetId;
        ApplyWindowDisplay();
        ApplyGameResolution();
    }

    public string GetGameResolutionPresetId() => _optionsMenu.Settings.GameResolutionPresetId;

    public void SetGameResolutionPresetId(string presetId)
    {
        _optionsMenu.Settings.GameResolutionPresetId = presetId;
        ApplyGameResolution();
    }

    public bool GetVSyncEnabled() => _optionsMenu.Settings.VSyncEnabled;

    public void SetVSyncEnabled(bool enabled)
    {
        _optionsMenu.Settings.VSyncEnabled = enabled;
        ApplyGraphicsSettings();
    }

    public int GetTargetFps() => _optionsMenu.Settings.TargetFps;

    public void SetTargetFps(int fps)
    {
        _optionsMenu.Settings.TargetFps = GraphicsFramePacing.ClampTargetFps(fps);
        ApplyGraphicsSettings();
    }

    public void ApplyGraphicsSettings() => GraphicsFramePacing.Apply(_optionsMenu.Settings);

    public void EnsureStartupDisplay()
    {
        WindowDisplayMode.Apply(_optionsMenu.Settings);
        WindowDisplayMode.SyncRenderDataFromWindow();

        if (KnownResolutions.FindById(_optionsMenu.Settings.GameResolutionPresetId).IsNative)
            ApplyGameResolution();
    }

    public void OnWindowResize()
    {
        WindowDisplayMode.SyncRenderDataFromWindow();
        if (KnownResolutions.FindById(_optionsMenu.Settings.GameResolutionPresetId).IsNative)
            ApplyGameResolution();
    }

    public void RecreateRenderTextures()
    {
        int sceneWidth = RenderData.InternalWidth;
        int sceneHeight = RenderData.InternalHeight;

        if (!_hasSceneRenderTexture ||
            _sceneRenderTexture.Texture.Width != sceneWidth ||
            _sceneRenderTexture.Texture.Height != sceneHeight)
        {
            if (_hasSceneRenderTexture)
                UnloadRenderTexture(_sceneRenderTexture);

            _sceneRenderTexture = LoadRenderTexture(sceneWidth, sceneHeight);
            _hasSceneRenderTexture = true;
        }

        if (!_hasHudRenderTexture ||
            _hudRenderTexture.Texture.Width != GameRenderSpace.HudTextureWidth ||
            _hudRenderTexture.Texture.Height != GameRenderSpace.HudTextureHeight)
        {
            if (_hasHudRenderTexture)
                UnloadRenderTexture(_hudRenderTexture);

            _hudRenderTexture = LoadRenderTexture(GameRenderSpace.HudTextureWidth, GameRenderSpace.HudTextureHeight);
            _hasHudRenderTexture = true;
        }
    }

    private static int ResolveWindowWidth()
    {
        int width = (int)RenderData.Resolution.X;
        return width > 0 ? width : GetScreenWidth();
    }

    private static int ResolveWindowHeight()
    {
        int height = (int)RenderData.Resolution.Y;
        return height > 0 ? height : GetScreenHeight();
    }
}
