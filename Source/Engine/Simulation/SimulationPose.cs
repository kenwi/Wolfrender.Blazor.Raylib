using System.Numerics;
using Raylib_cs;

namespace Game.Engine.Simulation;

public readonly struct SimulationPose
{
    private const float LookDistance = 1f;

    public Vector3 Position { get; init; }
    public Vector3 Forward { get; init; }

    public static SimulationPose FromPositionAndLook(Vector3 position, Vector3 lookTarget)
    {
        Vector3 forward = lookTarget - position;
        if (forward.LengthSquared() > 0.0001f)
            forward = Vector3.Normalize(forward);
        else
            forward = Vector3.UnitZ;

        return new SimulationPose
        {
            Position = position,
            Forward = forward
        };
    }

    public Camera3D ToCamera(Camera3D template) =>
        template with
        {
            Position = Position,
            Target = Position + Forward * LookDistance
        };

    public static SimulationPose Lerp(SimulationPose previous, SimulationPose current, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        Vector3 forward = Vector3.Lerp(previous.Forward, current.Forward, alpha);
        if (forward.LengthSquared() > 0.0001f)
            forward = Vector3.Normalize(forward);
        else
            forward = current.Forward;

        return new SimulationPose
        {
            Position = Vector3.Lerp(previous.Position, current.Position, alpha),
            Forward = forward
        };
    }
}
