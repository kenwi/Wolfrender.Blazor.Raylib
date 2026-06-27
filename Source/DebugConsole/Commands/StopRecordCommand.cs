namespace Game.DebugConsole.Commands;

public sealed class StopRecordCommand : IConsoleCommand
{
    public string Name => "stoprecord";
    public string Description => "Stops the active input recording and saves the .rec file.";
    public string Usage => "stoprecord";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 0)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.StopRecording();
    }
}
