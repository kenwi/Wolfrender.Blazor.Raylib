namespace Game.DebugConsole.Commands;

public sealed class ReplayCommand : IConsoleCommand
{
    public string Name => "replay";
    public string Description => "Replays a recorded session from recordings/<filename>.rec.";
    public string Usage => "replay <filename>";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.StartReplay(args[0]);
    }
}
