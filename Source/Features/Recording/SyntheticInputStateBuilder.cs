using System.Numerics;
using Game.Engine.Input;

namespace Game.Features.Recording;

public sealed class SyntheticInputStateBuilder
{
    private readonly HashSet<GameplayKey> _held = new();
    private bool _interactPressed;
    private int _weaponSlotPressed;
    private bool _primaryFirePressed;
    private Vector2 _mouseDelta;

    public void ProcessEvent(InputEvent evt)
    {
        switch (evt)
        {
            case KeyDownEvent down:
                ApplyKeyDown(down.Key);
                break;
            case KeyUpEvent up:
                _held.Remove(up.Key);
                break;
            case MouseDeltaEvent delta:
                _mouseDelta += new Vector2(delta.Dx, delta.Dy);
                break;
        }
    }

    private void ApplyKeyDown(GameplayKey key)
    {
        switch (key)
        {
            case GameplayKey.Interact:
                _interactPressed = true;
                break;
            case GameplayKey.WeaponSlot1:
                _weaponSlotPressed = 1;
                break;
            case GameplayKey.WeaponSlot2:
                _weaponSlotPressed = 2;
                break;
            case GameplayKey.WeaponSlot3:
                _weaponSlotPressed = 3;
                break;
            case GameplayKey.WeaponSlot4:
                _weaponSlotPressed = 4;
                break;
            case GameplayKey.PrimaryFire:
                _primaryFirePressed = true;
                break;
        }

        _held.Add(key);
    }

    public InputPollResult Build(bool isMouseFree = false)
    {
        var mouseDelta = isMouseFree ? Vector2.Zero : _mouseDelta;
        var state = new InputState
        {
            MoveForward = _held.Contains(GameplayKey.MoveForward),
            MoveBackward = _held.Contains(GameplayKey.MoveBackward),
            MoveLeft = _held.Contains(GameplayKey.MoveLeft),
            MoveRight = _held.Contains(GameplayKey.MoveRight),
            MouseDelta = mouseDelta,
            IsMouseFree = isMouseFree,
            IsDebugEnabled = false,
            IsPaused = false,
            IsInteractPressed = _interactPressed,
            IsChangeStatePressed = false,
            IsChangeAnimationPressed = false,
            IsMinimapEnabled = false,
            IsPrimaryFire = _primaryFirePressed,
            IsPrimaryFireHeld = _held.Contains(GameplayKey.PrimaryFire),
            WeaponSlotPressed = _weaponSlotPressed
        };

        return new InputPollResult(state, mouseDelta);
    }

    public void EndFrame()
    {
        _interactPressed = false;
        _weaponSlotPressed = 0;
        _primaryFirePressed = false;
        _mouseDelta = Vector2.Zero;
    }

    public void Reset()
    {
        _held.Clear();
        EndFrame();
    }
}
