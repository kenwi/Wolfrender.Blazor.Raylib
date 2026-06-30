namespace Game.Features.Recording;

public enum InputEventKind
{
    KeyDown,
    KeyUp,
    MouseDelta
}

public abstract record InputEvent(float Time, InputEventKind Kind)
{
    public long Tick { get; init; } = -1;

    public bool UsesTickTiming => Tick >= 0;
}

public sealed record KeyDownEvent(float Time, GameplayKey Key) : InputEvent(Time, InputEventKind.KeyDown);

public sealed record KeyUpEvent(float Time, GameplayKey Key) : InputEvent(Time, InputEventKind.KeyUp);

public sealed record MouseDeltaEvent(float Time, float Dx, float Dy) : InputEvent(Time, InputEventKind.MouseDelta);
