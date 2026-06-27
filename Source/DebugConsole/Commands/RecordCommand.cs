namespace Game.DebugConsole.Commands;

public sealed class RecordCommand : IConsoleCommand
{
    public string Name => "record";
    public string Description => "Starts recording gameplay input to demos/<filename>.rec.";
    public string Usage => "record <filename>";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.StartRecording(args[0]);
    }
}
