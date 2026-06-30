using System.Numerics;

namespace Game.Features.Recording;

public sealed class InputRecorder
{
    private readonly List<InputEvent> _events = new();
    private readonly Dictionary<GameplayKey, bool> _held = new();

    public IReadOnlyList<InputEvent> Events => _events;

    public float Duration =>
        _events.Count > 0 ? _events[^1].Time : 0f;

    public long DurationTicks =>
        _events.Count > 0 ? _events[^1].Tick : 0;

    public void Reset()
    {
        _events.Clear();
        _held.Clear();
    }

    public void CaptureTick(InputPollResult poll, long tickIndex, int tickHz)
    {
        float time = tickHz > 0 ? tickIndex / (float)tickHz : 0f;

        RecordHeldTransition(GameplayKey.MoveForward, poll.InputState.MoveForward, tickIndex, time);
        RecordHeldTransition(GameplayKey.MoveBackward, poll.InputState.MoveBackward, tickIndex, time);
        RecordHeldTransition(GameplayKey.MoveLeft, poll.InputState.MoveLeft, tickIndex, time);
        RecordHeldTransition(GameplayKey.MoveRight, poll.InputState.MoveRight, tickIndex, time);
        RecordHeldTransition(GameplayKey.PrimaryFire, poll.InputState.IsPrimaryFireHeld, tickIndex, time);

        if (poll.InputState.IsInteractPressed)
            RecordEvent(new KeyDownEvent(time, GameplayKey.Interact) { Tick = tickIndex });

        RecordWeaponSlot(poll.InputState.WeaponSlotPressed, tickIndex, time);

        if (poll.MouseDelta != Vector2.Zero)
        {
            RecordEvent(new MouseDeltaEvent(time, poll.MouseDelta.X, poll.MouseDelta.Y) { Tick = tickIndex });
        }
    }

    private void RecordWeaponSlot(int slot, long tickIndex, float time)
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
            RecordEvent(new KeyDownEvent(time, key.Value) { Tick = tickIndex });
    }

    private void RecordHeldTransition(GameplayKey key, bool isDown, long tickIndex, float time)
    {
        _held.TryGetValue(key, out bool wasDown);
        if (isDown == wasDown)
            return;

        _held[key] = isDown;
        RecordEvent(isDown
            ? new KeyDownEvent(time, key) { Tick = tickIndex }
            : new KeyUpEvent(time, key) { Tick = tickIndex });
    }

    private void RecordEvent(InputEvent evt)
    {
        _events.Add(evt);
    }
}
