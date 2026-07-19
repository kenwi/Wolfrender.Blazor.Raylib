namespace Game.Engine.Movement;

/// <summary>
/// Placed-object collision semantics supplied by Features/WorldObjects.
/// Keeps Engine.Movement free of feature sprite catalogs.
/// </summary>
public interface IObjectCollisionRules
{
    float CollisionRadius { get; }

    bool BlocksMovement(uint objectId);
}
