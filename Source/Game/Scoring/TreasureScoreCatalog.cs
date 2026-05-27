using Game.Entities;

namespace Game.Scoring;

/// <summary>Treasure pickup point values (Phase 1b). Non-treasure types return 0.</summary>
public static class TreasureScoreCatalog
{
    public static bool IsTreasure(PickupType type) => type switch
    {
        PickupType.TreasureCross => true,
        PickupType.TreasureChalice => true,
        PickupType.TreasureChest => true,
        PickupType.TreasureCrown => true,
        _ => false
    };

    public static int GetPoints(PickupType type) => type switch
    {
        PickupType.TreasureCross => 100,
        PickupType.TreasureChalice => 500,
        PickupType.TreasureChest => 1_000,
        PickupType.TreasureCrown => 5_000,
        _ => 0
    };
}
