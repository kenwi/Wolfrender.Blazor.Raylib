namespace Game.Features.Enemies;

/// <summary>JSON DTO for <see cref="PatrolWaypoint"/>.</summary>
public class PatrolWaypointData
{
    public int TileX { get; set; }
    public int TileY { get; set; }

    public static PatrolWaypointData FromWaypoint(PatrolWaypoint waypoint) => new()
    {
        TileX = waypoint.TileX,
        TileY = waypoint.TileY
    };

    public PatrolWaypoint ToWaypoint() => new()
    {
        TileX = TileX,
        TileY = TileY
    };
}
