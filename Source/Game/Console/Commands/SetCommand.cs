namespace Game.Console.Commands;

public sealed class SetCommand : IConsoleCommand
{
    public string Name => "set";
    public string Description => "Sets a variable value.";
    public string Usage => "set <variable> <value>";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count < 2)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        var path = args[0];
        var valueText = string.Join(" ", args.Skip(1));

        if (!context.Variables.TrySetValue(path, valueText, out var error))
            return ConsoleCommandResult.Fail(error);

        if (!context.Variables.TryGetValue(path, out var readback, out _))
            readback = valueText;

        return ConsoleCommandResult.Ok($"{path} = {readback}");
    }
}
