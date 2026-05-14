namespace Game.Console.Commands;

public sealed class RestartLevelCommand : IConsoleCommand
{
    public string Name => "restart";
    public string Description => "Reloads the current level from disk and resets the player.";
    public string Usage => "restart";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 0)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.RestartCurrentLevel();
    }
}
