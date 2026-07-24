namespace Game.DebugConsole.Commands;

public sealed class GodModeCommand : IConsoleCommand
{
    public string Name => "godmode";
    public string Description =>
        "Toggles god mode: damage flash still plays, health is unchanged, enemies keep attacking.";
    public string Usage => "godmode [on|off|status]";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        if (args.Count == 0)
            return context.ToggleGodMode();

        return args[0].ToLowerInvariant() switch
        {
            "on" => context.SetGodMode(true),
            "off" => context.SetGodMode(false),
            "status" => context.GetGodModeStatus(),
            _ => ConsoleCommandResult.Fail($"Usage: {Usage}")
        };
    }
}
