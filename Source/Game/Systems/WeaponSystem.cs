using System.Numerics;
using Game.Entities;
using Game.Utilities;
using Game.Weapons;
using Raylib_cs;

namespace Game.Systems;

public sealed class WeaponSystem
{
    private readonly MapData _mapData;
    private readonly DoorSystem _doorSystem;
    private readonly EnemySystem _enemySystem;
    private readonly Texture2D _enemySpriteSheet;
    private readonly EffectSystem _effectSystem;
    private readonly SoundSystem _soundSystem;
    private readonly AnimationSystem _animationSystem;

    public WeaponSystem(
        MapData mapData,
        DoorSystem doorSystem,
        EnemySystem enemySystem,
        Texture2D enemySpriteSheet,
        EffectSystem effectSystem,
        SoundSystem soundSystem,
        AnimationSystem animationSystem)
    {
        _mapData = mapData;
        _doorSystem = doorSystem;
        _enemySystem = enemySystem;
        _enemySpriteSheet = enemySpriteSheet;
        _effectSystem = effectSystem;
        _soundSystem = soundSystem;
        _animationSystem = animationSystem;
    }

    public void TrySwitchToSlot(Player player, int slot)
    {
        var weaponId = WeaponCatalog.GetWeaponForSlot(slot);
        if (weaponId is null)
            return;

        if (!player.Weapons.TrySetActive(weaponId.Value))
            return;

        _animationSystem.ResetWeaponOverlayToIdle();
    }

    public bool CanFire(Player player)
    {
        if (player.WeaponCooldownRemaining > 0f)
            return false;

        var def = WeaponCatalog.Get(player.Weapons.ActiveWeapon);
        if (def.UsesAmmo && player.Ammo < def.AmmoPerShot)
            return false;

        return true;
    }

    public void TryFire(Player player, int screenWidth, int screenHeight)
    {
        if (!CanFire(player))
            return;

        var def = WeaponCatalog.Get(player.Weapons.ActiveWeapon);
        Enemy? hit = null;

        if (def.Kind == WeaponKind.Hitscan)
        {
            if (Hitscan.TryHitEnemyScreenRay(
                    _mapData,
                    _doorSystem.Doors,
                    player.Camera,
                    screenWidth,
                    screenHeight,
                    _enemySystem.Enemies,
                    _enemySpriteSheet,
                    4f,
                    4f,
                    def.MaxRangeTiles,
                    out hit) && hit is not null)
            {
                hit.ApplyDamage(def.Damage);
            }
        }
        else
        {
            var rayDir = player.Camera.Target - player.Camera.Position;
            if (Hitscan.TryHitEnemy(
                    _mapData,
                    _doorSystem.Doors,
                    player.Camera.Position,
                    rayDir,
                    _enemySystem.Enemies,
                    Hitscan.DefaultEnemyHitRadiusWorld,
                    def.MaxRangeTiles,
                    out hit) && hit is not null)
            {
                hit.ApplyDamage(def.Damage);
            }
        }

        player.WeaponCooldownRemaining = def.CooldownSeconds;
        if (def.UsesAmmo)
            player.Ammo -= def.AmmoPerShot;

        _effectSystem.TriggerReticleFireFlash();
        _animationSystem.PlayWeaponFire(player.Weapons.ActiveWeapon);
        _soundSystem.PlayWeaponFire(player.Weapons.ActiveWeapon);
    }
}
