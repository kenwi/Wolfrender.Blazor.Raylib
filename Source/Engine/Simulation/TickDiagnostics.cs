namespace Game.Engine.Simulation;

public sealed class TickDiagnostics
{
    private float _renderFps;
    private float _simHz;
    private float _lastFrameDelta;
    private const float Smoothing = 0.1f;

    public bool OverlayEnabled { get; set; }

    public float RenderFps => _renderFps;
    public float SimHz => _simHz;
    public int LastTicksThisFrame { get; private set; }
    public long SimTickIndex { get; private set; }
    public float InterpolationAlpha { get; private set; }
    public int TickHz { get; private set; }
    public float FixedDeltaTimeMs { get; private set; }
    public bool HitTickCap { get; private set; }

    public void BeginFrame(float frameDeltaTime)
    {
        _lastFrameDelta = Math.Max(frameDeltaTime, 0f);
        if (_lastFrameDelta > 0f)
        {
            float instantFps = 1f / _lastFrameDelta;
            _renderFps = _renderFps <= 0f
                ? instantFps
                : float.Lerp(_renderFps, instantFps, Smoothing);
        }
    }

    public void RecordSimulationStep(FixedSimulationClock clock)
    {
        LastTicksThisFrame = clock.TicksConsumedLastFrame;
        SimTickIndex = clock.TickIndex;
        InterpolationAlpha = clock.InterpolationAlpha;
        TickHz = clock.TickHz;
        FixedDeltaTimeMs = clock.FixedDeltaTime * 1000f;
        HitTickCap = clock.HitTickCapLastFrame;

        if (_lastFrameDelta > 0f)
        {
            float instantSimHz = clock.TicksConsumedLastFrame / _lastFrameDelta;
            _simHz = _simHz <= 0f
                ? instantSimHz
                : float.Lerp(_simHz, instantSimHz, Smoothing);
        }
    }

    public void Reset()
    {
        _renderFps = 0f;
        _simHz = 0f;
        LastTicksThisFrame = 0;
        SimTickIndex = 0;
        InterpolationAlpha = 0f;
        HitTickCap = false;
    }

    public string BuildStatusLine() =>
        $"Render {_renderFps:F1} fps | Sim {_simHz:F0}/{TickHz} Hz | Tick {SimTickIndex} | Alpha {InterpolationAlpha:F2} | Ticks/frame {LastTicksThisFrame}" +
        (HitTickCap ? " | CAP" : string.Empty);
}
