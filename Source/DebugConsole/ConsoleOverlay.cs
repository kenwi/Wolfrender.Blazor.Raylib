using System.Numerics;
using System.Text;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.DebugConsole;

public sealed class ConsoleOverlay
{
    private const int MaxScrollback = 300;
    private const int FontSize = 20;
    private const int PaddingX = 12;
    private const int LineHeight = 24;
    private const int PromptBottomOffset = 34;

    private readonly List<string> _scrollback = new();
    private readonly List<string> _history = new();
    private readonly StringBuilder _inputBuffer = new();
    private int _cursor;
    private int _historyIndex = -1;
    private int _scrollbackOffsetLines;
    private CompletionSession? _completion;

    private float _backspaceHoldSeconds;
    private float _backspaceRepeatSeconds;
    private bool _backspaceRepeatPhase;

    private ArrowDir? _heldArrow;
    private float _arrowHoldSeconds;
    private float _arrowRepeatSeconds;
    private bool _arrowRepeatPhase;

    private const float KeyRepeatInitialDelaySeconds = 0.5f;
    private const float KeyRepeatIntervalSeconds = 0.05f;

    private ConsoleTextPos? _selectionAnchor;
    private ConsoleTextPos? _selectionFocus;
    private bool _isDraggingSelection;
    private bool _pasteUnavailableHintShown;
    private double _lastClickTime = -1;
    private ConsoleTextPos? _lastClickPos;
    private (ConsoleTextPos Anchor, ConsoleTextPos Focus)? _wordSelectionSnapshot;
    private int? _lineSelectOriginLine;
    private int? _wordSelectOriginLine;
    private int _wordSelectOriginStart;
    private int _wordSelectOriginEnd;

    private const double DoubleClickSeconds = 0.4;

    public bool IsOpen { get; private set; }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        _historyIndex = -1;
        if (IsOpen)
            _scrollbackOffsetLines = 0;
        ClearSelection();
        _isDraggingSelection = false;
    }

    public void Close()
    {
        IsOpen = false;
        _historyIndex = -1;
        ClearSelection();
        _isDraggingSelection = false;
    }

    public void AddLine(string line)
    {
        _scrollback.Add(line);
        if (_scrollback.Count > MaxScrollback)
        {
            _scrollback.RemoveAt(0);
            ShiftSelectionAfterOldestLineDropped();
        }
    }

    private void ShiftSelectionAfterOldestLineDropped()
    {
        if (_selectionAnchor is null && _selectionFocus is null)
            return;

        if (_selectionAnchor is { } anchor && !anchor.IsInput && anchor.LineIndex == 0)
        {
            ClearSelection();
            return;
        }

        if (_selectionFocus is { } focus && !focus.IsInput && focus.LineIndex == 0)
        {
            ClearSelection();
            return;
        }

        if (_selectionAnchor is { } a && !a.IsInput)
            _selectionAnchor = a with { LineIndex = a.LineIndex - 1 };
        if (_selectionFocus is { } f && !f.IsInput)
            _selectionFocus = f with { LineIndex = f.LineIndex - 1 };
    }

    /// <summary>Clears printed scrollback only; <see cref="_history"/> (↑/↓) is unchanged.</summary>
    public void ClearScrollback()
    {
        _scrollback.Clear();
        _scrollbackOffsetLines = 0;
        ClearSelection();
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

        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        bool shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);

        HandleMouseSelection();

        if (ctrl && IsKeyPressed(KeyboardKey.A))
        {
            SelectAllInput();
            ResetBackspaceRepeatState();
            ResetCompletionSession();
        }
        else if (ctrl && IsKeyPressed(KeyboardKey.C))
        {
            CopySelectionToClipboard();
            ResetBackspaceRepeatState();
        }
        else if (ctrl && IsKeyPressed(KeyboardKey.X))
        {
            CutInputSelectionToClipboard();
            ResetBackspaceRepeatState();
            ResetCompletionSession();
        }
        else if (ctrl && IsKeyPressed(KeyboardKey.V))
        {
            PasteClipboardIntoInput();
            ResetBackspaceRepeatState();
            ResetCompletionSession();
        }
        else if (ctrl && IsKeyPressed(KeyboardKey.Backspace))
        {
            if (TryDeleteInputSelection())
            {
                // Selection deleted.
            }
            else
            {
                DeleteWordLeft();
            }

            ResetBackspaceRepeatState();
            ResetCompletionSession();
        }
        else if (ctrl && IsKeyPressed(KeyboardKey.W))
        {
            if (TryDeleteInputSelection())
            {
                // Selection deleted.
            }
            else
            {
                DeleteWordLeft();
            }

            ResetBackspaceRepeatState();
            ResetCompletionSession();
        }
        else if (!ctrl && IsKeyDown(KeyboardKey.Backspace))
        {
            HandleBackspaceRepeat(deltaTime);
        }
        else if (!IsKeyDown(KeyboardKey.Backspace))
        {
            ResetBackspaceRepeatState();
        }

        while (true)
        {
            int key = GetCharPressed();
            if (key <= 0)
                break;

            char ch = (char)key;
            if (ctrl && IsClipboardOrEditControlChar(ch))
                continue;
            if (!consumeTypedCharsForThisFrame && !char.IsControl(ch))
            {
                InsertTextAtCursor(ch.ToString());
                ResetCompletionSession();
            }
        }

        if (IsKeyPressed(KeyboardKey.Delete))
        {
            if (TryDeleteInputSelection())
            {
                ResetCompletionSession();
            }
            else if (_cursor < _inputBuffer.Length)
            {
                _inputBuffer.Remove(_cursor, 1);
                ClearSelection();
                ResetCompletionSession();
            }
        }

        HandleArrowRepeat(deltaTime);

        if (IsKeyPressed(KeyboardKey.Home))
            MoveInputCursorTo(0, shift);

        if (IsKeyPressed(KeyboardKey.End))
            MoveInputCursorTo(_inputBuffer.Length, shift);

        if (IsKeyPressed(KeyboardKey.Tab))
        {
            ClearSelection();
            ApplyTabCompletion(getCompletions);
        }

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
            ClearSelection();
            ResetCompletionSession();
        }
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

        string prompt = $"> {_inputBuffer}";
        DrawSelectableLine(prompt, PaddingX, layout.PromptY, ConsoleTextPos.InputLineIndex, promptPrefixLength: 2);

        string beforeCursor = $"> {_inputBuffer.ToString()[.._cursor]}";
        int caretX = PaddingX + MeasureText(beforeCursor, FontSize);
        DrawText("_", caretX, layout.PromptY, FontSize, Color.White);
    }

    private void HandleMouseSelection()
    {
        LayoutMetrics layout = GetLayout();
        Vector2 mouse = GetMousePosition();
        bool overConsole = mouse.Y >= 0 && mouse.Y <= layout.Height;

        if (IsMouseButtonPressed(MouseButton.Left) && overConsole)
        {
            if (TryHitTest(mouse, layout, out ConsoleTextPos pos))
            {
                double now = GetTime();
                bool isDoubleClick = _lastClickPos is { } last
                    && last.LineIndex == pos.LineIndex
                    && Math.Abs(last.CharIndex - pos.CharIndex) <= 2
                    && now - _lastClickTime <= DoubleClickSeconds;

                _lastClickTime = now;
                _lastClickPos = pos;

                if (isDoubleClick)
                {
                    _lineSelectOriginLine = null;
                    ClearWordSelectOrigin();
                    ApplyDoubleClickSelection(pos);
                    _isDraggingSelection = false;
                    return;
                }

                // Preserve word selection across the first click of a follow-up double-click.
                _wordSelectionSnapshot = null;
                if (_selectionAnchor is { } anchor
                    && _selectionFocus is { } focus
                    && ConsoleTextSelection.IsExactWordSelection(GetLineText(pos), anchor, focus, pos))
                {
                    _wordSelectionSnapshot = (anchor, focus);
                }

                _lineSelectOriginLine = null;
                ClearWordSelectOrigin();
                _selectionAnchor = pos;
                _selectionFocus = pos;
                _isDraggingSelection = true;
                if (pos.IsInput)
                    _cursor = Math.Clamp(pos.CharIndex, 0, _inputBuffer.Length);
            }
        }

        if (_isDraggingSelection && IsMouseButtonDown(MouseButton.Left))
        {
            if (TryHitTest(mouse, layout, out ConsoleTextPos pos))
            {
                if (_selectionFocus is { } prev
                    && (prev.LineIndex != pos.LineIndex || prev.CharIndex != pos.CharIndex))
                {
                    // Drag moved - abandon word snapshot for line-expand.
                    _wordSelectionSnapshot = null;
                }

                _selectionFocus = pos;
                if (pos.IsInput)
                    _cursor = Math.Clamp(pos.CharIndex, 0, _inputBuffer.Length);
            }
        }

        if (IsMouseButtonReleased(MouseButton.Left))
            _isDraggingSelection = false;
    }

    private void ApplyDoubleClickSelection(ConsoleTextPos pos)
    {
        string lineText = GetLineText(pos);
        if (lineText.Length == 0)
        {
            _selectionAnchor = pos;
            _selectionFocus = pos;
            _wordSelectionSnapshot = null;
            if (pos.IsInput)
                _cursor = 0;
            return;
        }

        bool wordAlreadySelected =
            (_selectionAnchor is { } curAnchor
             && _selectionFocus is { } curFocus
             && ConsoleTextSelection.IsExactWordSelection(lineText, curAnchor, curFocus, pos))
            || (_wordSelectionSnapshot is { } snap
                && ConsoleTextSelection.IsExactWordSelection(lineText, snap.Anchor, snap.Focus, pos));

        _wordSelectionSnapshot = null;

        // Second double-click on an already-selected word expands to the whole line.
        if (wordAlreadySelected)
        {
            SetLineSelection(pos.LineIndex, lineText.Length);
            return;
        }

        var (wordStart, wordEnd) = ConsoleTextSelection.GetWordRange(lineText, pos.CharIndex);
        _selectionAnchor = new ConsoleTextPos(pos.LineIndex, wordStart);
        _selectionFocus = new ConsoleTextPos(pos.LineIndex, wordEnd);
        if (pos.IsInput)
            _cursor = wordEnd;
    }

    private void SetLineSelection(int lineIndex, int lineLength)
    {
        _selectionAnchor = new ConsoleTextPos(lineIndex, 0);
        _selectionFocus = new ConsoleTextPos(lineIndex, lineLength);
        if (lineIndex == ConsoleTextPos.InputLineIndex)
            _cursor = lineLength;
    }

    private string GetLineText(ConsoleTextPos pos)
    {
        if (pos.IsInput)
            return _inputBuffer.ToString();
        if (pos.LineIndex < 0 || pos.LineIndex >= _scrollback.Count)
            return string.Empty;
        return _scrollback[pos.LineIndex];
    }

    private bool TryHitTest(Vector2 mouse, LayoutMetrics layout, out ConsoleTextPos pos)
    {
        pos = default;

        // Input line hit box.
        if (mouse.Y >= layout.PromptY - 4 && mouse.Y <= layout.Height)
        {
            int charIndex = HitTestCharIndex(_inputBuffer.ToString(), mouse.X - PaddingX - MeasureText("> ", FontSize));
            pos = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, charIndex);
            return true;
        }

        if (_scrollback.Count == 0 || mouse.Y < 10 || mouse.Y >= layout.PromptY - 4)
            return false;

        int maxOffset = Math.Max(0, _scrollback.Count - layout.MaxLines);
        int clampedOffset = Math.Clamp(_scrollbackOffsetLines, 0, maxOffset);
        int start = Math.Max(0, _scrollback.Count - layout.MaxLines - clampedOffset);
        int lineFromTop = (int)((mouse.Y - 10) / LineHeight);
        int lineIndex = start + lineFromTop;
        if (lineIndex < start || lineIndex >= _scrollback.Count || lineIndex >= start + layout.MaxLines)
            return false;

        string line = _scrollback[lineIndex];
        int charIndexScroll = HitTestCharIndex(line, mouse.X - PaddingX);
        pos = new ConsoleTextPos(lineIndex, charIndexScroll);
        return true;
    }

    private static int HitTestCharIndex(string text, float localX)
    {
        if (localX <= 0 || text.Length == 0)
            return 0;

        int lo = 0;
        int hi = text.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            int width = MeasureText(text[..mid], FontSize);
            if (width <= localX)
                lo = mid;
            else
                hi = mid - 1;
        }

        // Snap to nearer boundary between lo and lo+1.
        if (lo < text.Length)
        {
            int left = MeasureText(text[..lo], FontSize);
            int right = MeasureText(text[..(lo + 1)], FontSize);
            if (localX - left > right - localX)
                return lo + 1;
        }

        return lo;
    }

    private void DrawSelectableLine(string text, int x, int y, int lineIndex, int promptPrefixLength = 0)
    {
        if (ConsoleTextSelection.HasSelection(_selectionAnchor, _selectionFocus))
        {
            var (selStart, selEnd) = ConsoleTextSelection.Normalize(_selectionAnchor!.Value, _selectionFocus!.Value);
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

    private void MoveInputCursor(int delta, bool extendSelection)
    {
        int target = Math.Clamp(_cursor + delta, 0, _inputBuffer.Length);
        MoveInputCursorTo(target, extendSelection);
    }

    private void MoveInputCursorTo(int target, bool extendSelection)
    {
        target = Math.Clamp(target, 0, _inputBuffer.Length);
        if (extendSelection)
        {
            ClearWordSelectOrigin();
            _lineSelectOriginLine = null;
            _selectionAnchor ??= new ConsoleTextPos(ConsoleTextPos.InputLineIndex, _cursor);
            _cursor = target;
            _selectionFocus = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, _cursor);
        }
        else
        {
            _cursor = target;
            ClearSelection();
        }

        ResetCompletionSession();
    }

    /// <summary>
    /// Extends or shrinks the selection by full words. Origin stays at the originally
    /// selected word; first Left grows leftward, first Right grows rightward.
    /// </summary>
    private void ExtendWordSelection(int direction)
    {
        if (_selectionAnchor is not { } anchor || _selectionFocus is not { } focus)
            return;

        _lineSelectOriginLine = null;

        bool first = _wordSelectOriginLine is null;
        if (first)
        {
            var (selStart, selEnd) = ConsoleTextSelection.Normalize(anchor, focus);
            // Word extend stays on the line where the selection started (anchor).
            int originLine = anchor.LineIndex;
            string originText = GetLineText(new ConsoleTextPos(originLine, 0));
            int localStart = selStart.LineIndex == originLine ? selStart.CharIndex : 0;
            int localEnd = selEnd.LineIndex == originLine
                ? selEnd.CharIndex
                : originText.Length;
            var (wordStart, wordEnd) = ConsoleTextSelection.SnapToWordBounds(originText, localStart, localEnd);
            _wordSelectOriginLine = originLine;
            _wordSelectOriginStart = wordStart;
            _wordSelectOriginEnd = wordEnd;
        }

        int originLineLocked = _wordSelectOriginLine!.Value;
        int oStart = _wordSelectOriginStart;
        int oEnd = _wordSelectOriginEnd;

        int currentFocusLine = first ? originLineLocked : focus.LineIndex;
        int currentFocusChar = first
            ? (direction < 0 ? oStart : oEnd)
            : focus.CharIndex;

        if (!TryMoveWordFocus(currentFocusLine, currentFocusChar, direction, out int focusLine, out int focusChar))
        {
            ApplyWordSelection(originLineLocked, oStart, oEnd, currentFocusLine, currentFocusChar);
            return;
        }

        ApplyWordSelection(originLineLocked, oStart, oEnd, focusLine, focusChar);
        EnsureLineVisible(focusLine);
        ResetCompletionSession();
    }

    private bool TryMoveWordFocus(
        int lineIndex,
        int charIndex,
        int direction,
        out int focusLine,
        out int focusChar)
    {
        focusLine = lineIndex;
        focusChar = charIndex;
        string text = GetLineText(new ConsoleTextPos(lineIndex, 0));
        int? next = ConsoleTextSelection.TryMoveWordBoundary(text, charIndex, direction);
        if (next is int moved)
        {
            focusChar = moved;
            return true;
        }

        int? nextLine = ConsoleTextSelection.TryMoveLineIndex(lineIndex, direction, _scrollback.Count);
        if (nextLine is null)
            return false;

        string nextText = GetLineText(new ConsoleTextPos(nextLine.Value, 0));
        if (nextText.Length == 0)
        {
            focusLine = nextLine.Value;
            focusChar = 0;
            return true;
        }

        if (direction < 0)
        {
            var (wordStart, _) = ConsoleTextSelection.GetWordRange(nextText, nextText.Length - 1);
            focusLine = nextLine.Value;
            focusChar = wordStart;
            return true;
        }

        var (_, wordEnd) = ConsoleTextSelection.GetWordRange(nextText, 0);
        focusLine = nextLine.Value;
        focusChar = wordEnd;
        return true;
    }

    private void ApplyWordSelection(int originLine, int originStart, int originEnd, int focusLine, int focusChar)
    {
        var (newAnchor, newFocus) = ConsoleTextSelection.BuildWordSelection(
            originLine,
            originStart,
            originEnd,
            focusLine,
            focusChar);
        _selectionAnchor = newAnchor;
        _selectionFocus = newFocus;
        if (newFocus.IsInput)
            _cursor = newFocus.CharIndex;
        else if (newAnchor.IsInput)
            _cursor = newAnchor.CharIndex;
    }

    private void ClearWordSelectOrigin()
    {
        _wordSelectOriginLine = null;
        _wordSelectOriginStart = 0;
        _wordSelectOriginEnd = 0;
    }

    /// <summary>
    /// Extends or shrinks the selection by full lines. Origin stays at the line where
    /// selection started; first Up grows upward, first Down grows downward.
    /// </summary>
    private void ExtendLineSelection(int direction)
    {
        if (_selectionAnchor is not { } anchor || _selectionFocus is not { } focus)
            return;

        ClearWordSelectOrigin();

        bool firstVerticalExtend = _lineSelectOriginLine is null;
        int originLine = firstVerticalExtend ? anchor.LineIndex : _lineSelectOriginLine!.Value;
        _lineSelectOriginLine = originLine;

        int currentFocusLine = firstVerticalExtend ? originLine : focus.LineIndex;
        int? nextFocusLine = ConsoleTextSelection.TryMoveLineIndex(
            currentFocusLine,
            direction,
            _scrollback.Count);

        int focusLine = nextFocusLine ?? currentFocusLine;
        ApplyFullLineSelection(originLine, focusLine);
        EnsureLineVisible(focusLine);
        ResetCompletionSession();
    }

    private void ApplyFullLineSelection(int originLine, int focusLine)
    {
        int originLength = GetLineLength(originLine);
        int focusLength = GetLineLength(focusLine);
        var (anchor, focus) = ConsoleTextSelection.BuildFullLineSelection(
            originLine,
            focusLine,
            originLength,
            focusLength);
        _selectionAnchor = anchor;
        _selectionFocus = focus;
        if (focus.IsInput)
            _cursor = focus.CharIndex;
    }

    private int GetLineLength(int lineIndex)
    {
        if (lineIndex == ConsoleTextPos.InputLineIndex)
            return _inputBuffer.Length;
        if (lineIndex < 0 || lineIndex >= _scrollback.Count)
            return 0;
        return _scrollback[lineIndex].Length;
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

    private void SelectAllInput()
    {
        if (_inputBuffer.Length == 0)
        {
            ClearSelection();
            return;
        }

        _selectionAnchor = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, 0);
        _selectionFocus = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, _inputBuffer.Length);
        _cursor = _inputBuffer.Length;
    }

    private void CopySelectionToClipboard()
    {
        if (!ConsoleTextSelection.HasSelection(_selectionAnchor, _selectionFocus))
            return;

        string text = ConsoleTextSelection.Extract(
            _scrollback,
            _inputBuffer.ToString(),
            _selectionAnchor!.Value,
            _selectionFocus!.Value);
        if (string.IsNullOrEmpty(text))
            return;

        ConsoleClipboard.SetText(text);
    }

    private void CutInputSelectionToClipboard()
    {
        if (!TryGetInputSelectionRange(out int start, out int end))
            return;

        string text = _inputBuffer.ToString(start, end - start);
        ConsoleClipboard.SetText(text);
        _cursor = ConsoleTextSelection.DeleteInputRange(_inputBuffer, start, end);
        ClearSelection();
    }

    private void PasteClipboardIntoInput()
    {
        string raw = ConsoleClipboard.GetText();
        string line = ConsoleTextSelection.FirstPasteLine(raw);
        if (string.IsNullOrEmpty(line))
        {
            if (OperatingSystem.IsBrowser() && !_pasteUnavailableHintShown)
            {
                _pasteUnavailableHintShown = true;
                AddLine("clipboard: paste unavailable or permission denied");
            }

            return;
        }

        InsertTextAtCursor(line);
    }

    private void InsertTextAtCursor(string text)
    {
        if (TryGetInputSelectionRange(out int start, out int end))
        {
            _cursor = ConsoleTextSelection.ReplaceInputRange(_inputBuffer, start, end, text);
            ClearSelection();
            return;
        }

        _inputBuffer.Insert(_cursor, text);
        _cursor += text.Length;
        ClearSelection();
    }

    private bool TryDeleteInputSelection()
    {
        if (!TryGetInputSelectionRange(out int start, out int end))
            return false;

        _cursor = ConsoleTextSelection.DeleteInputRange(_inputBuffer, start, end);
        ClearSelection();
        return true;
    }

    private bool TryGetInputSelectionRange(out int start, out int end)
    {
        start = 0;
        end = 0;
        if (!ConsoleTextSelection.HasSelection(_selectionAnchor, _selectionFocus))
            return false;

        var (selStart, selEnd) = ConsoleTextSelection.Normalize(_selectionAnchor!.Value, _selectionFocus!.Value);
        if (!selStart.IsInput || !selEnd.IsInput)
            return false;

        start = Math.Clamp(selStart.CharIndex, 0, _inputBuffer.Length);
        end = Math.Clamp(selEnd.CharIndex, 0, _inputBuffer.Length);
        return end > start;
    }

    private void ClearSelection()
    {
        _selectionAnchor = null;
        _selectionFocus = null;
        _wordSelectionSnapshot = null;
        _lineSelectOriginLine = null;
        ClearWordSelectOrigin();
    }

    private static bool IsClipboardOrEditControlChar(char ch)
    {
        char lower = char.ToLowerInvariant(ch);
        return lower is 'w' or 'a' or 'c' or 'x' or 'v';
    }

    private LayoutMetrics GetLayout()
    {
        int screenW = GetScreenWidth();
        int height = Math.Max(240, GetScreenHeight() / 3);
        int maxLines = (height - 56) / LineHeight;
        int promptY = height - PromptBottomOffset;
        return new LayoutMetrics(screenW, height, maxLines, promptY);
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

    private void ResetBackspaceRepeatState()
    {
        _backspaceHoldSeconds = 0f;
        _backspaceRepeatSeconds = 0f;
        _backspaceRepeatPhase = false;
    }

    private void ResetArrowRepeatState()
    {
        _heldArrow = null;
        _arrowHoldSeconds = 0f;
        _arrowRepeatSeconds = 0f;
        _arrowRepeatPhase = false;
    }

    private void HandleArrowRepeat(float deltaTime)
    {
        if (TryBeginArrowPress(KeyboardKey.Left, ArrowDir.Left)
            || TryBeginArrowPress(KeyboardKey.Right, ArrowDir.Right)
            || TryBeginArrowPress(KeyboardKey.Up, ArrowDir.Up)
            || TryBeginArrowPress(KeyboardKey.Down, ArrowDir.Down))
        {
            return;
        }

        if (_heldArrow is not { } held)
            return;

        if (!IsKeyDown(ToKeyboardKey(held)))
        {
            ResetArrowRepeatState();
            return;
        }

        _arrowHoldSeconds += deltaTime;
        if (!_arrowRepeatPhase)
        {
            if (_arrowHoldSeconds < KeyRepeatInitialDelaySeconds)
                return;

            _arrowRepeatPhase = true;
            _arrowRepeatSeconds = 0f;
            PerformArrowAction(held);
            return;
        }

        _arrowRepeatSeconds += deltaTime;
        while (_arrowRepeatSeconds >= KeyRepeatIntervalSeconds)
        {
            _arrowRepeatSeconds -= KeyRepeatIntervalSeconds;
            PerformArrowAction(held);
        }
    }

    private bool TryBeginArrowPress(KeyboardKey key, ArrowDir dir)
    {
        if (!IsKeyPressed(key))
            return false;

        _heldArrow = dir;
        _arrowHoldSeconds = 0f;
        _arrowRepeatSeconds = 0f;
        _arrowRepeatPhase = false;
        PerformArrowAction(dir);
        return true;
    }

    private void PerformArrowAction(ArrowDir dir)
    {
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        bool shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);

        switch (dir)
        {
            case ArrowDir.Left:
                if (ctrl && shift && ConsoleTextSelection.HasSelection(_selectionAnchor, _selectionFocus))
                    ExtendWordSelection(-1);
                else if (!ctrl)
                    MoveInputCursor(-1, shift);
                break;
            case ArrowDir.Right:
                if (ctrl && shift && ConsoleTextSelection.HasSelection(_selectionAnchor, _selectionFocus))
                    ExtendWordSelection(1);
                else if (!ctrl)
                    MoveInputCursor(1, shift);
                break;
            case ArrowDir.Up:
                if (shift && ConsoleTextSelection.HasSelection(_selectionAnchor, _selectionFocus))
                    ExtendLineSelection(-1);
                else if (!shift)
                {
                    ClearSelection();
                    ApplyHistoryDelta(-1);
                }

                break;
            case ArrowDir.Down:
                if (shift && ConsoleTextSelection.HasSelection(_selectionAnchor, _selectionFocus))
                    ExtendLineSelection(1);
                else if (!shift)
                {
                    ClearSelection();
                    ApplyHistoryDelta(1);
                }

                break;
        }
    }

    private static KeyboardKey ToKeyboardKey(ArrowDir dir) => dir switch
    {
        ArrowDir.Left => KeyboardKey.Left,
        ArrowDir.Right => KeyboardKey.Right,
        ArrowDir.Up => KeyboardKey.Up,
        ArrowDir.Down => KeyboardKey.Down,
        _ => KeyboardKey.Null
    };

    private void HandleBackspaceRepeat(float deltaTime)
    {
        if (IsKeyPressed(KeyboardKey.Backspace))
        {
            if (TryDeleteInputSelection())
            {
                ResetCompletionSession();
                _backspaceHoldSeconds = 0f;
                _backspaceRepeatSeconds = 0f;
                _backspaceRepeatPhase = false;
                return;
            }

            DeleteCharLeft();
            ClearSelection();
            ResetCompletionSession();
            _backspaceHoldSeconds = 0f;
            _backspaceRepeatSeconds = 0f;
            _backspaceRepeatPhase = false;
            return;
        }

        _backspaceHoldSeconds += deltaTime;
        if (!_backspaceRepeatPhase)
        {
            if (_backspaceHoldSeconds < KeyRepeatInitialDelaySeconds)
                return;

            _backspaceRepeatPhase = true;
            _backspaceRepeatSeconds = 0f;
            DeleteCharLeft();
            ClearSelection();
            ResetCompletionSession();
            return;
        }

        _backspaceRepeatSeconds += deltaTime;
        while (_backspaceRepeatSeconds >= KeyRepeatIntervalSeconds)
        {
            _backspaceRepeatSeconds -= KeyRepeatIntervalSeconds;
            if (!DeleteCharLeft())
                break;
            ClearSelection();
            ResetCompletionSession();
        }
    }

    /// <summary>Deletes one character left of the cursor. Returns false if nothing was removed.</summary>
    private bool DeleteCharLeft()
    {
        if (_cursor <= 0)
            return false;
        _inputBuffer.Remove(_cursor - 1, 1);
        _cursor--;
        return true;
    }

    /// <summary>Readline-style: skip spaces left of cursor, then remove the preceding word (non-whitespace run).</summary>
    private void DeleteWordLeft()
    {
        if (TryDeleteInputSelection())
            return;

        if (_cursor <= 0)
            return;

        int i = _cursor;
        while (i > 0 && char.IsWhiteSpace(_inputBuffer[i - 1]))
            i--;
        while (i > 0 && !char.IsWhiteSpace(_inputBuffer[i - 1]))
            i--;

        int removeLen = _cursor - i;
        if (removeLen <= 0)
            return;

        _inputBuffer.Remove(i, removeLen);
        _cursor = i;
        ClearSelection();
    }

    private readonly record struct LayoutMetrics(int ScreenW, int Height, int MaxLines, int PromptY);

    private enum ArrowDir
    {
        Left,
        Right,
        Up,
        Down
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
