namespace Game.Features.LevelProgress;

/// <summary>Authoring record for a push-wall secret on the wall tile grid.</summary>
public class SecretWallPlacement
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public SecretWallDirection Direction { get; set; } = SecretWallDirection.North;
    public int TravelTiles { get; set; } = 1;
}
