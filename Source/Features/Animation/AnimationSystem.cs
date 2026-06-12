using System.Numerics;
using Game.Features.Combat;
using Game.Features.Enemies;
using Game.Features.Players;
using Raylib_cs;

namespace Game.Features.Animation;

public class AnimationSystem
{
    private readonly Texture2D _enemyTexture;
    private readonly Texture2D _weaponTexture;
    private readonly Player _player;
    private readonly EnemySystem _enemySystem;

    private WeaponId _overlayWeaponId = WeaponId.Knife;
    private int _weaponFrameIndex;
    private bool _weaponAnimating;
    private float _weaponFrameTimer;
    private bool _sustainedFireLoop;

    public AnimationSystem(
        Texture2D enemyTexture,
        Texture2D weaponTexture,
        Player player,
        EnemySystem enemySystem)
    {
        _enemyTexture = enemyTexture;
        _weaponTexture = weaponTexture;
        _player = player;
        _enemySystem = enemySystem;
    }

    /// <summary>Hold-to-fire weapons with <see cref="WeaponDefinition.LoopFireAnimation"/> use this instead.</summary>
    public void SetSustainedFireLoop(bool active) => _sustainedFireLoop = active;

    public void PlayWeaponFire(WeaponId weaponId)
    {
        var def = WeaponCatalog.Get(weaponId);
        if (def.LoopFireAnimation)
        {
            _overlayWeaponId = weaponId;
            if (_weaponFrameIndex < 1)
                _weaponFrameIndex = 1;
            _weaponAnimating = true;
            return;
        }

        _overlayWeaponId = weaponId;
        _weaponFrameIndex = 1;
        _weaponAnimating = true;
        _weaponFrameTimer = 0f;
    }

    /// <summary>Legacy alias.</summary>
    public void PlayPistolFire() => PlayWeaponFire(WeaponId.Pistol);

    public void ResetWeaponOverlayToIdle()
    {
        _overlayWeaponId = _player.Weapons.ActiveWeapon;
        _weaponFrameIndex = 0;
        _weaponAnimating = false;
        _weaponFrameTimer = 0f;
    }

    public void Update(float deltaTime)
    {
        UpdateEnemies(deltaTime);
        UpdatePlayerWeapon(deltaTime);
    }

    private void UpdatePlayerWeapon(float deltaTime)
    {
        if (_sustainedFireLoop)
        {
            UpdateLoopingFireAnimation(deltaTime);
            return;
        }

        if (_weaponAnimating && WeaponCatalog.Get(_overlayWeaponId).LoopFireAnimation)
        {
            _weaponAnimating = false;
            _weaponFrameIndex = 0;
            _weaponFrameTimer = 0f;
            _overlayWeaponId = _player.Weapons.ActiveWeapon;
        }

        if (!_weaponAnimating)
        {
            _overlayWeaponId = _player.Weapons.ActiveWeapon;
            _weaponFrameIndex = 0;
            return;
        }

        var def = WeaponCatalog.Get(_overlayWeaponId);
        float frameDuration = def.GetFireFrameDurationSeconds();

        _weaponFrameTimer += deltaTime;
        while (_weaponFrameTimer >= frameDuration && _weaponAnimating)
        {
            _weaponFrameTimer -= frameDuration;
            _weaponFrameIndex++;
            if (_weaponFrameIndex >= def.Sprite.FrameCount)
            {
                _weaponFrameIndex = 0;
                _weaponAnimating = false;
                _overlayWeaponId = _player.Weapons.ActiveWeapon;
            }
        }
    }

    /// <summary>Frames 1..FrameCount-1 loop while fire is held (frame 0 = idle).</summary>
    private void UpdateLoopingFireAnimation(float deltaTime)
    {
        var weaponId = _player.Weapons.ActiveWeapon;
        var def = WeaponCatalog.Get(weaponId);
        float frameDuration = def.GetFireFrameDurationSeconds();

        _overlayWeaponId = weaponId;
        _weaponAnimating = true;
        if (_weaponFrameIndex < 1)
            _weaponFrameIndex = 1;

        _weaponFrameTimer += deltaTime;
        while (_weaponFrameTimer >= frameDuration)
        {
            _weaponFrameTimer -= frameDuration;
            _weaponFrameIndex++;
            if (_weaponFrameIndex >= def.Sprite.FrameCount)
                _weaponFrameIndex = 1;
        }
    }

    private void UpdateEnemies(float deltaTime)
    {
        var spriteSize = 64;
        var padding = 1;

        foreach (var enemy in _enemySystem?.Enemies ?? new List<Enemy>())
        {
            var frameColumnIndex = enemy.FrameColumnIndex;
            var frameRowIndex = enemy.FrameRowIndex;

            var currentColumnPixel = (frameColumnIndex % 8) * (spriteSize + padding);
            var currentRowPixel = (frameRowIndex % 5) * (spriteSize + padding);
            var currentAnimationSpeed = 1f;

            switch (enemy.EnemyState)
            {
                case EnemyState.IDLE:
                case EnemyState.COLLIDING:
                    enemy.FrameRowIndex = 0;
                    break;
                case EnemyState.WALKING:
                    if (enemy.AnimationTimer >= 1)
                    {
                        enemy.FrameRowIndex++;
                        enemy.AnimationTimer = 0;
                    }
                    currentRowPixel = (1 + frameRowIndex % 4) * (spriteSize + padding);
                    currentAnimationSpeed = 2f;
                    break;
                case EnemyState.NOTICING:
                case EnemyState.SEARCHING:
                    currentColumnPixel = 0 * (spriteSize + padding);
                    currentRowPixel = 6 * (spriteSize + padding);
                    break;
                case EnemyState.ATTACKING:
                    if (enemy.AnimationTimer >= 1)
                    {
                        enemy.AnimationTimer = 0;
                        enemy.ShootingAnimationIndex++;
                    }
                    currentColumnPixel = (1 + enemy.ShootingAnimationIndex % 2) * (spriteSize + padding);
                    currentRowPixel = 6 * (spriteSize + padding);
                    currentAnimationSpeed = 0.5f;

                    var currentFrameColumn = 1 + enemy.ShootingAnimationIndex % 2;
                    if (currentFrameColumn != enemy.PreviousAttackFrameColumn)
                    {
                        enemy.IsShooting = currentFrameColumn == 2;
                        enemy.PreviousAttackFrameColumn = currentFrameColumn;
                    }
                    break;
                case EnemyState.HIT:
                    currentColumnPixel = 0 * (spriteSize + padding);
                    currentRowPixel = 5 * (spriteSize + padding);
                    break;
                case EnemyState.DYING:
                    if (enemy.DyingAnimationIndex < 4 && enemy.AnimationTimer >= 1)
                    {
                        enemy.AnimationTimer = 0;
                        enemy.DyingAnimationIndex++;
                    }
                    currentColumnPixel = enemy.DyingAnimationIndex * (spriteSize + padding);
                    currentRowPixel = 5 * (spriteSize + padding);
                    currentAnimationSpeed = 3f;
                    break;
                case EnemyState.CORPSE:
                    currentColumnPixel = 4 * (spriteSize + padding);
                    currentRowPixel = 5 * (spriteSize + padding);
                    break;
            }

            enemy.FrameRect = new Rectangle(currentColumnPixel, currentRowPixel, spriteSize, spriteSize);
            enemy.AnimationTimer += deltaTime * currentAnimationSpeed * 2;
        }
    }

    public void Render()
    {
        foreach (var enemy in _enemySystem.Enemies)
        {
            bool isCorpse = enemy.EnemyState == EnemyState.CORPSE;
            bool smoothFaceOnDeath = enemy.EnemyState == EnemyState.DYING;
            PrimitiveRenderer.DrawSpriteTexture(
                _enemyTexture,
                enemy.Position,
                _player.Camera.Position,
                Color.White,
                frameRect: enemy.FrameRect,
                quantizeToEightDirections: !(isCorpse || smoothFaceOnDeath),
                cameraViewTarget: isCorpse ? _player.Camera.Target : null,
                facingMode: isCorpse
                    ? SpriteBillboardGeometry.FacingMode.ViewAligned
                    : SpriteBillboardGeometry.FacingMode.PointAtCamera);
        }
    }

    public void RenderWeaponOverlay(int screenWidth, int screenHeight)
    {
        var src = WeaponSprites.GetFrameRect(_overlayWeaponId, _weaponFrameIndex);
        float scale = PlayerWeaponSpriteLayout.ScreenOverlayScale;
        var pixelSize = screenWidth / 64 / 1.33f;

        float destW = screenWidth * scale / 1.33f;
        float destH = screenHeight * scale;
        var dest = new Rectangle(
            (screenWidth - destW - pixelSize) * 0.5f,
            (screenHeight - destH) * 0.5f,
            destW,
            destH);
        PrimitiveRenderer.DrawScreenSprite(_weaponTexture, src, dest, Color.White);
    }
}
