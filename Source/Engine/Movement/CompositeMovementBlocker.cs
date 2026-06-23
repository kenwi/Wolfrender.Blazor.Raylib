using System.Numerics;

namespace Game.Engine.Movement;

/// <summary>Combines multiple dynamic movement blockers (e.g. doors and sliding secret walls).</summary>
public sealed class CompositeMovementBlocker : IMovementBlocker
{
    private readonly IMovementBlocker[] _blockers;

    public CompositeMovementBlocker(params IMovementBlocker[] blockers) => _blockers = blockers;

    public bool IsBlocking(Vector3 position, float radius)
    {
        foreach (var blocker in _blockers)
        {
            if (blocker.IsBlocking(position, radius))
                return true;
        }

        return false;
    }
}
