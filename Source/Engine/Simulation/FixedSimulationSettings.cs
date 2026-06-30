namespace Game.Engine.Simulation;

public static class FixedSimulationSettings
{
    public const int DefaultTickHz = 60;
    public const int MaxTicksPerFrame = 5;
    public const int MinTickHz = 15;
    public const int MaxTickHz = 240;

    public static float DeltaTimeForHz(int tickHz) => 1f / tickHz;
}
