namespace Game.Console.Commands;

public sealed class ClearCommand : IConsoleCommand
{
    public string Name => "clear";
    public string Description => "Clears the printed console log. Command history (↑/↓) is unchanged.";
    public string Usage => "clear";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 0)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        return context.ClearConsoleScrollback();
    }
}
