using System.Runtime.InteropServices;

namespace Game.DebugConsole;

/// <summary>Ensures native builds can emit copy-paste friendly logs outside the in-game overlay.</summary>
internal static class NativeConsole
{
    public static void EnsureAttached()
    {
        if (OperatingSystem.IsBrowser())
            return;

        if (OperatingSystem.IsWindows() && GetConsoleWindow() == IntPtr.Zero)
            AllocConsole();

        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Error.WriteLine("[Wolfrender] Native console logging enabled (stderr).");
            Console.Error.Flush();
        }
        catch
        {
            // Headless or host without a console - file fallbacks still apply.
        }
    }

    public static void WriteLine(string line)
    {
        if (OperatingSystem.IsBrowser())
            return;

        try
        {
            Console.Error.WriteLine(line);
            Console.Error.Flush();
        }
        catch
        {
            // Ignore hosts without stderr.
        }
    }

    public static void WriteBlock(IReadOnlyList<string> lines)
    {
        if (OperatingSystem.IsBrowser() || lines.Count == 0)
            return;

        try
        {
            Console.Error.WriteLine();
            foreach (string line in lines)
                Console.Error.WriteLine(line);
            Console.Error.WriteLine();
            Console.Error.Flush();
        }
        catch
        {
            // Ignore hosts without stderr.
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
}
