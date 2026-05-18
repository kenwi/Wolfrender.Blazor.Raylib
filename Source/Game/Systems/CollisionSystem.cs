using System.Numerics;
using Game.Entities;
using Game.Utilities;

namespace Game.Systems;

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
        // Check center point
        int tileX = (int)(position.X / TileSize + 0.5f);
        int tileY = (int)(position.Z / TileSize + 0.5f);
        if (_level.GetWallTile(tileX, tileY) > 0)
            return true;

        // Check cardinal directions (N, S, E, W) at collision radius
        // North (positive Z)
        int northTileY = (int)((position.Z + radius) / TileSize + 0.5f);
        if (_level.GetWallTile(tileX, northTileY) > 0)
            return true;

        // South (negative Z)
        int southTileY = (int)((position.Z - radius) / TileSize + 0.5f);
        if (_level.GetWallTile(tileX, southTileY) > 0)
            return true;

        // East (positive X)
        int eastTileX = (int)((position.X + radius) / TileSize + 0.5f);
        if (_level.GetWallTile(eastTileX, tileY) > 0)
            return true;

        // West (negative X)
        int westTileX = (int)((position.X - radius) / TileSize + 0.5f);
        if (_level.GetWallTile(westTileX, tileY) > 0)
            return true;

        // Check diagonal directions at collision radius
        float diagonalOffset = radius * 0.707f; // 1/√2 for 45-degree angle
        
        // Northeast
        int neTileX = (int)((position.X + diagonalOffset) / TileSize + 0.5f);
        int neTileY = (int)((position.Z + diagonalOffset) / TileSize + 0.5f);
        if (_level.GetWallTile(neTileX, neTileY) > 0)
            return true;

        // Northwest
        int nwTileX = (int)((position.X - diagonalOffset) / TileSize + 0.5f);
        int nwTileY = (int)((position.Z + diagonalOffset) / TileSize + 0.5f);
        if (_level.GetWallTile(nwTileX, nwTileY) > 0)
            return true;

        // Southeast
        int seTileX = (int)((position.X + diagonalOffset) / TileSize + 0.5f);
        int seTileY = (int)((position.Z - diagonalOffset) / TileSize + 0.5f);
        if (_level.GetWallTile(seTileX, seTileY) > 0)
            return true;

        // Southwest
        int swTileX = (int)((position.X - diagonalOffset) / TileSize + 0.5f);
        int swTileY = (int)((position.Z - diagonalOffset) / TileSize + 0.5f);
        if (_level.GetWallTile(swTileX, swTileY) > 0)
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
}

