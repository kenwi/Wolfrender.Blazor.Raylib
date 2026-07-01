namespace Game.DebugConsole;

/// <summary>Persists <c>lightcheck</c> output for terminal copy/paste and file fallback.</summary>
public static class LightingReportWriter
{
    public const string FileName = "lightcheck.log";

    public static string Publish(string summary, IReadOnlyList<string> rows)
    {
        var lines = BuildLines(summary, rows);
        string path = TryWriteToFile(lines);
        NativeConsole.WriteBlock(lines);
        return path;
    }

    private static List<string> BuildLines(string summary, IReadOnlyList<string> rows)
    {
        var lines = new List<string>(rows.Count + 3)
        {
            "----- lightcheck begin -----",
            summary
        };
        lines.AddRange(rows);
        lines.Add("----- lightcheck end -----");
        return lines;
    }

    private static string TryWriteToFile(IReadOnlyList<string> lines)
    {
        try
        {
            string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, FileName));
            File.WriteAllLines(path, lines);
            return path;
        }
        catch
        {
            try
            {
                string fallback = Path.Combine(Path.GetTempPath(), FileName);
                File.WriteAllLines(fallback, lines);
                return Path.GetFullPath(fallback);
            }
            catch
            {
                return "(file write unavailable in this host)";
            }
        }
    }
}
