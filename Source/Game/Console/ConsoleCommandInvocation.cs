namespace Game.Console;

public sealed class ConsoleCommandInvocation
{
    public string Name { get; }
    public IReadOnlyList<string> Arguments { get; }

    public ConsoleCommandInvocation(string name, IReadOnlyList<string> arguments)
    {
        Name = name;
        Arguments = arguments;
    }
}
