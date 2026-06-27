using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Features.Recording;

public static class RecFileSerializer
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void Write(string path, RecFile file)
    {
        var dto = RecFileDto.From(file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
    }

    public static RecFile Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Recording not found: '{path}'.");

        return Parse(File.ReadAllText(path));
    }

    public static RecFile Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<RecFileDto>(json, JsonOptions)
            ?? throw new InvalidDataException("Recording file is empty or invalid.");

        return dto.ToRecFile();
    }

    public static bool TryParse(string json, out RecFile file, out string error)
    {
        file = null!;
        error = string.Empty;

        try
        {
            file = Parse(json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private sealed class RecFileDto
    {
        public int Version { get; init; }
        public string LevelPath { get; init; } = string.Empty;
        public float MouseSensitivity { get; init; }
        public PlayerSnapshot? Player { get; init; }
        public List<RecEventDto> Events { get; init; } = new();

        public static RecFileDto From(RecFile file) => new()
        {
            Version = file.Version,
            LevelPath = file.LevelPath,
            MouseSensitivity = file.MouseSensitivity,
            Player = file.PlayerSnapshot,
            Events = file.Events.Select(RecEventDto.From).ToList()
        };

        public RecFile ToRecFile()
        {
            if (Version is not 1 and not RecFile.CurrentVersion)
                throw new InvalidDataException($"Unsupported recording version {Version} (expected {RecFile.CurrentVersion}).");

            if (string.IsNullOrWhiteSpace(LevelPath))
                throw new InvalidDataException("Recording is missing levelPath.");

            var events = Events.Select(e => e.ToEvent()).OrderBy(e => e.Time).ToList();
            return new RecFile
            {
                Version = Version,
                LevelPath = LevelPath,
                MouseSensitivity = MouseSensitivity,
                PlayerSnapshot = Player,
                Events = events
            };
        }
    }

    private sealed class RecEventDto
    {
        public InputEventKind Kind { get; init; }
        public float Time { get; init; }
        public GameplayKey? Key { get; init; }
        public float? Dx { get; init; }
        public float? Dy { get; init; }

        public static RecEventDto From(InputEvent evt) => evt switch
        {
            KeyDownEvent down => new RecEventDto { Kind = InputEventKind.KeyDown, Time = down.Time, Key = down.Key },
            KeyUpEvent up => new RecEventDto { Kind = InputEventKind.KeyUp, Time = up.Time, Key = up.Key },
            MouseDeltaEvent delta => new RecEventDto
            {
                Kind = InputEventKind.MouseDelta,
                Time = delta.Time,
                Dx = delta.Dx,
                Dy = delta.Dy
            },
            _ => throw new InvalidOperationException("Unknown input event type.")
        };

        public InputEvent ToEvent() => Kind switch
        {
            InputEventKind.KeyDown when Key.HasValue => new KeyDownEvent(Time, Key.Value),
            InputEventKind.KeyUp when Key.HasValue => new KeyUpEvent(Time, Key.Value),
            InputEventKind.MouseDelta when Dx.HasValue && Dy.HasValue => new MouseDeltaEvent(Time, Dx.Value, Dy.Value),
            _ => throw new InvalidDataException($"Invalid event payload for kind '{Kind}'.")
        };
    }
}
