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
        var wire = RecFileWire.From(file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(wire, JsonOptions));
    }

    public static RecFile Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Recording not found: '{path}'.");

        return Parse(File.ReadAllText(path));
    }

    public static RecFile Parse(string json)
    {
        var wire = JsonSerializer.Deserialize<RecFileWire>(json, JsonOptions)
            ?? throw new InvalidDataException("Recording file is empty or invalid.");

        return wire.ToRecFile();
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
}
