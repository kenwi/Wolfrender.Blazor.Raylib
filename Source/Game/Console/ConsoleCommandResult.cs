namespace Game.Console;

public sealed class ConsoleCommandResult
{
    public bool Success { get; }
    public string Message { get; }
    public IReadOnlyList<string> Rows { get; }

    private ConsoleCommandResult(bool success, string message, IReadOnlyList<string>? rows = null)
    {
        Success = success;
        Message = message;
        Rows = rows ?? Array.Empty<string>();
    }

    public static ConsoleCommandResult Ok(string message, IReadOnlyList<string>? rows = null) =>
        new(true, message, rows);

    public static ConsoleCommandResult Fail(string message) =>
        new(false, message);
}
