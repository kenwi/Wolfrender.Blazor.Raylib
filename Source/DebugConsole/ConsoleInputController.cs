using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.DebugConsole;

/// <summary>
/// Owns typed-character input, key-repeat (backspace/arrows), command history navigation, and
/// tab completion for the console's input line. Depends on <see cref="ConsoleSelectionController"/>
/// (one-way) for selection-aware insert/delete and for extending selection via arrow keys.
/// </summary>
internal sealed class ConsoleInputController
{
    private const float KeyRepeatInitialDelaySeconds = 0.5f;
    private const float KeyRepeatIntervalSeconds = 0.05f;

    private readonly ConsoleInputLine _inputLine;
    private readonly ConsoleSelectionController _selection;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private CompletionSession? _completion;

    private float _backspaceHoldSeconds;
    private float _backspaceRepeatSeconds;
    private bool _backspaceRepeatPhase;

    private ArrowDir? _heldArrow;
    private float _arrowHoldSeconds;
    private float _arrowRepeatSeconds;
    private bool _arrowRepeatPhase;

    public ConsoleInputController(ConsoleInputLine inputLine, ConsoleSelectionController selection)
    {
        _inputLine = inputLine;
        _selection = selection;
    }

    /// <summary>Resets history navigation; called when the console is toggled or closed.</summary>
    public void ResetHistoryNavigation()
    {
        _historyIndex = -1;
    }

    public void ResetCompletionSession()
    {
        _completion = null;
    }

    public void Update(
        float deltaTime,
        Action<string> onSubmit,
        Func<string, int, IReadOnlyList<string>> getCompletions,
        bool consumeTypedCharsForThisFrame)
    {
        bool ctrl = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        bool shift = IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift);

        if (ctrl && IsKeyPressed(KeyboardKey.A))
        {
            _selection.SelectAllInput();
            ResetBackspaceRepeatState();
            ResetCompletionSession();
        }
        else if (ctrl && IsKeyPressed(KeyboardKey.C))
        {
            _selection.CopySelectionToClipboard();
            ResetBackspaceRepeatState();
        }
        else if (ctrl && IsKeyPressed(KeyboardKey.X))
        {
            _selection.CutInputSelectionToClipboard();
            ResetBackspaceRepeatState();
            ResetCompletionSession();
        }
        else if (ctrl && IsKeyPressed(KeyboardKey.V))
        {
            _selection.PasteClipboardIntoInput();
            ResetBackspaceRepeatState();
            ResetCompletionSession();
        }
        else if (ctrl && IsKeyPressed(KeyboardKey.Backspace))
        {
            if (_selection.TryDeleteInputSelection())
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
            if (_selection.TryDeleteInputSelection())
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
                _selection.InsertTextAtCursor(ch.ToString());
                ResetCompletionSession();
            }
        }

        if (IsKeyPressed(KeyboardKey.Delete))
        {
            if (_selection.TryDeleteInputSelection())
            {
                ResetCompletionSession();
            }
            else if (_inputLine.Cursor < _inputLine.Length)
            {
                _inputLine.Buffer.Remove(_inputLine.Cursor, 1);
                _selection.ClearSelection();
                ResetCompletionSession();
            }
        }

        HandleArrowRepeat(deltaTime);

        if (IsKeyPressed(KeyboardKey.Home))
        {
            _selection.MoveInputCursorTo(0, shift);
            ResetCompletionSession();
        }

        if (IsKeyPressed(KeyboardKey.End))
        {
            _selection.MoveInputCursorTo(_inputLine.Length, shift);
            ResetCompletionSession();
        }

        if (IsKeyPressed(KeyboardKey.Tab))
        {
            _selection.ClearSelection();
            ApplyTabCompletion(getCompletions);
        }

        if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.KpEnter))
        {
            var line = _inputLine.Text.Trim();
            if (!string.IsNullOrWhiteSpace(line))
            {
                _history.Add(line);
                onSubmit(line);
            }

            _inputLine.Clear();
            _historyIndex = -1;
            _selection.ClearSelection();
            ResetCompletionSession();
        }
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
            _inputLine.Clear();
            return;
        }

        _inputLine.Buffer.Clear();
        _inputLine.Buffer.Append(_history[_historyIndex]);
        _inputLine.Cursor = _inputLine.Length;
        ResetCompletionSession();
    }

    private void ApplyTabCompletion(Func<string, int, IReadOnlyList<string>> getCompletions)
    {
        var line = _inputLine.Text;
        var (start, end) = GetTokenRange(line, _inputLine.Cursor);
        string prefix = line[start.._inputLine.Cursor];

        if (_completion != null && _completion.CanContinue(line, start, prefix))
        {
            _completion.Index = (_completion.Index + 1) % _completion.Candidates.Count;
            ReplaceCurrentTokenWith(_completion.Candidates[_completion.Index], start);
            return;
        }

        var candidates = getCompletions(line, _inputLine.Cursor);
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
        var line = _inputLine.Text;
        var (_, tokenEnd) = GetTokenRange(line, _inputLine.Cursor);
        _inputLine.Buffer.Remove(tokenStart, tokenEnd - tokenStart);
        _inputLine.Buffer.Insert(tokenStart, replacement);
        _inputLine.Cursor = tokenStart + replacement.Length;
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
                if (ctrl && shift && _selection.HasSelection)
                {
                    if (_selection.ExtendWordSelection(-1))
                        ResetCompletionSession();
                }
                else if (!ctrl)
                {
                    _selection.MoveInputCursor(-1, shift);
                    ResetCompletionSession();
                }

                break;
            case ArrowDir.Right:
                if (ctrl && shift && _selection.HasSelection)
                {
                    if (_selection.ExtendWordSelection(1))
                        ResetCompletionSession();
                }
                else if (!ctrl)
                {
                    _selection.MoveInputCursor(1, shift);
                    ResetCompletionSession();
                }

                break;
            case ArrowDir.Up:
                if (shift && _selection.HasSelection)
                {
                    if (_selection.ExtendLineSelection(-1))
                        ResetCompletionSession();
                }
                else if (!shift)
                {
                    _selection.ClearSelection();
                    ApplyHistoryDelta(-1);
                }

                break;
            case ArrowDir.Down:
                if (shift && _selection.HasSelection)
                {
                    if (_selection.ExtendLineSelection(1))
                        ResetCompletionSession();
                }
                else if (!shift)
                {
                    _selection.ClearSelection();
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
            if (_selection.TryDeleteInputSelection())
            {
                ResetCompletionSession();
                _backspaceHoldSeconds = 0f;
                _backspaceRepeatSeconds = 0f;
                _backspaceRepeatPhase = false;
                return;
            }

            DeleteCharLeft();
            _selection.ClearSelection();
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
            _selection.ClearSelection();
            ResetCompletionSession();
            return;
        }

        _backspaceRepeatSeconds += deltaTime;
        while (_backspaceRepeatSeconds >= KeyRepeatIntervalSeconds)
        {
            _backspaceRepeatSeconds -= KeyRepeatIntervalSeconds;
            if (!DeleteCharLeft())
                break;
            _selection.ClearSelection();
            ResetCompletionSession();
        }
    }

    /// <summary>Deletes one character left of the cursor. Returns false if nothing was removed.</summary>
    private bool DeleteCharLeft()
    {
        if (_inputLine.Cursor <= 0)
            return false;
        _inputLine.Buffer.Remove(_inputLine.Cursor - 1, 1);
        _inputLine.Cursor--;
        return true;
    }

    /// <summary>Readline-style: skip spaces left of cursor, then remove the preceding word (non-whitespace run).</summary>
    private void DeleteWordLeft()
    {
        if (_selection.TryDeleteInputSelection())
            return;

        if (_inputLine.Cursor <= 0)
            return;

        int i = _inputLine.Cursor;
        while (i > 0 && char.IsWhiteSpace(_inputLine.Buffer[i - 1]))
            i--;
        while (i > 0 && !char.IsWhiteSpace(_inputLine.Buffer[i - 1]))
            i--;

        int removeLen = _inputLine.Cursor - i;
        if (removeLen <= 0)
            return;

        _inputLine.Buffer.Remove(i, removeLen);
        _inputLine.Cursor = i;
        _selection.ClearSelection();
    }

    private static bool IsClipboardOrEditControlChar(char ch)
    {
        char lower = char.ToLowerInvariant(ch);
        return lower is 'w' or 'a' or 'c' or 'x' or 'v';
    }

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
