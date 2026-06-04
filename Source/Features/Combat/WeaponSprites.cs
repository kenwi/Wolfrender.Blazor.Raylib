using Game.Features.Combat;
using Raylib_cs;

namespace Game.Features.Combat;

public static class WeaponSprites
{
    public static Rectangle GetFrameRect(WeaponId weaponId, int frameIndex)
    {
        var spec = WeaponCatalog.Get(weaponId).Sprite;
        frameIndex = Math.Clamp(frameIndex, 0, spec.FrameCount - 1);
        return new Rectangle(
            spec.OriginX + frameIndex * PlayerWeaponSpriteLayout.FrameStride,
            spec.OriginY,
            PlayerWeaponSpriteLayout.FrameSize,
            PlayerWeaponSpriteLayout.FrameSize);
    }
}
