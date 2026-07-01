namespace Game.DebugConsole;

public sealed class DebugConsoleOutput : IConsoleOutput
{
    public void WriteLine(string line)
    {
        Debug.Log($"[Console] {line}");
    }
}
