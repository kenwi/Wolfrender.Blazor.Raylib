namespace Game.Console;

public sealed class CompositeVariableAccessor : IConsoleVariableAccessor
{
    private readonly RuntimeVariableAccessor _runtimeAccessor;
    private readonly StringVariableStore _stringStore;
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["volume"] = "Audio.Volume",
        ["ResolutionDownScaleMultiplier"] = "RenderData.ResolutionDownScaleMultiplier"
    };

    public CompositeVariableAccessor(RuntimeVariableAccessor runtimeAccessor, StringVariableStore stringStore)
    {
        _runtimeAccessor = runtimeAccessor;
        _stringStore = stringStore;
    }

    public bool TryGetValue(string path, out string value, out string error)
    {
        var resolvedPath = ResolveAlias(path);
        if (_runtimeAccessor.CanHandlePath(resolvedPath))
            return _runtimeAccessor.TryGetValue(resolvedPath, out value, out error);
        return _stringStore.TryGetValue(path, out value, out error);
    }

    public bool TrySetValue(string path, string valueText, out string error)
    {
        var resolvedPath = ResolveAlias(path);
        if (_runtimeAccessor.CanHandlePath(resolvedPath))
            return _runtimeAccessor.TrySetValue(resolvedPath, valueText, out error);
        return _stringStore.TrySetValue(path, valueText, out error);
    }

    public IReadOnlyList<string> ListVariables(bool includeAll)
    {
        var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in _runtimeAccessor.ListVariables(includeAll))
            all.Add(key);
        foreach (var key in _stringStore.ListVariables(includeAll))
            all.Add(key);
        return all.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private string ResolveAlias(string path)
    {
        return _aliases.TryGetValue(path, out var resolved) ? resolved : path;
    }
}
