namespace Game.Editor;

public class EditorLayer
{
    public string Name { get; set; } = "";
    public uint[] Tiles { get; set; } = Array.Empty<uint>();
    public bool IsVisible { get; set; } = true;
}
