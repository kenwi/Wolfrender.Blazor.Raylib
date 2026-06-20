namespace Game.Features.Options;

public static class MouseSensitivitySetting
{
    public const float Min = 0.1f;
    public const float Max = 3f;
    public const float Step = 0.1f;
    public const float Default = 1f;

    public static float Clamp(float value) => Math.Clamp(value, Min, Max);

    public static float Adjust(float current, int direction)
    {
        float next = current + direction * Step;
        return MathF.Round(Clamp(next) / Step) * Step;
    }
}
