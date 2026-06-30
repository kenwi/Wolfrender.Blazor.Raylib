namespace Game.Engine.Simulation;

public sealed class FixedSimulationClock
{
    private float _accumulator;

    public FixedSimulationClock(int tickHz = FixedSimulationSettings.DefaultTickHz)
    {
        TickHz = ClampTickHz(tickHz);
    }

    public int TickHz { get; private set; }
    public float FixedDeltaTime => FixedSimulationSettings.DeltaTimeForHz(TickHz);
    public long TickIndex { get; private set; }
    public float Accumulator => _accumulator;
    public float InterpolationAlpha { get; private set; }
    public int TicksConsumedLastFrame { get; private set; }
    public bool HitTickCapLastFrame { get; private set; }

    public void SetTickHz(int tickHz)
    {
        TickHz = ClampTickHz(tickHz);
    }

    public int Advance(float frameDeltaTime)
    {
        if (frameDeltaTime < 0f)
            frameDeltaTime = 0f;

        _accumulator += frameDeltaTime;
        int ticks = 0;
        HitTickCapLastFrame = false;

        while (_accumulator >= FixedDeltaTime && ticks < FixedSimulationSettings.MaxTicksPerFrame)
        {
            _accumulator -= FixedDeltaTime;
            TickIndex++;
            ticks++;
        }

        if (_accumulator >= FixedDeltaTime)
            HitTickCapLastFrame = true;

        TicksConsumedLastFrame = ticks;
        InterpolationAlpha = FixedDeltaTime > 0f
            ? Math.Clamp(_accumulator / FixedDeltaTime, 0f, 1f)
            : 0f;

        return ticks;
    }

    public void Reset()
    {
        _accumulator = 0f;
        TickIndex = 0;
        InterpolationAlpha = 0f;
        TicksConsumedLastFrame = 0;
        HitTickCapLastFrame = false;
    }

    private static int ClampTickHz(int tickHz) =>
        Math.Clamp(tickHz, FixedSimulationSettings.MinTickHz, FixedSimulationSettings.MaxTickHz);
}
