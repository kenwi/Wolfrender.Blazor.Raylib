namespace Game.DebugConsole;

public sealed class ConsoleCommandContext
{
    public required IConsoleVariableAccessor Variables { get; init; }
    public required Func<string, ConsoleCommandResult> LoadLevel { get; init; }
    public required Func<ConsoleCommandResult> RestartCurrentLevel { get; init; }
    public required Func<ConsoleCommandResult> ClearConsoleScrollback { get; init; }
    public required Func<IReadOnlyCollection<IConsoleCommand>> GetAllCommands { get; init; }
    public required Func<string> GetCurrentLevelPath { get; init; }
    public required Func<ConsoleCommandResult> ListPickups { get; init; }
}
