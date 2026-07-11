namespace Game.Engine.Input;

/// <summary>
/// Browser pointer-lock state is read from JS via poll/query delegates wired by the Blazor host.
/// </summary>
public static class BrowserPointerLockBridge
{
    public static Func<bool>? IsPointerLockActive;
    public static Func<string?>? ConsumePointerLockEvent;
    public static Action? RequestPointerLock;
    public static Action<bool>? SetMovementCaptureArmed;

    /// <summary>When true, the browser host may request pointer lock on WASD keydown.</summary>
    public static bool MovementCaptureArmed { get; set; }

    public static bool QueryPointerLockActive() => IsPointerLockActive?.Invoke() ?? false;

    public static string? PollPointerLockEvent() => ConsumePointerLockEvent?.Invoke();
}
