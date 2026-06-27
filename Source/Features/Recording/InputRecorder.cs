using System.Numerics;

namespace Game.Features.Recording;

public sealed class InputRecorder
{
    private readonly List<InputEvent> _events = new();
    private readonly Dictionary<GameplayKey, bool> _held = new();
    private float _elapsed;

    public IReadOnlyList<InputEvent> Events => _events;
    public float Duration => _elapsed;

    public void Reset()
    {
        _events.Clear();
        _held.Clear();
        _elapsed = 0f;
    }

    public void CaptureFrame(InputPollResult poll, float deltaTime)
    {
        _elapsed += deltaTime;

        RecordHeldTransition(GameplayKey.MoveForward, poll.InputState.MoveForward);
        RecordHeldTransition(GameplayKey.MoveBackward, poll.InputState.MoveBackward);
        RecordHeldTransition(GameplayKey.MoveLeft, poll.InputState.MoveLeft);
        RecordHeldTransition(GameplayKey.MoveRight, poll.InputState.MoveRight);
        RecordHeldTransition(GameplayKey.PrimaryFire, poll.InputState.IsPrimaryFireHeld);

        if (poll.InputState.IsInteractPressed)
            RecordEvent(new KeyDownEvent(_elapsed, GameplayKey.Interact));

        RecordWeaponSlot(poll.InputState.WeaponSlotPressed);

        if (poll.MouseDelta != Vector2.Zero)
        {
            RecordEvent(new MouseDeltaEvent(_elapsed, poll.MouseDelta.X, poll.MouseDelta.Y));
        }
    }

    private void RecordWeaponSlot(int slot)
    {
        if (slot == 0)
            return;

        var key = slot switch
        {
            1 => GameplayKey.WeaponSlot1,
            2 => GameplayKey.WeaponSlot2,
            3 => GameplayKey.WeaponSlot3,
            4 => GameplayKey.WeaponSlot4,
            _ => (GameplayKey?)null
        };

        if (key.HasValue)
            RecordEvent(new KeyDownEvent(_elapsed, key.Value));
    }

    private void RecordHeldTransition(GameplayKey key, bool isDown)
    {
        _held.TryGetValue(key, out bool wasDown);
        if (isDown == wasDown)
            return;

        _held[key] = isDown;
        RecordEvent(isDown
            ? new KeyDownEvent(_elapsed, key)
            : new KeyUpEvent(_elapsed, key));
    }

    private void RecordEvent(InputEvent evt)
    {
        _events.Add(evt);
    }
}
