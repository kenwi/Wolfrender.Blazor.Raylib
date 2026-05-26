using System.Numerics;

namespace Game.Entities;

public enum PickupType
{
    Health,
    Ammo,
    MachineGun,
    ChainGun,
    GoldKey,
    SilverKey
}

/// <summary>Authoring / serialization record for a pickup on the tile grid.</summary>
public class PickupPlacement
{
    public int TileX { get; set; }
    public int TileY { get; set; }
    public PickupType Type { get; set; }
    /// <summary>0 = use <see cref="PickupDefaults"/> for this type.</summary>
    public int Amount { get; set; }
}

/// <summary>Runtime pickup instance used for rendering and (later) collection.</summary>
public class Pickup
{
    public PickupType Type { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int Amount { get; set; }
    public Vector3 Position { get; set; }
}
