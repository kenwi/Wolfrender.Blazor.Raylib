using Game.Engine.Input;

namespace Game.Features.Recording;

public sealed class LiveInputProvider : IInputProvider
{
    private readonly InputSystem _inputSystem;

    public LiveInputProvider(InputSystem inputSystem)
    {
        _inputSystem = inputSystem;
    }

    public InputPollResult Poll(float deltaTime)
    {
        var state = _inputSystem.GetInputState();
        var mouseDelta = state.MouseDelta;
        if (state.IsMouseFree)
            mouseDelta = System.Numerics.Vector2.Zero;

        return new InputPollResult(state, mouseDelta);
    }
}
