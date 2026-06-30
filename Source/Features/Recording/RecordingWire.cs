namespace Game.Features.Recording;

public sealed class RecordingUploadWireRequest
{
    public required string Name { get; init; }
    public required RecFileWire Recording { get; init; }
}

public sealed class RecFileWire
{
    public int Version { get; init; }
    public string LevelPath { get; init; } = string.Empty;
    public float MouseSensitivity { get; init; }
    public int TickHz { get; init; }
    public PlayerSnapshot? Player { get; init; }
    public List<RecEventWire> Events { get; init; } = new();

    public static RecFileWire From(RecFile file) => new()
    {
        Version = file.Version,
        LevelPath = file.LevelPath,
        MouseSensitivity = file.MouseSensitivity,
        TickHz = file.ResolveTickHz(),
        Player = file.PlayerSnapshot,
        Events = file.Events.Select(RecEventWire.From).ToList()
    };

    public RecFile ToRecFile()
    {
        if (Version < 1 || Version > RecFile.CurrentVersion)
            throw new InvalidDataException($"Unsupported recording version {Version} (expected 1-{RecFile.CurrentVersion}).");

        if (string.IsNullOrWhiteSpace(LevelPath))
            throw new InvalidDataException("Recording is missing levelPath.");

        int tickHz = Version >= 3
            ? Math.Clamp(
                TickHz > 0 ? TickHz : RecordingSimulationDefaults.DefaultTickHz,
                RecordingSimulationDefaults.MinTickHz,
                RecordingSimulationDefaults.MaxTickHz)
            : RecordingSimulationDefaults.DefaultTickHz;

        var events = Events.Select(e => e.ToEvent()).ToList();
        if (Version >= 4)
            events = events.OrderBy(e => e.Tick).ThenBy(e => e.Time).ToList();
        else
            events = events.OrderBy(e => e.Time).ToList();
        return new RecFile
        {
            Version = Version,
            LevelPath = LevelPath,
            MouseSensitivity = MouseSensitivity,
            TickHz = tickHz,
            PlayerSnapshot = Player,
            Events = events
        };
    }
}

public sealed class RecEventWire
{
    public InputEventKind Kind { get; init; }
    public long Tick { get; init; } = -1;
    public float Time { get; init; }
    public GameplayKey? Key { get; init; }
    public float? Dx { get; init; }
    public float? Dy { get; init; }

    public static RecEventWire From(InputEvent evt) => evt switch
    {
        KeyDownEvent down => new RecEventWire { Kind = InputEventKind.KeyDown, Tick = down.Tick, Time = down.Time, Key = down.Key },
        KeyUpEvent up => new RecEventWire { Kind = InputEventKind.KeyUp, Tick = up.Tick, Time = up.Time, Key = up.Key },
        MouseDeltaEvent delta => new RecEventWire
        {
            Kind = InputEventKind.MouseDelta,
            Tick = delta.Tick,
            Time = delta.Time,
            Dx = delta.Dx,
            Dy = delta.Dy
        },
        _ => throw new InvalidOperationException("Unknown input event type.")
    };

    public InputEvent ToEvent() => Kind switch
    {
        InputEventKind.KeyDown when Key.HasValue => new KeyDownEvent(Time, Key.Value) { Tick = Tick },
        InputEventKind.KeyUp when Key.HasValue => new KeyUpEvent(Time, Key.Value) { Tick = Tick },
        InputEventKind.MouseDelta when Dx.HasValue && Dy.HasValue => new MouseDeltaEvent(Time, Dx.Value, Dy.Value) { Tick = Tick },
        _ => throw new InvalidDataException($"Invalid event payload for kind '{Kind}'.")
    };
}
