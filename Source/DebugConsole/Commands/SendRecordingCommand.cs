namespace Game.DebugConsole.Commands;

public sealed class SendRecordingCommand : IConsoleCommand
{
    public string Name => "sendrecording";
    public string Description => "Uploads a local recordings/<filename>.rec to the server.";
    public string Usage => "sendrecording <filename>";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.SendRecording(args[0]);
    }
}
