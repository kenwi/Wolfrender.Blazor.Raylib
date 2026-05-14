namespace Game.Console;

public interface IConsoleCommand
{
    string Name { get; }
    string Description { get; }
    string Usage { get; }

    ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args);
}
