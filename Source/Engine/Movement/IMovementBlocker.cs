using System.Numerics;

namespace Game.Engine.Movement;

/// <summary>
/// A dynamic obstacle that can block movement beyond the static tile layers
/// (e.g. closed doors). Lets <see cref="CollisionSystem"/> stay feature-agnostic.
/// </summary>
public interface IMovementBlocker
{
    bool IsBlocking(Vector3 position, float radius);
}
