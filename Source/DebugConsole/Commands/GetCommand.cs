namespace Game.DebugConsole.Commands;

public sealed class GetCommand : IConsoleCommand
{
    public string Name => "get";
    public string Description => "Prints a variable value, or lists level pickups.";
    public string Usage => "get <variable> | get pickups";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        if (string.Equals(args[0], "pickups", StringComparison.OrdinalIgnoreCase))
            return context.ListPickups();

        var path = args[0];
        if (!context.Variables.TryGetValue(path, out var value, out var error))
            return ConsoleCommandResult.Fail(error);

        return ConsoleCommandResult.Ok($"{path} = {value}");
    }
}
