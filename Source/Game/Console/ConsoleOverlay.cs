using System.Text;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Console;

public sealed class ConsoleOverlay
{
    private const int MaxScrollback = 300;
    private readonly List<string> _scrollback = new();
    private readonly List<string> _history = new();
    private readonly StringBuilder _inputBuffer = new();
    private int _cursor;
    private int _historyIndex = -1;
    private int _scrollbackOffsetLines;
    private CompletionSession? _completion;

    public bool IsOpen { get; private set; }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        _historyIndex = -1;
        if (IsOpen)
            _scrollbackOffsetLines = 0;
    }

    public void AddLine(string line)
    {
        _scrollback.Add(line);
        if (_scrollback.Count > MaxScrollback)
            _scrollback.RemoveAt(0);
    }

    public void UpdateInput(Action<string> onSubmit, Func<string, int, IReadOnlyList<string>> getCompletions, bool consumeTypedCharsForThisFrame = false)
    {
        float wheel = GetMouseWheelMove();
        if (MathF.Abs(wheel) > 0.01f)
            _scrollbackOffsetLines += (int)MathF.Round(wheel * 3f);

        if (IsKeyPressed(KeyboardKey.PageUp))
            _scrollbackOffsetLines += 8;
        if (IsKeyPressed(KeyboardKey.PageDown))
            _scrollbackOffsetLines -= 8;

        while (true)
        {
            int key = GetCharPressed();
            if (key <= 0)
                break;

            char ch = (char)key;
            if (!consumeTypedCharsForThisFrame && !char.IsControl(ch))
            {
                _inputBuffer.Insert(_cursor, ch);
                _cursor++;
                ResetCompletionSession();
            }
        }

        if (IsKeyPressed(KeyboardKey.Backspace) && _cursor > 0)
        {
            _inputBuffer.Remove(_cursor - 1, 1);
            _cursor--;
            ResetCompletionSession();
        }

        if (IsKeyPressed(KeyboardKey.Delete) && _cursor < _inputBuffer.Length)
        {
            _inputBuffer.Remove(_cursor, 1);
            ResetCompletionSession();
        }

        if (IsKeyPressed(KeyboardKey.Left) && _cursor > 0)
        {
            _cursor--;
            ResetCompletionSession();
        }

        if (IsKeyPressed(KeyboardKey.Right) && _cursor < _inputBuffer.Length)
        {
            _cursor++;
            ResetCompletionSession();
        }

        if (IsKeyPressed(KeyboardKey.Home))
        {
            _cursor = 0;
            ResetCompletionSession();
        }

        if (IsKeyPressed(KeyboardKey.End))
        {
            _cursor = _inputBuffer.Length;
            ResetCompletionSession();
        }

        if (IsKeyPressed(KeyboardKey.Tab))
            ApplyTabCompletion(getCompletions);

        if (IsKeyPressed(KeyboardKey.Up))
            ApplyHistoryDelta(-1);

        if (IsKeyPressed(KeyboardKey.Down))
            ApplyHistoryDelta(1);

        if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.KpEnter))
        {
            var line = _inputBuffer.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                _history.Add(line);
                onSubmit(line);
            }

            _inputBuffer.Clear();
            _cursor = 0;
            _historyIndex = -1;
            ResetCompletionSession();
        }
    }

    public void Render()
    {
        if (!IsOpen)
            return;

        int screenW = GetScreenWidth();
        int height = Math.Max(240, GetScreenHeight() / 3);
        DrawRectangle(0, 0, screenW, height, new Color(0, 0, 0, 215));
        DrawLine(0, height, screenW, height, new Color(90, 90, 90, 255));

        const int fontSize = 20;
        const int paddingX = 12;
        const int lineHeight = 24;
        int maxLines = (height - 56) / lineHeight;
        int maxOffset = Math.Max(0, _scrollback.Count - maxLines);
        _scrollbackOffsetLines = Math.Clamp(_scrollbackOffsetLines, 0, maxOffset);
        int start = Math.Max(0, _scrollback.Count - maxLines - _scrollbackOffsetLines);
        int end = Math.Min(_scrollback.Count, start + maxLines);
        int y = 10;
        for (int i = start; i < end; i++)
        {
            DrawText(_scrollback[i], paddingX, y, fontSize, Color.LightGray);
            y += lineHeight;
        }

        if (_scrollbackOffsetLines > 0)
        {
            string scrollLabel = $"Scrollback: {_scrollbackOffsetLines}/{maxOffset}";
            int labelWidth = MeasureText(scrollLabel, 16);
            DrawText(scrollLabel, screenW - labelWidth - 12, 10, 16, Color.Gray);
        }

        string prompt = $"> {_inputBuffer}";
        int promptY = height - 34;
        DrawText(prompt, paddingX, promptY, fontSize, Color.White);

        string beforeCursor = $"> {_inputBuffer.ToString()[.._cursor]}";
        int caretX = paddingX + MeasureText(beforeCursor, fontSize);
        DrawText("_", caretX, promptY, fontSize, Color.White);
    }

    private void ApplyHistoryDelta(int delta)
    {
        if (_history.Count == 0)
            return;

        if (_historyIndex == -1)
            _historyIndex = _history.Count;

        _historyIndex = Math.Clamp(_historyIndex + delta, 0, _history.Count);

        if (_historyIndex == _history.Count)
        {
            _inputBuffer.Clear();
            _cursor = 0;
            return;
        }

        _inputBuffer.Clear();
        _inputBuffer.Append(_history[_historyIndex]);
        _cursor = _inputBuffer.Length;
        ResetCompletionSession();
    }

    private void ApplyTabCompletion(Func<string, int, IReadOnlyList<string>> getCompletions)
    {
        var line = _inputBuffer.ToString();
        var (start, end) = GetTokenRange(line, _cursor);
        string prefix = line[start.._cursor];

        if (_completion != null && _completion.CanContinue(line, start, prefix))
        {
            _completion.Index = (_completion.Index + 1) % _completion.Candidates.Count;
            ReplaceCurrentTokenWith(_completion.Candidates[_completion.Index], start);
            return;
        }

        var candidates = getCompletions(line, _cursor);
        if (candidates.Count == 0)
            return;

        _completion = new CompletionSession
        {
            TokenStart = start,
            Prefix = prefix,
            Candidates = candidates.ToList(),
            Index = 0
        };

        ReplaceCurrentTokenWith(_completion.Candidates[0], start);
    }

    private void ReplaceCurrentTokenWith(string replacement, int tokenStart)
    {
        var line = _inputBuffer.ToString();
        var (_, tokenEnd) = GetTokenRange(line, _cursor);
        _inputBuffer.Remove(tokenStart, tokenEnd - tokenStart);
        _inputBuffer.Insert(tokenStart, replacement);
        _cursor = tokenStart + replacement.Length;
    }

    private static (int Start, int End) GetTokenRange(string line, int cursor)
    {
        int clampedCursor = Math.Clamp(cursor, 0, line.Length);
        int start = clampedCursor;
        while (start > 0 && !char.IsWhiteSpace(line[start - 1]))
            start--;

        int end = clampedCursor;
        while (end < line.Length && !char.IsWhiteSpace(line[end]))
            end++;

        return (start, end);
    }

    private void ResetCompletionSession()
    {
        _completion = null;
    }

    private sealed class CompletionSession
    {
        public int TokenStart { get; init; }
        public required string Prefix { get; init; }
        public required List<string> Candidates { get; init; }
        public int Index { get; set; }

        public bool CanContinue(string line, int tokenStart, string currentPrefix)
        {
            return tokenStart == TokenStart
                && currentPrefix.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && Candidates.Count > 0;
        }
    }
}
