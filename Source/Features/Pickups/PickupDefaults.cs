
namespace Game.Features.Pickups;

public static class PickupDefaults
{
    public const int HealthAmount = 25;
    public const int AmmoAmount = 30;
    public const int MachineGunAmmoAmount = 30;
    public const int ChainGunAmmoAmount = 40;

    public static int GetAmount(PickupType type, int placementAmount) =>
        placementAmount > 0 ? placementAmount : GetDefaultAmount(type);

    public static int GetDefaultAmount(PickupType type) => type switch
    {
        PickupType.Health => HealthAmount,
        PickupType.Ammo => AmmoAmount,
        PickupType.MachineGun => MachineGunAmmoAmount,
        PickupType.ChainGun => ChainGunAmmoAmount,
        PickupType.GoldKey => 0,
        PickupType.SilverKey => 0,
        PickupType.TreasureCross => 0,
        PickupType.TreasureChalice => 0,
        PickupType.TreasureChest => 0,
        PickupType.TreasureCrown => 0,
        _ => 0
    };
}
