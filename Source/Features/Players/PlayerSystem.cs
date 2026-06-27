using System.Numerics;
using Game.Features.Animation;
using Game.Features.Combat;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Pickups;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Players;

/// <summary>
/// Orchestrates player update order.
/// Alive order: exit → secret → cooldown → velocity → movement → collision → pickup → camera → doors → animation → weapon switch → fire.
/// </summary>
public sealed class PlayerSystem
{
    private readonly Player _player;
    private readonly InputSystem _inputSystem;
    private readonly CollisionSystem _collisionSystem;
    private readonly CameraSystem _cameraSystem;
    private readonly PickupSystem _pickupSystem;
    private readonly DoorSystem _doorSystem;
    private readonly AnimationSystem _animationSystem;
    private readonly EnemySystem _enemySystem;
    private readonly WeaponSystem _weaponSystem;
    private readonly EffectSystem _effectSystem;
    private readonly ExitSystem _exitSystem;
    private readonly SecretSystem _secretSystem;

    private bool _deathHandled;
    private bool _levelCompleteHandled;
    private Func<bool> _isConsoleOpen = () => false;
    private Func<bool> _isHighscoreBlockingRestart = () => false;
    private Action? _restartLevel;

    public PlayerSystem(
        Player player,
        InputSystem inputSystem,
        CollisionSystem collisionSystem,
        CameraSystem cameraSystem,
        PickupSystem pickupSystem,
        DoorSystem doorSystem,
        AnimationSystem animationSystem,
        EnemySystem enemySystem,
        WeaponSystem weaponSystem,
        EffectSystem effectSystem,
        ExitSystem exitSystem,
        SecretSystem secretSystem)
    {
        _player = player;
        _inputSystem = inputSystem;
        _collisionSystem = collisionSystem;
        _cameraSystem = cameraSystem;
        _pickupSystem = pickupSystem;
        _doorSystem = doorSystem;
        _animationSystem = animationSystem;
        _enemySystem = enemySystem;
        _weaponSystem = weaponSystem;
        _effectSystem = effectSystem;
        _exitSystem = exitSystem;
        _secretSystem = secretSystem;
    }

    public Player Player => _player;

    public void ConfigureLifecycle(Func<bool> isConsoleOpen, Action restartLevel, Func<bool>? isHighscoreBlockingRestart = null)
    {
        _isConsoleOpen = isConsoleOpen;
        _restartLevel = restartLevel;
        _isHighscoreBlockingRestart = isHighscoreBlockingRestart ?? (() => false);
    }

    public void ResetForLevelLoad(MapData mapData)
    {
        PlayerSpawn.ApplyFromMap(_player, mapData, PlayerSpawnApplyMode.FullReset);
        _deathHandled = false;
        _levelCompleteHandled = false;
        _cameraSystem.ResetDeathFall();
    }

    public void ResetFromMap(MapData mapData) =>
        ResetForLevelLoad(mapData);

    public void UpdateAlive(float deltaTime, InputState input, Vector2 mouseDelta, int screenWidth, int screenHeight)
    {
        if (_exitSystem.IsLevelComplete)
        {
            HandleLevelCompleteOnce();
            TryRestartFromLevelComplete();
            return;
        }

        bool exitConsumedInteract = _exitSystem.Update(deltaTime, input, _player);
        if (_exitSystem.IsBlockingGameplay)
            return;

        var secretInput = exitConsumedInteract ? input.WithoutInteract() : input;
        bool secretConsumedInteract = _secretSystem.Update(deltaTime, secretInput, _player);
        var doorInput = secretConsumedInteract ? secretInput.WithoutInteract() : secretInput;

        _weaponSystem.Update(deltaTime);
        _player.WeaponCooldownRemaining = MathF.Max(0f, _player.WeaponCooldownRemaining - deltaTime);

        UpdateVelocity(deltaTime, input);
        MoveWithCollision(deltaTime);
        _pickupSystem.Update(_player);

        var camera = _player.Camera;
        _cameraSystem.Update(ref camera, _player.Position, input.IsMouseFree, mouseDelta);
        _player.Camera = camera;
        _doorSystem.Update(deltaTime, doorInput, _player, _enemySystem.Enemies);

        bool wantsFire = !input.IsMouseFree && WantsPrimaryFire(input, _player);
        var activeDef = WeaponCatalog.Get(_player.Weapons.ActiveWeapon);
        _animationSystem.SetSustainedFireLoop(wantsFire && activeDef.LoopFireAnimation);
        _animationSystem.Update(deltaTime);

        if (input.WeaponSlotPressed > 0)
            _weaponSystem.TrySwitchToSlot(_player, input.WeaponSlotPressed);

        if (wantsFire)
            _weaponSystem.TryFire(_player, screenWidth, screenHeight);
    }

    public void UpdateDead(float deltaTime, InputState input, Vector2 mouseDelta)
    {
        HandleDeathOnce();
        _player.Velocity = Vector3.Zero;

        var camera = _player.Camera;
        _cameraSystem.UpdateDeathFall(ref camera, _player.Position, deltaTime);
        _player.Camera = camera;
        _doorSystem.Update(deltaTime, input, _player, _enemySystem.Enemies);
        _animationSystem.Update(deltaTime);
        TryRestartFromGameOver();
    }

    private void UpdateVelocity(float deltaTime, InputState input)
    {
        Vector3 moveDirection = _inputSystem.GetMoveDirection(_player.Camera, input);
        Vector3 targetVelocity = moveDirection * _player.MoveSpeed;
        float rate = targetVelocity.LengthSquared() > 0f ? _player.MoveAcceleration : _player.MoveDeceleration;
        _player.Velocity = MoveToward(_player.Velocity, targetVelocity, rate * deltaTime);
    }

    private static Vector3 MoveToward(Vector3 current, Vector3 target, float maxDelta)
    {
        Vector3 delta = target - current;
        float distance = delta.Length();
        if (distance <= maxDelta || distance <= 0f)
            return target;

        return current + delta / distance * maxDelta;
    }

    /// <summary>Advance position by velocity, then resolve walls/objects/doors with axis sliding.</summary>
    private void MoveWithCollision(float deltaTime)
    {
        _player.OldPosition = _player.Position;
        if (_player.Velocity.LengthSquared() <= 0f)
            return;

        Vector3 desired = _player.Position + _player.Velocity * deltaTime;
        _player.Position = _collisionSystem.ResolveMovement(_player.OldPosition, desired, _player.CollisionRadius);
    }

    private void HandleDeathOnce()
    {
        if (_deathHandled)
            return;

        _deathHandled = true;
        _effectSystem.EnableDeathOverlay();
        _inputSystem.EnableMouse();
    }

    private void TryRestartFromGameOver()
    {
        if (_isConsoleOpen())
            return;

        bool restartPressed = IsKeyPressed(KeyboardKey.R)
            || IsMouseButtonPressed(MouseButton.Left);

        if (!restartPressed)
            return;

        _restartLevel?.Invoke();
        ApplyMouseCaptureAfterRestart();
    }

    private void HandleLevelCompleteOnce()
    {
        if (_levelCompleteHandled)
            return;

        _levelCompleteHandled = true;
        _inputSystem.EnableMouse();
    }

    private void TryRestartFromLevelComplete()
    {
        if (_isConsoleOpen() || _isHighscoreBlockingRestart())
            return;

        bool restartPressed = IsKeyPressed(KeyboardKey.R)
            || IsMouseButtonPressed(MouseButton.Left);

        if (!restartPressed)
            return;

        _restartLevel?.Invoke();
        ApplyMouseCaptureAfterRestart();
    }

    private void ApplyMouseCaptureAfterRestart()
    {
        if (OperatingSystem.IsBrowser())
            _inputSystem.EnableMouse();
        else
            _inputSystem.DisableMouse();
    }

    private static bool WantsPrimaryFire(InputState input, Player player)
    {
        var def = WeaponCatalog.Get(player.Weapons.ActiveWeapon);
        return def.HoldToFire ? input.IsPrimaryFireHeld : input.IsPrimaryFire;
    }
}
