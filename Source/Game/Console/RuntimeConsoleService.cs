using Game.Console.Commands;

namespace Game.Console;

public sealed class RuntimeConsoleService
{
    private readonly ConsoleCommandParser _parser = new();
    private readonly ConsoleCommandDispatcher _dispatcher;
    private readonly ConsoleCommandContext _context;
    private readonly IConsoleOutput _output;

    public RuntimeConsoleService(
        IConsoleVariableAccessor variables,
        IConsoleOutput output,
        Func<string, ConsoleCommandResult> loadLevel,
        Func<ConsoleCommandResult> restartCurrentLevel,
        Func<ConsoleCommandResult> clearConsoleScrollback)
    {
        _output = output;

        var commands = new IConsoleCommand[]
        {
            new HelpCommand(),
            new ListVariablesCommand(),
            new GetCommand(),
            new SetCommand(),
            new LoadCommand(),
            new RestartLevelCommand(),
            new ClearCommand()
        };

        _dispatcher = new ConsoleCommandDispatcher(commands);
        _context = new ConsoleCommandContext
        {
            Variables = variables,
            LoadLevel = loadLevel,
            RestartCurrentLevel = restartCurrentLevel,
            ClearConsoleScrollback = clearConsoleScrollback,
            GetAllCommands = () => _dispatcher.Commands
        };
    }

    public ConsoleCommandResult Execute(string line)
    {
        _output.WriteLine($"> {line}");

        var parseResult = _parser.TryParse(line, out var invocation);
        if (!parseResult.Success || invocation == null)
        {
            _output.WriteLine(parseResult.Message);
            return parseResult;
        }

        var result = _dispatcher.Execute(_context, invocation);
        _output.WriteLine(result.Message);
        foreach (var row in result.Rows)
            _output.WriteLine(row);
        return result;
    }

    public IReadOnlyList<string> GetCompletions(string line, int cursor)
    {
        cursor = Math.Clamp(cursor, 0, line.Length);
        var tokenRange = FindTokenRange(line, cursor);
        string token = line[tokenRange.Start..tokenRange.End];

        var tokensBefore = TokenizeSimple(line[..tokenRange.Start]);
        int tokenIndex = tokensBefore.Count;

        IEnumerable<string> candidates;
        if (tokenIndex == 0)
        {
            candidates = _dispatcher.Commands.Select(c => c.Name);
        }
        else
        {
            var command = tokensBefore[0];
            candidates = GetArgumentCompletions(command, tokenIndex);
        }

        return candidates
            .Where(c => c.StartsWith(token, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IEnumerable<string> GetArgumentCompletions(string command, int tokenIndex)
    {
        if (tokenIndex == 1)
        {
            if (string.Equals(command, "get", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "set", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "list-variables", StringComparison.OrdinalIgnoreCase))
            {
                return _context.Variables.ListVariables(includeAll: true);
            }

            if (string.Equals(command, "help", StringComparison.OrdinalIgnoreCase))
                return _dispatcher.Commands.Select(c => c.Name);
        }

        return Array.Empty<string>();
    }

    private static (int Start, int End) FindTokenRange(string line, int cursor)
    {
        int start = cursor;
        while (start > 0 && !char.IsWhiteSpace(line[start - 1]))
            start--;

        int end = cursor;
        while (end < line.Length && !char.IsWhiteSpace(line[end]))
            end++;

        return (start, end);
    }

    private static List<string> TokenizeSimple(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
