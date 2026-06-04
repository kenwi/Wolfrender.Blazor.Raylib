using System.Numerics;

namespace Game.Features.Pickups;

public enum PickupType
{
    Health,
    Ammo,
    MachineGun,
    ChainGun,
    GoldKey,
    SilverKey,
    TreasureCross,
    TreasureChalice,
    TreasureChest,
    TreasureCrown
}

/// <summary>Runtime pickup instance used for rendering and collection.</summary>
public class Pickup
{
    public PickupType Type { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int Amount { get; set; }
    public Vector3 Position { get; set; }
}
