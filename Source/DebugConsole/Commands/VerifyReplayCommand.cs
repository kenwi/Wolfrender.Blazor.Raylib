namespace Game.DebugConsole.Commands;

public sealed class VerifyReplayCommand : IConsoleCommand
{
    public string Name => "verifyreplay";
    public string Description => "Replays a recording and verifies simulation checksums against it (reports first divergent tick).";
    public string Usage => "verifyreplay <filename>";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.VerifyReplay(args[0]);
    }
}
