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
    private readonly ActorDoorOccupancyProbe _doorOccupancy = new();
    private readonly List<Vector3> _doorOccupancyOthers = new();

    private bool _deathHandled;
    private bool _levelCompleteHandled;
    private Func<bool> _isConsoleOpen = () => false;
    private Func<bool> _isHighscoreBlockingRestart = () => false;
    private Func<bool> _isReplaying = () => false;
    private Func<bool> _suppressLevelCompleteClickRestart = () => false;
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

    public void ConfigureLifecycle(
        Func<bool> isConsoleOpen,
        Action restartLevel,
        Func<bool>? isHighscoreBlockingRestart = null,
        Func<bool>? isReplaying = null,
        Func<bool>? suppressLevelCompleteClickRestart = null)
    {
        _isConsoleOpen = isConsoleOpen;
        _restartLevel = restartLevel;
        _isHighscoreBlockingRestart = isHighscoreBlockingRestart ?? (() => false);
        _isReplaying = isReplaying ?? (() => false);
        _suppressLevelCompleteClickRestart = suppressLevelCompleteClickRestart ?? (() => false);
    }

    public void ResetForLevelLoad(MapData mapData)
    {
        PlayerSpawn.ApplyFromMap(_player, mapData, PlayerSpawnApplyMode.FullReset);
        _player.IsFlying = false;
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
        UpdateDoors(deltaTime, doorInput);

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
        UpdateDoors(deltaTime, input);
        _animationSystem.Update(deltaTime);
        TryRestartFromGameOver();
    }

    private void UpdateDoors(float deltaTime, InputState doorInput)
    {
        _doorOccupancyOthers.Clear();
        var enemies = _enemySystem.Enemies;
        for (int i = 0; i < enemies.Count; i++)
            _doorOccupancyOthers.Add(enemies[i].Position);

        _doorOccupancy.BeginFrame(_player.Position, _doorOccupancyOthers);
        _doorSystem.Update(deltaTime, doorInput, _player.Position, _player, _doorOccupancy);
    }

    private void UpdateVelocity(float deltaTime, InputState input)
    {
        Vector3 moveDirection = _inputSystem.GetMoveDirection(_player.Camera, input);

        // Raw Raylib reads below bypass the input provider; ignore them during
        // replay so live keyboard state cannot alter deterministic playback.
        if (_player.IsFlying && !_isReplaying())
        {
            float vertical = 0f;
            if (IsKeyDown(KeyboardKey.LeftShift) || IsKeyDown(KeyboardKey.RightShift))
                vertical += 1f;
            if (IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl))
                vertical -= 1f;

            if (MathF.Abs(vertical) > 0f)
                moveDirection += new Vector3(0f, vertical, 0f);
        }

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
        if (_player.IsFlying)
        {
            _player.Position = desired;
            return;
        }

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
        if (_isConsoleOpen() || _isReplaying())
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
        if (_isConsoleOpen() || _isHighscoreBlockingRestart() || _isReplaying())
            return;

        bool restartPressed = IsKeyPressed(KeyboardKey.R)
            || (!_suppressLevelCompleteClickRestart() && IsMouseButtonPressed(MouseButton.Left));

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
