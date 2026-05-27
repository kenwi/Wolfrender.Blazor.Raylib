namespace Game.Entities;

/// <summary>Runtime exit placement discovered from the wall layer at level load.</summary>
public sealed class ExitTile
{
    public int TileX { get; init; }
    public int TileY { get; init; }
    public bool IsActivated { get; set; }
}
