using System.Numerics;

namespace Game.Engine.Movement;

public class CollisionSystem
{
    private readonly LevelData _level;
    private readonly DoorSystem _doorSystem;
    private const float TileSize = 4.0f;

    public CollisionSystem(LevelData level, DoorSystem doorSystem)
    {
        _level = level;
        _doorSystem = doorSystem;
    }

    public bool CheckCollisionAtPosition(Vector3 position, float radius)
    {
        if (IsWallBlockingProbe(position.X, position.Z))
            return true;

        float diagonalOffset = radius * 0.707f;

        if (IsWallBlockingProbe(position.X, position.Z + radius)
            || IsWallBlockingProbe(position.X, position.Z - radius)
            || IsWallBlockingProbe(position.X + radius, position.Z)
            || IsWallBlockingProbe(position.X - radius, position.Z)
            || IsWallBlockingProbe(position.X + diagonalOffset, position.Z + diagonalOffset)
            || IsWallBlockingProbe(position.X - diagonalOffset, position.Z + diagonalOffset)
            || IsWallBlockingProbe(position.X + diagonalOffset, position.Z - diagonalOffset)
            || IsWallBlockingProbe(position.X - diagonalOffset, position.Z - diagonalOffset))
            return true;

        if (IsObjectBlockingAt(position.X, position.Z, radius))
            return true;

        if (_doorSystem.IsDoorBlocking(position, radius))
            return true;

        return false;
    }

    /// <summary>
    /// Move from <paramref name="from"/> toward <paramref name="desired"/> with axis-aligned wall sliding.
    /// Returns the resolved position: <paramref name="desired"/> if clear, an X- or Z-only slide if one axis is clear,
    /// or <paramref name="from"/> when both slides are blocked.
    /// </summary>
    public Vector3 ResolveMovement(Vector3 from, Vector3 desired, float radius)
    {
        if (!CheckCollisionAtPosition(desired, radius))
            return desired;

        Vector3 slideX = new Vector3(desired.X, from.Y, from.Z);
        if (!CheckCollisionAtPosition(slideX, radius))
            return slideX;

        Vector3 slideZ = new Vector3(from.X, from.Y, desired.Z);
        if (!CheckCollisionAtPosition(slideZ, radius))
            return slideZ;

        return from;
    }

    public void Update(Player player, float deltaTime)
    {
        if (player.Velocity.Length() <= 0)
            return;

        // MovementSystem has already set player.Position to the desired position
        // (player.Position = oldPosition + player.Velocity * deltaTime)
        player.Position = ResolveMovement(player.OldPosition, player.Position, player.CollisionRadius);
    }

    private bool IsWallBlockingProbe(float probeX, float probeZ)
    {
        int tileX = (int)(probeX / TileSize + 0.5f);
        int tileY = (int)(probeZ / TileSize + 0.5f);
        return _level.GetWallTile(tileX, tileY) > 0;
    }

    /// <summary>
    /// Circle overlap against placed objects on the entity tile and its neighbors.
    /// Uses gameplay tile lookup (anchor + half tile) and sprite anchor for distance.
    /// </summary>
    private bool IsObjectBlockingAt(float worldX, float worldZ, float entityRadius)
    {
        var (entityTileX, entityTileY) = LevelData.GetEntityTileFromWorld(worldX, worldZ);
        float minDist = entityRadius + ObjectSprites.CollisionRadius;
        float minDistSq = minDist * minDist;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int tileX = entityTileX + dx;
                int tileY = entityTileY + dy;
                if (tileX < 0 || tileX >= _level.Width || tileY < 0 || tileY >= _level.Height)
                    continue;
                uint objectId = _level.GetObjectTile(tileX, tileY);
                if (!ObjectSprites.BlocksMovement(objectId))
                    continue;

                var anchor = LevelData.GetTileAnchorWorld(tileX, tileY);
                float ddx = worldX - anchor.X;
                float ddz = worldZ - anchor.Z;
                if (ddx * ddx + ddz * ddz < minDistSq)
                    return true;
            }
        }

        return false;
    }
}
