namespace Game.Features.Pickups;

/// <summary>Authoring / serialization record for a pickup on the tile grid.</summary>
public class PickupPlacement
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public PickupType Type { get; set; }
    /// <summary>0 = use <see cref="PickupDefaults"/> for this type.</summary>
    public int Amount { get; set; }
}
