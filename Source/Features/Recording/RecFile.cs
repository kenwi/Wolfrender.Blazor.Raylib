namespace Game.Features.Recording;

public sealed class RecFile
{
    public const int CurrentVersion = 5;

    public required int Version { get; init; }
    public required string LevelPath { get; init; }
    public required float MouseSensitivity { get; init; }
    public int TickHz { get; init; } = RecordingSimulationDefaults.DefaultTickHz;
    public PlayerSnapshot? PlayerSnapshot { get; init; }
    public required IReadOnlyList<InputEvent> Events { get; init; }

    /// <summary>Total simulated ticks in the recording (v5+). 0 for legacy files.</summary>
    public long DurationTicks { get; init; }

    /// <summary>RNG seed applied at level reset when the recording started (v5+).</summary>
    public int? RngSeed { get; init; }

    /// <summary>Simulation state keyframes for replay divergence detection (v5+).</summary>
    public IReadOnlyList<ChecksumKeyframe> Checksums { get; init; } = Array.Empty<ChecksumKeyframe>();

    public int ResolveTickHz() =>
        Version >= 3
            ? Math.Clamp(TickHz, RecordingSimulationDefaults.MinTickHz, RecordingSimulationDefaults.MaxTickHz)
            : RecordingSimulationDefaults.DefaultTickHz;

    public bool UsesTickIndexedEvents => Version >= 4;

    /// <summary>
    /// Ticks the replay should run for. Legacy files (pre-v5) fall back to the last event tick,
    /// which truncates trailing gameplay after the final input.
    /// </summary>
    public long ResolveDurationTicks()
    {
        if (DurationTicks > 0)
            return DurationTicks;

        return Events.Count > 0 ? Math.Max(0, Events[^1].Tick) : 0;
    }
}
