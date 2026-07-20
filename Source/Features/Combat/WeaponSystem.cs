using System.Numerics;
using Game.Features.Animation;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.Players;
using Game.Features.SoundPropagation;
using Raylib_cs;

namespace Game.Features.Combat;

public sealed class WeaponSystem
{
    private const float NoAmmoHintDurationSeconds = 2.5f;

    private float _noAmmoHintRemaining;
    private string _noAmmoHintTitle = string.Empty;

    private static readonly Color NoAmmoBannerAccent = new(255, 220, 40, 255);

    private readonly MapData _mapData;
    private readonly DoorSystem _doorSystem;
    private readonly EnemySystem _enemySystem;
    private readonly Texture2D _enemySpriteSheet;
    private readonly EffectSystem _effectSystem;
    private readonly SoundSystem _soundSystem;
    private readonly AnimationSystem _animationSystem;
    private readonly SoundPropagationSystem _soundPropagationSystem;

    public WeaponSystem(
        MapData mapData,
        DoorSystem doorSystem,
        EnemySystem enemySystem,
        Texture2D enemySpriteSheet,
        EffectSystem effectSystem,
        SoundSystem soundSystem,
        AnimationSystem animationSystem,
        SoundPropagationSystem soundPropagationSystem)
    {
        _mapData = mapData;
        _doorSystem = doorSystem;
        _enemySystem = enemySystem;
        _enemySpriteSheet = enemySpriteSheet;
        _effectSystem = effectSystem;
        _soundSystem = soundSystem;
        _animationSystem = animationSystem;
        _soundPropagationSystem = soundPropagationSystem;
    }

    public bool HasNoAmmoHint => _noAmmoHintRemaining > 0f;
    public string NoAmmoHintSubtitle => "NO AMMO";
    public string NoAmmoHintTitle => _noAmmoHintTitle;
    public Color NoAmmoHintColor => NoAmmoBannerAccent;

    public void Update(float deltaTime)
    {
        if (_noAmmoHintRemaining > 0f)
            _noAmmoHintRemaining = MathF.Max(0f, _noAmmoHintRemaining - deltaTime);
    }

    public void TrySwitchToSlot(Player player, int slot)
    {
        var weaponId = WeaponCatalog.GetWeaponForSlot(slot);
        if (weaponId is null)
            return;

        if (!player.Weapons.IsOwned(weaponId.Value))
            return;

        var def = WeaponCatalog.Get(weaponId.Value);
        if (def.UsesAmmo && player.Ammo < def.AmmoPerShot)
        {
            ShowNoAmmoHint(weaponId.Value);
            return;
        }

        if (player.Weapons.ActiveWeapon == weaponId.Value)
            return;

        player.Weapons.TrySetActive(weaponId.Value);
        _animationSystem.ResetWeaponOverlayToIdle();
    }

    private void ShowNoAmmoHint(WeaponId weaponId)
    {
        var def = WeaponCatalog.Get(weaponId);
        _noAmmoHintTitle = $"CANNOT SWITCH TO {def.DisplayName}";
        _noAmmoHintRemaining = NoAmmoHintDurationSeconds;
        Debug.Log($"{_noAmmoHintTitle} (out of ammo).");
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
        {
            if (TrySwitchToKnifeForEmptyGun(player))
                ExecuteFire(player, screenWidth, screenHeight);
            return;
        }

        ExecuteFire(player, screenWidth, screenHeight);
    }

    /// <summary>Wolf-style: empty gun + fire attempts melee with knife.</summary>
    private bool TrySwitchToKnifeForEmptyGun(Player player)
    {
        var active = WeaponCatalog.Get(player.Weapons.ActiveWeapon);
        if (!active.UsesAmmo || player.Ammo >= active.AmmoPerShot)
            return false;

        if (player.Weapons.ActiveWeapon == WeaponId.Knife)
            return false;

        if (!player.Weapons.TrySetActive(WeaponId.Knife))
            return false;

        _animationSystem.ResetWeaponOverlayToIdle();
        return CanFire(player);
    }

    private void ExecuteFire(Player player, int screenWidth, int screenHeight)
    {
        var def = WeaponCatalog.Get(player.Weapons.ActiveWeapon);
        ICombatTarget? hit = null;

        // List<Enemy> is covariant as IReadOnlyList<ICombatTarget> via Enemy : ICombatTarget.
        IReadOnlyList<ICombatTarget> targets = _enemySystem.Enemies;

        if (def.Kind == WeaponKind.Hitscan)
        {
            if (Hitscan.TryHitEnemyScreenRay(
                    _mapData,
                    _doorSystem.Doors,
                    player.Camera,
                    screenWidth,
                    screenHeight,
                    targets,
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
                    targets,
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
        _soundSystem.PlaySfx(def.FireSoundPath);

        if (def.Kind == WeaponKind.Hitscan)
            _soundPropagationSystem.EmitPlayerGunshot(player.Position);
    }
}
