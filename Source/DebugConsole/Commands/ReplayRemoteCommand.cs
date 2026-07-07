namespace Game.DebugConsole.Commands;

public sealed class ReplayRemoteCommand : IConsoleCommand
{
    public string Name => "replayremote";
    public string Description => "Downloads and replays a highscore recording from the server.";
    public string Usage => "replayremote <highscore position>";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 1 || !int.TryParse(args[0], out var rank))
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.ReplayRemote(rank);
    }
}
