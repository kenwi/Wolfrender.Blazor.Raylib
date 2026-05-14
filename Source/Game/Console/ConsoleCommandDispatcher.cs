namespace Game.Console;

public sealed class ConsoleCommandDispatcher
{
    private readonly Dictionary<string, IConsoleCommand> _commands;

    public ConsoleCommandDispatcher(IEnumerable<IConsoleCommand> commands)
    {
        _commands = commands.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<IConsoleCommand> Commands => _commands.Values;

    public ConsoleCommandResult Execute(ConsoleCommandContext context, ConsoleCommandInvocation invocation)
    {
        if (!_commands.TryGetValue(invocation.Name, out var command))
            return ConsoleCommandResult.Fail($"Unknown command: '{invocation.Name}'. Type 'help' for a list.");

        return command.Execute(context, invocation.Arguments);
    }
}
