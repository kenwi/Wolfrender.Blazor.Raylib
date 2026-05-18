using Raylib_cs;

namespace Game.Systems;

/// <summary>Sprite sheet layout for <c>weapons2.png</c> (64×64 cells, 1px column gap).</summary>
public static class PlayerWeaponSprites
{
    public const int FrameSize = 64;
    /// <summary>Horizontal distance between frame origins (64px art + 2px gap; frame 0 at x=1, frame 1 at x=67).</summary>
    public const int FrameStride = 66;

    public const int PistolRowY = 96;
    public const int PistolOriginX = 1;
    public const int PistolFrameCount = 5;

    /// <summary>Weapon overlay size relative to the viewport (1 = full screen, centered).</summary>
    public const float ScreenOverlayScale = 1f;

    public static Rectangle PistolFrameRect(int frameIndex)
    {
        frameIndex = Math.Clamp(frameIndex, 0, PistolFrameCount - 1);
        return new Rectangle(
            (1 + frameIndex) + (frameIndex * FrameSize),
            PistolRowY,
            FrameSize,
            FrameSize);
    }
}
