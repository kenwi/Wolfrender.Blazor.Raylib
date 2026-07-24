namespace Game.DebugConsole;

/// <summary>Level load/restart/list and console scrollback clear.</summary>
public sealed class ConsoleLevelActions
{
    public required Func<string, ConsoleCommandResult> LoadLevel { get; init; }
    public required Func<ConsoleCommandResult> RestartCurrentLevel { get; init; }
    public required Func<ConsoleCommandResult> ClearConsoleScrollback { get; init; }
    public required Func<string> GetCurrentLevelPath { get; init; }
    public required Func<ConsoleCommandResult> ListPickups { get; init; }
}

/// <summary>Recording and replay console actions.</summary>
public sealed class ConsoleRecordingActions
{
    public required Func<string, ConsoleCommandResult> StartRecording { get; init; }
    public required Func<ConsoleCommandResult> StopRecording { get; init; }
    public required Func<string, ConsoleCommandResult> StartReplay { get; init; }
    public required Func<string, ConsoleCommandResult> VerifyReplay { get; init; }
    public required Func<ConsoleCommandResult> StopReplay { get; init; }
    public required Func<string, ConsoleCommandResult> SendRecording { get; init; }
    public required Func<int, ConsoleCommandResult> ReplayRemote { get; init; }
}

/// <summary>Tick diagnostics and lighting debug actions.</summary>
public sealed class ConsoleDiagnosticsActions
{
    public required Func<ConsoleCommandResult> ToggleTickDiagnostics { get; init; }
    public required Func<bool, ConsoleCommandResult> SetTickDiagnostics { get; init; }
    public required Func<ConsoleCommandResult> GetTickDiagnosticsStatus { get; init; }
    public required Func<ConsoleCommandResult> DumpLightingCheck { get; init; }
}

/// <summary>Render/mesh and player locomotion debug toggles.</summary>
public sealed class ConsoleRenderToggleActions
{
    public required Func<ConsoleCommandResult> ToggleStaticMeshes { get; init; }
    public required Func<bool, ConsoleCommandResult> SetStaticMeshes { get; init; }
    public required Func<ConsoleCommandResult> GetStaticMeshesStatus { get; init; }
    public required Func<ConsoleCommandResult> ToggleFlying { get; init; }
    public required Func<bool, ConsoleCommandResult> SetFlying { get; init; }
    public required Func<ConsoleCommandResult> GetFlyingStatus { get; init; }
    public required Func<ConsoleCommandResult> ToggleGodMode { get; init; }
    public required Func<bool, ConsoleCommandResult> SetGodMode { get; init; }
    public required Func<ConsoleCommandResult> GetGodModeStatus { get; init; }
    public required Func<ConsoleCommandResult> ToggleFullBright { get; init; }
    public required Func<bool, ConsoleCommandResult> SetFullBright { get; init; }
    public required Func<ConsoleCommandResult> GetFullBrightStatus { get; init; }
}

/// <summary>
/// Console command host surface. Actions are grouped so new capabilities
/// extend one sub-context instead of a flat 26-delegate bag.
/// Flat accessors remain for existing command classes.
/// </summary>
public sealed class ConsoleCommandContext
{
    public required IConsoleVariableAccessor Variables { get; init; }
    public required Func<IReadOnlyCollection<IConsoleCommand>> GetAllCommands { get; init; }
    public required ConsoleLevelActions Level { get; init; }
    public required ConsoleRecordingActions Recording { get; init; }
    public required ConsoleDiagnosticsActions Diagnostics { get; init; }
    public required ConsoleRenderToggleActions RenderToggles { get; init; }

    public Func<string, ConsoleCommandResult> LoadLevel => Level.LoadLevel;
    public Func<ConsoleCommandResult> RestartCurrentLevel => Level.RestartCurrentLevel;
    public Func<ConsoleCommandResult> ClearConsoleScrollback => Level.ClearConsoleScrollback;
    public Func<string> GetCurrentLevelPath => Level.GetCurrentLevelPath;
    public Func<ConsoleCommandResult> ListPickups => Level.ListPickups;

    public Func<string, ConsoleCommandResult> StartRecording => Recording.StartRecording;
    public Func<ConsoleCommandResult> StopRecording => Recording.StopRecording;
    public Func<string, ConsoleCommandResult> StartReplay => Recording.StartReplay;
    public Func<string, ConsoleCommandResult> VerifyReplay => Recording.VerifyReplay;
    public Func<ConsoleCommandResult> StopReplay => Recording.StopReplay;
    public Func<string, ConsoleCommandResult> SendRecording => Recording.SendRecording;
    public Func<int, ConsoleCommandResult> ReplayRemote => Recording.ReplayRemote;

    public Func<ConsoleCommandResult> ToggleTickDiagnostics => Diagnostics.ToggleTickDiagnostics;
    public Func<bool, ConsoleCommandResult> SetTickDiagnostics => Diagnostics.SetTickDiagnostics;
    public Func<ConsoleCommandResult> GetTickDiagnosticsStatus => Diagnostics.GetTickDiagnosticsStatus;
    public Func<ConsoleCommandResult> DumpLightingCheck => Diagnostics.DumpLightingCheck;

    public Func<ConsoleCommandResult> ToggleStaticMeshes => RenderToggles.ToggleStaticMeshes;
    public Func<bool, ConsoleCommandResult> SetStaticMeshes => RenderToggles.SetStaticMeshes;
    public Func<ConsoleCommandResult> GetStaticMeshesStatus => RenderToggles.GetStaticMeshesStatus;
    public Func<ConsoleCommandResult> ToggleFlying => RenderToggles.ToggleFlying;
    public Func<bool, ConsoleCommandResult> SetFlying => RenderToggles.SetFlying;
    public Func<ConsoleCommandResult> GetFlyingStatus => RenderToggles.GetFlyingStatus;
    public Func<ConsoleCommandResult> ToggleGodMode => RenderToggles.ToggleGodMode;
    public Func<bool, ConsoleCommandResult> SetGodMode => RenderToggles.SetGodMode;
    public Func<ConsoleCommandResult> GetGodModeStatus => RenderToggles.GetGodModeStatus;
    public Func<ConsoleCommandResult> ToggleFullBright => RenderToggles.ToggleFullBright;
    public Func<bool, ConsoleCommandResult> SetFullBright => RenderToggles.SetFullBright;
    public Func<ConsoleCommandResult> GetFullBrightStatus => RenderToggles.GetFullBrightStatus;
}
