namespace Game.DebugConsole.Commands;

public sealed class StaticMeshCommand : IConsoleCommand
{
    public string Name => "staticmesh";
    public string Description => "Toggles baked static wall meshes vs legacy immediate-mode quads.";
    public string Usage => "staticmesh [on|off|status]";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 1)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        if (args.Count == 0)
            return context.ToggleStaticMeshes();

        return args[0].ToLowerInvariant() switch
        {
            "on" => context.SetStaticMeshes(true),
            "off" => context.SetStaticMeshes(false),
            "status" => context.GetStaticMeshesStatus(),
            _ => ConsoleCommandResult.Fail($"Usage: {Usage}")
        };
    }
}
