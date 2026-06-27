using System.Text.RegularExpressions;

namespace Game.Features.Recording;

public static partial class RecordingNameSanitizer
{
    private const int MaxNameLength = 64;

    [GeneratedRegex(@"[^a-zA-Z0-9._-]")]
    private static partial Regex InvalidCharacters();

    public static bool TrySanitize(string? rawName, out string sanitized, out string error)
    {
        sanitized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawName))
        {
            error = "Recording name is required.";
            return false;
        }

        rawName = rawName.Trim();
        string fileName = Path.GetFileNameWithoutExtension(rawName);
        sanitized = InvalidCharacters().Replace(fileName, string.Empty).Trim('.');

        if (string.IsNullOrEmpty(sanitized))
        {
            error = "Recording name contains no valid characters.";
            return false;
        }

        if (sanitized.Length > MaxNameLength)
            sanitized = sanitized[..MaxNameLength];

        return true;
    }
}
