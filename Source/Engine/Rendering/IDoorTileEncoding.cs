namespace Game.Engine.Rendering;

/// <summary>
/// Recognizes door portal tiles on <see cref="Game.Core.Level.MapData.Doors"/>.
/// Implemented by Features/Doors so rendering never imports door feature types.
/// </summary>
public interface IDoorTileEncoding
{
    bool IsDoorTile(uint doorLayerValue);
}
