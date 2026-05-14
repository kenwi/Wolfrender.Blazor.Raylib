namespace Game.Console;

public sealed class ConsoleCommandContext
{
    public required IConsoleVariableAccessor Variables { get; init; }
    public required Func<string, ConsoleCommandResult> LoadLevel { get; init; }
    public required Func<ConsoleCommandResult> RestartCurrentLevel { get; init; }
    public required Func<IReadOnlyCollection<IConsoleCommand>> GetAllCommands { get; init; }
}
