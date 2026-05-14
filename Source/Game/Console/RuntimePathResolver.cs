using System.Text.RegularExpressions;

namespace Game.Console;

public sealed class RuntimePathResolver
{
    private static readonly Regex RootPattern = new(
        @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)(\[(?<index>\d+)\])?$",
        RegexOptions.Compiled);

    public bool TryParse(string path, out ResolvedPath resolved, out string error)
    {
        resolved = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Path is empty.";
            return false;
        }

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2)
        {
            error = "Path must include a root and a member (example: Player.MoveSpeed).";
            return false;
        }

        var rootMatch = RootPattern.Match(segments[0]);
        if (!rootMatch.Success)
        {
            error = $"Invalid root token: '{segments[0]}'.";
            return false;
        }

        int? index = null;
        if (rootMatch.Groups["index"].Success)
            index = int.Parse(rootMatch.Groups["index"].Value);

        resolved = new ResolvedPath(
            rootMatch.Groups["name"].Value,
            index,
            segments.Skip(1).ToArray(),
            Normalize(path));

        return true;
    }

    public string Normalize(string path)
    {
        return Regex.Replace(path, @"\[\d+\]", "[index]");
    }
}

public readonly record struct ResolvedPath(
    string RootName,
    int? Index,
    IReadOnlyList<string> Members,
    string NormalizedPath);
