namespace Game.Features.Pickups;

/// <summary>JSON DTO for <see cref="PickupPlacement"/>.</summary>
public class PickupPlacementData
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string Type { get; set; } = "Health";
    public int Amount { get; set; }
}
