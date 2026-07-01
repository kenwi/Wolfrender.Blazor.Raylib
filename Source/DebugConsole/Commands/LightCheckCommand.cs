namespace Game.DebugConsole.Commands;

public sealed class LightCheckCommand : IConsoleCommand
{
    public string Name => "lightcheck";

    public string Description => "Dumps placed-light selection, room maps, shader uniforms, and per-light contribution checks.";

    public string Usage => "lightcheck";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 0)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.DumpLightingCheck();
    }
}
