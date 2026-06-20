using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Engine.Input;

/// <summary>
/// Browser pointer-lock exits (usually ESC) are handled outside Raylib. JS notifies us here.
/// </summary>
public static class BrowserPointerLockBridge
{
    public static Action<bool>? PointerLockLost;

    public static void NotifyLost()
    {
        bool escapeHeld = IsKeyDown(KeyboardKey.Escape);
        PointerLockLost?.Invoke(escapeHeld);
    }
}
