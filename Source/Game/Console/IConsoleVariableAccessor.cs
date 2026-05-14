namespace Game.Console;

public interface IConsoleVariableAccessor
{
    bool TryGetValue(string path, out string value, out string error);
    bool TrySetValue(string path, string valueText, out string error);
    IReadOnlyList<string> ListVariables(bool includeAll);
}
