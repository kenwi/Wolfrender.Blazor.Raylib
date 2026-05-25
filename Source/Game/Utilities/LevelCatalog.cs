namespace Game.Utilities;

/// <summary>
/// Resolves and discovers level JSON files under <see cref="ResourcesDirectory"/>.
/// On WASM, only files present in the Emscripten VFS (preloaded at startup) can be loaded.
/// </summary>
public static class LevelCatalog
{
    public const string ResourcesDirectory = "resources";
    public const string DefaultLevelPath = $"{ResourcesDirectory}/level.json";

    /// <summary>Levels shipped for browser play when directory listing is unavailable.</summary>
    private static readonly string[] BrowserFallbackLevels =
    {
        DefaultLevelPath,
        $"{ResourcesDirectory}/test.json",
    };

    public static string NormalizePath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var path = input.Trim().Replace('\\', '/');
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            path += ".json";

        if (!path.Contains('/'))
            path = $"{ResourcesDirectory}/{path}";

        return path;
    }

    public static bool TryResolve(string input, out string resolvedPath, out string error)
    {
        resolvedPath = NormalizePath(input);
        if (string.IsNullOrEmpty(resolvedPath))
        {
            error = "Level path is empty.";
            return false;
        }

        if (File.Exists(resolvedPath))
        {
            error = string.Empty;
            return true;
        }

        error = $"Level file not found: '{resolvedPath}'. Type 'list-levels' to see available levels.";
        return false;
    }

    public static IReadOnlyList<string> ListJsonLevels()
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(ResourcesDirectory))
        {
            foreach (var file in Directory.GetFiles(ResourcesDirectory, "*.json"))
                found.Add(file.Replace('\\', '/'));
        }

        foreach (var path in BrowserFallbackLevels)
        {
            if (File.Exists(path))
                found.Add(path);
        }

        return found.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
