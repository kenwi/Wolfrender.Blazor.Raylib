using System.Text;

namespace Game.DebugConsole;

/// <summary>Shared input-line state (text buffer + cursor) used by both the input and selection controllers.</summary>
internal sealed class ConsoleInputLine
{
    public StringBuilder Buffer { get; } = new();

    public int Cursor { get; set; }

    public int Length => Buffer.Length;

    public string Text => Buffer.ToString();

    public void Clear()
    {
        Buffer.Clear();
        Cursor = 0;
    }
}
