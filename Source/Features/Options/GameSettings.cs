namespace Game.Features.Options;

public sealed class GameSettings
{
    public bool FullscreenEnabled { get; set; } = true;
    public string WindowResolutionPresetId { get; set; } = KnownResolutions.NativeId;
    public string GameResolutionPresetId { get; set; } = KnownResolutions.NativeId;
    public bool VSyncEnabled { get; set; } = true;
    public int TargetFps { get; set; } = GraphicsFramePacing.DefaultTargetFps;

    public static GameSettings CreateDefault() => new();
}
