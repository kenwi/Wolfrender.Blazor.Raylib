namespace Game.Features.Recording;

public interface IInputProvider
{
    InputPollResult Poll(float deltaTime);
}
