namespace Game.Features.Pickups;

/// <summary>JSON DTO for <see cref="PickupPlacement"/>. Owns the mapping for this slice.</summary>
public class PickupPlacementData
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string Type { get; set; } = "Health";
    public int Amount { get; set; }

    public static PickupPlacementData FromPlacement(PickupPlacement placement) => new()
    {
        TileX = placement.TileX,
        TileY = placement.TileY,
        Type = placement.Type.ToString(),
        Amount = placement.Amount
    };

    public PickupPlacement ToPlacement() => new()
    {
        TileX = TileX,
        TileY = TileY,
        Type = ParseType(Type),
        Amount = Amount
    };

    private static PickupType ParseType(string type) =>
        Enum.TryParse<PickupType>(type, ignoreCase: true, out var result)
            ? result
            : PickupType.Health;
}
