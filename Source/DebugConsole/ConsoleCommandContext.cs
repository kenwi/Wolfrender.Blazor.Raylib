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
    public required Func<string, ConsoleCommandResult> StartRecording { get; init; }
    public required Func<ConsoleCommandResult> StopRecording { get; init; }
    public required Func<string, ConsoleCommandResult> StartReplay { get; init; }
    public required Func<ConsoleCommandResult> StopReplay { get; init; }
    public required Func<string, ConsoleCommandResult> SendRecording { get; init; }
    public required Func<ConsoleCommandResult> ToggleTickDiagnostics { get; init; }
    public required Func<bool, ConsoleCommandResult> SetTickDiagnostics { get; init; }
    public required Func<ConsoleCommandResult> GetTickDiagnosticsStatus { get; init; }
    public required Func<ConsoleCommandResult> ToggleStaticMeshes { get; init; }
    public required Func<bool, ConsoleCommandResult> SetStaticMeshes { get; init; }
    public required Func<ConsoleCommandResult> GetStaticMeshesStatus { get; init; }
}
