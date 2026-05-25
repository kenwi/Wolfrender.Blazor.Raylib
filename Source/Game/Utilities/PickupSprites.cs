using Game.Entities;
using Raylib_cs;

namespace Game.Utilities;

/// <summary>Sprite sheet layout for <c>Objects.png</c> (64×64 pickup frames).</summary>
public static class PickupSprites
{
    public const int FrameSize = 64;

    public const int PlaceholderX = 2;
    public const int PlaceholderY = 35;

    public const int HealthX = 66;
    public const int HealthY = 756;

    public const int AmmoX = 1;
    public const int AmmoY = 829 + 5;

    public const int MachineGunX = 66;
    public const int MachineGunY = 829 + 5;

    public const int GoldKeyX = 1;
    public const int GoldKeyY = 989 + 5;

    public const int SilverKeyX = 66;
    public const int SilverKeyY = 995;

    public static Rectangle GetFrameRect(PickupType type) => type switch
    {
        PickupType.Health => new Rectangle(HealthX, HealthY, FrameSize, FrameSize),
        PickupType.Ammo => new Rectangle(AmmoX, AmmoY, FrameSize, FrameSize),
        PickupType.MachineGun => new Rectangle(MachineGunX, MachineGunY, FrameSize, FrameSize),
        PickupType.GoldKey => new Rectangle(GoldKeyX, GoldKeyY, FrameSize, FrameSize),
        PickupType.SilverKey => new Rectangle(SilverKeyX, SilverKeyY, FrameSize, FrameSize),
        _ => new Rectangle(PlaceholderX, PlaceholderY, FrameSize, FrameSize)
    };
}
