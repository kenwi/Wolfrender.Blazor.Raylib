namespace Game.Features.Enemies;

/// <summary>
/// A waypoint in an enemy patrol path, stored as tile coordinates.
/// </summary>
public class PatrolWaypoint
{
    public int TileX { get; set; }
    public int TileY { get; set; }
}
