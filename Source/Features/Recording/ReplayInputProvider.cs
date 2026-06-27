namespace Game.Features.Recording;

public sealed class ReplayInputProvider : IInputProvider
{
    private IReadOnlyList<InputEvent> _events = Array.Empty<InputEvent>();
    private readonly SyntheticInputStateBuilder _builder = new();
    private int _nextEventIndex;
    private float _elapsed;

    public float Duration { get; private set; }
    public bool IsFinished { get; private set; }

    public void Reset(IReadOnlyList<InputEvent> events)
    {
        _events = events;
        _nextEventIndex = 0;
        _elapsed = 0f;
        IsFinished = events.Count == 0;
        Duration = events.Count > 0 ? events[^1].Time : 0f;
        _builder.Reset();
    }

    public InputPollResult Poll(float deltaTime)
    {
        if (IsFinished)
            return _builder.Build(isMouseFree: false);

        _elapsed += deltaTime;
        ApplyEventsUpTo(_elapsed);

        if (_nextEventIndex >= _events.Count)
            IsFinished = true;

        var result = _builder.Build(isMouseFree: false);
        _builder.EndFrame();
        return result;
    }

    private void ApplyEventsUpTo(float time)
    {
        while (_nextEventIndex < _events.Count && _events[_nextEventIndex].Time <= time)
        {
            _builder.ProcessEvent(_events[_nextEventIndex]);
            _nextEventIndex++;
        }
    }
}
