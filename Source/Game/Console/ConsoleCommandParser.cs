using System.Text;

namespace Game.Console;

public sealed class ConsoleCommandParser
{
    public ConsoleCommandResult TryParse(string input, out ConsoleCommandInvocation? invocation)
    {
        invocation = null;
        if (string.IsNullOrWhiteSpace(input))
            return ConsoleCommandResult.Fail("Command is empty.");

        var tokens = Tokenize(input);
        if (tokens == null)
            return ConsoleCommandResult.Fail("Unterminated quoted string.");
        if (tokens.Count == 0)
            return ConsoleCommandResult.Fail("Command is empty.");

        invocation = new ConsoleCommandInvocation(tokens[0], tokens.Skip(1).ToArray());
        return ConsoleCommandResult.Ok("Parsed.");
    }

    private static List<string>? Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        bool escaping = false;

        foreach (char ch in input)
        {
            if (escaping)
            {
                current.Append(ch);
                escaping = false;
                continue;
            }

            if (ch == '\\')
            {
                escaping = true;
                continue;
            }

            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(ch);
        }

        if (escaping)
            current.Append('\\');

        if (inQuotes)
            return null;

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}
