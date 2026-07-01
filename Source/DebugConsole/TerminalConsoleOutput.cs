namespace Game.DebugConsole;

/// <summary>Writes console output to stderr on native builds (Raylib often blocks stdout).</summary>
public sealed class TerminalConsoleOutput : IConsoleOutput
{
    public void WriteLine(string line) => NativeConsole.WriteLine(line);
}
