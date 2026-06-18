using Game.Core.Level;

namespace Game.Features.Options;

public static class RenderResolution
{
    public static (int Width, int Height) ResolveInternalSize(GameSettings settings, int windowWidth, int windowHeight)
    {
        if (windowWidth <= 0)
            windowWidth = 1;
        if (windowHeight <= 0)
            windowHeight = 1;

        var preset = KnownResolutions.FindById(settings.ResolutionPresetId);
        return KnownResolutions.Resolve(preset, windowWidth, windowHeight);
    }

    public static void ApplyToRenderData(GameSettings settings, int windowWidth, int windowHeight)
    {
        var (width, height) = ResolveInternalSize(settings, windowWidth, windowHeight);
        RenderData.InternalWidth = width;
        RenderData.InternalHeight = height;
    }
}
