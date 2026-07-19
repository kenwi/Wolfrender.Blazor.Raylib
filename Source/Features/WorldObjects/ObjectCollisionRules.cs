using Game.Engine.Movement;

namespace Game.Features.WorldObjects;

/// <summary>Engine collision adapter over <see cref="ObjectSprites"/> domain rules.</summary>
public sealed class ObjectCollisionRules : IObjectCollisionRules
{
    public static readonly ObjectCollisionRules Instance = new();

    public float CollisionRadius => ObjectSprites.CollisionRadius;

    public bool BlocksMovement(uint objectId) => ObjectSprites.BlocksMovement(objectId);
}
