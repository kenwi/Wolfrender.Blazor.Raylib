namespace Game.DebugConsole.Commands;

public sealed class FullBrightCommand : IConsoleCommand
{
    public string Name => "fullbright";

    public string Description => "Toggles fullbright rendering (no torch or placed lights, 100% texture brightness).";

    public string Usage => "fullbright [on|off|status]";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        if (args.Count == 0)
            return context.ToggleFullBright();

        return args[0].ToLowerInvariant() switch
        {
            "on" => context.SetFullBright(true),
            "off" => context.SetFullBright(false),
            "status" => context.GetFullBrightStatus(),
            _ => ConsoleCommandResult.Fail($"Usage: {Usage}")
        };
    }
}
