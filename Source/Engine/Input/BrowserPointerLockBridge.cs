using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Input;

/// <summary>
/// Browser pointer-lock changes are handled outside Raylib. JS notifies us here.
/// </summary>
public static class BrowserPointerLockBridge
{
    public static Action? PointerLockAcquired;
    public static Action? PointerLockFailed;
    public static Action<bool>? PointerLockLost;
    public static Func<bool>? IsPointerLockActive;

    public static void NotifyAcquired() => PointerLockAcquired?.Invoke();

    public static void NotifyFailed() => PointerLockFailed?.Invoke();

    public static bool QueryPointerLockActive() => IsPointerLockActive?.Invoke() ?? false;

    public static void NotifyLost()
    {
        bool escapeHeld = IsKeyDown(KeyboardKey.Escape);
        PointerLockLost?.Invoke(escapeHeld);
    }
}
