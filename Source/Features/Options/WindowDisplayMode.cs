using System.Numerics;
using Game.Core.Level;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Options;

/// <summary>Window size and fullscreen mode. Does not change the internal render texture.</summary>
public static class WindowDisplayMode
{
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
            EnterFullscreen();

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
            EnterFullscreen();
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

    public static void EnterFullscreen()
    {
        int monitor = GetCurrentMonitor();
        SetWindowSize(GetMonitorWidth(monitor), GetMonitorHeight(monitor));
        SetWindowState(ConfigFlags.FullscreenMode);

        if (!IsWindowFullscreen())
            ToggleFullscreen();
    }

    private static void ApplyDesktop(GameSettings settings)
    {
        if (settings.FullscreenEnabled)
        {
            EnterFullscreen();
            return;
        }

        if (IsWindowFullscreen())
            ToggleFullscreen();

        ClearWindowState(ConfigFlags.FullscreenMode);
        var (width, height) = ResolveWindowedSize(settings.WindowResolutionPresetId);
        SetWindowSize(width, height);
    }

    public static (int Width, int Height) ResolveWindowedSize(string presetId)
    {
        var (monitorW, monitorH) = GetReferenceDimensions();
        var preset = KnownResolutions.FindById(presetId);
        return KnownResolutions.Resolve(preset, monitorW, monitorH);
    }
}
