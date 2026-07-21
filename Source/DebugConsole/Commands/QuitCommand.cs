using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.DebugConsole.Commands;

public sealed class QuitCommand : IConsoleCommand
{
    public string Name => "quit";
    public string Description => "Cleanly shuts down the application.";
    public string Usage => "quit";

    public ConsoleCommandResult Execute(ConsoleCommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 0)
            return ConsoleCommandResult.Fail($"Usage: {Usage}");

        // Ends the Application.Run loop so Cleanup restores display and closes audio/window.
        CloseWindow();
        return ConsoleCommandResult.Ok("Quitting...");
    }
}
