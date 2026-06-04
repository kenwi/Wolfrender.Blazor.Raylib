namespace Game.DebugConsole.Commands;

public sealed class LoadCommand : IConsoleCommand
{
    public string Name => "load";
    public string Description => "Loads a level JSON from resources/ (name or path).";
    public string Usage => "load <level>";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.LoadLevel(args[0]);
    }
}
