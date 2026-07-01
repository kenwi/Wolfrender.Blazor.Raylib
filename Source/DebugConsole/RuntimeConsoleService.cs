using Game.DebugConsole.Commands;

namespace Game.DebugConsole;

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
        Func<ConsoleCommandResult> clearConsoleScrollback,
        Func<string> getCurrentLevelPath,
        Func<ConsoleCommandResult> listPickups,
        Func<string, ConsoleCommandResult> startRecording,
        Func<ConsoleCommandResult> stopRecording,
        Func<string, ConsoleCommandResult> startReplay,
        Func<ConsoleCommandResult> stopReplay,
        Func<string, ConsoleCommandResult> sendRecording,
        Func<ConsoleCommandResult> toggleTickDiagnostics,
        Func<bool, ConsoleCommandResult> setTickDiagnostics,
        Func<ConsoleCommandResult> getTickDiagnosticsStatus,
        Func<ConsoleCommandResult> toggleStaticMeshes,
        Func<bool, ConsoleCommandResult> setStaticMeshes,
        Func<ConsoleCommandResult> getStaticMeshesStatus,
        Func<ConsoleCommandResult> toggleFlying,
        Func<bool, ConsoleCommandResult> setFlying,
        Func<ConsoleCommandResult> getFlyingStatus,
        Func<ConsoleCommandResult> dumpLightingCheck,
        Func<ConsoleCommandResult> toggleFullBright,
        Func<bool, ConsoleCommandResult> setFullBright,
        Func<ConsoleCommandResult> getFullBrightStatus)
    {
        _output = output;

        var commands = new IConsoleCommand[]
        {
            new HelpCommand(),
            new ListVariablesCommand(),
            new GetCommand(),
            new SetCommand(),
            new LoadCommand(),
            new ListLevelsCommand(),
            new RestartLevelCommand(),
            new ClearCommand(),
            new RecordCommand(),
            new StopRecordCommand(),
            new ReplayCommand(),
            new DemoCommand(),
            new StopReplayCommand(),
            new SendRecordingCommand(),
            new ListRecordingsCommand(),
            new TickDiagnosticsCommand(),
            new StaticMeshCommand(),
            new FlyCommand(),
            new LightCheckCommand(),
            new FullBrightCommand()
        };

        _dispatcher = new ConsoleCommandDispatcher(commands);
        _context = new ConsoleCommandContext
        {
            Variables = variables,
            LoadLevel = loadLevel,
            RestartCurrentLevel = restartCurrentLevel,
            ClearConsoleScrollback = clearConsoleScrollback,
            GetAllCommands = () => _dispatcher.Commands,
            GetCurrentLevelPath = getCurrentLevelPath,
            ListPickups = listPickups,
            StartRecording = startRecording,
            StopRecording = stopRecording,
            StartReplay = startReplay,
            StopReplay = stopReplay,
            SendRecording = sendRecording,
            ToggleTickDiagnostics = toggleTickDiagnostics,
            SetTickDiagnostics = setTickDiagnostics,
            GetTickDiagnosticsStatus = getTickDiagnosticsStatus,
            ToggleStaticMeshes = toggleStaticMeshes,
            SetStaticMeshes = setStaticMeshes,
            GetStaticMeshesStatus = getStaticMeshesStatus,
            ToggleFlying = toggleFlying,
            SetFlying = setFlying,
            GetFlyingStatus = getFlyingStatus,
            DumpLightingCheck = dumpLightingCheck,
            ToggleFullBright = toggleFullBright,
            SetFullBright = setFullBright,
            GetFullBrightStatus = getFullBrightStatus
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

    public void WriteFeedback(ConsoleCommandResult result)
    {
        _output.WriteLine(result.Message);
        foreach (var row in result.Rows)
            _output.WriteLine(row);
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
            if (string.Equals(command, "get", StringComparison.OrdinalIgnoreCase))
            {
                return _context.Variables.ListVariables(includeAll: true)
                    .Append("pickups");
            }

            if (string.Equals(command, "set", StringComparison.OrdinalIgnoreCase)
                || string.Equals(command, "list-variables", StringComparison.OrdinalIgnoreCase))
            {
                return _context.Variables.ListVariables(includeAll: true);
            }

            if (string.Equals(command, "help", StringComparison.OrdinalIgnoreCase))
                return _dispatcher.Commands.Select(c => c.Name);

            if (string.Equals(command, "load", StringComparison.OrdinalIgnoreCase))
            {
                return LevelCatalog.ListJsonLevels()
                    .SelectMany(p => new[] { p, Path.GetFileName(p), Path.GetFileNameWithoutExtension(p) })
                    .Distinct(StringComparer.OrdinalIgnoreCase);
            }
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
