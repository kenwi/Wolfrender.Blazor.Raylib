namespace Game.Features.Options;

public sealed class GameSettings
{
    public string ResolutionPresetId { get; set; } = KnownResolutions.NativeId;

    public static GameSettings CreateDefault() => new();
}
