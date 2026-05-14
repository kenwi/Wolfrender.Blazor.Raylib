namespace Game.Console;

public sealed class ConsoleOutputMultiplexer : IConsoleOutput
{
    private readonly List<IConsoleOutput> _outputs;

    public ConsoleOutputMultiplexer(IEnumerable<IConsoleOutput> outputs)
    {
        _outputs = outputs.ToList();
    }

    public void WriteLine(string line)
    {
        foreach (var output in _outputs)
            output.WriteLine(line);
    }
}
