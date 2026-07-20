using System.Numerics;
using Game.Core.Level;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Options;

/// <summary>Window size and fullscreen mode. Does not change the internal render texture.</summary>
public static class WindowDisplayMode
{
    private static int _nativeWidth;
    private static int _nativeHeight;
    private static bool _hasNativeCapture;

    /// <summary>
    /// Record the desktop/monitor resolution before the game changes display mode.
    /// Call once after <see cref="InitWindow"/> and before entering fullscreen.
    /// </summary>
    public static void CaptureNativeResolution()
    {
        if (OperatingSystem.IsBrowser() || _hasNativeCapture)
            return;

        var (width, height) = GetReferenceDimensions();
        _nativeWidth = width;
        _nativeHeight = height;
        _hasNativeCapture = true;
    }

    public static (int Width, int Height) GetNativeDimensions() =>
        _hasNativeCapture
            ? (_nativeWidth, _nativeHeight)
            : GetReferenceDimensions();

    /// <summary>
    /// Leave fullscreen and restore the captured native resolution when the display size changed.
    /// Call from desktop host cleanup before <see cref="CloseWindow"/>.
    /// </summary>
    public static void RestoreNativeResolutionIfNeeded()
    {
        if (OperatingSystem.IsBrowser() || !_hasNativeCapture)
            return;

        bool wasFullscreen = IsWindowFullscreen();
        if (wasFullscreen)
            ToggleFullscreen();

        ClearWindowState(ConfigFlags.FullscreenMode);

        int currentWidth = GetScreenWidth();
        int currentHeight = GetScreenHeight();
        if (wasFullscreen
            || currentWidth != _nativeWidth
            || currentHeight != _nativeHeight)
        {
            SetWindowSize(_nativeWidth, _nativeHeight);
        }

        SyncRenderDataFromWindow();
    }

    /// <summary>
    /// Apply display mode immediately after <see cref="InitWindow"/> (before heavy loading).
    /// </summary>
    public static void ApplyStartup(GameSettings settings)
    {
        if (OperatingSystem.IsBrowser())
        {
            SyncRenderDataFromWindow();
            return;
        }

        if (settings.FullscreenEnabled)
            EnterFullscreen(ResolveDisplaySize(settings.WindowResolutionPresetId));

        SyncRenderDataFromWindow();
    }

    public static void Apply(GameSettings settings)
    {
        if (OperatingSystem.IsBrowser())
        {
            SyncRenderDataFromWindow();
            return;
        }

        ApplyDesktop(settings);
        SyncRenderDataFromWindow();
    }

    /// <summary>Re-enter fullscreen when another subsystem touched window flags (e.g. VSync).</summary>
    public static void ReapplyFullscreenIfNeeded(GameSettings settings)
    {
        if (OperatingSystem.IsBrowser() || !settings.FullscreenEnabled)
            return;

        if (!IsWindowFullscreen())
            EnterFullscreen(ResolveDisplaySize(settings.WindowResolutionPresetId));
    }

    public static void SyncRenderDataFromWindow()
    {
        RenderData.Resolution = new Vector2(GetScreenWidth(), GetScreenHeight());
    }

    public static (int Width, int Height) GetReferenceDimensions()
    {
        if (OperatingSystem.IsBrowser())
            return (Math.Max(1, GetScreenWidth()), Math.Max(1, GetScreenHeight()));

        int monitor = GetCurrentMonitor();
        return (Math.Max(1, GetMonitorWidth(monitor)), Math.Max(1, GetMonitorHeight(monitor)));
    }

    public static void EnterFullscreen(int width, int height)
    {
        SetWindowSize(Math.Max(1, width), Math.Max(1, height));
        SetWindowState(ConfigFlags.FullscreenMode);

        if (!IsWindowFullscreen())
            ToggleFullscreen();
    }

    public static void EnterFullscreen((int Width, int Height) size) =>
        EnterFullscreen(size.Width, size.Height);

    private static void ApplyDesktop(GameSettings settings)
    {
        var size = ResolveDisplaySize(settings.WindowResolutionPresetId);

        if (settings.FullscreenEnabled)
        {
            EnterFullscreen(size);
            return;
        }

        if (IsWindowFullscreen())
            ToggleFullscreen();

        ClearWindowState(ConfigFlags.FullscreenMode);
        SetWindowSize(size.Width, size.Height);
    }

    /// <summary>
    /// Resolve the window/display size for a preset. Native uses the captured desktop
    /// resolution when available so fullscreen native stays at the real monitor size.
    /// </summary>
    public static (int Width, int Height) ResolveDisplaySize(string presetId)
    {
        var (referenceW, referenceH) = GetNativeDimensions();
        var preset = KnownResolutions.FindById(presetId);
        return KnownResolutions.Resolve(preset, referenceW, referenceH);
    }

    public static (int Width, int Height) ResolveWindowedSize(string presetId) =>
        ResolveDisplaySize(presetId);
}
