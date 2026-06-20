using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Options;

public static class GraphicsFramePacing
{
    public const int MinTargetFps = 30;
    public const int MaxTargetFps = 3000;
    public const int DefaultTargetFps = 120;

    /// <summary>Browser host hook (registered by the Blazor shell). Args: vsync, targetFps.</summary>
    public static Action<bool, int>? BrowserApply { get; set; }

    public static void Apply(GameSettings settings)
    {
        int fps = ClampTargetFps(settings.TargetFps);

        if (OperatingSystem.IsBrowser())
        {
            BrowserApply?.Invoke(settings.VSyncEnabled, fps);
            return;
        }

        if (settings.VSyncEnabled)
        {
            SetWindowState(ConfigFlags.VSyncHint);
            SetTargetFPS(0);
        }
        else
        {
            ClearWindowState(ConfigFlags.VSyncHint);
            SetTargetFPS(fps);
        }

        WindowDisplayMode.ReapplyFullscreenIfNeeded(settings);
    }

    public static int ClampTargetFps(int fps) => Math.Clamp(fps, MinTargetFps, MaxTargetFps);
}
