namespace Game.Console.Commands;

public sealed class GetCommand : IConsoleCommand
{
    public string Name => "get";
    public string Description => "Prints a variable value.";
    public string Usage => "get <variable>";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        var path = args[0];
        if (!context.Variables.TryGetValue(path, out var value, out var error))
            return ConsoleCommandResult.Fail(error);

        return ConsoleCommandResult.Ok($"{path} = {value}");
    }
}
