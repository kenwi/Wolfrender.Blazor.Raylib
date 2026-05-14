namespace Game.Console;

public sealed class OverlayConsoleOutput : IConsoleOutput
{
    private readonly ConsoleOverlay _overlay;

    public OverlayConsoleOutput(ConsoleOverlay overlay)
    {
        _overlay = overlay;
    }

    public void WriteLine(string line)
    {
        _overlay.AddLine(line);
    }
}
