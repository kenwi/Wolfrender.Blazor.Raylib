namespace Game.Editor.Undo;

public sealed class EditorUndoStack
{
    private const int MaxDepth = 100;

    private readonly Stack<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public string? UndoDescription => CanUndo ? _undo.Peek().Description : null;
    public string? RedoDescription => CanRedo ? _redo.Peek().Description : null;

    public void Push(IEditorCommand command)
    {
        _undo.Push(command);
        _redo.Clear();
        while (_undo.Count > MaxDepth)
        {
            var items = _undo.ToArray();
            _undo.Clear();
            for (int i = items.Length - 2; i >= 0; i--)
                _undo.Push(items[i]);
        }
    }

    public bool Undo(EditorState state)
    {
        if (!CanUndo) return false;
        var command = _undo.Pop();
        command.Undo(state);
        _redo.Push(command);
        state.ApplyAfterMapMutation();
        return true;
    }

    public bool Redo(EditorState state)
    {
        if (!CanRedo) return false;
        var command = _redo.Pop();
        command.Redo(state);
        _undo.Push(command);
        state.ApplyAfterMapMutation();
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
