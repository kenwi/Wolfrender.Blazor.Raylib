namespace Game.DebugConsole.Commands;

public sealed class ExitCommand : IConsoleCommand
{
    private readonly QuitCommand _quit = new();

    public string Name => "exit";
    public string Description => "Alias for quit - cleanly shuts down the application.";
    public string Usage => "exit";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args) =>
        _quit.Execute(context, args);
}
