namespace Game.Console;

public sealed class StringVariableStore : IConsoleVariableAccessor
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public StringVariableStore(IDictionary<string, string>? seed = null)
    {
        if (seed == null)
            return;

        foreach (var (key, value) in seed)
            _values[key] = value;
    }

    public bool TryGetValue(string path, out string value, out string error)
    {
        if (_values.TryGetValue(path, out value!))
        {
            error = string.Empty;
            return true;
        }

        value = string.Empty;
        error = $"Variable not found: {path}";
        return false;
    }

    public bool TrySetValue(string path, string valueText, out string error)
    {
        _values[path] = valueText;
        error = string.Empty;
        return true;
    }

    public IReadOnlyList<string> ListVariables(bool includeAll)
    {
        return _values.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
