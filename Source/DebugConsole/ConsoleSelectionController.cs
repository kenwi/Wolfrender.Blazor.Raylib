using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.DebugConsole;

/// <summary>
/// Owns the console's mark/selection state: mouse selection (click, drag, double-click word/line
/// select), clipboard copy/cut/paste, and word/line selection extension. Also owns the low-level
/// text mutation helpers (insert/delete respecting the current selection) since those must consult
/// and clear selection state.
/// </summary>
internal sealed class ConsoleSelectionController
{
    private const double DoubleClickSeconds = 0.4;

    private readonly ConsoleInputLine _inputLine;
    private readonly IReadOnlyList<string> _scrollback;
    private readonly Func<ConsoleOverlay.LayoutMetrics> _getLayout;
    private readonly Func<int> _getScrollbackOffset;
    private readonly Action<int> _ensureLineVisible;
    private readonly Action<string> _addDiagnosticLine;

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

    public ConsoleSelectionController(
        ConsoleInputLine inputLine,
        IReadOnlyList<string> scrollback,
        Func<ConsoleOverlay.LayoutMetrics> getLayout,
        Func<int> getScrollbackOffset,
        Action<int> ensureLineVisible,
        Action<string> addDiagnosticLine)
    {
        _inputLine = inputLine;
        _scrollback = scrollback;
        _getLayout = getLayout;
        _getScrollbackOffset = getScrollbackOffset;
        _ensureLineVisible = ensureLineVisible;
        _addDiagnosticLine = addDiagnosticLine;
    }

    public ConsoleTextPos? Anchor => _selectionAnchor;

    public ConsoleTextPos? Focus => _selectionFocus;

    public bool HasSelection => ConsoleTextSelection.HasSelection(_selectionAnchor, _selectionFocus);

    /// <summary>Resets selection and drag state; used when the console is toggled or closed.</summary>
    public void Reset()
    {
        ClearSelection();
        _isDraggingSelection = false;
    }

    public void ClearSelection()
    {
        _selectionAnchor = null;
        _selectionFocus = null;
        _wordSelectionSnapshot = null;
        _lineSelectOriginLine = null;
        ClearWordSelectOrigin();
    }

    /// <summary>Shifts selection line indices after the oldest scrollback line is dropped.</summary>
    public void ShiftAfterOldestLineDropped()
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

    public void HandleMouseSelection()
    {
        ConsoleOverlay.LayoutMetrics layout = _getLayout();
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
                    _inputLine.Cursor = Math.Clamp(pos.CharIndex, 0, _inputLine.Length);
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
                    _inputLine.Cursor = Math.Clamp(pos.CharIndex, 0, _inputLine.Length);
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
                _inputLine.Cursor = 0;
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
            _inputLine.Cursor = wordEnd;
    }

    private void SetLineSelection(int lineIndex, int lineLength)
    {
        _selectionAnchor = new ConsoleTextPos(lineIndex, 0);
        _selectionFocus = new ConsoleTextPos(lineIndex, lineLength);
        if (lineIndex == ConsoleTextPos.InputLineIndex)
            _inputLine.Cursor = lineLength;
    }

    private string GetLineText(ConsoleTextPos pos)
    {
        if (pos.IsInput)
            return _inputLine.Text;
        if (pos.LineIndex < 0 || pos.LineIndex >= _scrollback.Count)
            return string.Empty;
        return _scrollback[pos.LineIndex];
    }

    private bool TryHitTest(Vector2 mouse, ConsoleOverlay.LayoutMetrics layout, out ConsoleTextPos pos)
    {
        pos = default;

        // Input line hit box.
        if (mouse.Y >= layout.PromptY - 4 && mouse.Y <= layout.Height)
        {
            int charIndex = HitTestCharIndex(
                _inputLine.Text,
                mouse.X - ConsoleOverlay.PaddingX - MeasureText("> ", ConsoleOverlay.FontSize));
            pos = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, charIndex);
            return true;
        }

        if (_scrollback.Count == 0 || mouse.Y < 10 || mouse.Y >= layout.PromptY - 4)
            return false;

        int maxOffset = Math.Max(0, _scrollback.Count - layout.MaxLines);
        int clampedOffset = Math.Clamp(_getScrollbackOffset(), 0, maxOffset);
        int start = Math.Max(0, _scrollback.Count - layout.MaxLines - clampedOffset);
        int lineFromTop = (int)((mouse.Y - 10) / ConsoleOverlay.LineHeight);
        int lineIndex = start + lineFromTop;
        if (lineIndex < start || lineIndex >= _scrollback.Count || lineIndex >= start + layout.MaxLines)
            return false;

        string line = _scrollback[lineIndex];
        int charIndexScroll = HitTestCharIndex(line, mouse.X - ConsoleOverlay.PaddingX);
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
            int width = MeasureText(text[..mid], ConsoleOverlay.FontSize);
            if (width <= localX)
                lo = mid;
            else
                hi = mid - 1;
        }

        // Snap to nearer boundary between lo and lo+1.
        if (lo < text.Length)
        {
            int left = MeasureText(text[..lo], ConsoleOverlay.FontSize);
            int right = MeasureText(text[..(lo + 1)], ConsoleOverlay.FontSize);
            if (localX - left > right - localX)
                return lo + 1;
        }

        return lo;
    }

    public void MoveInputCursor(int delta, bool extendSelection)
    {
        int target = Math.Clamp(_inputLine.Cursor + delta, 0, _inputLine.Length);
        MoveInputCursorTo(target, extendSelection);
    }

    public void MoveInputCursorTo(int target, bool extendSelection)
    {
        target = Math.Clamp(target, 0, _inputLine.Length);
        if (extendSelection)
        {
            ClearWordSelectOrigin();
            _lineSelectOriginLine = null;
            _selectionAnchor ??= new ConsoleTextPos(ConsoleTextPos.InputLineIndex, _inputLine.Cursor);
            _inputLine.Cursor = target;
            _selectionFocus = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, _inputLine.Cursor);
        }
        else
        {
            _inputLine.Cursor = target;
            ClearSelection();
        }
    }

    /// <summary>
    /// Extends or shrinks the selection by full words. Origin stays at the originally
    /// selected word; first Left grows leftward, first Right grows rightward.
    /// Returns true when the selection was applied (caller should reset tab-completion state).
    /// </summary>
    public bool ExtendWordSelection(int direction)
    {
        if (_selectionAnchor is not { } anchor || _selectionFocus is not { } focus)
            return false;

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
            return false;
        }

        ApplyWordSelection(originLineLocked, oStart, oEnd, focusLine, focusChar);
        _ensureLineVisible(focusLine);
        return true;
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
            _inputLine.Cursor = newFocus.CharIndex;
        else if (newAnchor.IsInput)
            _inputLine.Cursor = newAnchor.CharIndex;
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
    /// Returns true when the selection was applied (caller should reset tab-completion state).
    /// </summary>
    public bool ExtendLineSelection(int direction)
    {
        if (_selectionAnchor is not { } anchor || _selectionFocus is not { } focus)
            return false;

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
        _ensureLineVisible(focusLine);
        return true;
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
            _inputLine.Cursor = focus.CharIndex;
    }

    private int GetLineLength(int lineIndex)
    {
        if (lineIndex == ConsoleTextPos.InputLineIndex)
            return _inputLine.Length;
        if (lineIndex < 0 || lineIndex >= _scrollback.Count)
            return 0;
        return _scrollback[lineIndex].Length;
    }

    public void SelectAllInput()
    {
        if (_inputLine.Length == 0)
        {
            ClearSelection();
            return;
        }

        _selectionAnchor = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, 0);
        _selectionFocus = new ConsoleTextPos(ConsoleTextPos.InputLineIndex, _inputLine.Length);
        _inputLine.Cursor = _inputLine.Length;
    }

    public void CopySelectionToClipboard()
    {
        if (!HasSelection)
            return;

        string text = ConsoleTextSelection.Extract(
            _scrollback,
            _inputLine.Text,
            _selectionAnchor!.Value,
            _selectionFocus!.Value);
        if (string.IsNullOrEmpty(text))
            return;

        ConsoleClipboard.SetText(text);
    }

    public void CutInputSelectionToClipboard()
    {
        if (!TryGetInputSelectionRange(out int start, out int end))
            return;

        string text = _inputLine.Buffer.ToString(start, end - start);
        ConsoleClipboard.SetText(text);
        _inputLine.Cursor = ConsoleTextSelection.DeleteInputRange(_inputLine.Buffer, start, end);
        ClearSelection();
    }

    public void PasteClipboardIntoInput()
    {
        string raw = ConsoleClipboard.GetText();
        string line = ConsoleTextSelection.FirstPasteLine(raw);
        if (string.IsNullOrEmpty(line))
        {
            if (OperatingSystem.IsBrowser() && !_pasteUnavailableHintShown)
            {
                _pasteUnavailableHintShown = true;
                _addDiagnosticLine("clipboard: paste unavailable or permission denied");
            }

            return;
        }

        InsertTextAtCursor(line);
    }

    public void InsertTextAtCursor(string text)
    {
        if (TryGetInputSelectionRange(out int start, out int end))
        {
            _inputLine.Cursor = ConsoleTextSelection.ReplaceInputRange(_inputLine.Buffer, start, end, text);
            ClearSelection();
            return;
        }

        _inputLine.Buffer.Insert(_inputLine.Cursor, text);
        _inputLine.Cursor += text.Length;
        ClearSelection();
    }

    public bool TryDeleteInputSelection()
    {
        if (!TryGetInputSelectionRange(out int start, out int end))
            return false;

        _inputLine.Cursor = ConsoleTextSelection.DeleteInputRange(_inputLine.Buffer, start, end);
        ClearSelection();
        return true;
    }

    public bool TryGetInputSelectionRange(out int start, out int end)
    {
        start = 0;
        end = 0;
        if (!HasSelection)
            return false;

        var (selStart, selEnd) = ConsoleTextSelection.Normalize(_selectionAnchor!.Value, _selectionFocus!.Value);
        if (!selStart.IsInput || !selEnd.IsInput)
            return false;

        start = Math.Clamp(selStart.CharIndex, 0, _inputLine.Length);
        end = Math.Clamp(selEnd.CharIndex, 0, _inputLine.Length);
        return end > start;
    }
}
