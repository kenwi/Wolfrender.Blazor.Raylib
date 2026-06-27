namespace Game.DebugConsole.Commands;

public sealed class StopReplayCommand : IConsoleCommand
{
    public string Name => "stopreplay";
    public string Description => "Stops an active input replay and returns to live controls.";
    public string Usage => "stopreplay";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 0)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.StopReplay();
    }
}
