namespace Game.Console.Commands;

public sealed class ListVariablesCommand : IConsoleCommand
{
    public string Name => "list-variables";
    public string Description => "Lists available console variables.";
    public string Usage => "list-variables [--all]";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        bool includeAll = args.Any(a => string.Equals(a, "--all", StringComparison.OrdinalIgnoreCase));
        var variables = context.Variables.ListVariables(includeAll);
        return ConsoleCommandResult.Ok($"Variables ({variables.Count}):", variables);
    }
}
