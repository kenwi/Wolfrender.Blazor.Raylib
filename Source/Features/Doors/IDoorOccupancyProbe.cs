namespace Game.Features.Doors;

/// <summary>
/// Queries whether any actor occupies a map tile (blocks door auto-close).
/// Owned by Doors so DoorSystem stays free of Player/Enemy concrete types.
/// </summary>
public interface IDoorOccupancyProbe
{
    bool IsTileOccupied(int tileX, int tileY);
}
