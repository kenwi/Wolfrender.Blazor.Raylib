using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.DebugConsole;

public sealed class ConsoleOverlay
{
    private const int MaxScrollback = 300;
    internal const int FontSize = 20;
    internal const int PaddingX = 12;
    internal const int LineHeight = 24;
    private const int PromptBottomOffset = 34;

    private readonly List<string> _scrollback = new();
    private readonly ConsoleInputLine _inputLine = new();
    private readonly ConsoleSelectionController _selection;
    private readonly ConsoleInputController _input;
    private int _scrollbackOffsetLines;

    public ConsoleOverlay()
    {
        _selection = new ConsoleSelectionController(
            _inputLine,
            _scrollback,
            GetLayout,
            () => _scrollbackOffsetLines,
            EnsureLineVisible,
            AddLine);
        _input = new ConsoleInputController(_inputLine, _selection);
    }

    public bool IsOpen { get; private set; }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        _input.ResetHistoryNavigation();
        if (IsOpen)
            _scrollbackOffsetLines = 0;
        _selection.Reset();
        _input.ResetCompletionSession();
    }

    public void Close()
    {
        IsOpen = false;
        _input.ResetHistoryNavigation();
        _selection.Reset();
        _input.ResetCompletionSession();
    }

    public void AddLine(string line)
    {
        _scrollback.Add(line);
        if (_scrollback.Count > MaxScrollback)
        {
            _scrollback.RemoveAt(0);
            _selection.ShiftAfterOldestLineDropped();
        }
    }

    /// <summary>Clears printed scrollback only; command history (↑/↓) is unchanged.</summary>
    public void ClearScrollback()
    {
        _scrollback.Clear();
        _scrollbackOffsetLines = 0;
        _selection.ClearSelection();
    }

    public void UpdateInput(
        float deltaTime,
        Action<string> onSubmit,
        Func<string, int, IReadOnlyList<string>> getCompletions,
        bool consumeTypedCharsForThisFrame = false)
    {
        float wheel = GetMouseWheelMove();
        if (MathF.Abs(wheel) > 0.01f)
            _scrollbackOffsetLines += (int)MathF.Round(wheel);

        if (IsKeyPressed(KeyboardKey.PageUp))
            _scrollbackOffsetLines += 8;
        if (IsKeyPressed(KeyboardKey.PageDown))
            _scrollbackOffsetLines -= 8;

        _selection.HandleMouseSelection();

        _input.Update(deltaTime, onSubmit, getCompletions, consumeTypedCharsForThisFrame);
    }

    public void Render()
    {
        if (!IsOpen)
            return;

        LayoutMetrics layout = GetLayout();
        DrawRectangle(0, 0, layout.ScreenW, layout.Height, new Color(0, 0, 0, 215));
        DrawLine(0, layout.Height, layout.ScreenW, layout.Height, new Color(90, 90, 90, 255));

        int maxOffset = Math.Max(0, _scrollback.Count - layout.MaxLines);
        _scrollbackOffsetLines = Math.Clamp(_scrollbackOffsetLines, 0, maxOffset);
        int start = Math.Max(0, _scrollback.Count - layout.MaxLines - _scrollbackOffsetLines);
        int end = Math.Min(_scrollback.Count, start + layout.MaxLines);
        int y = 10;
        for (int i = start; i < end; i++)
        {
            DrawSelectableLine(_scrollback[i], PaddingX, y, i);
            y += LineHeight;
        }

        if (_scrollbackOffsetLines > 0)
        {
            string scrollLabel = $"Scrollback: {_scrollbackOffsetLines}/{maxOffset}";
            int labelWidth = MeasureText(scrollLabel, 16);
            DrawText(scrollLabel, layout.ScreenW - labelWidth - 12, 10, 16, Color.Gray);
        }

        string prompt = $"> {_inputLine.Text}";
        DrawSelectableLine(prompt, PaddingX, layout.PromptY, ConsoleTextPos.InputLineIndex, promptPrefixLength: 2);

        string beforeCursor = $"> {_inputLine.Text[.._inputLine.Cursor]}";
        int caretX = PaddingX + MeasureText(beforeCursor, FontSize);
        DrawText("_", caretX, layout.PromptY, FontSize, Color.White);
    }

    private void DrawSelectableLine(string text, int x, int y, int lineIndex, int promptPrefixLength = 0)
    {
        if (_selection.HasSelection)
        {
            var (selStart, selEnd) = ConsoleTextSelection.Normalize(_selection.Anchor!.Value, _selection.Focus!.Value);
            if (TryGetHighlightRange(lineIndex, text.Length, selStart, selEnd, promptPrefixLength, out int from, out int to))
                DrawSelectionHighlight(text, x, y, from, to);
        }

        DrawText(text, x, y, FontSize, lineIndex == ConsoleTextPos.InputLineIndex ? Color.White : Color.LightGray);
    }

    private static bool TryGetHighlightRange(
        int lineIndex,
        int textLength,
        ConsoleTextPos selStart,
        ConsoleTextPos selEnd,
        int promptPrefixLength,
        out int from,
        out int to)
    {
        from = 0;
        to = 0;

        if (lineIndex == ConsoleTextPos.InputLineIndex)
        {
            if (selStart.IsInput && selEnd.IsInput)
            {
                from = selStart.CharIndex + promptPrefixLength;
                to = selEnd.CharIndex + promptPrefixLength;
                return to > from;
            }

            // Selection starts in scrollback and ends in the input line.
            if (!selStart.IsInput && selEnd.IsInput)
            {
                from = promptPrefixLength;
                to = selEnd.CharIndex + promptPrefixLength;
                return to > from;
            }

            return false;
        }

        // Scrollback line.
        if (selStart.IsInput)
            return false;

        if (selEnd.IsInput)
        {
            if (lineIndex < selStart.LineIndex)
                return false;
            from = lineIndex == selStart.LineIndex ? selStart.CharIndex : 0;
            to = textLength;
            return to > from;
        }

        if (lineIndex < selStart.LineIndex || lineIndex > selEnd.LineIndex)
            return false;

        from = lineIndex == selStart.LineIndex ? selStart.CharIndex : 0;
        to = lineIndex == selEnd.LineIndex ? selEnd.CharIndex : textLength;
        return to > from;
    }

    private static void DrawSelectionHighlight(string text, int x, int y, int fromChar, int toChar)
    {
        int start = Math.Clamp(Math.Min(fromChar, toChar), 0, text.Length);
        int end = Math.Clamp(Math.Max(fromChar, toChar), 0, text.Length);
        if (end <= start)
            return;

        int x0 = x + MeasureText(text[..start], FontSize);
        int x1 = x + MeasureText(text[..end], FontSize);
        DrawRectangle(x0, y - 2, Math.Max(1, x1 - x0), FontSize + 4, new Color(60, 100, 180, 160));
    }

    private void EnsureLineVisible(int lineIndex)
    {
        if (lineIndex == ConsoleTextPos.InputLineIndex || _scrollback.Count == 0)
            return;

        LayoutMetrics layout = GetLayout();
        int maxOffset = Math.Max(0, _scrollback.Count - layout.MaxLines);
        int start = Math.Max(0, _scrollback.Count - layout.MaxLines - _scrollbackOffsetLines);
        int end = Math.Min(_scrollback.Count, start + layout.MaxLines);
        if (lineIndex >= start && lineIndex < end)
            return;

        if (lineIndex < start)
            _scrollbackOffsetLines = Math.Clamp(_scrollback.Count - layout.MaxLines - lineIndex, 0, maxOffset);
        else
            _scrollbackOffsetLines = Math.Clamp(_scrollback.Count - lineIndex - 1, 0, maxOffset);
    }

    private LayoutMetrics GetLayout()
    {
        int screenW = GetScreenWidth();
        int height = Math.Max(240, GetScreenHeight() / 3);
        int maxLines = (height - 56) / LineHeight;
        int promptY = height - PromptBottomOffset;
        return new LayoutMetrics(screenW, height, maxLines, promptY);
    }

    internal readonly record struct LayoutMetrics(int ScreenW, int Height, int MaxLines, int PromptY);
}
