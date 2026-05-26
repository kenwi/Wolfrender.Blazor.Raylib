using Game.Entities;
using Raylib_cs;

namespace Game.Utilities;

public static class PickupVisuals
{
    public static Color GetColor(PickupType type) => type switch
    {
        PickupType.Health => new Color(40, 220, 80, 180),
        PickupType.Ammo => new Color(255, 220, 40, 180),
        PickupType.MachineGun => new Color(255, 140, 0, 180),
        PickupType.ChainGun => new Color(255, 90, 20, 180),
        PickupType.GoldKey => new Color(255, 210, 40, 200),
        PickupType.SilverKey => new Color(200, 220, 255, 200),
        _ => new Color(255, 255, 255, 180)
    };

    public static string GetLabel(PickupType type) => type switch
    {
        PickupType.Health => "H",
        PickupType.Ammo => "A",
        PickupType.MachineGun => "MG",
        PickupType.ChainGun => "CG",
        PickupType.GoldKey => "GK",
        PickupType.SilverKey => "SK",
        _ => "?"
    };
}
