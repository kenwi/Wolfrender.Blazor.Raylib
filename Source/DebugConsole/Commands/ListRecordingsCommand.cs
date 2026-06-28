using Game.Features.Recording;

namespace Game.DebugConsole.Commands;

public sealed class ListRecordingsCommand : IConsoleCommand
{
    public string Name => "list-recordings";
    public string Description => "Lists available recordings in the recordings directory.";
    public string Usage => "list-recordings";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 0)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        var recordings = RecordingSystem.ListRecordings();
        if (recordings.Count == 0)
            return ConsoleCommandResult.Fail("No recordings found.");

        return ConsoleCommandResult.Ok("Recordings:", recordings);
    }
}