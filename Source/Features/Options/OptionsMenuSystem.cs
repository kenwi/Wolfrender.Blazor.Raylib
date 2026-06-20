using Game.Engine.Input;

namespace Game.Features.Options;

public sealed class OptionsMenuSystem
{
    public bool IsOpen { get; private set; }
    public GameSettings Settings { get; } = GameSettings.CreateDefault();

    public void Open(InputSystem input)
    {
        if (IsOpen)
            return;

        IsOpen = true;
        input.EnableMouse();
    }

    public void Close(InputSystem input)
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        input.RestoreGameplayMouse();
    }

    public void Dismiss() => IsOpen = false;

    public OptionsMenuInput.Result HandleInput(int screenWidth, int screenHeight)
    {
        return OptionsMenuInput.Update(Settings, screenWidth, screenHeight);
    }
}
