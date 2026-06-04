using System.Numerics;
using Game.Features.Combat;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Players;

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
    private readonly ExitSystem _exitSystem;

    private bool _deathHandled;
    private bool _levelCompleteHandled;
    private Func<bool> _isConsoleOpen = () => false;
    private Action? _restartLevel;

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
        EffectSystem effectSystem,
        ExitSystem exitSystem)
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
        _exitSystem = exitSystem;
    }

    public Player Player => _player;

    public void ConfigureLifecycle(Func<bool> isConsoleOpen, Action restartLevel)
    {
        _isConsoleOpen = isConsoleOpen;
        _restartLevel = restartLevel;
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

        var doorInput = exitConsumedInteract ? input.WithoutInteract() : input;

        _weaponSystem.Update(deltaTime);
        _player.WeaponCooldownRemaining = MathF.Max(0f, _player.WeaponCooldownRemaining - deltaTime);

        _player.Velocity = _inputSystem.GetMoveDirection(_player) * _player.MoveSpeed;
        _movementSystem.Update(_player, deltaTime);
        _collisionSystem.Update(_player, deltaTime);
        _pickupSystem.Update(_player);
        _cameraSystem.Update(_player, input.IsMouseFree, mouseDelta);
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
        _cameraSystem.UpdateDeathFall(_player, deltaTime);
        _doorSystem.Update(deltaTime, input, _player, _enemySystem.Enemies);
        _animationSystem.Update(deltaTime);
        TryRestartFromGameOver();
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
        if (_isConsoleOpen())
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
