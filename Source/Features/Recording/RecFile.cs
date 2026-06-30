namespace Game.Features.Recording;

public sealed class RecFile
{
    public const int CurrentVersion = 3;

    public required int Version { get; init; }
    public required string LevelPath { get; init; }
    public required float MouseSensitivity { get; init; }
    public int TickHz { get; init; } = RecordingSimulationDefaults.DefaultTickHz;
    public PlayerSnapshot? PlayerSnapshot { get; init; }
    public required IReadOnlyList<InputEvent> Events { get; init; }

    public int ResolveTickHz() =>
        Version >= 3
            ? Math.Clamp(TickHz, RecordingSimulationDefaults.MinTickHz, RecordingSimulationDefaults.MaxTickHz)
            : RecordingSimulationDefaults.DefaultTickHz;
}
