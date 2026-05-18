using System.Numerics;
using Game.Entities;
using Game.Utilities;
using Raylib_cs;

namespace Game.Systems;

public class AnimationSystem
{
    private readonly Texture2D _enemyTexture;
    private readonly Texture2D _weaponTexture;
    private readonly Player _player;
    private readonly EnemySystem _enemySystem;

    private int _pistolFrameIndex;
    private bool _pistolAnimating;
    private float _pistolFrameTimer;
    private const float PistolFrameDuration = 0.07f;

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

    /// <summary>Start the pistol fire cycle (frames 1–4, then return to idle frame 0).</summary>
    public void PlayPistolFire()
    {
        _pistolFrameIndex = 1;
        _pistolAnimating = true;
        _pistolFrameTimer = 0f;
    }

    public void Update(float deltaTime)
    {
        UpdateEnemies(deltaTime);
        UpdatePlayerWeapon(deltaTime);
    }

    private void UpdatePlayerWeapon(float deltaTime)
    {
        if (!_pistolAnimating)
        {
            _pistolFrameIndex = 0;
            return;
        }

        _pistolFrameTimer += deltaTime;
        while (_pistolFrameTimer >= PistolFrameDuration && _pistolAnimating)
        {
            _pistolFrameTimer -= PistolFrameDuration;
            _pistolFrameIndex++;
            if (_pistolFrameIndex >= PlayerWeaponSprites.PistolFrameCount)
            {
                _pistolFrameIndex = 0;
                _pistolAnimating = false;
            }
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

    /// <summary>Billboard enemy sprites in the 3D scene.</summary>
    public void Render()
    {
        foreach (var enemy in _enemySystem.Enemies)
        {
            PrimitiveRenderer.DrawSpriteTexture(_enemyTexture,
                enemy.Position,
                _player.Camera.Position,
                Color.White,
                frameRect: enemy.FrameRect);
        }
    }

    /// <summary>First-person weapon overlay drawn in screen space after the 3D view.</summary>
    public void RenderWeaponOverlay(int screenWidth, int screenHeight)
    {
        var src = PlayerWeaponSprites.PistolFrameRect(_pistolFrameIndex);
        float scale = PlayerWeaponSprites.ScreenOverlayScale;
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
