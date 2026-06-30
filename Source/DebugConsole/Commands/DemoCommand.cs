namespace Game.DebugConsole.Commands;

public sealed class DemoCommand : IConsoleCommand
{
    private readonly ReplayCommand _replay = new();

    public string Name => "demo";
    public string Description => "Alias for replay - plays back a recording from recordings/<filename>.rec.";
    public string Usage => "demo <filename>";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args) =>
        _replay.Execute(context, args);
}
