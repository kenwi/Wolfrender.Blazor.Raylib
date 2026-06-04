
namespace Game.DebugConsole.Commands;

public sealed class ListLevelsCommand : IConsoleCommand
{
    public string Name => "list-levels";
    public string Description => "Lists available level JSON files under resources/.";
    public string Usage => "list-levels";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 0)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        var levels = LevelCatalog.ListJsonLevels();
        if (levels.Count == 0)
            return ConsoleCommandResult.Fail("No level JSON files found under resources/.");

        var current = context.GetCurrentLevelPath();
        var rows = levels
            .Select(path =>
            {
                var marker = string.Equals(path, current, StringComparison.OrdinalIgnoreCase) ? " *" : "";
                return $"{path}{marker}";
            })
            .ToArray();

        var message = levels.Count == 1
            ? "1 level available (* = loaded):"
            : $"{levels.Count} levels available (* = loaded):";

        return ConsoleCommandResult.Ok(message, rows);
    }
}
