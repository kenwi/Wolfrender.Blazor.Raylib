namespace Game.Features.Options;

public readonly record struct ResolutionPreset(string Id, int Width, int Height, string Label)
{
    public bool IsNative => Id == KnownResolutions.NativeId;
}

public static class KnownResolutions
{
    public const string NativeId = "native";

    public static readonly ResolutionPreset Native = new(NativeId, 0, 0, "Native");

    public static readonly ResolutionPreset[] Presets =
    [
        new("320x200", 320, 200, "320 x 200"),
        new("320x240", 320, 240, "320 x 240"),
        new("640x400", 640, 400, "640 x 400"),
        new("640x480", 640, 480, "640 x 480"),
        new("800x600", 800, 600, "800 x 600"),
        new("1024x768", 1024, 768, "1024 x 768"),
        new("1280x1024", 1280, 1024, "1280 x 1024"),
        new("1280x720", 1280, 720, "1280 x 720"),
        new("1366x768", 1366, 768, "1366 x 768"),
        new("1600x900", 1600, 900, "1600 x 900"),
        new("1920x1080", 1920, 1080, "1920 x 1080"),
        new("2560x1440", 2560, 1440, "2560 x 1440"),
        new("3840x2160", 3840, 2160, "3840 x 2160"),
        Native,
    ];

    public static ResolutionPreset FindById(string id)
    {
        foreach (var preset in Presets)
        {
            if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
                return preset;
        }

        return Native;
    }

    public static (int Width, int Height) Resolve(ResolutionPreset preset, int windowWidth, int windowHeight)
    {
        if (preset.IsNative)
            return (Math.Max(1, windowWidth), Math.Max(1, windowHeight));

        return (Math.Max(1, preset.Width), Math.Max(1, preset.Height));
    }

    public static string FormatLabel(ResolutionPreset preset, int windowWidth, int windowHeight)
    {
        if (preset.IsNative)
            return $"Native ({windowWidth} x {windowHeight})";

        return preset.Label;
    }
}
