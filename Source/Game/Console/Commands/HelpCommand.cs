namespace Game.Console.Commands;

public sealed class HelpCommand : IConsoleCommand
{
    public string Name => "help";
    public string Description => "Lists available commands or detailed usage for one command.";
    public string Usage => "help [command]";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        var commands = context.GetAllCommands()
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (args.Count == 0)
        {
            var rows = commands.Select(c => $"{c.Name} - {c.Description}").ToArray();
            return ConsoleCommandResult.Ok("Available commands:", rows);
        }

        var commandName = args[0];
        var command = commands.FirstOrDefault(c => string.Equals(c.Name, commandName, StringComparison.OrdinalIgnoreCase));
        if (command == null)
            return ConsoleCommandResult.Fail($"Unknown command: '{commandName}'.");

        return ConsoleCommandResult.Ok($"{command.Name}: {command.Description}", new[] { $"Usage: {command.Usage}" });
    }
}
