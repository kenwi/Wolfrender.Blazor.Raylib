using System.Numerics;
using Game.Entities;
using Game.Utilities;
using Raylib_cs;

namespace Game.Systems;

/// <summary>
/// Orchestrates player update order. Does not replace <see cref="MovementSystem"/> / <see cref="CameraSystem"/>.
/// Alive order: cooldown → velocity → movement → collision → pickup → camera → doors → animation → weapon switch → fire.
/// </summary>
public sealed class PlayerSystem
{
    private readonly Player _player;
    private readonly InputSystem _inputSystem;
    private readonly MovementSystem _movementSystem;
    private readonly CollisionSystem _collisionSystem;
    private readonly CameraSystem _cameraSystem;
    private readonly PickupSystem _pickupSystem;
    private readonly DoorSystem _doorSystem;
    private readonly AnimationSystem _animationSystem;
    private readonly EnemySystem _enemySystem;
    private readonly WeaponSystem _weaponSystem;
    private readonly EffectSystem _effectSystem;

    public PlayerSystem(
        Player player,
        InputSystem inputSystem,
        MovementSystem movementSystem,
        CollisionSystem collisionSystem,
        CameraSystem cameraSystem,
        PickupSystem pickupSystem,
        DoorSystem doorSystem,
        AnimationSystem animationSystem,
        EnemySystem enemySystem,
        WeaponSystem weaponSystem,
        EffectSystem effectSystem)
    {
        _player = player;
        _inputSystem = inputSystem;
        _movementSystem = movementSystem;
        _collisionSystem = collisionSystem;
        _cameraSystem = cameraSystem;
        _pickupSystem = pickupSystem;
        _doorSystem = doorSystem;
        _animationSystem = animationSystem;
        _enemySystem = enemySystem;
        _weaponSystem = weaponSystem;
        _effectSystem = effectSystem;
    }

    public Player Player => _player;

    public void ResetFromMap(MapData mapData) =>
        PlayerSpawn.ApplyFromMap(_player, mapData, PlayerSpawnApplyMode.FullReset);

    public void UpdateAlive(float deltaTime, InputState input, Vector2 mouseDelta, int screenWidth, int screenHeight)
    {
        _player.WeaponCooldownRemaining = MathF.Max(0f, _player.WeaponCooldownRemaining - deltaTime);

        _player.Velocity = _inputSystem.GetMoveDirection(_player) * _player.MoveSpeed;
        _movementSystem.Update(_player, deltaTime);
        _collisionSystem.Update(_player, deltaTime);
        _pickupSystem.Update(_player);
        _cameraSystem.Update(_player, input.IsMouseFree, mouseDelta);
        _doorSystem.Update(deltaTime, input, _player, _enemySystem.Enemies);
        _animationSystem.Update(deltaTime);

        if (input.WeaponSlotPressed > 0)
            _weaponSystem.TrySwitchToSlot(_player, input.WeaponSlotPressed);

        if (input.IsPrimaryFire && !input.IsMouseFree)
            _weaponSystem.TryFire(_player, screenWidth, screenHeight);
    }

    public void UpdateDead(float deltaTime, InputState input, Vector2 mouseDelta)
    {
        _player.Velocity = Vector3.Zero;
        _cameraSystem.UpdateDeathFall(_player, deltaTime);
        _doorSystem.Update(deltaTime, input, _player, _enemySystem.Enemies);
        _animationSystem.Update(deltaTime);
    }

    public void HandleDeathOnce(ref bool deathHandled, InputSystem inputSystem)
    {
        if (deathHandled)
            return;

        deathHandled = true;
        _effectSystem.EnableDeathOverlay();
        inputSystem.EnableMouse();
    }
}
