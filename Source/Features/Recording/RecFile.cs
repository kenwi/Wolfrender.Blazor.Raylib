namespace Game.Features.Recording;

public sealed class RecFile
{
    public const int CurrentVersion = 2;

    public required int Version { get; init; }
    public required string LevelPath { get; init; }
    public required float MouseSensitivity { get; init; }
    public PlayerSnapshot? PlayerSnapshot { get; init; }
    public required IReadOnlyList<InputEvent> Events { get; init; }
}
