using Game.Core.Level;

namespace Game.Features.Options;

/// <summary>Internal render texture resolution (upscaled to the window when drawn).</summary>
public static class GameRenderResolution
{
    public static (int Width, int Height) ResolveInternalSize(GameSettings settings, int windowWidth, int windowHeight)
    {
        if (windowWidth <= 0)
            windowWidth = 1;
        if (windowHeight <= 0)
            windowHeight = 1;

        var preset = KnownResolutions.FindById(settings.GameResolutionPresetId);
        return KnownResolutions.Resolve(preset, windowWidth, windowHeight);
    }

    public static void Apply(GameSettings settings, int windowWidth, int windowHeight)
    {
        ClampToWindowIfNeeded(settings, windowWidth, windowHeight);
        var (width, height) = ResolveInternalSize(settings, windowWidth, windowHeight);
        RenderData.InternalWidth = width;
        RenderData.InternalHeight = height;
    }

    /// <summary>
    /// When the window is smaller than the selected game preset, fall back to native (match window).
    /// </summary>
    public static void ClampToWindowIfNeeded(GameSettings settings, int windowWidth, int windowHeight)
    {
        if (windowWidth <= 0)
            windowWidth = 1;
        if (windowHeight <= 0)
            windowHeight = 1;

        var gamePreset = KnownResolutions.FindById(settings.GameResolutionPresetId);
        if (gamePreset.IsNative)
            return;

        var (gameWidth, gameHeight) = KnownResolutions.Resolve(gamePreset, windowWidth, windowHeight);
        if (gameWidth <= windowWidth && gameHeight <= windowHeight)
            return;

        settings.GameResolutionPresetId = KnownResolutions.NativeId;
    }
}
