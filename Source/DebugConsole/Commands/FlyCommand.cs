namespace Game.DebugConsole.Commands;

public sealed class FlyCommand : IConsoleCommand
{
    public string Name => "fly";
    public string Description => "Toggles free-flight mode (Shift up, Ctrl down, no collision).";
    public string Usage => "fly [on|off|status]";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        if (args.Count == 0)
            return context.ToggleFlying();

        return args[0].ToLowerInvariant() switch
        {
            "on" => context.SetFlying(true),
            "off" => context.SetFlying(false),
            "status" => context.GetFlyingStatus(),
            _ => ConsoleCommandResult.Fail($"Usage: {Usage}")
        };
    }
}
