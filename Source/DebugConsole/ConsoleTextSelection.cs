using System.Text;

namespace Game.DebugConsole;

/// <summary>Text position in the debug console. <see cref="InputLineIndex"/> is the input line.</summary>
internal readonly record struct ConsoleTextPos(int LineIndex, int CharIndex)
{
    public const int InputLineIndex = -1;

    public bool IsInput => LineIndex == InputLineIndex;
}

/// <summary>Pure helpers for console mark/selection (testable without Raylib input).</summary>
internal static class ConsoleTextSelection
{
    public static bool AreEqual(ConsoleTextPos a, ConsoleTextPos b)
        => a.LineIndex == b.LineIndex && a.CharIndex == b.CharIndex;

    public static bool HasSelection(ConsoleTextPos? anchor, ConsoleTextPos? focus)
        => anchor is { } a && focus is { } f && !AreEqual(a, f);

    public static (ConsoleTextPos Start, ConsoleTextPos End) Normalize(ConsoleTextPos a, ConsoleTextPos b)
    {
        int cmp = Compare(a, b);
        return cmp <= 0 ? (a, b) : (b, a);
    }

    public static int Compare(ConsoleTextPos a, ConsoleTextPos b)
    {
        // Input line sorts after all scrollback lines.
        int lineA = a.IsInput ? int.MaxValue : a.LineIndex;
        int lineB = b.IsInput ? int.MaxValue : b.LineIndex;
        int lineCmp = lineA.CompareTo(lineB);
        return lineCmp != 0 ? lineCmp : a.CharIndex.CompareTo(b.CharIndex);
    }

    public static string Extract(
        IReadOnlyList<string> scrollback,
        string input,
        ConsoleTextPos anchor,
        ConsoleTextPos focus)
    {
        var (start, end) = Normalize(anchor, focus);
        if (start.IsInput && end.IsInput)
        {
            int s = Math.Clamp(start.CharIndex, 0, input.Length);
            int e = Math.Clamp(end.CharIndex, 0, input.Length);
            if (e <= s)
                return string.Empty;
            return input[s..e];
        }

        var sb = new StringBuilder();
        if (start.IsInput || end.IsInput)
        {
            // Mixed scrollback+input: take from start through end of scrollback, then input prefix.
            if (!start.IsInput)
            {
                AppendScrollbackRange(sb, scrollback, start, new ConsoleTextPos(scrollback.Count - 1, int.MaxValue));
                if (end.IsInput && end.CharIndex > 0)
                {
                    if (sb.Length > 0)
                        sb.Append('\n');
                    int e = Math.Clamp(end.CharIndex, 0, input.Length);
                    sb.Append(input.AsSpan(0, e));
                }
            }

            return sb.ToString();
        }

        AppendScrollbackRange(sb, scrollback, start, end);
        return sb.ToString();
    }

    public static string FirstPasteLine(string clipboard)
    {
        if (string.IsNullOrEmpty(clipboard))
            return string.Empty;

        string normalized = clipboard.Replace("\r\n", "\n").Replace('\r', '\n');
        int newline = normalized.IndexOf('\n');
        return newline < 0 ? normalized : normalized[..newline];
    }

    /// <summary>Deletes [start, end) in the input buffer and returns the new cursor.</summary>
    public static int DeleteInputRange(StringBuilder buffer, int start, int end)
    {
        int s = Math.Clamp(start, 0, buffer.Length);
        int e = Math.Clamp(end, 0, buffer.Length);
        if (e > s)
            buffer.Remove(s, e - s);
        return s;
    }

    /// <summary>Replaces [start, end) with <paramref name="text"/> and returns the cursor after the insert.</summary>
    public static int ReplaceInputRange(StringBuilder buffer, int start, int end, string text)
    {
        int s = DeleteInputRange(buffer, start, end);
        if (!string.IsNullOrEmpty(text))
            buffer.Insert(s, text);
        return s + (text?.Length ?? 0);
    }

    /// <summary>
    /// Returns the half-open [start, end) character range of the word (non-whitespace run)
    /// or whitespace run under <paramref name="charIndex"/>.
    /// </summary>
    public static (int Start, int End) GetWordRange(string text, int charIndex)
    {
        if (text.Length == 0)
            return (0, 0);

        int i = Math.Clamp(charIndex, 0, text.Length);
        if (i >= text.Length)
            i = text.Length - 1;

        if (char.IsWhiteSpace(text[i]))
        {
            int start = i;
            while (start > 0 && char.IsWhiteSpace(text[start - 1]))
                start--;
            int end = i + 1;
            while (end < text.Length && char.IsWhiteSpace(text[end]))
                end++;
            return (start, end);
        }

        int wordStart = i;
        while (wordStart > 0 && !char.IsWhiteSpace(text[wordStart - 1]))
            wordStart--;
        int wordEnd = i + 1;
        while (wordEnd < text.Length && !char.IsWhiteSpace(text[wordEnd]))
            wordEnd++;
        return (wordStart, wordEnd);
    }

    /// <summary>
    /// True when the current selection is exactly the word range at <paramref name="pos"/> on the same line.
    /// </summary>
    public static bool IsExactWordSelection(
        string lineText,
        ConsoleTextPos anchor,
        ConsoleTextPos focus,
        ConsoleTextPos pos)
    {
        if (anchor.LineIndex != pos.LineIndex || focus.LineIndex != pos.LineIndex)
            return false;

        var (selStart, selEnd) = Normalize(anchor, focus);
        var (wordStart, wordEnd) = GetWordRange(lineText, pos.CharIndex);
        return selStart.CharIndex == wordStart && selEnd.CharIndex == wordEnd;
    }

    /// <summary>
    /// Builds a full-line selection between an origin line and a focus line.
    /// Anchor stays on the origin line; focus is the moving end.
    /// </summary>
    public static (ConsoleTextPos Anchor, ConsoleTextPos Focus) BuildFullLineSelection(
        int originLine,
        int focusLine,
        int originLength,
        int focusLength)
    {
        if (originLine == focusLine)
        {
            return (
                new ConsoleTextPos(originLine, 0),
                new ConsoleTextPos(originLine, originLength));
        }

        var originPos = new ConsoleTextPos(originLine, 0);
        var focusPos = new ConsoleTextPos(focusLine, 0);
        if (Compare(focusPos, originPos) < 0)
        {
            // Focus is above origin: select from start of focus line through end of origin line.
            return (
                new ConsoleTextPos(originLine, originLength),
                new ConsoleTextPos(focusLine, 0));
        }

        // Focus is below origin: select from start of origin line through end of focus line.
        return (
            new ConsoleTextPos(originLine, 0),
            new ConsoleTextPos(focusLine, focusLength));
    }

    /// <summary>
    /// Moves a line index up (-1) or down (+1) through scrollback, then the input line.
    /// Returns null when already at the top or bottom.
    /// </summary>
    public static int? TryMoveLineIndex(int lineIndex, int direction, int scrollbackCount)
    {
        if (direction == 0 || scrollbackCount < 0)
            return null;

        if (lineIndex == ConsoleTextPos.InputLineIndex)
        {
            if (direction < 0)
                return scrollbackCount > 0 ? scrollbackCount - 1 : null;
            return null;
        }

        if (direction < 0)
            return lineIndex > 0 ? lineIndex - 1 : null;

        if (lineIndex < scrollbackCount - 1)
            return lineIndex + 1;

        // Past last scrollback line -> input.
        return ConsoleTextPos.InputLineIndex;
    }

    /// <summary>
    /// Moves to the next word boundary on a single line.
    /// Left (-1): start of the previous word. Right (+1): end of the next word.
    /// Returns null when already at the edge.
    /// </summary>
    public static int? TryMoveWordBoundary(string text, int charIndex, int direction)
    {
        if (string.IsNullOrEmpty(text) || direction == 0)
            return null;

        int i = Math.Clamp(charIndex, 0, text.Length);

        if (direction < 0)
        {
            if (i <= 0)
                return null;

            int j = i;
            while (j > 0 && char.IsWhiteSpace(text[j - 1]))
                j--;
            if (j <= 0)
                return j < i ? 0 : null;

            while (j > 0 && !char.IsWhiteSpace(text[j - 1]))
                j--;
            return j < i ? j : null;
        }

        if (i >= text.Length)
            return null;

        int k = i;
        while (k < text.Length && char.IsWhiteSpace(text[k]))
            k++;
        if (k >= text.Length)
            return k > i ? text.Length : null;

        while (k < text.Length && !char.IsWhiteSpace(text[k]))
            k++;
        return k > i ? k : null;
    }

    /// <summary>
    /// Builds a word-span selection from an origin word [originStart, originEnd) to a moving focus edge.
    /// When focus is left of the origin word, <paramref name="focusChar"/> is a word start;
    /// when right, it is a word end.
    /// </summary>
    public static (ConsoleTextPos Anchor, ConsoleTextPos Focus) BuildWordSelection(
        int originLine,
        int originStart,
        int originEnd,
        int focusLine,
        int focusChar)
    {
        var originStartPos = new ConsoleTextPos(originLine, originStart);
        var originEndPos = new ConsoleTextPos(originLine, originEnd);
        var focusPos = new ConsoleTextPos(focusLine, focusChar);

        if (originLine == focusLine && focusChar >= originStart && focusChar <= originEnd)
            return (originStartPos, originEndPos);

        if (Compare(focusPos, originStartPos) < 0)
            return (originEndPos, focusPos);

        return (originStartPos, focusPos);
    }

    /// <summary>
    /// Snaps a same-line selection range to full word boundaries.
    /// </summary>
    public static (int Start, int End) SnapToWordBounds(string text, int selStart, int selEnd)
    {
        if (text.Length == 0)
            return (0, 0);

        int s = Math.Clamp(Math.Min(selStart, selEnd), 0, text.Length);
        int e = Math.Clamp(Math.Max(selStart, selEnd), 0, text.Length);
        if (e <= s)
        {
            var (ws, we) = GetWordRange(text, s);
            return (ws, we);
        }

        int start = GetWordRange(text, s).Start;
        int end = GetWordRange(text, e - 1).End;
        return (start, end);
    }

    private static void AppendScrollbackRange(
        StringBuilder sb,
        IReadOnlyList<string> scrollback,
        ConsoleTextPos start,
        ConsoleTextPos end)
    {
        if (scrollback.Count == 0)
            return;

        int startLine = Math.Clamp(start.LineIndex, 0, scrollback.Count - 1);
        int endLine = Math.Clamp(end.LineIndex, 0, scrollback.Count - 1);
        if (endLine < startLine)
            return;

        for (int line = startLine; line <= endLine; line++)
        {
            string text = scrollback[line];
            int from = line == startLine ? Math.Clamp(start.CharIndex, 0, text.Length) : 0;
            int to = line == endLine
                ? Math.Clamp(end.CharIndex, 0, text.Length)
                : text.Length;

            if (line > startLine)
                sb.Append('\n');

            if (to > from)
                sb.Append(text.AsSpan(from, to - from));
        }
    }
}
