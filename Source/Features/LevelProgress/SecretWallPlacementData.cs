namespace Game.Features.LevelProgress;

/// <summary>JSON DTO for <see cref="SecretWallPlacement"/>. Owns the mapping for this slice.</summary>
public class SecretWallPlacementData
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string Direction { get; set; } = nameof(SecretWallDirection.North);
    public int TravelTiles { get; set; } = 1;

    public static SecretWallPlacementData FromPlacement(SecretWallPlacement placement) => new()
    {
        TileX = placement.TileX,
        TileY = placement.TileY,
        Direction = placement.Direction.ToString(),
        TravelTiles = placement.TravelTiles
    };

    public SecretWallPlacement ToPlacement() => new()
    {
        TileX = TileX,
        TileY = TileY,
        Direction = ParseDirection(Direction),
        TravelTiles = TravelTiles > 0 ? TravelTiles : 1
    };

    private static SecretWallDirection ParseDirection(string direction) =>
        Enum.TryParse<SecretWallDirection>(direction, ignoreCase: true, out var result)
            ? result
            : SecretWallDirection.North;
}
