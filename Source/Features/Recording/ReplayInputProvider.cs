namespace Game.Features.Recording;

public sealed class ReplayInputProvider : IInputProvider
{
    private IReadOnlyList<InputEvent> _events = Array.Empty<InputEvent>();
    private readonly SyntheticInputStateBuilder _builder = new();
    private int _nextEventIndex;
    private float _elapsed;
    private long _currentTick = 1;
    private bool _useTickTiming;

    public float Duration { get; private set; }
    public bool IsFinished { get; private set; }

    public void Reset(IReadOnlyList<InputEvent> events, bool useTickTiming)
    {
        _events = events;
        _nextEventIndex = 0;
        _elapsed = 0f;
        _currentTick = 1;
        _useTickTiming = useTickTiming;
        IsFinished = events.Count == 0;
        Duration = events.Count > 0 ? events[^1].Time : 0f;
        _builder.Reset();
    }

    public InputPollResult Poll(float deltaTime)
    {
        if (IsFinished)
            return _builder.Build(isMouseFree: false);

        if (_useTickTiming)
            ApplyEventsForTick(_currentTick);
        else
        {
            _elapsed += deltaTime;
            ApplyEventsUpTo(_elapsed);
        }

        if (_nextEventIndex >= _events.Count)
            IsFinished = true;

        var result = _builder.Build(isMouseFree: false);
        _builder.EndFrame();

        if (_useTickTiming)
            _currentTick++;

        return result;
    }

    private void ApplyEventsForTick(long tick)
    {
        while (_nextEventIndex < _events.Count && _events[_nextEventIndex].Tick == tick)
        {
            _builder.ProcessEvent(_events[_nextEventIndex]);
            _nextEventIndex++;
        }
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
