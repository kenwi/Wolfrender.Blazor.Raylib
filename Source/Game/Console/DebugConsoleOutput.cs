using Game.Utilities;

namespace Game.Console;

public sealed class DebugConsoleOutput : IConsoleOutput
{
    public void WriteLine(string line)
    {
        Debug.Log($"[Console] {line}");
    }
}
