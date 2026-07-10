using Game.Engine.Input;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Features.Hud;

/// <summary>First-session and on-demand controls overlay.</summary>
public sealed class ControlsIntroSystem
{
    private bool _sessionIntroComplete;
    private bool _manualOpen;

    public bool IsBlockingIntro => !_sessionIntroComplete;
    public bool IsManualOpen => _manualOpen;
    public bool IsVisible => IsBlockingIntro || IsManualOpen;

    public void Dismiss()
    {
        _sessionIntroComplete = true;
        _manualOpen = false;
    }

    public void ToggleManual(InputSystem inputSystem)
    {
        if (!_sessionIntroComplete)
            return;

        _manualOpen = !_manualOpen;
        if (_manualOpen)
            inputSystem.EnableMouse();
        else
            inputSystem.RestoreGameplayMouse();
    }

    public void CloseManual(InputSystem inputSystem)
    {
        if (!_manualOpen)
            return;

        _manualOpen = false;
        inputSystem.RestoreGameplayMouse();
    }

    /// <returns>True while the first-play intro still blocks gameplay.</returns>
    public bool UpdateBlockingIntro(InputSystem inputSystem)
    {
        if (_sessionIntroComplete)
            return false;

        if (!inputSystem.IsMouseFree)
            inputSystem.EnableMouse();

        if (!IsMovementKeyPressed())
            return true;

        _sessionIntroComplete = true;
        inputSystem.RestoreGameplayMouse();
        return false;
    }

    private static bool IsMovementKeyPressed() =>
        IsKeyPressed(KeyboardKey.W)
        || IsKeyPressed(KeyboardKey.A)
        || IsKeyPressed(KeyboardKey.S)
        || IsKeyPressed(KeyboardKey.D);
}
