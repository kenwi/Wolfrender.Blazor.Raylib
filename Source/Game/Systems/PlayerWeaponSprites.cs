using Game.Weapons;
using Raylib_cs;

namespace Game.Systems;

/// <summary>Legacy helpers; prefer <see cref="WeaponSprites"/>.</summary>
public static class PlayerWeaponSprites
{
    public const int FrameSize = PlayerWeaponSpriteLayout.FrameSize;
    public const int FrameStride = FrameSize + 1;
    public const int PistolRowY = PlayerWeaponSpriteLayout.PistolRowY;
    public const int PistolOriginX = PlayerWeaponSpriteLayout.PistolOriginX;
    public const int PistolFrameCount = PlayerWeaponSpriteLayout.PistolFrameCount;
    public const float ScreenOverlayScale = PlayerWeaponSpriteLayout.ScreenOverlayScale;

    public static Rectangle PistolFrameRect(int frameIndex) =>
        WeaponSprites.GetFrameRect(WeaponId.Pistol, frameIndex);
}
