namespace Game.Engine.Input;

/// <summary>
/// Browser pointer-lock state is read from JS via poll/query delegates wired by the Blazor host.
/// </summary>
public static class BrowserPointerLockBridge
{
    public static Func<bool>? IsPointerLockActive;
    public static Func<string?>? ConsumePointerLockEvent;

    public static bool QueryPointerLockActive() => IsPointerLockActive?.Invoke() ?? false;

    public static string? PollPointerLockEvent() => ConsumePointerLockEvent?.Invoke();
}
