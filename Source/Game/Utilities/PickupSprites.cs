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

    public static Rectangle GetFrameRect(PickupType type) => type switch
    {
        PickupType.Health => new Rectangle(HealthX, HealthY, FrameSize, FrameSize),
        _ => new Rectangle(PlaceholderX, PlaceholderY, FrameSize, FrameSize)
    };
}
