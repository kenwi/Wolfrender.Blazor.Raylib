namespace Game.DebugConsole.Commands;

public sealed class TickDiagnosticsCommand : IConsoleCommand
{
    public string Name => "tickdiag";
    public string Description => "Toggles or queries fixed-tick diagnostics overlay (render fps, sim hz, tick index).";
    public string Usage => "tickdiag [on|off|status]";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        if (args.Count == 0)
            return context.ToggleTickDiagnostics();

        return args[0].ToLowerInvariant() switch
        {
            "on" => context.SetTickDiagnostics(true),
            "off" => context.SetTickDiagnostics(false),
            "status" => context.GetTickDiagnosticsStatus(),
            _ => ConsoleCommandResult.Fail($"Usage: {Usage}")
        };
    }
}
