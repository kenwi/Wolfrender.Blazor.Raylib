namespace Game.Editor.Undo;

/// <summary>Reversible editor mutation. Commands are pushed after the change is applied.</summary>
public interface IEditorCommand
{
    string Description { get; }

    void Undo(EditorState state);

    void Redo(EditorState state);
}
